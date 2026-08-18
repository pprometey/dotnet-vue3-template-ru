import { defineStore } from "pinia";
import { ref } from "vue";
import type { ConfigurationGetResult } from "@dotnet-vue3-template-ru/api-client";

/**
 * Хранилище прикладной конфигурации, пришедшей с бэкенда. Ленивая инициализация:
 * до первой загрузки поле undefined.
 */
export const useConfigurationStore = defineStore("configurationStore", () => {
  const configuration = ref<ConfigurationGetResult>();

  const setConfiguration = (value: ConfigurationGetResult) => {
    configuration.value = value;
  };

  return { configuration, setConfiguration };
});
