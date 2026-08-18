# Guide: моки API через MSW

Моки позволяют разрабатывать, демонстрировать и тестировать экраны без backend и без поднятия стека через Aspire. Перехват идёт на сетевом уровне через [MSW](https://mswjs.io): сгенерированный Orval axios-клиент не меняется. Почему так - [ADR: Моки API через MSW](../adr/0033-api-mocks-msw.md).

Хендлеры лежат в `libs/frontend/api-client` (рядом со сгенерированным клиентом) и работают в трёх контекстах: Storybook, dev web, Vitest-тесты.

## Откуда берутся хендлеры

Два слоя:

1. **Faker-хендлеры от Orval.** `orval.config.ts` включает `mock: { generators: [{ type: "msw" }] }`, поэтому при генерации клиента рядом с каждым tag-split файлом появляется `*.msw.ts` с генераторами ответов на faker и массивами хендлеров на тег (`getNotesMock()`, `getPingMock()`). Они покрывают все эндпоинты контракта и обновляются вместе с ним. Случайные данные - годятся как фолбэк, но не для конкретных демо-состояний.
2. **Курируемые override.** Ручные детерминированные хендлеры для демо/Storybook. Лежат в `src/mocks/overrides/<tag>/<orvalFn>.ts` и идут в итоговом списке ПЕРВЫМИ - в MSW выигрывает первый подходящий хендлер, поэтому override перекрывают faker.

Итоговый список собирается в [src/mocks/handlers.ts](../../libs/frontend/api-client/src/mocks/handlers.ts):

```ts
export const handlers = [
  ...overrideHandlers, // курируемые - первыми (перекрывают faker)
  ...getConfigurationsMock(), // faker-фолбэк
  ...getNotesMock(),
  ...getPingMock(),
];
```

## Структура

```text
libs/frontend/api-client/src/mocks/
  overrides/
    notes/
      getApiV1NotesId.ts     # один файл - один метод (имя = имя Orval-функции)
      postApiV1Notes.ts
      postApiV2Notes.ts
      getApiV2NotesId.ts
      index.ts               # notesOverrides = [...]
    ping/
      getApiV1Ping.ts
      index.ts               # pingOverrides = [...]
    configurations/ notes/ ping/ session-context/
                             # остальные теги - по тому же образцу
    index.ts                 # overrideHandlers = [...] по всем тегам
  handlers.ts                # overrideHandlers + faker
  browser.ts                 # worker = setupWorker(...handlers)  (msw/browser)
  server.ts                  # server = setupServer(...handlers)  (msw/node)
  index.ts                   # реэкспорт handlers, overrideHandlers
```

Экспорт - тремя сабпутями, чтобы рантайм-специфика не утекала в чужой контекст:

| Импорт                                              | Что отдаёт               | Где использовать            |
| --------------------------------------------------- | ------------------------ | --------------------------- |
| `@dotnet-vue3-template-ru/api-client/mocks`         | `handlers`               | Storybook (набор по умолч.) |
| `@dotnet-vue3-template-ru/api-client/mocks/browser` | `worker` (`msw/browser`) | web dev                     |
| `@dotnet-vue3-template-ru/api-client/mocks/server`  | `server` (`msw/node`)    | Vitest                      |

## Storybook (моки по умолчанию)

Включены глобально: [.storybook/preview.ts](../../apps/frontend/web/.storybook/preview.ts) вызывает `initialize()` из `msw-storybook-addon`, регистрирует `mswLoader` и кладёт `handlers` в `parameters.msw.handlers`. Worker (`mockServiceWorker.js`) отдаётся как статика из `.storybook/public` (`staticDirs` в [.storybook/main.ts](../../apps/frontend/web/.storybook/main.ts)).

Story может переопределить ответы локально:

```ts
import { http, HttpResponse } from "msw";

export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(
          "*/api/v1/Notes/:id",
          () => new HttpResponse(null, { status: 404 }),
        ),
      ],
    },
  },
};
```

## Dev web (без backend)

Опционально по флагу - чтобы обычный dev по-прежнему ходил в реальный API:

```bash
VITE_API_MOCKING=enabled yarn nx serve web
```

[main.ts](../../apps/frontend/web/src/main.ts) при флаге динамически импортирует `worker` и стартует его ДО монтирования. Worker (`mockServiceWorker.js`) лежит в `apps/frontend/web/public`. Без флага моки не подключаются.

## Vitest-тесты

Глобальный мок-сервер поднят в [src/test/msw-setup.ts](../../apps/frontend/web/src/test/msw-setup.ts) (подключён через `setupFiles` в [vitest.config.ts](../../apps/frontend/web/vitest.config.ts)): `server.listen()` до тестов, `resetHandlers()` после каждого, `close()` в конце. По умолчанию активны те же `handlers`.

Тест, которому нужен особый ответ, переопределяет его локально и не заботится об откате (его делает `resetHandlers`):

```ts
import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@dotnet-vue3-template-ru/api-client/mocks/server";

describe("страница заметок", () => {
  it("показывает ошибку при 500", async () => {
    server.use(
      http.get(
        "*/api/v1/Notes/:id",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    // ... рендерим компонент, ждём состояние ошибки, ассертим
    expect(true).toBe(true);
  });
});
```

## Добавить мок нового метода

1. Узнай имя сгенерированной Orval-функции метода (например `getApiV1ContactsId`) - оно же путь и метод в `src/generated/<tag>/<tag>.ts`.
2. Создай `src/mocks/overrides/<tag>/<orvalFn>.ts` с одним хендлером. Путь зеркаль из сгенерированного `*.msw.ts` (паттерн `*/api/...`, PascalCase сегмента, `:id` для параметров):

   ```ts
   import { http, HttpResponse } from "msw";
   import type { ContactResult } from "../../../generated/models";

   export default http.get("*/api/v1/Contacts/:id", ({ params }) =>
     HttpResponse.json<ContactResult>({ id: String(params.id), name: "Demo" }),
   );
   ```

3. Допиши хендлер в `index.ts` тега (новый тег - заведи папку и подключи её массив в `overrides/index.ts`).
4. `long` отдавай строкой (соглашение проекта, [api-client.md](api-client.md)).

Не покрытый вручную эндпоинт автоматически обслуживает faker-хендлер из Orval.

## Грабли

- **Апгрейд `msw`.** `mockServiceWorker.js` привязан к версии. После обновления перегенерируй его в обоих public-каталогах:

  ```bash
  yarn dlx msw init apps/frontend/web/.storybook/public --no-save
  yarn dlx msw init apps/frontend/web/public --no-save
  ```

- **Путь хендлера не матчит запрос.** Сегменты в контракте PascalCase (`/api/v1/Notes`, не `/notes`). Сверяйся со сгенерированным `*.msw.ts` или с `url` в `src/generated/<tag>/<tag>.ts`.
- **Импорт worker/server не из того сабпути.** В браузер - `.../mocks/browser`, в Node - `.../mocks/server`. Общий `.../mocks` отдаёт только данные хендлеров (без рантайма).
- **Клиент не сгенерирован.** `*.msw.ts` лежат в gitignored `src/generated` - сначала `yarn generate:api-client` (см. [api-client.md](api-client.md)).

## См. также

- [ADR: Моки API через MSW](../adr/0033-api-mocks-msw.md) - почему MSW, а не axios-mock-adapter/отдельный сервер.
- [Guide: API-клиент](api-client.md) - откуда берутся типы и почему `long` строкой.
- [Guide: фронтенд-тесты](frontend-tests.md) - инфраструктура Vitest.
