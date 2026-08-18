<script setup lang="ts">
import { computed, toRef } from "vue";
import { useI18n } from "vue-i18n";
import { useGetApiV2NotesId } from "@dotnet-vue3-template-ru/api-client";

const props = defineProps<{ id: string }>();

const { t } = useI18n();

// Реактивный параметр: при смене id запрос уходит заново сам.
// Версия v2 выбрана намеренно - она отличается от v1 полем textLength и
// показывает, что версионирование API доходит до сгенерированного клиента.
const id = toRef(props, "id");
const { data, isPending, refetch } = useGetApiV2NotesId(
  computed(() => id.value),
);
</script>

<template>
  <el-card v-loading="isPending">
    <template #header>{{ t("notes.view.title") }}</template>

    <p>{{ data?.text }}</p>
    <p class="note-card__meta">
      {{ t("notes.view.createdAt") }}: {{ data?.createdAt }}
    </p>
    <p class="note-card__meta">
      {{ t("notes.view.length") }}: {{ data?.textLength }}
    </p>

    <el-button size="small" @click="refetch()">
      {{ t("notes.view.reload") }}
    </el-button>
  </el-card>
</template>

<style scoped>
.note-card__meta {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  margin: 4px 0;
}
</style>
