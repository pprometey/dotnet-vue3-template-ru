# Справочник: Core

Что есть в модуле сейчас. Обоснования - [explanation.md](explanation.md).

## Эндпоинты

| Метод и путь                        | Доступ               | Что делает                                                |
| ----------------------------------- | -------------------- | --------------------------------------------------------- |
| `GET /api/v1/ping`                  | анонимно, rate limit | Проверка живости через Wolverine, без обращения к БД      |
| `POST /api/v1/notes`                | анонимно, rate limit | Создаёт заметку из набора переводов; `201` + `NoteResult` |
| `GET /api/v1/notes/{id}`            | анонимно             | Заметка, текст на культуре запроса с фолбэком             |
| `GET /api/v2/notes/{id}`            | анонимно             | То же плюс `textLength`                                   |
| `GET /api/v1/configurations/client` | анонимно, rate limit | Список культур интерфейса и культура по умолчанию         |
| `GET /api/v1/session-context`       | `[Authorize]`        | `userId` (claim `sub`) из токена                          |
| `GET /health`, `GET /alive`         | анонимно             | Готовность (включая БД) и живость                         |
| `GET /`                             | только Development   | Редирект на `/scalar/v1`; исключён из OpenAPI             |

## Типы Domain

| Тип                        | Файл                                 | Назначение                                             |
| -------------------------- | ------------------------------------ | ------------------------------------------------------ |
| `Entity`                   | `SeedWork/Entity.cs`                 | Базовая сущность: `Guid Id`, равенство по идентичности |
| `AggregateRoot`            | `SeedWork/AggregateRoot.cs`          | Корень агрегата: добавляет rowversion `Version`        |
| `ISoftDeletable`           | `SeedWork/ISoftDeletable.cs`         | Маркер мягкого удаления: `DeletedAtUtc`                |
| `EquatableArray<T>`        | `SeedWork/EquatableArray.cs`         | Массив со значимым равенством для value object         |
| `DomainException`          | `Errors/DomainException.cs`          | Нарушение инварианта: несёт `ErrorCode` и `Args`       |
| `ErrorCodes`               | `Errors/ErrorCodes.cs`               | Каталог кодов модуля                                   |
| `Result<T>`                | `Errors/Result.cs`                   | Результат фабрики переиспользуемого VO                 |
| `LocalizationEntity`       | `Localization/LocalizationEntity.cs` | База строки перевода: `RelationId`, `Culture`          |
| `Note`, `NoteLocalization` | `Notes/Models/`                      | Демо-агрегат и его переводы                            |
| `INoteRepository`          | `Notes/Repositories/`                | Write-контракт агрегата                                |

## Типы Application

| Тип                                                                                                 | Назначение                                        |
| --------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| `IUserContext`                                                                                      | Идентичность запроса: `IsAuthenticated`, `UserId` |
| `IUserContextResolver`                                                                              | Порт разбора claims в `UserContextSnapshot`       |
| `CultureOptions`                                                                                    | Культуры интерфейса из секции `Cultures`          |
| `NotFoundException`                                                                                 | Ресурс не найден: код + аргументы, отдаёт `404`   |
| `UpstreamUnavailableException`                                                                      | Внешний сервис недоступен, отдаёт `502`           |
| `INoteQueryRepository`                                                                              | Read-порт демо-сущности                           |
| `PingQuery`, `CreateNoteCommand`, `GetNoteQuery`, `ConfigurationGetQuery`, `SessionContextGetQuery` | Операции                                          |

## Типы Infrastructure

| Тип                                     | Назначение                                                   |
| --------------------------------------- | ------------------------------------------------------------ |
| `DotnetVue3TemplateRuDbContext`         | Контекст EF Core; `DbSet<Note>`                              |
| `DotnetVue3TemplateRuDbContextFactory`  | Design-time фабрика для инструментов EF                      |
| `NoteRepository`, `NoteQueryRepository` | Реализации write- и read-порта                               |
| `StandardClaimsUserContextResolver`     | Разбор `sub`                                                 |
| `RequestUserContext`                    | Контейнер идентичности на запрос                             |
| `ModelBuilderLocalizationExtensions`    | `ConfigureLocalization<TEntity, TLocalization>`              |
| `ModelBuilderSoftDeleteExtensions`      | `ApplySoftDeleteConvention`                                  |
| `SoftDeleteSaveChangesInterceptor`      | Превращает удаление в пометку                                |
| `WolverineDurability`                   | `UseDotnetVue3TemplateRuDurability`, enum `MessagingProfile` |
| `KeysetPagination`                      | Постраничная выборка по курсору                              |

## Типы Api

| Тип                                                                | Назначение                                               |
| ------------------------------------------------------------------ | -------------------------------------------------------- |
| `GlobalExceptionHandler`                                           | Единственная точка резолва текста ошибки; ProblemDetails |
| `UserContextMiddleware`                                            | Заполняет контекст, кладёт в `HttpContext.Items`         |
| `AuthenticationExtensions`                                         | `AddJwtAuthentication`: Authority, Audience, JWKS        |
| `LongAsStringJsonConverter`                                        | `long` в JSON как строка                                 |
| `Int64AsStringSchemaTransformer`, `Int32AsNumberSchemaTransformer` | Правка типов в OpenAPI                                   |
| `RateLimitingOptions`, `RateLimitPolicies`                         | Политика `public`                                        |
| `DatabaseMigrationExtensions`                                      | Применение миграций на старте в Development              |

## Схема БД

```text
notes
  id                uuid          PK
  text              varchar(1000) NOT NULL   -- значение культуры по умолчанию, инлайн
  created_at        timestamptz   NOT NULL

note_localizations
  id                uuid          PK
  text              varchar(1000) NOT NULL
  relation_id       uuid          NOT NULL  FK -> notes(id) ON DELETE CASCADE
  culture           varchar(16)   NOT NULL
  UNIQUE (relation_id, culture)
```

Схема `wolverine` - message store шины, ею владеет Wolverine и создаёт сам на старте.

## Конфигурация

| Секция              | Ключи                                           |
| ------------------- | ----------------------------------------------- |
| `ConnectionStrings` | `dotnet-vue3-template-ru-db`                    |
| `Jwt`               | `Authority`, `Audience`                         |
| `Cultures`          | `DefaultCulture`, `SupportedCultures[].Culture` |
| `Cors`              | `AllowedOrigins[]`                              |
| `Messaging`         | `Durability`: `Persistent` или `InMemory`       |
| `RateLimiting`      | `PermitLimit`, `WindowSeconds`                  |
| `Database`          | `RunStartupMigrations`                          |

Порядок источников: `appsettings.json`, `appsettings.{Environment}.json`, переменные окружения, `appsettings.Local.json`. Последний перебивает всё, включая значения, которые подставляет Aspire. Что в него кладут и когда он нужен - [гайд по локальной конфигурации](../../guides/local-configuration.md).

## Коды ошибок

| Код                           | Когда                                    |
| ----------------------------- | ---------------------------------------- |
| `note.text.required`          | Пустой текст на культуре по умолчанию    |
| `note.text.too_long`          | Текст длиннее 1000 символов              |
| `note.not_found`              | Заметки с таким идентификатором нет      |
| `common.bad_request`          | Прочий `400`                             |
| `common.concurrency_conflict` | Конфликт оптимистичной блокировки, `409` |
| `common.rate_limit_exceeded`  | Превышен лимит частоты запросов, `429`   |
| `common.unexpected_error`     | Непредвиденная ошибка, `500`             |
