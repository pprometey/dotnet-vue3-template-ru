<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { CreateNoteForm } from "./features/create-note";
import { NoteCard } from "./features/note-view";

const { t } = useI18n();

// Страница владеет состоянием, которое связывает две фичи. Сами фичи друг о друге
// не знают и импортировать сестру не могут - это правило проверяет линт
// (boundaries, ADR 0028). Демо-раздел существует ровно затем, чтобы правило было
// не декларацией, а работающим примером.
//
// Созданные заметки копятся списком, новая встаёт первой. Показывать только
// последнюю значило бы делать вид, что предыдущая исчезла: на деле каждая
// создаётся отдельной записью и доступна по своему адресу.
const createdNoteIds = ref<string[]>([]);

function rememberCreated(id: string) {
  createdNoteIds.value = [id, ...createdNoteIds.value];
}
</script>

<template>
  <section class="notes-page">
    <h1 class="notes-page__title">{{ t("notes.title") }}</h1>
    <p class="notes-page__hint">{{ t("notes.hint") }}</p>

    <CreateNoteForm @created="rememberCreated" />

    <NoteCard v-for="id in createdNoteIds" :key="id" :id="id" />
  </section>
</template>

<style scoped>
.notes-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.notes-page__title {
  margin: 0;
  font-size: 20px;
}

.notes-page__hint {
  margin: 0;
  color: var(--el-text-color-secondary);
}
</style>
