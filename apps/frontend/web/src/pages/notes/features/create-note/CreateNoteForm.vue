<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { usePostApiV1Notes } from "@dotnet-vue3-template-ru/api-client";
import { DEFAULT_UI_LOCALE, UI_LOCALES, type UiLocale } from "@core/i18n";

const emit = defineEmits<{ created: [id: string] }>();

const { t } = useI18n();

// Текст на культуре по умолчанию обязателен: он ложится в запись инлайн и служит
// фолбэком при чтении на культуре без перевода (ADR 0021). Перевод - дополнение
// к нему, а не замена, поэтому в списке языков перевода дефолтной культуры нет.
const TRANSLATION_LOCALES = UI_LOCALES.filter(
  (locale) => locale !== DEFAULT_UI_LOCALE,
);

const text = ref("");
const translationText = ref("");
const translationCulture = ref<UiLocale>(TRANSLATION_LOCALES[0]);

const { mutate, isPending, error } = usePostApiV1Notes();

// Ошибки приходят двух видов. Валидация - ValidationProblemDetails со словарём
// errors по полям; доменный отказ и прочие сбои - ProblemDetails с одним detail.
// Показываем и то, и другое: иначе доменный отказ не даёт на экране ничего, и
// форма выглядит так, будто нажатие кнопки просто не сработало.
const errorMessages = computed<string[]>(() => {
  const problem = (error.value as { response?: { data?: unknown } } | null)
    ?.response?.data as
    | { errors?: Record<string, string[]>; detail?: string }
    | undefined;

  const fieldMessages = Object.values(problem?.errors ?? {}).flat();
  if (fieldMessages.length > 0) {
    return fieldMessages;
  }

  return problem?.detail ? [problem.detail] : [];
});

function submit() {
  const texts: Record<string, string> = { [DEFAULT_UI_LOCALE]: text.value };
  if (translationText.value.trim()) {
    texts[translationCulture.value] = translationText.value;
  }

  mutate(
    { data: { texts } },
    {
      onSuccess: (note) => {
        emit("created", note.id);
        text.value = "";
        translationText.value = "";
      },
    },
  );
}
</script>

<template>
  <el-card>
    <el-form label-position="top" @submit.prevent="submit">
      <el-form-item :label="`${t('notes.create.text')} (${DEFAULT_UI_LOCALE})`">
        <el-input v-model="text" type="textarea" :rows="3" />
      </el-form-item>

      <el-form-item :label="t('notes.create.translationCulture')">
        <el-select v-model="translationCulture" class="create-note__culture">
          <el-option
            v-for="code in TRANSLATION_LOCALES"
            :key="code"
            :label="code"
            :value="code"
          />
        </el-select>
      </el-form-item>

      <el-form-item :label="t('notes.create.translation')">
        <el-input v-model="translationText" type="textarea" :rows="3" />
      </el-form-item>

      <el-alert
        v-for="message in errorMessages"
        :key="message"
        type="error"
        :title="message"
        :closable="false"
        class="create-note__error"
      />

      <el-button type="primary" native-type="submit" :loading="isPending">
        {{ t("notes.create.submit") }}
      </el-button>
    </el-form>
  </el-card>
</template>

<style scoped>
.create-note__culture {
  width: 120px;
}

.create-note__error {
  margin-bottom: 12px;
}
</style>
