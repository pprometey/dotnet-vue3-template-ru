# Рецепты: Core

Короткие ответы на "как сделать X". Полный путь на живом примере - [tutorial.md](tutorial.md).

## Добавить команду (операцию записи)

Папка операции внутри сущности, один тип на файл:

```text
Core.Application/<Entity>/Commands/<Op>/
  <Op>Command.cs
  <Op>CommandHandler.cs
  <Op>CommandValidator.cs
  <Op>Result.cs        # если результат не общий для нескольких операций
```

Обработчик - static-класс со static-методом `Handle`; зависимости приходят параметрами метода, не конструктором:

```csharp
public static class CreateNoteCommandHandler
{
    public static async Task<NoteResult> Handle(
        CreateNoteCommand command,
        INoteRepository repository,
        IOptions<CultureOptions> options,
        CancellationToken ct)
    {
        var note = new Note(options.Value.DefaultCulture, command.Texts);
        await repository.AddAsync(note, ct);
        return new NoteResult(note.Id, note.Text, note.CreatedAt);
    }
}
```

Регистрировать обработчик нигде не нужно - Wolverine находит его по соглашению в сканируемой сборке.

## Добавить запрос (операцию чтения)

Запрос идёт через read-порт, а не через write-репозиторий. Порт объявляется в корне папки сущности (`Core.Application/<Entity>/I<Entity>QueryRepository.cs`), реализация - в `Core.Infrastructure/Persistence/<Entity>/`.

```csharp
public Task<NoteResult?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    var culture = CultureInfo.CurrentCulture.Name;

    return _db.Notes
        .Where(n => n.Id == id)
        .Select(n => new NoteResult(n.Id, /* ... */))
        .FirstOrDefaultAsync(ct);
}
```

Проекция строится в SQL: нужные колонки, без подъёма агрегата и без change-tracking. Загрузить агрегат write-репозиторием и отфильтровать в памяти - утечка слоя (правило 13 в `.claude/CLAUDE.md`).

## Добавить код ошибки

1. Константа в `Core.Domain/Errors/ErrorCodes.cs`, нотация `модуль.поле.правило`.
2. Строка в `Api/Resources/Localization/ErrorMessages.resx` (русский), `.en.resx` и `.kk.resx`.

Забытый перевод роняет `ErrorCodesLocalizationTests` - страж обходит каталог рефлексией и проверяет каждую культуру без фолбэка.

Бросать в домене:

```csharp
throw new DomainException(ErrorCodes.Note.TextRequired);
```

Текст не хранится в исключении: он резолвится один раз на границе, в `GlobalExceptionHandler`, на культуре запроса.

## Сделать поле сущности локализуемым

1. Завести класс перевода - наследник `LocalizationEntity`.
2. В `OnModelCreating` вызвать хелпер и задать длину локализуемого поля:

```csharp
modelBuilder.ConfigureLocalization<Note, NoteLocalization>(n => n.Localizations);
modelBuilder.Entity<NoteLocalization>(b =>
    b.Property(l => l.Text).IsRequired().HasMaxLength(1000));
```

Хелпер сам ставит имя таблицы (`note_localizations`), FK с каскадом, уникальный индекс `(relation_id, culture)` и длину `culture`. Подробнее - [entity-localization.md](../../guides/entity-localization.md).

## Добавить миграцию

```bash
dotnet ef migrations add <Name> \
  --project libs/backend/core/DotnetVue3TemplateRu.Core.Infrastructure \
  --startup-project libs/backend/core/DotnetVue3TemplateRu.Core.Infrastructure \
  --output-dir Persistence/Migrations
```

Startup-проект указывает на саму Infrastructure намеренно: там лежит `DotnetVue3TemplateRuDbContextFactory`, и инструменты EF не поднимают приложение целиком. Иначе им понадобились бы и настроенный провайдер идентичности, и живая база - для генерации миграции не нужно ни то, ни другое.

## Сделать эндпоинт защищённым

```csharp
[Authorize]
public class SessionContextController : ControllerBase
```

Идентичность снимает контроллер и кладёт полем в команду - обработчик `IUserContext` не инжектирует. Почему так - [authentication.md](../../guides/authentication.md).

## Выпустить вторую версию эндпоинта

Контроллер объявляет обе версии; неизменившийся метод остаётся без `[MapToApiVersion]` и обслуживает обе. Изменившийся разносится на два метода. Типы результатов обязаны различаться именами, иначе схемы в OpenAPI столкнутся.

## Добавить фоновую операцию

```csharp
await _bus.PublishAsync(new SendWelcomeEmailCommand(userId));
```

Очередь по умолчанию буферная: сообщение теряется при рестарте. Там, где потеря недопустима (письма пользователям), очередь делается durable точечно - `UseDurableInbox()`. Подробнее - [wolverine.md](../../guides/wolverine.md).

## Пересобрать клиент фронтенда после изменения контракта

```bash
yarn nx run api-client:generate
```

OpenAPI-документ коммитится. Хук pre-push сравнивает его с `HEAD` и не пускает пуш, если контроллеры изменили, а спеку не перегенерировали.
