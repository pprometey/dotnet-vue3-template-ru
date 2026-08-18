# Вывести демо-срез Notes

`Ping` и `Notes` достались проекту от шаблона. Они не заглушки: это работающий сквозной срез, который показывает весь путь от формы в SPA до строки в PostgreSQL, и на нём же держится часть проверок инфраструктуры. Поэтому удалять их сразу после генерации нельзя - сначала нужен первый реальный модуль, на который переедут якоря и тесты.

`Ping` не выводится вообще. Разберём почему, а потом - порядок вывода `Notes`.

## Ping остаётся навсегда

`Ping` выглядит демо-эндпоинтом, но выполняет две работы, которые никуда не денутся.

**Он подключает сборку Application к обнаружению хендлеров.** В `Program.cs` Wolverine ищет обработчики так:

```csharp
options.Discovery.IncludeAssembly(typeof(PingQuery).Assembly);
```

Тип `PingQuery` здесь - якорь: он нужен только чтобы назвать сборку. Удалив его, вы обязаны подставить другой тип из той же сборки, иначе Wolverine не найдёт ни одного хендлера, и приложение поднимется без единой рабочей операции - без ошибки на старте, что делает поломку особенно неприятной.

**На нём ездят тесты ограничения частоты запросов.** `RateLimitingEndpointTests` долбит `/api/v1/ping`, потому что ему нужен дешёвый эндпоинт без базы и без аутентификации. Любая замена будет тем же самым `Ping` под другим именем.

Плюс это просто полезный диагностический эндпоинт: он отвечает, если жив процесс и работает конвейер Wolverine, но не трогает базу - тем и отличается от `/health`.

## Что держит Notes

Прежде чем удалять, посмотрите на список - он объясняет, почему это не операция "удалить папку".

| Что                       | Где                                                                                                                                                             | Почему держит                                                                                                                   |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Якоря сборок в арх-тестах | `LayeringTests.cs:18-19` - `typeof(Note).Assembly`, `typeof(CreateNoteCommand).Assembly`; `DomainModelingTests.cs:19` - `typeof(CoreNote).Assembly`             | Без этих типов арх-тесты не компилируются вовсе                                                                                 |
| Единственная миграция     | `Persistence/Migrations/*_Initial.cs`                                                                                                                           | Создаёт только `notes` и `note_localizations`. После удаления сущности миграцию надо пересоздать                                |
| Модель в `DbContext`      | `Persistence/<Project>DbContext.cs`                                                                                                                             | `DbSet<Note>`, настройка `Note` и вызов `ConfigureLocalization<Note, NoteLocalization>`                                         |
| Регистрации в DI          | `Core.Infrastructure/DependencyInjection.cs:30-31`                                                                                                              | `INoteRepository` и `INoteQueryRepository`                                                                                      |
| Коды ошибок               | `Core.Domain/Errors/ErrorCodes.cs:22` - вложенный класс `Note`; переводы в трёх `ErrorMessages*.resx`                                                           | На них ссылается `ErrorCodesLocalizationTests`                                                                                  |
| Тесты инфраструктуры      | `ErrorHandlingEndpointTests` (404, 400, CORS preflight), `ErrorLocalizationEndpointTests`, `LocalizationMappingTests`                                           | Проверяют не Notes, а сквозные механизмы - но ездят через `/api/v1/notes`, потому что им нужен хоть какой-то настоящий эндпоинт |
| Тесты самого среза        | `NotesEndpointTests`, `CreateNoteCommandValidatorTests`, снапшот `Snapshots/NotesEndpointTests/`                                                                | Удаляются вместе со срезом                                                                                                      |
| Корневой маршрут SPA      | `apps/frontend/web/src/router/index.ts`                                                                                                                         | `{ path: "/", redirect: { name: "notes" } }` плюс импорт `notesRoutes`                                                          |
| Моки MSW                  | `libs/frontend/api-client/src/mocks/overrides/notes/` и `handlers.ts`                                                                                           | Раздают ответы для Storybook и Vitest                                                                                           |
| Примеры в документации    | `docs/guides/entity-localization.md`, `docs/backend/core/tutorial.md`, `docs/backend/core/reference.md`, ADR 0010, 0021, 0026, 0031, `docs/guides/wolverine.md` | Notes - сквозной пример почти во всех руководствах                                                                              |

Всего файлов с `Note` в имени или содержимом - около 37.

## Порядок вывода

Выводить `Notes` имеет смысл только тогда, когда первый реальный модуль уже написан, у него есть хотя бы одна команда, одна сущность и один эндпоинт. Иначе якорям и тестам инфраструктуры не на что переехать.

**1. Переставьте якоря сборок.** В `LayeringTests.cs` и `DomainModelingTests.cs` замените `Note` и `CreateNoteCommand` на сущность и команду своего модуля. В `Program.cs` замените `typeof(PingQuery)` только если убираете и `Ping` - но его убирать не надо.

**2. Переведите тесты инфраструктуры на свой эндпоинт.** `ErrorHandlingEndpointTests`, `ErrorLocalizationEndpointTests` и `LocalizationMappingTests` должны ездить через ваш эндпоинт: они проверяют обработку ошибок и локализацию, а не Notes. Прогоните их до удаления Notes - так вы отделите поломку от переезда.

**3. Удалите код среза.** Домен (`Notes/`), Application (`Notes/`), Infrastructure (`Persistence/Notes/`), контроллер `NotesController.cs`, регистрации в `DependencyInjection.cs`, `DbSet<Note>` и настройку модели в `DbContext`, вложенный класс `ErrorCodes.Note` и его ключи из трёх `.resx`.

**4. Удалите тесты среза** - `NotesEndpointTests`, `CreateNoteCommandValidatorTests` и каталог снапшотов `Snapshots/NotesEndpointTests/`.

**5. Пересоздайте начальную миграцию.** Если проект ещё не разворачивался нигде, кроме локальной машины, проще всего удалить `Persistence/Migrations/` целиком и сделать миграцию заново:

```bash
dotnet ef migrations add Initial -p libs/backend/core/<Project>.Core.Infrastructure -s apps/backend/<Project>.Api
```

Если база уже развёрнута где-то, кроме разработки, миграцию не удаляют, а добавляют новую - с удалением таблиц `notes` и `note_localizations`.

**6. Уберите фронтенд-часть.** Каталог `apps/frontend/web/src/pages/notes/`, импорт `notesRoutes` и редирект в `src/router/index.ts`. Корневому маршруту нужен новый адресат - иначе `/` останется без обработчика и попадёт в `not-found`. Затем `libs/frontend/api-client/src/mocks/overrides/notes/` и упоминания в `mocks/index.ts` и `handlers.ts`.

**7. Перегенерируйте контракт и клиент.**

```bash
dotnet build <Project>.slnx
yarn nx run api-client:generate
```

Первая команда перепишет `openapi/<Project>.Api.json` - его надо закоммитить, иначе хук `pre-push` остановит пуш из-за расхождения спеки.

**8. Обновите документацию.** Notes - сквозной пример в руководствах и нескольких ADR (таблица выше). Замените его примерами из своего модуля: документация с примерами на несуществующей сущности хуже, чем документация без примеров.

## Проверка

```bash
grep -ri "note" --include="*.cs" --include="*.ts" --include="*.vue" . --exclude-dir=node_modules
dotnet build <Project>.slnx
yarn nx run architecture-tests:test
yarn nx run integration-tests:test
yarn nx run web:test
git diff --exit-code -- apps/backend/<Project>.Api/openapi/
```

Последняя команда должна показать пустой diff после сборки: спека перегенерирована и закоммичена.
