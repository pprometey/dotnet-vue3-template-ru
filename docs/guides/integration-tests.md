# Guide: как писать интеграционные тесты

Интеграционные тесты проекта проверяют реальный путь целиком: HTTP-запрос -> бизнес-логика (Wolverine) -> запись в реальный PostgreSQL -> HTTP-ответ. Без моков БД и без подмены слоёв. Почему именно так - [ADR: Интеграционные тесты](../adr/0031-integration-tests.md) и [стратегия тестирования](../spec/test-strategy.md).

Стек: **TUnit** (фреймворк + ассерты), **Testcontainers** (PostgreSQL в Docker), **Respawn** (сброс данных между тестами), **Verify.TUnit** (snapshot-тесты), **Bogus** (генерация тестовых данных).

Проект тестов: `tests/DotnetVue3TemplateRu.IntegrationTests`.

## Предусловия

- Запущенный **Docker Desktop** - Testcontainers поднимает в нём PostgreSQL. Без Docker тесты не стартуют.
- Установленный **.NET SDK 10**.

## Как запустить

```bash
yarn nx run integration-tests:test    # через Nx (как в CI)
dotnet test tests/DotnetVue3TemplateRu.IntegrationTests   # напрямую
```

Первый прогон медленный: скачивается образ `postgres:17-alpine` (около 80 МБ) и стартует контейнер. Последующие быстрые: образ закэширован, контейнер поднимается один раз на всю сессию.

## Анатомия теста

Вся инфраструктура спрятана в `DotnetVue3TemplateRuApiFactory`. От тебя - только тестовый класс по шаблону из трёх частей:

```cs
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]  // (1)
[NotInParallel]                                                       // (2)
public class MyEndpointTests(DotnetVue3TemplateRuApiFactory factory)                   // (3)
{
    [Before(Test)]                                                    // (4)
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task MyTest()
    {
        var client = factory.CreateClient();
        // ...
    }
}
```

Что делает каждая строка:

1. `[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]` - TUnit создаёт `DotnetVue3TemplateRuApiFactory` один раз на всю сессию и передаёт её в конструктор класса. Один контейнер на все тесты, а не на каждый класс.
2. `[NotInParallel]` - классы не выполняются параллельно. Обязательно: все тесты делят одну БД, а Respawn чистит её целиком. Без этого атрибута один тест затрёт данные другого.
3. Primary constructor `(DotnetVue3TemplateRuApiFactory factory)` - сюда TUnit инжектит фабрику. Через неё создаёшь HTTP-клиент (`factory.CreateClient()`) и достаёшь сервисы из DI (`factory.Services`).
4. `[Before(Test)]` + `ResetDatabaseAsync()` - перед каждым тестом Respawn удаляет все строки (~10 мс), не пересоздавая схему. Каждый тест стартует на чистой БД.

> Тесты **внутри одного класса** делят данные между собой - Respawn чистит БД перед тестом, но порядок тестов TUnit не гарантирует. Не пиши тесты, которые зависят от данных, оставленных другим тестом того же класса.

## Структура теста: Arrange-Act-Assert

Эталон - `NotesEndpointTests.CreateNote_WithValidData_Returns201_AndPersistsToDb`:

```cs
[Test]
public async Task CreateNote_WithValidData_Returns201_AndPersistsToDb()
{
    // Arrange: клиент + входные данные
    var client = factory.CreateClient();
    var text = new Faker().Lorem.Sentence();

    // Act: один HTTP-вызов
    var response = await client.PostAsJsonAsync(
        "/api/v1/notes",
        new { texts = new Dictionary<string, string> { ["ru"] = text } });

    // Assert: статус
    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

    // Assert: тело ответа
    var body = await response.Content.ReadFromJsonAsync<NoteResponse>();
    await Assert.That(body).IsNotNull();
    await Assert.That(body!.Text).IsEqualTo(text);
    await Assert.That(body.Id).IsNotEqualTo(Guid.Empty);

    // Assert: запись реально легла в БД (а не только вернулась в ответе)
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DotnetVue3TemplateRuDbContext>();
    var saved = await db.Notes.FindAsync(body.Id);
    await Assert.That(saved).IsNotNull();
    await Assert.That(saved!.Text).IsEqualTo(text);
}
```

Три уровня проверки в позитивном тесте: статус-код, тело ответа, состояние БД. Проверка БД - важная часть: она ловит случай, когда ответ корректный, а сохранение молча не сработало.

Имя теста - по схеме `Метод_Условие_ОжидаемыйРезультат`. Оно должно читаться как спецификация: `CreateNote_WithEmptyText_Returns400`.

## Ассерты TUnit

Базовый синтаксис - `await Assert.That(actual).Matcher(expected)`. Всегда с `await` (ассерты асинхронные).

```cs
await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
await Assert.That(body).IsNotNull();
await Assert.That(body!.Id).IsNotEqualTo(Guid.Empty);
await Assert.That(body.Text).IsEqualTo(expectedText);
await Assert.That(list).HasCount().EqualTo(3);
await Assert.That(text).Contains("substring");
```

Доступ к БД для проверки состояния - через scope из `factory.Services`:

```cs
using var scope = factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<DotnetVue3TemplateRuDbContext>();
var saved = await db.Notes.FindAsync(id);
```

Тестовые данные генерируй через **Bogus** (`new Faker()`), а не хардкодом - это исключает случайное прохождение теста на "удобном" значении:

```cs
var text = new Faker().Lorem.Sentence();
var email = new Faker().Internet.Email();
```

## Snapshot-тесты (Verify)

### Что это, если не сталкивался

Обычный ассерт ты пишешь руками: "поле X должно быть равно Y". Snapshot-тест работает наоборот: ты не описываешь ожидаемое значение, а один раз **фиксируешь фактический результат в файл-эталон** (snapshot). При следующих прогонах тест берёт свежий результат и сравнивает его с этим файлом байт в байт. Совпало - зелёный, разошлось - красный с диффом.

Аналогия: эталон - это "фотография" ответа эндпоинта. Тест каждый раз делает новую фотографию и сличает со старой.

Порядок жизни снапшота:

1. **Пишешь тест** с вызовом `await Verify(объект)`. Эталона ещё нет.
2. **Первый прогон падает** - сравнивать не с чем. Verify записывает фактический результат в файл `*.received.txt`.
3. **Ты глазами проверяешь** `*.received.txt`: то ли вернулось, что ожидал?
4. **Принимаешь эталон** командой `dotnet-verify accept` - файл превращается в `*.verified.txt` и коммитится в git.
5. **Дальше тест зелёный**, пока результат совпадает с эталоном. Изменился результат - тест падает и показывает, что именно поменялось.
6. Если изменение **намеренное** (поменял API) - снова смотришь дифф и принимаешь новый эталон. Если **случайное** (баг) - чинишь код.

Главное отличие от ассерта: ассерт проверяет **конкретное значение**, снапшот - **всю форму результата сразу** (какие поля есть, как называются, как вложены). Поэтому снапшот ловит то, о чём ты не подумал написать ассерт: исчезнувшее поле, переименованный ключ, новый блок в ответе.

### Как это выглядит в коде

Snapshot фиксирует **форму ответа целиком** - состав полей и структуру - в файле `*.verified.txt`, который коммитится в git. Вместо ручного ассерта на каждое поле сравнивается весь объект с эталоном.

```cs
[Test]
public async Task CreateNote_ResponseShape_MatchesSnapshot()
{
    // Фиксированный текст: случайный менялся бы при каждом запуске и ломал снапшот.
    var response = await factory.CreateClient()
        .PostAsJsonAsync(
            "/api/v1/notes",
            new { texts = new Dictionary<string, string> { ["ru"] = "Snapshot test note" } });

    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

    var content = await response.Content.ReadFromJsonAsync<NoteResponse>();

    // Id и CreatedAt нестабильны между запусками - скрабируем (заменяем на плейсхолдер).
    // Остальные поля проверяются как есть.
    await Verify(content)
        .ScrubMembers<NoteResponse>(_ => _.Id, _ => _.CreatedAt)
        .UseFileName("CreateNote_ResponseShape");
}
```

Ключевые моменты:

- **Нестабильные поля скрабируй** через `ScrubMembers<T>(...)` - иначе Id/дата/timestamp будут меняться каждый прогон и снапшот никогда не совпадёт.
- **Вход делай детерминированным** - фиксированный текст вместо `Faker`, иначе нестабилен и сам ответ.
- **Задавай `UseFileName("...")`** - без него Verify.TUnit добавит в имя файла параметр конструктора из `ClassDataSource` (`factory=...`), и имя станет длинным и нечитаемым.

### Где лежат снапшоты и как они структурированы

Снапшоты складываются в подпапку на каждый тестовый класс - иначе при росте сьюты получилась бы плоская папка из сотен файлов, которую невозможно листать:

```text
Snapshots/
  NotesEndpointTests/
    CreateNote_ResponseShape.verified.txt
  <ДругойКлассТеста>/
    <ИмяСнапшота>.verified.txt
```

Папку (`Snapshots/<Класс>`) задаёт `DerivePathInfo` в `VerifyConfig.cs` (один раз на проект). Имя файла - аргумент `UseFileName(...)` в самом тесте. Описание конвенции и все способы принять снапшот лежат рядом со снапшотами: [tests/DotnetVue3TemplateRu.IntegrationTests/Snapshots/README.md](../../tests/DotnetVue3TemplateRu.IntegrationTests/Snapshots/README.md).

Формат файла - relaxed-вид Verify (не строгий JSON, имена полей без кавычек):

```text
{
  Id: {Scrubbed},
  Text: Snapshot test note,
  CreatedAt: {Scrubbed}
}
```

### Workflow принятия снапшота

При первом запуске (или после намеренного изменения API) тест **падает** и пишет рядом `*.received.txt` - фактический ответ. Нужно глазами проверить его и принять:

```bash
dotnet tool install -g verify.tool                 # один раз на машину
cd tests/DotnetVue3TemplateRu.IntegrationTests
dotnet-verify accept                               # принять все *.received -> *.verified
```

После принятия `*.verified.txt` коммитится в git. Дальше тест зелёный, пока форма ответа не изменится. Изменилась случайно (баг рефакторинга) - тест падает и показывает дифф. Изменилась намеренно - снова `dotnet-verify accept` и коммит нового эталона.

`*.received.txt` в git не идёт (в `.gitignore`); `*.verified.txt` - идёт.

## Ассерты или снапшоты: что выбрать

| Ситуация                                                      | Подход                 |
| ------------------------------------------------------------- | ---------------------- |
| Проверяешь конкретное известное значение (статус, одно поле)  | Ассерт                 |
| Негативный сценарий (400/403/404)                             | Ассерт (только статус) |
| Проверяешь факт записи в БД                                   | Ассерт                 |
| Фиксируешь форму ответа целиком (много полей, структура JSON) | Снапшот                |
| Ловишь случайное изменение контракта API при рефакторинге     | Снапшот                |
| Нужна живая документация "что возвращает эндпоинт"            | Снапшот                |

Правила:

- **Знаешь точное стабильное значение - пиши ассерт.**
- **Важна форма ответа как целое и она не должна меняться - делай снапшот.**

Снапшот **не нужен**, когда: все поля нестабильны (сплошные GUID и даты - останется один скраббинг), полей один-два (проще явный ассерт), либо это негативный сценарий (достаточно статуса).

**Оба подхода в одном классе - норма, а не дублирование.** Ассерт-тест ловит "неправильное значение", snapshot-тест ловит "изменилась структура". См. `NotesEndpointTests`: `CreateNote_WithValidData` (ассерты + проверка БД) и `CreateNote_ResponseShape` (снапшот) живут рядом.

## Что покрывать: позитив и негатив

В интеграционных тестах проверяются и позитивные, и негативные сценарии, но негатив тут трактуется иначе, чем в юнит-тестах. Цель интеграционного теста - не перебрать все способы, которыми что-то может пойти не так, а проверить стыки: как код работает с реальной БД, с брокером сообщений, с внешними API и между слоями приложения. Если тащить сюда весь негатив, сьюта станет медленной и хрупкой, поэтому действует правило разумного баланса.

### Где какой негатив проверять

- **Юнит-тесты - вся микро-логика.** Они быстрые, поэтому именно в них изолированно гоняется максимум негативных случаев: валидация полей (пустая строка, неверный формат email, слишком длинный текст), граничные значения (число меньше нуля, выход за допустимый диапазон), логика отдельных доменных сущностей и чистых функций.
- **Интеграционные тесты - стыки и контракты.** Дублировать сюда весь негатив из юнитов не нужно. Вместо перебора отдельных кейсов проверяются классы (типы) интеграционных сбоев.

### Какие негативные сценарии писать в интеграционных тестах

Покрывай принципиальные технические и бизнес-сбои, которые случаются на стыке систем:

- **Ошибки контракта (API/БД).** Что произойдёт, если внешняя система вернула 400 Bad Request или неожиданный формат JSON - корректно ли это обработается.
- **Проблемы доступности (инфраструктурные сбои).** БД временно недоступна, таймаут соединения, внешний API ответил 503 Service Unavailable. Проверяется работа повторных попыток (Retry), автоматического отключения (Circuit Breaker) или корректный откат транзакции.
- **Конфликты данных и уникальности.** Вставка записи с уже существующим уникальным ключом (например, повторная регистрация того же адреса почты). Тест должен убедиться, что БД выбрасывает ограничение (Constraint), а сервис переводит его в понятную бизнес-ошибку (например, 409 Conflict).
- **Бизнес-отказы в сквозных процессах.** Если тестируешь цепочку с внешним вызовом, то позитив - запись сохранена и уведомление ушло, а негатив - внешний сервис вернул ошибку, и система должна откатить транзакцию, а не оставить половину результата.

### Как не написать лишнего

Чтобы не утонуть в комбинаторике, применяй метод эквивалентных классов: если у внешнего сервиса есть 10 причин уронить валидацию (ошибки в 10 разных полях), для интеграции достаточно одной. Важен сам факт - "если внешняя система вернула ошибку валидации, наш сервис корректно обработал этот статус", а не каждое поле по отдельности.

Разделяй тесты на реальных компонентах и тесты с заглушками. БД гоняй на настоящем PostgreSQL через Testcontainers (как в этом проекте), а сбои сети, таймауты и ответы внешних API проще и быстрее симулировать заглушкой (WireMock), не мучая реальную инфраструктуру.

**Резюме:** в интеграционных тестах проверяют не все негативные сценарии, а все типы принципиальных сбоев взаимодействия, которые могут нарушить контракт или сломать консистентность данных.

## Типичные сценарии

**Негативный тест** - проверяй только статус, тело обычно не важно:

```cs
[Test]
public async Task CreateNote_WithEmptyText_Returns400()
{
    var response = await factory.CreateClient()
        .PostAsJsonAsync(
            "/api/v1/notes",
            new { texts = new Dictionary<string, string> { ["ru"] = "" } });

    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
}
```

**GET-эндпоинт** - см. `HealthEndpointTests`, `PingEndpointTests`:

```cs
[Test]
public async Task Ping_ReturnsOk_ViaWolverine()
{
    var response = await factory.CreateClient().GetAsync("/api/v1/ping");

    await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<PongResponse>();
    await Assert.That(body).IsNotNull();
    await Assert.That(body!.Status).IsEqualTo("ok");
}
```

**Тип ответа** объявляй как локальный `record` рядом с тестами (`internal record NoteResponse(...)`) - отдельный DTO ради теста не нужен.

## Грабли

- **Забыл `await` у `Assert.That`** - ассерт не выполнится, тест ложно зелёный. Всегда `await`.
- **Забыл `[NotInParallel]`** - классы пойдут параллельно на одной БД, Respawn затрёт данные чужого теста -> плавающие падения.
- **Зависимость между тестами одного класса** - порядок не гарантирован. Каждый тест сам готовит свои данные.
- **Снапшот с нескрабленным Id/датой** - падает каждый прогон. Скрабь всё нестабильное.
- **Снапшот со случайным входом (`Faker`)** - нестабилен сам ответ. Для снапшота вход фиксированный.
- **Docker не запущен** - тесты не стартуют вовсе. Проверь Docker Desktop.

## Новый тестовый класс: чеклист

1. Создай файл `<Entity>EndpointTests.cs` в `tests/DotnetVue3TemplateRu.IntegrationTests`.
2. Скопируй шаблон из раздела "Анатомия теста" (два атрибута + `[Before(Test)]`).
3. Пиши тесты по Arrange-Act-Assert, имена по схеме `Метод_Условие_Результат`.
4. Позитив - проверяй статус + тело + БД. Негатив - статус. Контракт - снапшот.
5. Прогони `yarn nx run integration-tests:test`. Для снапшотов прими `*.received.txt` через `dotnet-verify accept` и закоммить `*.verified.txt`.
