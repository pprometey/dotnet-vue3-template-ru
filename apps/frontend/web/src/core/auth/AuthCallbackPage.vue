<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { completeSignIn } from "@core/auth/auth-client";
import { setAuthenticated, setUserEmail } from "@core/auth/session-state";

const router = useRouter();
const { t } = useI18n();
const error = ref<string>();

// Завершение обмена authorization code на токен. Провайдер вернул пользователя
// сюда с параметрами в адресной строке; после обмена уводим на исходный экран,
// заменяя запись в истории - иначе кнопка "назад" вернёт на callback с уже
// использованным кодом.
onMounted(async () => {
  try {
    const user = await completeSignIn();
    setAuthenticated(true);
    setUserEmail(user.profile.email);
    await router.replace({ name: "notes" });
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : String(cause);
  }
});
</script>

<template>
  <el-alert v-if="error" type="error" :title="error" :closable="false" />
  <p v-else>{{ t("app.loading") }}</p>
</template>
