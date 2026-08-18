# ADR: Подход к интеграционному тестированию API

- Статус: Принято (обновлено 2026-06-17: миграция с xUnit + Shouldly на TUnit)
- Дата: 2026-06-09
- Контекст: монорепозиторий DotnetVue3TemplateRu, проект `tests/DotnetVue3TemplateRu.IntegrationTests`

## Контекст

Стратегия тестирования проекта (см. `docs/spec/test-strategy.md`) ставит интеграционные тесты на первое место: тест должен проверять путь HTTP-запрос -> бизнес-логика -> запись в БД -> HTTP-ответ целиком, не разбивая его на изолированные моки. Значит, нужна реальная база данных.

Требования к инфраструктуре тестов:

- база данных поднимается автоматически, без ручной настройки окружения;
- каждый тест получает чистое состояние и не зависит от других тестов;
- прогон всей тест-сьюты должен быть быстрым: контейнер не должен пересоздаваться для каждого теста или тестового класса.

## Решение

Выбрана связка из трёх компонентов:

**1. Testcontainers.PostgreSql** запускает реальный PostgreSQL в Docker-контейнере прямо во время теста. Никаких внешних зависимостей, база всегда в чистом начальном состоянии, контейнер гасится после прогона автоматически.

**2. TUnit ClassDataSource** обеспечивает один контейнер на весь прогон. Все тестовые классы объявляют `[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]` и получают одну и ту же фабрику (а значит, один и тот же контейнер) через primary constructor injection. TUnit создаёт `DotnetVue3TemplateRuApiFactory` один раз, вызывает `InitializeAsync` (через интерфейс `IAsyncInitializer`) один раз в начале, `DisposeAsync` - один раз в конце.

**3. Respawn** сбрасывает данные в таблицах между тестами. Вместо перезапуска контейнера или пересоздания схемы Respawn выполняет DELETE/TRUNCATE только по нужным таблицам (с учётом FK-зависимостей). Операция занимает 5-50 мс против 15-30 секунд на новый контейнер.

Respawn настраивается на `DbAdapter.Postgres` и `SchemasToInclude = ["public"]`. Ограничение схемой существенно: служебные таблицы Wolverine живут в схеме `wolverine` (ADR-0007), и вычистить их между тестами значило бы выдернуть конверты из-под работающего durability agent.

Ссылки: [Respawn на GitHub](https://github.com/jbogard/Respawn), автор - Jimmy Bogard (автор AutoMapper, MediatR).

## Как это работает

```text
dotnet test
  |
  +-- TUnit создаёт DotnetVue3TemplateRuApiFactory (ОДИН РАЗ, через ClassDataSource)
        |
        +-- IAsyncInitializer.InitializeAsync:
        |     - docker: sqlserver container up          (~15-25s, один раз)
        |     - db.Database.MigrateAsync()             (миграции, один раз)
        |     - Respawner.CreateAsync(...)             (snapshot схемы, один раз)
        |
        +-- NotesEndpointTests.[Before(Test)]  -> ResetAsync (~10ms)
        |     [тесты Notes]
        +-- HealthEndpointTests.[Before(Test)] -> ResetAsync (~10ms)
        |     [тесты Health]
        +-- PingEndpointTests.[Before(Test)]   -> ResetAsync (~10ms)
              [тесты Ping]
        |
        +-- IAsyncDisposable.DisposeAsync:
              - connection close
              - sqlserver container down
```

Ключевые свойства:

- Контейнер стартует **один раз** за весь прогон `dotnet test`.
- Миграции накатываются **один раз** при старте контейнера.
- Каждый тест вызывает `ResetDatabaseAsync()` в методе с атрибутом `[Before(Test)]` и получает чистую БД за ~10 мс.
- Тесты внутри одного класса выполняются на одних данных (TUnit не гарантирует порядок, поэтому тесты в классе не должны зависеть друг от друга по состоянию БД).

Структура кода:

```text
DotnetVue3TemplateRuApiFactory : WebApplicationFactory<Program>, IAsyncInitializer, IAsyncDisposable
  IAsyncInitializer.InitializeAsync()  - запуск контейнера, миграции, Respawner
  IAsyncDisposable.DisposeAsync()      - завершение контейнера
  ResetDatabaseAsync()                 - вызов Respawn между тестами

[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class NotesEndpointTests(DotnetVue3TemplateRuApiFactory factory)
    [Before(Test)] ResetAsync() -> factory.ResetDatabaseAsync()
```

## Почему не альтернативы

**Один контейнер на каждый тестовый класс / тест.** Затраты ~15-25 с на старт контейнера при 20 тестовых классах дают 5-8 минут только на поднятие БД. Неприемлемо.

**SQLite in-memory вместо Testcontainers.** SQLite ведёт себя иначе, чем PostgreSQL: другие типы данных, другие ограничения, нет полного соответствия диалекта. Тест, прошедший на SQLite, может упасть на реальной БД в проде. Это антипаттерн (см. `docs/spec/test-strategy.md`, раздел "Распространённые ошибки").

**Транзакция на тест с rollback.** Классический паттерн изоляции, но несовместим с тестами через HTTP: `WebApplicationFactory` использует свой scope DI и свои транзакции, внешнюю транзакцию из теста применить к ним нельзя. Работает только для прямых вызовов репозиториев, не для HTTP-стека.

**Пересоздание схемы через `EnsureDeletedAsync` + `MigrateAsync`.** Занимает 500-2000 мс на каждый сброс (пересоздаются все таблицы и индексы). Respawn делает то же самое (изоляция данных) за 5-50 мс, потому что трогает только строки, не схему.

## Почему TUnit вместо xUnit + Shouldly

Проект начинался на xUnit 2.9 + Shouldly 4.3. В 2026-06-17 стек заменён на TUnit 0.6. Причины:

**Один пакет вместо четырёх.** Старый стек требовал четырёх NuGet-пакетов с разными мейнтейнерами и циклами выпуска: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`. TUnit - один пакет, включающий runner, discovery и встроенные fluent-ассерты.

**Нативные async-ассерты.** `await Assert.That(x).IsEqualTo(y)` - асинхронный по природе. Shouldly (`ShouldBe`) синхронный: для async-кода требовались обходные пути или потеря async-контекста. TUnit не создаёт этой проблемы.

**Декларативный жизненный цикл.** xUnit для общих ресурсов требовал: xUnit-специфичного `IAsyncLifetime` + маркерного класса-заглушки `public class ApiCollection : ICollectionFixture<DotnetVue3TemplateRuApiFactory>` + атрибута `[Collection(nameof(ApiCollection))]` на каждом тестовом классе. Три артефакта ради одной задачи. TUnit заменяет это одним атрибутом `[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]` на тестовом классе и стандартными .NET-интерфейсами `IAsyncInitializer` и `IAsyncDisposable` на фабрике. Нет магических маркерных классов - intent очевиден из атрибута.

**Параллелизм по умолчанию.** xUnit коллекция = один поток для всех классов в ней; масштабируется плохо. TUnit запускает тестовые классы параллельно по умолчанию. Там, где параллелизм небезопасен из-за общего состояния, используется явный атрибут `[NotInParallel]`. Когда состояние станет независимым (например, каждый тест получит отдельную схему БД) - атрибут убирается без изменений остального кода.

**Primary constructor injection.** `public class MyTests(DotnetVue3TemplateRuApiFactory factory)` - стандарт C# 12, нет boilerplate с приватным полем и explicit constructor.

**Snapshot testing в той же экосистеме.** `Verify.TUnit` - официальная адаптация библиотеки Verify для TUnit. Оба подхода (fluent-ассерты и snapshot) живут в одном тестовом классе без дополнительных настроек.

## Когда выбирать ассерты, когда снапшоты

Практический гайд "как писать интеграционные тесты" с пошаговым шаблоном - [docs/guides/integration-tests.md](../guides/integration-tests.md). Ниже - краткое обоснование выбора между двумя подходами.

TUnit предоставляет два подхода к верификации. Разработчик выбирает тот, что подходит ситуации; их можно комбинировать в одном тестовом классе.

### Fluent-ассерты: `await Assert.That(x).IsEqualTo(y)`

Использовать когда:

- Проверяете конкретное известное значение: статус-код, отдельное поле ответа.
- Негативный сценарий (400/403/404): достаточно проверить сам факт статуса.
- Проверяете запись в БД: конкретные поля конкретной записи.
- Тест должен читаться как спецификация: "статус должен быть 201, Id не должен быть пустым".

Правило: **если знаете точное ожидаемое значение и оно стабильно - пишите ассерт**.

```cs
[Test]
public async Task CreateNote_WithValidData_Returns201_AndPersistsToDb()
{
    var text = new Faker().Lorem.Sentence();
    var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/notes", new { texts = new Dictionary<string, string> { ["ru"] = text } });

    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

    var body = await response.Content.ReadFromJsonAsync<NoteResponse>();
    await Assert.That(body!.Text).IsEqualTo(text);
    await Assert.That(body.Id).IsNotEqualTo(Guid.Empty);

    // Проверяем, что запись реально появилась в БД
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DotnetVue3TemplateRuDbContext>();
    var saved = await db.Notes.FindAsync(body.Id);
    await Assert.That(saved).IsNotNull();
}
```

### Snapshot-тест: `await Verify(content).ScrubMembers<T>(...)`

Использовать когда:

- Хотите зафиксировать форму ответа целиком: состав полей, структуру JSON, имена ключей.
- Ответ содержит много полей - явные ассерты на каждое избыточны.
- Хотите поймать случайный рефакторинг API: переименовали поле, убрали вложенный объект - snapshot упадёт и предупредит до PR.
- Файл `*.verified.txt` в git служит живой документацией: "вот что возвращает эндпоинт".

Правило: **если форма ответа важна как целое и меняться не должна - делайте снапшот**.

```cs
[Test]
public async Task CreateNote_ResponseShape_MatchesSnapshot()
{
    // Фиксированный текст - случайный менялся бы при каждом запуске.
    var response = await factory.CreateClient()
        .PostAsJsonAsync("/api/v1/notes", new { texts = new Dictionary<string, string> { ["ru"] = "Snapshot test note" } });

    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

    var content = await response.Content.ReadFromJsonAsync<NoteResponse>();

    // Id и CreatedAt нестабильны между запусками - скрабируем.
    // Text ("Snapshot test note") фиксирован и проверяется как есть.
    await Verify(content)
        .ScrubMembers<NoteResponse>(_ => _.Id, _ => _.CreatedAt)
        .UseFileName("CreateNote_ResponseShape");
}
```

Снапшоты складываются в подпапку на каждый тестовый класс (`Snapshots/<Класс>/<Имя>.verified.txt`), чтобы папка не превращалась в плоскую простыню. Структуру задаёт `DerivePathInfo` в `VerifyConfig.cs`, имя файла - `UseFileName(...)` в тесте. Подробности - [docs/guides/integration-tests.md](../guides/integration-tests.md).

При первом запуске тест упадёт и запишет `Snapshots/<Класс>/*.received.txt`. Разработчик проверяет файл и принимает его:

```bash
dotnet tool install -g verify.tool   # один раз
dotnet-verify accept
```

После этого `*.verified.txt` коммитится в git, последующие прогоны проходят.

**Когда снапшот НЕ нужен:**

- Все поля нестабильны (сплошные GUID и даты) - пользы нет, только скраббинг.
- Один-два поля - проще явный ассерт.
- Негативный сценарий - достаточно статуса.

**Оба подхода в одном классе - норма.** `CreateNote_WithValidData` проверяет ассертами конкретные значения и факт записи в БД. `CreateNote_ResponseShape` фиксирует JSON-форму снапшотом. Это не дублирование - это дополнение: один тест ловит "неправильное значение", другой ловит "изменилась структура".

## Токены в тестах

Защищённые эндпоинты требуют настоящего JWT (ADR-0023), а поднимать провайдера идентичности в прогоне значило бы удвоить его длительность ради проверки чужого сервиса.

Вместо этого фабрика в `ConfigureTestServices` подменяет параметры проверки токена: снимает `Authority` и `MetadataAddress`, ставит `IssuerSigningKey` в тестовый RSA-ключ, сгенерированный один раз на сессию, и выключает проверку издателя. Хелпер `TestTokens.Issue(sub)` подписывает RS256-токен тем же ключом.

Подпись остаётся асимметричной - симметричный секрет не появляется даже в тестах. Непроверенным остаётся ровно одно звено: получение открытых ключей по JWKS из discovery-документа провайдера. Это осознанный размен, и он записан здесь, чтобы не выглядел упущением.

## Плюсы выбранного подхода

- Контейнер стартует один раз - время прогона не растёт линейно с количеством тестовых классов.
- Реальный PostgreSQL: тесты ловят ошибки, специфичные для PostgreSQL (типы, FK, уникальность и т.д.).
- Изоляция данных через Respawn: каждый тест получает чистую БД, тесты не влияют друг на друга.
- TUnit заменяет xUnit + Shouldly одним пакетом: нет рассинхрона версий, встроенные async-ассерты, читаемый fluent-синтаксис.
- Декларативная конфигурация: `[ClassDataSource]` + `[NotInParallel]` вместо маркерных классов и xUnit-специфичных интерфейсов.
- Snapshot testing (Verify.TUnit) позволяет фиксировать форму API-ответа без написания ассерта на каждое поле.
- Отсутствие ручной настройки: Docker-контейнер поднимается и гасится автоматически в рамках `dotnet test`.

## Минусы и ограничения

- Требует запущенного Docker Desktop. В CI это стандарт; локально разработчик должен держать Docker включённым при запуске тестов.
- Первый старт контейнера занимает 15-25 с (pull образа при первом запуске может занять дольше). Все последующие прогоны быстрее (образ закэширован).
- Тесты внутри одного класса не изолированы друг от друга по данным: если нужна изоляция на уровне каждого метода, надо перенести `ResetDatabaseAsync()` внутрь каждого теста (увеличит время прогона).
- `[NotInParallel]` на всех тестовых классах убирает параллелизм между ними. Это сделано намеренно: общий Respawn сбрасывает данные всех тестов подряд. Когда тесты перейдут на изолированные схемы (отдельная схема на тест) - `[NotInParallel]` можно убрать.
- Snapshot-тесты требуют ручного шага "принять" при первом запуске и после намеренного изменения API. Это особенность, а не баг: разработчик явно подтверждает, что новая форма ответа правильная.
- Respawn строит граф зависимостей таблиц запросами к системным каталоговым представлениям PostgreSQL; стандартных прав подключения для этого достаточно.

## Последствия

Новые тестовые классы создаются по шаблону из двух атрибутов и одного метода:

```cs
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class MyEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task MyTest() { ... }
}
```

Новые классы автоматически используют разделяемый контейнер. По мере роста сьюты выигрыш от одного контейнера растёт: 100 тестовых классов добавляют только 100 x 10 мс = 1 с на сброс данных, а не 100 x 20 с = 33 мин на старт контейнеров.
