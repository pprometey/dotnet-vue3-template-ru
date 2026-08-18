# Guide: как писать фронтенд-тесты

Фронтенд-тесты проекта проверяют Vue-компоненты и логику композаблов в изоляции, в DOM-среде, без поднятия всего стека через Aspire. Почему именно этот стек и зачем инфраструктура заведена сейчас, а массовое покрытие отложено - [ADR: Инфраструктура фронтенд-тестов](../adr/0032-frontend-unit-tests.md) и [стратегия тестирования](../spec/test-strategy.md).

Стек: **Vitest** (движок + ассерты), **happy-dom** (DOM-среда), **@vue/test-utils** (монтирование компонентов), **@testing-library/vue** (тесты от поведения), **@vitest/coverage-v8** (покрытие).

Тесты живут в `apps/frontend/web` - единственной библиотеке с Vue-компонентами. Конфиг: [apps/frontend/web/vitest.config.ts](../../apps/frontend/web/vitest.config.ts). Nx-таргет `test` - в [apps/frontend/web/project.json](../../apps/frontend/web/project.json).

## Предусловия

- Установленные зависимости: `yarn install`. Docker не нужен (в отличие от интеграционных тестов).

## Как запустить

```bash
yarn nx test web              # тесты библиотеки web (как в CI)
yarn nx test web --coverage   # с отчётом покрытия
yarn test                     # все проекты с таргетом test (nx run-many)
```

Покрытие пишется в `coverage/apps/frontend/web` (есть `index.html` и текстовый вывод в консоли). Порога покрытия нет: измерять можно, проваливать сборку по проценту - нет (см. стратегию тестирования).

Результат таргета кэшируется Nx: повторный `nx test web` без изменений отдаётся из кэша. `nx affected --target=test` подхватывает таргет автоматически.

## Где лежат тесты

Файл теста лежит рядом с тем, что тестирует, и называется `*.spec.ts` (или `*.test.ts`):

```text
src/pages/notes/features/create-note/
  details-panel-store.ts
  details-panel-store.spec.ts   # <- тест рядом с тестируемым кодом
```

`nx.json` уже исключает `*.spec.ts`/`*.test.ts` и `*.stories.ts` из `production`-инпутов, поэтому на сборку библиотеки тесты не влияют.

## Smoke-тест (эталон)

Минимальный рабочий пример - `src/pages/notes/NotesPage.spec.ts`. Поднимаем Pinia, вызываем действия стора и сверяем ожидаемое состояние:

```ts
import { describe, it, expect, beforeEach } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useDetailsPanelStore } from "./details-panel-store";

describe("useDetailsPanelStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("starts closed", () => {
    const store = useDetailsPanelStore();

    expect(store.isOpen).toBe(false);
  });

  it("open shows the panel", () => {
    const store = useDetailsPanelStore();

    store.open();

    expect(store.isOpen).toBe(true);
  });
});
```

Ключевые моменты:

- **`describe/it/expect` импортируй из `vitest` явно.** Глобалы выключены (`globals: false`), поэтому без импорта тест не соберётся - и поэтому же `eslint.config.mjs` не приходится трогать.
- **Проверяй конкретное значение, а не его отсутствие** (правило 8 из [.claude/CLAUDE.md](../../.claude/CLAUDE.md)): сравнивай с ожидаемым значением целиком, а не пиши негативный ассерт.
- **Компонент монтируй через `@vue/test-utils` `mount`** (см. раздел про композаблы) - презентационный компонент тестируется так же, проверяя отрендеренный текст/разметку.

## Тест композабла

Композабл - первый кандидат на юнит-тест по [стратегии тестирования](../spec/test-strategy.md): тестируй нетривиальную логику, а не геттеры и разметку. Композабл с реактивностью вызывается напрямую:

```ts
import { describe, it, expect } from "vitest";
import { useCounter } from "./useCounter";

describe("useCounter", () => {
  it("increments the count", () => {
    const { count, increment } = useCounter();

    increment();

    expect(count.value).toBe(1);
  });
});
```

Если композабл использует `onMounted`/`provide`/`inject` или иной контекст компонента, оберни его вызов в тестовый компонент через `@vue/test-utils` либо `@testing-library/vue` - вызывать такой композабл вне `setup` нельзя.

## Когда писать фронтенд-тест

По стратегии тестирования инфраструктура заведена не ради покрытия, а чтобы было где написать тест, когда он оправдан:

- **Писать:** нетривиальная логика композабла (расчёты, ветвления, работа с состоянием), нетривиальное поведение компонента (условный рендер, реакция на ввод).
- **Пока не писать:** простые презентационные компоненты без логики, тонкие обёртки над Element Plus, геттеры. Массовое покрытие UI осознанно отложено.

## Грабли

- **Забыл импортировать `describe/it/expect` из `vitest`** - тест не соберётся (глобалы выключены намеренно). Импортируй явно.
- **Новый внешний пакет тянет `.scss`/стили из исходников** - Node-загрузчик упадёт на расширении файла стилей. Добавь пакет в `test.server.deps.inline` в [vitest.config.ts](../../apps/frontend/web/vitest.config.ts) (там уже инлайнится `element-plus`, а CSS отключён через `test.css: false`).
- **Композабл с контекстом компонента вызван напрямую** - `onMounted`/`inject` вне `setup` не работают. Оберни в тестовый компонент.

## Новый тест: чеклист

1. Создай файл `<Имя>.spec.ts` рядом с компонентом/композаблом в `apps/frontend/web/src`.
2. Импортируй `describe/it/expect` из `vitest` явно.
3. Компонент монтируй через `@vue/test-utils` (`mount`); композабл вызывай напрямую (или через тестовый компонент, если нужен контекст setup).
4. Ассерти конкретное ожидаемое значение, а не его отсутствие.
5. Прогони `yarn nx test web` (при необходимости `--coverage`).
