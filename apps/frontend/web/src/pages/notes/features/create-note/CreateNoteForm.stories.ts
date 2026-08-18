import type { Meta, StoryObj } from "@storybook/vue3-vite";
import CreateNoteForm from "./CreateNoteForm.vue";

// Единственная история шаблона. Она существует не ради самой формы, а чтобы
// конфигурация Storybook не была мёртвой: на отрендеренной истории панель
// доступности прогоняет axe-core (ADR 0030), а запросы перехватывает MSW.
const meta: Meta<typeof CreateNoteForm> = {
  title: "Notes/CreateNoteForm",
  component: CreateNoteForm,
};

export default meta;

export const Default: StoryObj<typeof CreateNoteForm> = {};
