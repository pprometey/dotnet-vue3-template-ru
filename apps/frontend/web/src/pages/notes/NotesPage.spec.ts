import { describe, expect, it } from "vitest";
import { render, screen, waitFor } from "@testing-library/vue";
import userEvent from "@testing-library/user-event";
import { createPinia } from "pinia";
import { VueQueryPlugin } from "@tanstack/vue-query";
import ElementPlus from "element-plus";
import { createAppI18n } from "@core/i18n";
import NotesPage from "./NotesPage.vue";

// Один тест на склейку страницы: он проверяет не разметку, а то, что форма и
// карточка соединены через состояние страницы и что обе ходят в API через
// сгенерированный клиент. Ответы отдаёт MSW (курируемые override), поэтому
// бэкенд для прогона не нужен.
describe("NotesPage", () => {
  it("создаёт заметку и показывает её карточку", async () => {
    const user = userEvent.setup();

    render(NotesPage, {
      global: {
        plugins: [createPinia(), VueQueryPlugin, ElementPlus, createAppI18n()],
      },
    });

    // Первое поле - текст на культуре по умолчанию; второе, для перевода,
    // необязательно и в этом сценарии не заполняется.
    await user.type(screen.getAllByRole("textbox")[0], "Проверка среза");
    await user.click(screen.getByRole("button", { name: /создать/i }));

    // Карточка появляется только после успешного создания: до него у страницы
    // нет идентификатора, и рендерить нечего.
    await waitFor(() =>
      expect(screen.getByText(/созданная заметка/i)).toBeTruthy(),
    );
  });
});
