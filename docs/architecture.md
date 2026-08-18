# Архитектура DotnetVue3TemplateRu

Целостный обзор: что это за система, из чего состоит, как части связаны и
почему выбраны такие инструменты. Онбординг (как поднять проект) - в корневом
[README.md](../README.md). Обоснования отдельных решений - в [ADR](adr/README.md).

## Назначение

Каркас продуктового веб-приложения: одностраничное приложение во вкладке
браузера, REST API за ним, PostgreSQL под ним. Этот раздел заменяется описанием
конкретного продукта, когда он появляется.

Репозиторий сводит вместе две части:

- **Backend** - модульный монолит на ASP.NET Core (Clean Architecture). Один
  процесс, внутри - изолированные модули; сейчас есть только `Core` (сквозная
  инфраструктура) с демо-срезом `Notes`.
- **Frontend** - самостоятельное SPA на Vue 3, которому принадлежит вся
  вкладка: адресная строка, история навигации, состояние сессии.

Локально всё (БД, провайдер идентичности, backend, frontend, телеметрия)
поднимает Aspire, оркеструемый TypeScript AppHost. Сборки и кэш - Nx; пакеты -
yarn workspaces.

## Стадия

Это каркас, а не готовый продукт. Предметных модулей ещё нет: `Ping` и `Notes` -
сквозные срезы, которые существуют, чтобы показать весь путь и проверить, что
механизмы работают. Удалять их сразу нельзя: на `Notes` держится часть тестов
инфраструктуры и якоря сборок в арх-тестах, а `Ping` подключает сборку
Application к обнаружению хендлеров Wolverine. Порядок вывода `Notes` -
[docs/guides/retire-demo-slice.md](guides/retire-demo-slice.md).

## Стек по слоям

| Слой             | Технологии                                                                                      |
| ---------------- | ----------------------------------------------------------------------------------------------- |
| Backend          | .NET 10, ASP.NET Core, Clean Architecture, модульный монолит                                    |
| Persistence      | EF Core + PostgreSQL (snake_case), миграции через EF Core Migrations                            |
| Messaging / CQRS | Wolverine                                                                                       |
| Аутентификация   | OIDC resource server (JWT по JWKS); провайдер - Logto контейнером                               |
| Сквозное         | Serilog, OpenTelemetry, health checks, service discovery (ServiceDefaults); rate limiting (Api) |
| API-контракт     | OpenAPI (нативный .NET) + Scalar UI в dev                                                       |
| Frontend         | Vue 3 (Composition API), Pinia, Vue Router, TanStack Query, Vite                                |
| UI-компоненты    | Element Plus + своя тема                                                                        |
| Вход в SPA       | oidc-client-ts (authorization code + PKCE)                                                      |
| API-клиент       | Orval генерирует TS-клиент (vue-query) из OpenAPI                                               |
| Документация UI  | Storybook (порт 6006)                                                                           |
| Монорепо         | Nx + yarn workspaces                                                                            |
| Оркестрация      | .NET Aspire, TypeScript AppHost (`apphost.mts`)                                                 |
| Тесты            | TUnit + WebApplicationFactory + Testcontainers (PostgreSQL) + Verify.TUnit; Vitest + MSW        |
| Хуки             | Husky (pre-commit / pre-push)                                                                   |

## Структура каталогов

`<Project>` ниже - имя проекта в PascalCase, `<Module>` - имя модуля, `<domain>` - его же папка в kebab-case.

```text
apps/
  backend/<Project>.Api                  # ASP.NET Core Web API - точка входа, DI, источник OpenAPI
  backend/<Project>.ServiceDefaults      # Aspire: телеметрия, health checks, service discovery
  frontend/web                           # SPA на Vue 3 (страницы, фичи, вход по OIDC)
  aspire-host                            # Nx-проект для `aspire run` (составной serve-таргет)
libs/
  backend/<domain>/<Project>.<Module>.Domain          # сущности, бизнес-правила
  backend/<domain>/<Project>.<Module>.Application     # use-cases, абстракции, handlers
  backend/<domain>/<Project>.<Module>.Infrastructure  # EF Core, репозитории
                                         # domain: сейчас только core
  frontend/api-client                    # сгенерированный Orval-клиент
tests/
  <Project>.IntegrationTests             # интеграционные тесты (Testcontainers)
  <Project>.ArchitectureTests            # правила слоёв (NetArchTest), без Docker
  <Project>.<Слой>.UnitTests             # юнит-тесты слоя, без Docker
aspire-apphost/apphost.mts               # Aspire TypeScript AppHost (Aspire CLI 13)
aspire-apphost/logto/bootstrap.mts       # настройка провайдера: клиент, ресурс, почта, пользователи
docs/                                    # эта документация (architecture, adr, guides)
docs/spec/                               # design-time дизайн ядра и стратегия тестирования
scripts/add-new-module.sh                # генерация нового backend-модуля
scripts/init-from-template.sh            # развернуть новый проект из шаблона
```

Backend-проекты сгруппированы по доменам: три слоя каждого модуля лежат в
`libs/backend/<domain>/` (имя папки в kebab-case), а имена проектов и namespace
остаются в PascalCase (`DotnetVue3TemplateRu.<Module>.<Layer>`).

Правила слоёв backend строгие: `Api -> Application -> Domain`,
`Infrastructure -> Application/Domain`. `apps/backend/` содержит только точки
входа, вся логика - в `libs/backend/`. Правила проверяются архитектурными
тестами, а не только ревью ([ADR-0015](adr/0015-architecture-tests.md)).

Фронтенд-часть асимметрична: приложение одно, а в `libs/frontend/` лежит
единственная библиотека `api-client`. Она библиотека не ради переиспользования,
а потому что генерируется из другого источника и её нельзя править руками.

Структура `web`: `core/` (кросс-раздельная инфраструктура - каркас страницы,
i18n, OIDC-клиент, тема) и `pages/` (по папке на раздел). Внутри раздела -
фичи (`pages/<page>/features/<feature>/`), общее для фич раздела - в
`pages/<page>/shared/`. Разделы и фичи изолированы: сестру импортировать
нельзя, интеграция - уровнем выше. Это проверяет `eslint-plugin-boundaries`
([ADR-0028](adr/0028-frontend-structure.md)).

## Как всё связано в рантайме

```text
                 yarn dev  ->  Aspire (TypeScript AppHost)
                                  |
      +---------------+-----------+-----------+------------------+
      |               |                       |                  |
 PostgreSQL       Logto                      Api             web (Vite)
      ^               ^                  |  ^                    |
      |  EF Core      |  JWKS            |  | OpenAPI            | VITE_API_BASE_URL
      +---------------+------------------+  | (build-time)       | VITE_OIDC_AUTHORITY
                      ^                     v                    v
                      |  code + PKCE     openapi/<Project>.Api.json
                      |                     |
                      |                     | Orval
                      |                     v
                      +-------------  libs/frontend/api-client
                                            ^
                                            |  импорт
                                       apps/frontend/web
```

- Aspire поднимает в Docker PostgreSQL, Logto и Mailpit, плюс backend и SPA,
  которому прокидывает адрес API и адрес провайдера идентичности - без
  захардкоженных портов в коде.
- Backend на build-time генерирует OpenAPI-документ; Orval читает его и
  генерирует типизированный TS-клиент в `libs/frontend/api-client`.
- SPA импортирует `api-client`, получает токен у Logto по authorization code
  с PKCE и отдаёт его HTTP-клиенту через провайдер токена.
- Backend проверяет подпись токена по JWKS из discovery-документа Logto;
  своих токенов он не выпускает.
- В dev backend отдаёт интерактивный API через Scalar (`/scalar/v1`).

Порт SPA (5173) закреплён жёстко и известен четырём местам: `vite.config.ts`,
AppHost, `Cors:AllowedOrigins` в конфигурации API и списку адресов возврата,
который заводит в Logto скрипт настройки. Случайный порт молча ломал бы либо
CORS, либо вход.

## Ключевые решения (ADR)

Почему именно так - в отдельных ADR (полный список - [docs/adr/](adr/README.md)):

- [Nx](adr/0001-nx.md) и [Yarn](adr/0002-yarn.md) - один репозиторий на Nx + yarn
  workspaces для разнородного стека (.NET + Vue).
- [Модульный монолит](adr/0006-modular-monolith.md) - backend на Clean Architecture
  вместо микросервисов.
- [PostgreSQL](adr/0007-postgresql.md) - СУБД, snake_case в схеме, timestamptz.
- [Медиатор и шина сообщений](adr/0012-wolverine-mediator-and-messaging.md) - Wolverine
  как медиатор/CQRS сейчас и шина сообщений в перспективе, одним стеком.
- [Aspire TS AppHost](adr/0005-aspire-typescript-apphost.md) - .NET Aspire с
  TypeScript AppHost вместо C# AppHost.
- [Аутентификация по OIDC](adr/0023-authentication-oidc.md) - resource server;
  из токена берётся только идентичность, права решает домен.
- [Фронтенд - SPA](adr/0027-frontend-spa.md) - самостоятельное приложение,
  владеющее адресной строкой.
- [Структура фронтенда](adr/0028-frontend-structure.md) - страницы и фичи с
  жёсткой изоляцией через boundaries.
- [Автогенерация API](adr/0026-auto-generate-api.md) - генерация API-клиента через
  Orval из OpenAPI.
- [TanStack Query](adr/0029-tan-stack-query.md) - управление серверным состоянием.
- [Интеграционные тесты](adr/0031-integration-tests.md) - Testcontainers + TUnit + Respawn.

## Дальше

- Практические задачи - в [guides](guides/) (добавить модуль, генерация клиента,
  аутентификация).
- Стратегия тестирования - [docs/spec/test-strategy.md](spec/test-strategy.md).
- Design-time проработка архитектуры - [docs/spec/arch-design/](spec/arch-design/README.md).
