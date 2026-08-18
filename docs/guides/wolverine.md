# Guide: работа с Wolverine (медиатор / CQRS)

Бизнес-операции проходят как команды и запросы через шину `IMessageBus`: контроллер отправляет команду/запрос, Wolverine находит хендлер, прогоняет вызов через конвейер middleware и возвращает результат. Почему выбран именно Wolverine - [ADR: Медиатор и шина сообщений (Wolverine)](../adr/0012-wolverine-mediator-and-messaging.md). Как встроить новую команду в структуру модуля целиком - [add-backend-module.md](add-backend-module.md); здесь - только механика самого Wolverine.

## Как это работает

- Контроллер зовёт `IMessageBus.InvokeAsync<TResult>(message, ct)`; какой хендлер обработает сообщение, веб-слой не знает.
- Хендлер - static-класс `*Handler` со static-методом `Handle(...)` (ADR-0013). Wolverine обнаруживает его по соглашению (имя `*Handler` + метод `Handle`) в ассемблиах, подключённых через `options.Discovery.IncludeAssembly(...)` (в `Program.cs` - ассембли Application). Маркер-интерфейсы не нужны: команда и запрос - простые `record`.
- Зависимости хендлера инжектируются параметрами метода `Handle`, а не через конструктор.
- Каждый вызов проходит через конвейер middleware (транзакции, валидация - см. ниже).

## Команда и хендлер

Команда - `record` с суффиксом `Command`; хендлер пишет через write-репозиторий агрегата (контракт в Domain) и не касается `DbContext`. Репозиторий сохраняет изменение - его `DbContext` служит unit of work.

```csharp
public record CreateNoteCommand(IReadOnlyDictionary<string, string> Texts);

public static class CreateNoteCommandHandler
{
    public static async Task<NoteResult> Handle(
        CreateNoteCommand command,
        INoteRepository repository,   // зависимости - параметры метода
        IOptions<CultureOptions> options,
        CancellationToken ct)
    {
        var note = new Note(options.Value.DefaultCulture, command.Texts);
        await repository.AddAsync(note, ct);
        return new NoteResult(note.Id, /* ... */, note.CreatedAt);
    }
}
```

Пример целиком - [CreateNoteCommandHandler.cs](../../libs/backend/core/DotnetVue3TemplateRu.Core.Application/Notes/Commands/CreateNote/CreateNoteCommandHandler.cs). Команда, её хендлер и валидатор - отдельными файлами в подпапке операции `Notes/Commands/CreateNote/` (ADR-0010).

## Запрос и хендлер

Запрос - `record` с суффиксом `Query`; хендлер читает через read-порт (query-репозиторий, проекция в БД), минуя доменную сущность. Отсутствие записи - `NotFoundException`, которую `GlobalExceptionHandler` мапит в 404.

```csharp
public record GetNoteQuery(Guid Id);

public static class GetNoteQueryHandler
{
    public static async Task<NoteResult> Handle(
        GetNoteQuery query,
        INoteQueryRepository repository,
        CancellationToken ct)
        => await repository.GetByIdAsync(query.Id, ct)
           ?? throw new NotFoundException(ErrorCodes.Note.NotFound, query.Id);
}
```

Пример целиком - [GetNoteQueryHandler.cs](../../libs/backend/core/DotnetVue3TemplateRu.Core.Application/Notes/Queries/GetNote/GetNoteQueryHandler.cs). Read-порт `INoteQueryRepository` - в корне папки сущности (`Notes/`), общий `NoteResult` - в `Notes/Models/` (ADR-0010).

## Вызов из контроллера

Контроллер инжектирует `IMessageBus` и отправляет команду/запрос через `InvokeAsync<TResult>`:

```csharp
public NotesController(IMessageBus bus) => _bus = bus;

[HttpPost]
public async Task<ActionResult<NoteResult>> Create(
    [FromBody] CreateNoteRequest request, CancellationToken ct)
{
    var result = await _bus.InvokeAsync<NoteResult>(new CreateNoteCommand(request.Texts), ct);
    return CreatedAtAction(nameof(GetByIdV1), new { id = result.Id }, result);
}
```

Пример целиком - [NotesController.cs](../../apps/backend/DotnetVue3TemplateRu.Api/Controllers/NotesController.cs).

## Валидатор

Валидатор команды - `*CommandValidator : AbstractValidator<TCommand>` в подпапке операции команды (`<Entity>/Commands/<Op>/`, ADR-0010). Middleware `UseFluentValidation()` находит его по discovery и прогоняет перед хендлером внутри `InvokeAsync`; провал бросает `ValidationException`, которую `GlobalExceptionHandler` отдаёт как `400 ValidationProblemDetails`. Отдельная регистрация в DI не нужна. Подробнее - [ADR: Валидация ввода](../adr/0019-input-validation.md).

## Middleware-конвейер

Конвейер настраивается один раз в [Program.cs](../../apps/backend/DotnetVue3TemplateRu.Api/Program.cs) при `builder.Host.UseWolverine(...)`:

```csharp
options.Discovery.IncludeAssembly(typeof(PingQuery).Assembly); // где искать хендлеры
options.UseEntityFrameworkCoreTransactions();                  // обработка в транзакции DbContext
options.UseFluentValidation();                                 // прогон IValidator<T> перед хендлером
```

Хендлеры Wolverine выполняются в отдельном DI-скоупе, поэтому request-scoped данные (`IUserContext`, культура запроса) не видны напрямую и пробрасываются через `HttpContext` - см. [ADR: Пользовательский контекст](../adr/0023-authentication-oidc.md).

## Синхронно и асинхронно

Рантайм работает в режиме `Solo` (см. раздел ниже), поэтому доступны две отправки - это не разные режимы, а два метода `IMessageBus`:

- `InvokeAsync<TResult>(message, ct)` - **синхронно**: хендлер выполняется тут же, в том же запросе, результат возвращается вызывающему. Так идут все команды и запросы (Notes, Ping) - это медиатор.
- `PublishAsync(message, ct)` - **асинхронно**: сообщение кладётся в локальную очередь, метод сразу возвращается, хендлер отрабатывает в фоне вне запроса. Для тяжёлых фоновых задач (например, рассылка уведомлений всем адресатам), чтобы не держать HTTP-запрос. Фоновый хендлер - тот же `*Handler` с `Handle(...)`; отличается только способ отправки.

`InvokeAsync` доступен всегда; `PublishAsync` в фоновую очередь работает потому, что рантайм в `Solo`, а не в `MediatorOnly` (тот отключил бы асинхронную обработку целиком).

## Профиль обмена и durability

Режим Wolverine задаётся профилем `Messaging:Durability` ([ADR-0014](../adr/0014-wolverine-durability-profile.md)); оба значения работают в `DurabilityMode.Solo`:

- `Persistent` (дефолт рантайма) - `Solo` + SQL message store: доступны sync и async, поднят фундамент для durable-очередей и транзакционного outbox.
- `InMemory` - `Solo` без store: async-очереди в памяти, без обращения к БД. Форсится под build-time экспортом OpenAPI (`GetDocument.Insider`) и годится для запусков без БД.

Локальные очереди по умолчанию **буферные** (in-memory): фоновое сообщение обрабатывается в памяти и теряется при рестарте. Этого достаточно, когда задача восстановима идемпотентным перезапуском по доменному статусу - надёжность обеспечивает домен, а не очередь. Отдельный случай - сообщение, которое несёт короткоживущий секрет (например, токен доступа): его durable не делают вовсе. Поднятое позже срока жизни токена, оно упало бы на просроченном значении, а сам секрет успел бы полежать в БД (ADR 0014).

Durable-доставку (сообщение переживает рестарт) включают **точечно на конкретной очереди** - `UseDurableInbox()` - и только там, где потеря сообщения при рестарте недопустима; глобально все очереди durable не делают. Атомарную связку "запись доменного изменения + публикация" даёт транзакционный outbox (`AddDbContextWithWolverineIntegration<TDbContext>`) для того `DbContext`, чей хендлер этого требует. Схему message store Wolverine создаёт и обновляет сам на старте (`AddResourceSetupOnStartup`), поэтому её нет в EF-миграциях.

## Best practices

Коротко - выжимка из официальных доков Wolverine (Handlers, Best Practices), чтобы новый хендлер
сразу писался идиоматично:

- Хендлер - `static class` со `static` методом `Handle(...)` (ADR-0013): без создания и последующей
  сборки мусора экземпляра класса на каждое сообщение, лучше ложится на codegen-модель Wolverine
  (прямой вызов метода вместо резолва через IoC на каждый вызов).
- Зависимости - только параметрами метода `Handle` (method injection), не через конструктор.
  В параметры метода можно принять: сообщение (команда/запрос) первым аргументом, любой сервис из
  IoC-контейнера, `Envelope`, `IMessageContext`/`IMessageBus` (шина, привязанная к текущему
  сообщению), `CancellationToken`, текущее время (`DateTime`/`DateTimeOffset`).
- Не резолвить scoped-сервисы вручную из контейнера внутри хендлера (`IServiceProvider.GetService<T>`
  и т.п.) - это создаёт отдельный от Wolverine экземпляр сервиса вне её модели времени жизни и ломает
  codegen-путь.
- Для codegen тип и метод хендлера обязаны быть `public` (и конструктор, если он вообще есть).

## Проверка

- Сборка: `dotnet build apps/backend/DotnetVue3TemplateRu.Api`. Незарегистрированный тип результата или необнаруженный хендлер проявляется как ошибка `InvokeAsync` в рантайме, а не при сборке - покрывается тестом ниже.
- Интеграционный тест: дёрнуть эндпоинт по HTTP (команда -> хендлер -> репозиторий -> БД) и проверить результат; см. [integration-tests.md](integration-tests.md).

## Перспектива

Наращивание идёт внутри того же Wolverine, без смены API: durable-доставка per-queue и транзакционный outbox (см. раздел про профиль выше), отложенные и запланированные сообщения, sagas, внешние брокеры (RabbitMQ, Kafka), эндпоинты на `WolverineFx.Http`.
