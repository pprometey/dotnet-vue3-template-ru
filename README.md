# DotnetVue3TemplateRu

Шаблон монорепозитория для продуктового веб-приложения: SPA на Vue 3 и модульный монолит на .NET 10 (Clean Architecture, CQRS через Wolverine), PostgreSQL, локальный стек одной командой через Aspire.

Из шаблона разворачивается новый проект, в котором сквозные задачи уже решены: вход по OIDC, локализация интерфейса и содержимого, коды ошибок с переводами, генерация типизированного клиента API из OpenAPI, границы слоёв под защитой тестов. Каждое решение обосновано в [docs/adr/](docs/adr/README.md) - 36 записей.

**Развернуть новый проект** - [docs/guides/create-project-from-template.md](docs/guides/create-project-from-template.md). Коротко:

```bash
git clone https://github.com/pprometey/dotnet-vue3-template-ru.git my-app
cd my-app && rm -rf .git
./scripts/init-from-template.sh --name MyApp --port-offset 100
```

Устройство системы - [docs/architecture.md](docs/architecture.md).

## Стадия

Предметных модулей в шаблоне нет: `Ping` и `Notes` - сквозные срезы, которые показывают весь путь от формы в SPA до строки в PostgreSQL и проверяют, что механизмы работают.

Из сгенерированного проекта они сразу не удаляются: на них держится много интеграционных тестов и якоря сборок в архитектурных тестах. Как вывести `Notes`, когда появится первый реальный модуль, и почему `Ping` остаётся навсегда - [docs/guides/retire-demo-slice.md](docs/guides/retire-demo-slice.md).

## Стек

| Слой           | Технологии                                                                     |
| -------------- | ------------------------------------------------------------------------------ |
| Backend        | .NET 10, ASP.NET Core, Clean Architecture, модульный монолит, Wolverine (CQRS) |
| Хранилище      | PostgreSQL, EF Core, имена в схеме snake_case                                  |
| Аутентификация | OIDC resource server (JWT по JWKS); провайдер - Logto                          |
| Frontend       | Vue 3, Vite, Pinia, Vue Router, TanStack Query, Element Plus                   |
| Контракт API   | OpenAPI (нативный .NET) -> Orval -> TS-клиент с vue-query                      |
| Монорепо       | Nx + yarn workspaces                                                           |
| Оркестрация    | .NET Aspire, AppHost на TypeScript                                             |
| Тесты          | TUnit + Testcontainers + Respawn + Verify; Vitest + MSW                        |

## Требования

- .NET SDK 10
- Node.js 22+ и Corepack (`corepack enable`) - без него yarn нужной версии не подхватится
- Docker Desktop - в нём поднимаются PostgreSQL, Logto, Mailpit и контейнеры интеграционных тестов
- Aspire CLI 13.4.x: `dotnet tool install -g Aspire.Cli`

## Быстрый старт

```bash
corepack enable
yarn install
yarn dev
```

`yarn dev` генерирует TS-клиент из OpenAPI и поднимает через Aspire весь локальный стек:

| Ресурс                        | Адрес                                              |
| ----------------------------- | -------------------------------------------------- |
| Дашборд Aspire                | `https://localhost:18181`                          |
| API                           | `http://localhost:5249` (Scalar UI - `/scalar/v1`) |
| SPA                           | `http://localhost:5173`                            |
| Logto (выдача токенов)        | `http://localhost:3481/oidc`                       |
| Logto (консоль)               | `http://localhost:3482`                            |
| Mailpit (перехваченная почта) | `http://localhost:8425`                            |

Тестовые пользователи: `alice` / `Passw0rd!2026` и `bob` / `Passw0rd!2026`. Их вместе с приложением SPA, ресурсом API и соединителем почты заводит `aspire-apphost/logto/bootstrap.mts` - шаг идемпотентный, поэтому повторный запуск ничего не дублирует, а после удаления тома настройка восстанавливается сама.

Первый запуск дольше обычного: Logto создаёт схему в своей базе. API ждёт его, поэтому до готовности провайдера `http://localhost:5249` не отвечает.

## Команды

```bash
yarn dev                      # весь стек через Aspire
yarn nx run api:serve         # только backend
yarn nx run web:serve         # только SPA (нужен поднятый backend)
yarn nx run api-client:generate  # пересобрать TS-клиент из OpenAPI

dotnet build DotnetVue3TemplateRu.slnx
yarn nx run architecture-tests:test
yarn nx run-many --target=test --projects=*-unit-tests   # юнит-тесты слоёв, без Docker
yarn nx run integration-tests:test
yarn nx run web:test
yarn nx run web:storybook     # Storybook на порту 6006

yarn nx run-many --target=lint
yarn format:write
```

## Структура

```text
apps/backend/DotnetVue3TemplateRu.Api             точка входа, контроллеры, DI, источник OpenAPI
apps/backend/DotnetVue3TemplateRu.ServiceDefaults телеметрия, health checks, service discovery
apps/frontend/web                         SPA: core/ (инфраструктура) + pages/ (разделы)
libs/backend/core/DotnetVue3TemplateRu.Core.*     Domain / Application / Infrastructure
libs/frontend/api-client                  сгенерированный Orval клиент (руками не править)
tests/                                    юнит-тесты по слоям, интеграционные и архитектурные
aspire-apphost/                           AppHost и настройка Logto для разработки
docs/                                     ADR, архитектура, гайды, дизайн ядра
```

## Порты

Все порты проекта заданы явно, случайных нет.

| Порт  | Что                    | Где задан                           |
| ----- | ---------------------- | ----------------------------------- |
| 18181 | Дашборд Aspire (https) | `aspire.config.json`                |
| 16197 | Дашборд Aspire (http)  | `aspire.config.json`                |
| 21316 | OTLP gRPC              | `aspire.config.json`                |
| 22120 | Resource service       | `aspire.config.json`                |
| 5249  | API (http)             | `Properties/launchSettings.json`    |
| 7324  | API (https)            | `Properties/launchSettings.json`    |
| 5173  | SPA                    | `vite.config.ts`, `apphost.mts`     |
| 3481  | Logto, выдача токенов  | `apphost.mts` (`LOGTO_PORT`)        |
| 3482  | Logto, консоль         | `apphost.mts` (`LOGTO_ADMIN_PORT`)  |
| 1425  | Mailpit, приём почты   | `apphost.mts` (`MAILPIT_SMTP_PORT`) |
| 8425  | Mailpit, чтение почты  | `apphost.mts` (`MAILPIT_UI_PORT`)   |

PostgreSQL публикуется на случайный порт: его адрес и приложение, и Logto получают от Aspire, и знать его наизусть не нужно. Сервер один на весь стек, базы на нём две - `dotnet-vue3-template-ru-db` и `logto-db`. Пароль задан в `apphost.mts` явно, а не сгенерирован: шаг настройки Logto ходит в базу за секретом служебного приложения из процесса AppHost, а значение сгенерированного параметра оттуда не прочитать.

**Порт SPA и порт Logto известны нескольким сторонам сразу.** 5173 фигурирует в `vite.config.ts`, `apphost.mts`, `Cors:AllowedOrigins` и в списке адресов возврата, который заводит `aspire-apphost/logto/bootstrap.mts`. 3481 - в `apphost.mts` и в `.env.development`, а через него в issuer выпущенного токена и в `Jwt:Authority`, по которому API этот issuer сверяет. Внутри контейнера Logto слушает 3001 и 3002 (`targetPort`) - меняется только порт на хосте. Менять их поодиночке нельзя: расхождение проявится молчаливым отказом CORS или ошибкой на входе, а не ошибкой сборки.

### Параллельный запуск с другими проектами на Aspire

Все порты закреплены жёстко, поэтому два приложения с одинаковыми значениями одновременно не поднимутся. Проект, развёрнутый из шаблона, получает свой набор портов сразу: `scripts/init-from-template.sh --port-offset N` прибавляет смещение ко всем внешним портам. Как выбрать смещение - [docs/guides/create-project-from-template.md](docs/guides/create-project-from-template.md).

Это важно знать при отладке: Aspire на занятом порту дашборда не пишет "порт занят", а падает с сообщением `Cannot access a disposed object`. Если два проекта делят хотя бы один из четырёх портов дашборда, второй запуск не поднимется никогда, и причина по сообщению не читается.

Порты дашборда меняются в `aspire.config.json`, порты API - в `Properties/launchSettings.json`.

## Troubleshooting

**`Cannot access a disposed object. Object name: 'IServiceProvider'` при `yarn dev`.** Ошибка выглядит внутренней, но почти всегда означает одно: порты дашборда Aspire (18181, 16197, 21316, 22120) заняты предыдущим запуском или другим проектом. Aspire не сообщает об этом внятно. Прошлый запуск, снятый не через Ctrl+C, оставляет живые процессы `dcp` и `aspire-managed` - именно они держат порты.

```powershell
Get-Process | Where-Object { $_.ProcessName -match '^(aspire|aspire-managed|dcp)$' } | Stop-Process -Force
docker ps -aq | ForEach-Object { docker rm -f $_ }
```

Останавливать стек штатно нужно Ctrl+C в терминале с `yarn dev` - тогда осиротевших процессов не остаётся.

**`aspire` не находится в git-bash.** CLI ставится как `aspire.cmd`; в git-bash голое имя не резолвится. Запускайте `yarn dev` из PowerShell или зовите `aspire.cmd`.

**API не поднимается, в логе - обращение к чужому адресу БД.** Проверьте, нет ли `apps/backend/DotnetVue3TemplateRu.Api/appsettings.Local.json`. Этот файл (gitignored) подключается последним и перебивает переменные окружения, которые подставляет Aspire, - в том числе строку подключения и адрес провайдера идентичности. Это его назначение: личный оверрайд поверх всего. Забытый файл с устаревшими адресами уводит приложение мимо поднятого стека. Что в него кладут и когда он нужен - [docs/guides/local-configuration.md](docs/guides/local-configuration.md).

**PostgreSQL отказывает в аутентификации после обновления.** Пароль сервера задан в `apphost.mts` явно, а том с данными помнит тот пароль, с которым его инициализировали. Том, созданный до этого изменения, хранит прежний сгенерированный пароль, и подключение падает на `password authentication failed`. Лечится удалением тома - данные в нём только локальные:

```powershell
docker volume rm dotnet-vue3-template-ru-pgdata
```

**Первый `aspire run` в свежем клоне.** Папку `aspire-apphost/.aspire/modules/` создаёт сам Aspire CLI, в репозитории её нет. До первого запуска `apphost.mts` ссылается на несуществующий модуль - это нормально, файл появится при первом `yarn dev`.

**`pre-push` падает на "unknown revision" в пустом репозитории.** Хук сравнивает OpenAPI-спеку с её версией в `HEAD`, а в репозитории без коммитов `HEAD` нет. Первый коммит делается с `--no-verify` и обязан включать `apps/backend/DotnetVue3TemplateRu.Api/openapi/DotnetVue3TemplateRu.Api.json`; дальше хук работает штатно. Подробнее - [CONTRIBUTING.md](CONTRIBUTING.md).

## Документация

- [docs/architecture.md](docs/architecture.md) - устройство системы, стек, рантайм
- [docs/adr/](docs/adr/README.md) - 37 архитектурных решений с обоснованиями
- [docs/guides/](docs/guides/) - практические задачи: развернуть проект из шаблона, добавить модуль, сгенерировать клиент, написать тест
- [docs/guides/local-configuration.md](docs/guides/local-configuration.md) - какие файлы лежат вне git, что в них кладут и когда они нужны
- [docs/spec/test-strategy.md](docs/spec/test-strategy.md) - что и как тестируем
