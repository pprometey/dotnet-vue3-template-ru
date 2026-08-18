<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import ru from "element-plus/es/locale/lang/ru";
import en from "element-plus/es/locale/lang/en";
import AppLayout from "@core/layout/AppLayout.vue";
import { useAppConfiguration } from "@core/services/configuration/use-app-configuration";
import { useSessionContext } from "@core/services/session/use-session-context";

useAppConfiguration();
useSessionContext();

const { locale } = useI18n();

// Локали встроенных компонентов Element Plus. Казахской в пакете нет, поэтому для
// kk подставляется английская: это видно только в подписях пагинации и датапикера.
const elementLocales = { ru, en, kk: en } as const;

const elementLocale = computed(
  () => elementLocales[locale.value as keyof typeof elementLocales] ?? ru,
);
</script>

<template>
  <el-config-provider :locale="elementLocale">
    <AppLayout>
      <RouterView />
    </AppLayout>
  </el-config-provider>
</template>
