# Project Map

## What this is

DotnetVue3TemplateRu - монорепозиторий-шаблон: SPA на Vue 3 плюс модульный монолит на .NET 10 (Clean Architecture, CQRS через Wolverine), PostgreSQL, локальный стек через .NET Aspire с AppHost на TypeScript. Контракт API - OpenAPI-first: C#-контроллеры источник истины, TS-клиент генерирует Orval.

Стадия - каркас. Предметных модулей нет: `Ping` и `Notes` - сквозные срезы, показывающие весь путь от формы в SPA до строки в PostgreSQL. Удалять их сразу нельзя: на `Notes` держатся 16 из 26 интеграционных тестов и якоря сборок в арх-тестах, а `Ping` подключает сборку Application к обнаружению хендлеров Wolverine. Порядок вывода - `docs/guides/retire-demo-slice.md`.

## Repository layout

```text
apps/backend/<Project>.Api                 точка входа, контроллеры, DI, источник OpenAPI
apps/backend/<Project>.ServiceDefaults     Serilog, OpenTelemetry, health checks, service discovery
apps/frontend/web                          SPA: core/ (инфраструктура) + pages/ (разделы)
apps/aspire-host                           nx-проект: составной таргет serve -> aspire run
libs/backend/core/<Project>.Core.*         Domain / Application / Infrastructure
libs/frontend/api-client                   сгенерированный Orval клиент (руками не править)
tests/<Project>.IntegrationTests           Testcontainers + Respawn + Verify
tests/<Project>.ArchitectureTests          NetArchTest, без Docker
tests/<Project>.<Слой>.UnitTests           юнит-тесты одного слоя, без Docker (Api, Core.Application, Core.Infrastructure)
aspire-apphost/apphost.mts                 оркестрация локального стека
aspire-apphost/logto/bootstrap.mts         настройка Logto: клиент, ресурс API, почта, пользователи
docs/                                      ADR, архитектура, гайды, дизайн ядра
scripts/init-from-template.sh              развернуть новый проект из шаблона
```

Корневые конфиги: `nx.json`, `package.json` + `yarn.lock` (yarn workspaces), `tsconfig.base.json`, `eslint.config.mjs` (в нём же границы фронтенда), `DotnetVue3TemplateRu.slnx`, `Directory.Build.props`, `Directory.Packages.props` (Central Package Management), `nuget.config`, `global.json`, `aspire.config.json`, `.editorconfig`.

## Backend: layers and dependency rule

| Слой            | Проект                                     | Содержимое                                                                    |
| --------------- | ------------------------------------------ | ----------------------------------------------------------------------------- |
| Domain          | `DotnetVue3TemplateRu.Core.Domain`         | Сущности, value object, инварианты, коды ошибок, контракты write-репозиториев |
| Application     | `DotnetVue3TemplateRu.Core.Application`    | Команды, запросы, хендлеры, валидаторы, порты (read-порты, шов идентичности)  |
| Infrastructure  | `DotnetVue3TemplateRu.Core.Infrastructure` | EF Core, репозитории, миграции, durability Wolverine, реализации портов       |
| Api             | `DotnetVue3TemplateRu.Api`                 | Контроллеры, middleware, обработчик ошибок, композиционный корень             |
| ServiceDefaults | `DotnetVue3TemplateRu.ServiceDefaults`     | Сквозное для .NET-сервисов: телеметрия, health, discovery                     |

Правило: `Api -> Application -> Domain`, `Infrastructure -> Application/Domain`. Плюс `Application` не зависит ни от EF Core, ни от ASP.NET Core. Всё это проверяют арх-тесты (ADR 0015), а не только ревью.

## Backend: conventions

- **Модуль - три проекта** в `libs/backend/<domain>/` (папка kebab-case, проекты PascalCase `DotnetVue3TemplateRu.<Module>.<Layer>`). Модуль обращается к соседнему только через его `Contracts` (ADR 0006).
- **Domain-слой:** папка-агрегат делится на `Models/` и `Repositories/`; namespace зеркалит папку. Ядро (`SeedWork/`, `Errors/`, `Localization/`) плоское (ADR 0009).
- **Application-слой:** сущность - верхняя группировка, внутри `Commands/<Op>/` и `Queries/<Op>/`, один тип на файл. Read-порт `I<Entity>QueryRepository` - в корне папки сущности (ADR 0010).
- **Хендлер - static-класс** `*Handler` со static-методом `Handle`; зависимости приходят параметрами метода, не конструктором (ADR 0013).
- **Команда - полный вход операции.** Край (контроллер) снимает идентичность из `IUserContext` и кладёт явным полем; хендлер окружающий контекст не читает (ADR 0011).
- **Реды - через read-порт**, write-репозиторий в запросе появляется только чтобы мутировать (правило 13 в CLAUDE.md).
- **PostgreSQL, snake_case, timestamptz.** Имена таблиц и колонок в нижнем регистре с подчёркиваниями (`UseSnakeCaseNamingConvention`). `DateTimeOffset` только с нулевым смещением: `DateTimeOffset.UtcNow`, не `.Now` - иначе Npgsql падает на первом сохранении. Сравнение строк регистрозависимое (ADR 0007).
- **Конфигурация - штатный конвейер .NET.** Секции лежат в корне (`Jwt`, `Cultures`, `Cors`, `RateLimiting`, `Messaging`, `ConnectionStrings`), обёртки над ними нет. Строка подключения читается штатным `GetConnectionString("dotnet-vue3-template-ru-db")`. `appsettings.Local.json` (gitignored) подключается последним и перебивает всё, включая переменные Aspire.
- **Идентичность:** `IUserContext` несёт только `UserId` (claim `sub`) - ни прав, ни профиля. Профиль стандарт адресует SPA через ID-токен, поэтому почту показывает фронтенд, а не бэкенд. Разбор claims - за `IUserContextResolver`; заполняет `UserContextMiddleware`, кладёт в `HttpContext.Items` (Wolverine исполняет хендлер в другом DI-скоупе). Права контекст не несёт: доступ решает обработчик предметного модуля по своим данным (ADR 0023). Провайдер - Logto (ADR 0036).
- **Ошибки:** домен бросает `DomainException` с кодом из `ErrorCodes`; текст резолвится один раз в `GlobalExceptionHandler` из resx на культуре запроса. Ответ - ProblemDetails с `errorCode` (ADR 0017, 0018).
- **Валидация:** FluentValidation на команде, в подпапке своей операции; прогоняется middleware Wolverine до хендлера (ADR 0019).
- **Локализация контента:** таблица переводов `<entity>_localizations` + инлайн-значение культуры по умолчанию; хелпер `ConfigureLocalization` (ADR 0021).
- **Мягкое удаление:** маркер `ISoftDeletable` + интерцептор + конвенция query-фильтра. Пока ни одна сущность его не реализует (ADR 0022).
- **Wolverine:** режим `Solo` + message store в схеме `wolverine`; локальные очереди буферные, durable включается точечно per-queue (ADR 0014).
- **Исходящий HTTP:** типизированный клиент фабрики, устойчивость наследуется из ServiceDefaults, база в Options с `ValidateOnStart` (ADR 0025).

## Frontend

- **SPA `apps/frontend/web`** владеет всей вкладкой: `createWebHistory`, один composition root в `main.ts` (один Pinia, один `QueryClient`, один i18n, один OIDC-клиент). Адреса приходят через `import.meta.env` (ADR 0027).
- **Уровни:** `app` (main/App/router) -> `core` (каркас, i18n, auth, сервисы, тема) -> `page` (`pages/<page>/`, ветка маршрутов) -> `feature` (`pages/<page>/features/<feature>/`), плюс `page-shared`. Страница не импортирует сестру, фича не импортирует сестру. Проверяет `eslint-plugin-boundaries` в `eslint.config.mjs`; в настройке важен `dependency-nodes: ["import", "export", "dynamic-import"]` - без него барели и ленивые импорты маршрутов не проверялись бы (ADR 0028).
- **Серверное состояние - TanStack Query** через сгенерированные Orval composables. UI-состояние - обычные `ref`/`reactive` (ADR 0029).
- **i18n:** словари лежат рядом с кодом в `**/i18n/<locale>.json` и собираются глобом. Культуры `ru`, `en`, `kk`; список приходит с бэкенда, чтобы не разъехаться с resx.
- **Тема:** Element Plus напрямую, переопределения переменных в `core/theme/element-vars.scss` через `@forward`; подмешивается в каждый `.scss` через `additionalData`, sass резолвит по `loadPaths`, а не по алиасам TypeScript.
- **Моки MSW:** сгенерированные faker-хендлеры плюс курируемые override в `libs/frontend/api-client/src/mocks/overrides/`. Используются в Storybook, в Vitest и в dev по `VITE_API_MOCKING=enabled` (ADR 0033).
- **Контракт API:** `dotnet build Api` пишет `openapi/DotnetVue3TemplateRu.Api.json` (коммитится), Orval генерирует клиент в `libs/frontend/api-client/src/generated/` (gitignored). Хук pre-push стережёт дрейф спеки.

## Порты закреплены жёстко

5173 (SPA) задан в `vite.config.ts`, `apphost.mts`, `Cors:AllowedOrigins` и в списке адресов возврата, который заводит `bootstrap.mts`. 3481 (Logto, выдача токенов) и 3482 (консоль) - в `apphost.mts` (`LOGTO_PORT`, `LOGTO_ADMIN_PORT`) и `.env.development`; внутри контейнера Logto слушает 3001 и 3002 (`targetPort`). Порты Logto публикуются мимо прокси DCP (`isProxied: false`), потому что адрес провайдера обязан совпадать в issuer выпущенного токена и в `Jwt:Authority`, по которому API этот issuer сверяет. 1425 и 8425 - приём и чтение почты в Mailpit.

PostgreSQL один на весь стек, баз на нём две: `dotnet-vue3-template-ru-db` и `logto-db`. Порт случайный, а пароль задан явно в `apphost.mts`: шаг настройки Logto читает из базы секрет служебного приложения из процесса AppHost, а значение сгенерированного параметра оттуда недоступно. Контейнер Logto получает строку подключения через `uriExpression()` - Aspire отдаёт её в виде URI, который Logto и ждёт.

Порты дашборда Aspire (18181/16197/21316/22120) и API (5249/7324) закреплены жёстко. Проект, развёрнутый из шаблона, сдвигает все внешние порты разом через `--port-offset` у `scripts/init-from-template.sh` - иначе два приложения на этом стеке одновременно не поднимутся.

## Run and test

```bash
yarn dev                                  # весь стек: PostgreSQL, Logto, Mailpit, API, SPA
yarn nx run api:serve                     # только backend
yarn nx run api-client:generate           # пересобрать TS-клиент из OpenAPI
dotnet build DotnetVue3TemplateRu.slnx
yarn nx run architecture-tests:test       # быстрые, без Docker
yarn nx run-many --target=test --projects=*-unit-tests   # юнит-тесты всех слоёв, без Docker
yarn nx run integration-tests:test        # поднимает контейнер PostgreSQL
yarn nx run web:test                      # Vitest + MSW
yarn nx run-many --target=lint
```

Первый запуск `yarn dev` дольше обычного: Logto создаёт схему в своей базе, а скрипт настройки заводит клиента, ресурс API и пользователей. Если Aspire падает с `Cannot access a disposed object`, порты дашборда держит прошлый запуск - см. Troubleshooting в README.

## Current features (so you do not search)

- `GET /api/v1/ping` - Wolverine без БД.
- `POST /api/v1/notes`, `GET /api/v1/notes/{id}`, `GET /api/v2/notes/{id}` - демо-срез: версионирование, валидация, коды ошибок, локализация контента. `Note` - временная сущность.
- `GET /api/v1/configurations/client` - список культур интерфейса, анонимный.
- `GET /api/v1/session-context` - `userId` из токена, требует `[Authorize]`.
- `/health` и `/alive` из ServiceDefaults; `/scalar/v1` в Development.
- Раздел `/notes` в SPA - сквозной срез: форма -> Orval -> API -> Wolverine -> EF Core -> PostgreSQL и обратно. Две фичи (`create-note`, `note-view`) не знают друг о друге; их связывает страница.

## Where to look for details

| Тема                                | Документ                             |
| ----------------------------------- | ------------------------------------ |
| Устройство системы, стек, рантайм   | `docs/architecture.md`               |
| Все решения с обоснованиями         | `docs/adr/README.md` (0001-0037)     |
| Design-time дизайн ядра             | `docs/spec/arch-design/`             |
| Что и как тестируем                 | `docs/spec/test-strategy.md`         |
| Практические задачи                 | `docs/guides/`                       |
| Локальные и приватные файлы         | `docs/guides/local-configuration.md` |
| Запуск, требования, troubleshooting | `README.md`                          |
