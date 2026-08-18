# ADR: Автогенерация API-клиента фронтенда из OpenAPI-схемы бэкенда

- Статус: Принято
- Дата: 2026-06-09
- Контекст: монорепозиторий DotnetVue3TemplateRu, `libs/frontend/api-client`, `apps/backend/DotnetVue3TemplateRu.Api`

## Контекст

Фронтенд - SPA на Vue, которое общается с ASP.NET Core API.
Без автогенерации процесс выглядит так:

1. Бэкенд-разработчик меняет эндпоинт (переименовывает поле, меняет тип, добавляет параметр).
2. Фронтенд-разработчик узнаёт об этом из документации или устного обмена.
3. Фронтенд-разработчик вручную правит TypeScript-типы, URL-строку, обработку ответа.
4. Расхождение обнаруживается в runtime - в браузере, не на этапе сборки.

Это медленно, error-prone и требует постоянной синхронизации двух команд вручную.

## Решение

Выбрана цепочка **OpenAPI + Orval**: C# контроллеры являются единственным
источником истины для HTTP-контракта. Из них автоматически генерируются
TypeScript-типы и Vue Query composables в отдельной библиотеке `@dotnet-vue3-template-ru/api-client`.

Любое изменение API в C# автоматически отражается в TypeScript после одной команды
`nx run api-client:generate`. TypeScript-компилятор показывает ошибки во всех местах,
где используется изменённый контракт - ещё до запуска приложения.

**Почему отдельная библиотека `libs/frontend/api-client`.**
API-клиент - отдельный артефакт с чётко определённой зоной ответственности:
HTTP-коммуникация с одним конкретным бэкендом. Отделение от приложения
`apps/frontend/web` даёт несколько преимуществ:

- Граница видна явно: всё, что касается HTTP, лежит в одном месте.
- Правило "сгенерированное не правят руками" распространяется на папку целиком,
  а не на отдельные файлы вперемешку с рукописными.
- Раздел SPA импортирует только нужные ему composables.
- Nx может пересобирать только эту библиотеку при изменениях в бэкенде
  (не всё дерево зависимостей).

## Механизм генерации: цепочка из 4 звеньев

### Звено 1 - C# контроллер декларирует типы ответов

```csharp
// NotesController.cs
[HttpPost]
[ProducesResponseType<NoteResult>(StatusCodes.Status201Created)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<NoteResult>> Create(
    [FromBody] CreateNoteRequest request, ...)
```

`ActionResult<NoteResult>` + `ProducesResponseType<T>` - это не просто
документация. `Microsoft.AspNetCore.OpenApi` читает эти атрибуты во время сборки
и строит полную OpenAPI-схему: тело запроса, возможные тела ответов, HTTP-статусы,
типы. Контроллер является **единственным источником истины** для контракта --
никаких отдельных YAML-файлов с описанием API.

### Звено 2 - Build-time генерация OpenAPI JSON

В `apps/backend/DotnetVue3TemplateRu.Api/DotnetVue3TemplateRu.Api.csproj`:

```xml
<OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
<OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/openapi</OpenApiDocumentsDirectory>
```

`Microsoft.Extensions.ApiDescription.Server` при каждом `dotnet build` запускает
приложение в специальном режиме (без HTTP-сервера, без реальной БД) и дампит
OpenAPI-схему в файл:

```text
apps/backend/DotnetVue3TemplateRu.Api/openapi/DotnetVue3TemplateRu.Api.json
```

Файл содержит полное описание всех эндпоинтов - методы, URL-параметры, схемы
тел запроса/ответа, HTTP-статусы и их типы. Этот JSON коммитится в репозиторий:
он является артефактом сборки, который читает Orval.

### Звено 3 - Orval читает JSON и генерирует TypeScript

Запускается командой `nx run api-client:generate`, которая по цепочке делает
два шага (см. `libs/frontend/api-client/project.json`):

```json
"commands": [
  "dotnet build apps/backend/DotnetVue3TemplateRu.Api/...",
  "orval --config ./libs/frontend/api-client/orval.config.ts"
]
```

Сначала пересобирается бэкенд (обновляет JSON), затем Orval читает JSON и
генерирует TypeScript.

Конфиг Orval (`libs/frontend/api-client/orval.config.ts`):

```ts
input: {
  target: openApiSpec       // apps/backend/DotnetVue3TemplateRu.Api/openapi/DotnetVue3TemplateRu.Api.json
},
output: {
  workspace: "./src/generated",
  mode: "tags-split",       // один файл на тег (группу эндпоинтов)
  target: "./",
  schemas: { path: "./models", splitByTags: true },   // типы тоже по тегам
  client: "vue-query",      // генерировать Vue Query composables
  httpClient: "axios",
  clean: true,
  indexFiles: true,
  prettier: true,
  mock: { generators: [{ type: "msw" }] },            // MSW-хендлеры (ADR-0033)
  override: {
    mutator: { path: "../mutator/custom-axios.ts", name: "customAxios" }
  }
}
```

`mode: "tags-split"` - эндпоинты группируются по тегу контроллера:

```text
NotesController  --> тег "Notes"  --> src/generated/notes/notes.ts
PingController   --> тег "Ping"   --> src/generated/ping/ping.ts
```

Что генерируется из одного C# эндпоинта (на примере `POST /api/v1/notes`):

```text
C# код                                      TypeScript артефакт
-----------------------------------------------+-----------------------------
[FromBody] CreateNoteRequest                -->  тип CreateNoteRequest
ActionResult<NoteResult>                    -->  тип NoteResult
ProducesResponseType<ValidationProblemDetails>(400) --> тип ошибки
сигнатура метода POST /api/v1/notes            -->  функция postApiV1Notes(data)
                                            -->  composable usePostApiV1Notes(options)
```

Итоговый сгенерированный файл `src/generated/notes/notes.ts` содержит:

- **Типы** (`CreateNoteRequest`, `NoteResult`, `ValidationProblemDetails`) --
  точное отражение C# DTO-классов.
- **Чистую HTTP-функцию** (`postApiV1Notes`) - вызов через customAxios.
- **Vue Query composable** (`usePostApiV1Notes`) - обёртка `useMutation` вокруг
  HTTP-функции, готовая к использованию в компонентах.

### Звено 4 - custom-axios как адаптер

`customAxios` (`libs/frontend/api-client/src/mutator/custom-axios.ts`) - не
просто настроенный Axios. Он делает две вещи:

**Распаковывает `response.data`:** возвращает само тело ответа, а не весь
`AxiosResponse<T>`. Сгенерированный composable получает сразу `NoteResult`,
не `AxiosResponse<NoteResult>`. Разработчик работает с данными напрямую.

**Добавляет `.cancel()` на промис:** TanStack Query вызывает отмену при
демонтировании компонента. Без этого в полёте могут оставаться запросы после
того, как пользователь ушёл с экрана (race conditions, утечки).

`baseURL` мутатор читает из сборочной переменной Vite `import.meta.env.VITE_API_BASE_URL`.
В разработке её подставляет Aspire из адреса ресурса API, в образе - шаг сборки.
Пустое значение означает обращение к тому же origin, откуда загружена страница: это
рабочий режим прода, где SPA и API стоят за общим reverse proxy.

Токен мутатор берёт не из переменной, а через провайдер - функцию, которую
приложение регистрирует один раз при старте. Через функцию потому, что токен
появляется после входа и обновляется автоматическим продлением сессии, то есть
меняется в течение жизни приложения (ADR-0023).

## Использование в компоненте Vue

```ts
import { usePostApiV1Notes } from "@dotnet-vue3-template-ru/api-client";

const { mutate, isPending, error } = usePostApiV1Notes();

function createNote(text: string) {
  mutate({ data: { texts: { ru: text } } });
  // TypeScript знает тип аргумента, IDE подсказывает поля
}
```

Никаких ручных типов, URL-строк, обработки `response.data`. Если в C#
переименовать поле `Text` в `Content` - `nx run api-client:generate` пересоздаст
типы и TypeScript немедленно покажет ошибку компиляции во всех местах, где
используется старое имя. Расхождение обнаруживается в compile time, не в runtime.

## Полная схема цепочки

```text
C# контроллер (атрибуты ProducesResponseType)
  |
  | dotnet build
  v
openapi/DotnetVue3TemplateRu.Api.json   (build-time артефакт, в git)
  |
  | nx run api-client:generate -> orval
  v
libs/frontend/api-client/src/generated/
  +-- notes/notes.ts     (типы + postApiV1Notes + usePostApiV1Notes)
  +-- ping/ping.ts       (типы + getApiPing + useGetApiPing)
  +-- models/            (общие DTO-типы)
  |
  | import { usePostApiV1Notes } from "@dotnet-vue3-template-ru/api-client"
  v
Vue-компоненты в apps/frontend/web/  (используют composables)
```

## Почему не альтернативы

**Ручное написание TypeScript-типов и клиентов.**
При любом изменении API нужно вручную синхронизировать C# и TypeScript.
Расхождение обнаруживается в runtime. Не масштабируется: при 50+ эндпоинтах
это становится основным источником багов.

**Swagger UI / ReadMe.io (только документация).**
Документация помогает читать контракт, но не даёт compile-time проверку.
Фронтенд всё равно пишет типы вручную.

**tRPC.**
Позволяет делать end-to-end типизацию, но требует Node.js-бэкенда. Бэкенд на
ASP.NET Core, tRPC несовместим.

**GraphQL + кодогенерация.**
Мощный вариант, но требует полной миграции API на GraphQL. Бэкенд - REST,
миграция неоправданна. Orval работает с существующим REST без изменений.

**Swagger Codegen / OpenAPI Generator.**
Генерируют типы и клиенты, но не генерируют Vue Query composables. Нужен
дополнительный слой. Orval генерирует готовые `useQuery` / `useMutation` за
один шаг.

## Плюсы выбранного подхода

- Единственный источник истины - C# контроллер. Нет дублирования контракта.
- Изменение API немедленно отражается в TypeScript после одной команды.
- Compile-time, а не runtime: ошибки несоответствия видны до запуска приложения.
- Нет ручного написания HTTP-кода, URL-строк, типов ответов.
- Сгенерированный код никто не редактирует вручную - нет конфликтов.
- Orval генерирует Vue Query composables, а не просто типы - готово к
  использованию в компонентах без дополнительного кода.

## Минусы и ограничения

- Зависимость от `dotnet build` при генерации: если SDK не установлен или
  бэкенд не компилируется, `nx run api-client:generate` упадёт.
- Сгенерированные файлы коммитятся в репозиторий - при частых изменениях API
  diff может быть шумным (много изменённых сгенерированных строк).
- Orval генерирует код под конкретный `client`. Здесь используется `"vue-query"` --
  это внутреннее название Orval для генерации [TanStack Query](0029-tan-stack-query.md)
  composables (`@tanstack/vue-query`). При смене фреймворка конфиг нужно менять,
  а сгенерированный код пересоздавать.
- Круговая зависимость: для генерации фронтенда нужна актуальная сборка бэкенда.
  Если бэкенд и фронтенд меняются одновременно, порядок имеет значение.

## Последствия

- При добавлении нового эндпоинта на бэкенде: добавить атрибуты
  `ProducesResponseType<T>`, запустить `nx run api-client:generate` - composable
  появится автоматически. Никакого ручного TypeScript.
- Сгенерированные файлы в `libs/frontend/api-client/src/generated/` никогда не
  редактируются вручную. Все правки делаются через C# контроллер или orval.config.ts.
- Переименование поля в C# DTO без запуска генерации приведёт к compile-time
  ошибкам в фронтенде при следующей сборке. Это намеренное поведение.
