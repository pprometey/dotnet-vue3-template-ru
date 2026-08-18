# Guide: API-клиент и dev API UI

TS-клиент генерируется из OpenAPI через Orval; почему так - в
[ADR: Автогенерация API-клиента](../adr/0026-auto-generate-api.md).

## Генерация клиента

```bash
yarn nx run api-client:generate    # dotnet build Api -> orval
```

Таргет сначала собирает `DotnetVue3TemplateRu.Api` (чтобы получить актуальный
OpenAPI-документ), затем запускает Orval по
`libs/frontend/api-client/orval.config.ts` - модели и репозитории
(с интеграцией TanStack Query) кладутся в `libs/frontend/api-client/src`.

## Версии API

API версионируется по URL-сегменту (`/api/v1/...`), все версии лежат в одном общем OpenAPI-документе (см. [ADR: Версионирование API](../adr/0016-api-versioning.md)). Для генерации клиента это означает минимум обвязки:

- build-time экспорт пишет один документ `DotnetVue3TemplateRu.Api.json` со всеми версиями;
- `orval.config.ts` имеет один вход и генерирует единый клиент в `src/generated` (опция Orval `workspace` создаёт корневой barrel-индекс, который реэкспортирует `src/index.ts`);
- модели группируются по тегам (`schemas.splitByTags`): тип одного тега лежит в `src/generated/models/<tag>/`, общий (используемый несколькими тегами) - в корне `src/generated/models/`; корневой barrel реэкспортирует всё, поэтому потребители импортируют типы из `@dotnet-vue3-template-ru/api-client` без привязки к раскладке;
- версии различимы по имени функции: пути `/api/v1/...`, `/api/v2/...` дают `getApiV1NotesId` / `getApiV2NotesId`, `usePostApiV1Notes` / `usePostApiV2Notes` и т.д. - неймспейсы не нужны.

Потребление: `import { useGetApiV2NotesId } from "@dotnet-vue3-template-ru/api-client"`.

Условие корректности: DTO разных версий должны иметь разные имена (`NoteResult` vs `NoteResultV2`) - иначе в общем документе будет коллизия схем. Новую версию эндпоинта добавляют новым методом контроллера с `[MapToApiVersion("N.0")]` (см. [guide: добавить backend-модуль](add-backend-module.md)); конфиг Orval и `src/index.ts` менять не нужно.

Генерацию запускают по требованию (или при старте проекта), но **не** на
git-хуках - иначе появляется незакоммиченный сгенерированный код.

## Соглашение: long идёт строкой

C# `long` (`int64`) всегда сериализуется в JSON строкой и в TS-клиенте получает тип `string`, а не `number`. Причина: JS `Number` безопасно держит целые только до 2^53-1 (~9e15), а `long` доходит до ~9.2e18; при этом точность теряется уже на `JSON.parse`, поэтому одного TS-типа `string` мало - значение должно идти строкой по проводу.

Правило задано на бэкенде (источник контракта) и работает автоматически для любого `long`/`long?`:

- сериализация: `LongAsStringJsonConverter` / `NullableLongAsStringJsonConverter` (`apps/backend/DotnetVue3TemplateRu.Api/Serialization/`), подключены через `AddControllers().AddJsonOptions(...)`;
- OpenAPI-спек: `Int64AsStringSchemaTransformer` приводит `integer/int64` к `string`, чтобы документ совпадал с рантаймом и Orval сгенерировал `string`.

Менять `orval.config.ts` для этого не нужно - спек уже содержит `string`. На вход (десериализация) конвертер терпимо принимает и строку, и число.

## API UI (dev)

В режиме разработки (`ASPNETCORE_ENVIRONMENT=Development`) backend отдаёт
интерактивный UI через [Scalar](https://scalar.com) поверх нативного OpenAPI
.NET:

- `/scalar/v1` - сам UI с "try it" (один документ со всеми версиями эндпоинтов);
- `/openapi/v1.json` - сырой документ (его же читает Orval);
- корень `/` редиректит на `/scalar/v1`, поэтому базовый адрес бэкенда из
  Aspire-дашборда сразу открывает UI.

В проде UI и редирект не подключаются.

## См. также

- [ADR: Автогенерация API-клиента](../adr/0026-auto-generate-api.md) - почему генерация, а не ручной клиент.
- [Guide: добавить frontend-модуль](add-frontend-module.md) - кто потребляет клиент.
- [Guide: моки API](api-mocks.md) - MSW-хендлеры из того же контракта для Storybook/dev/тестов.
