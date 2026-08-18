import { watch } from "vue";
import { useI18n } from "vue-i18n";
import { useGetApiV1ConfigurationsClient } from "@dotnet-vue3-template-ru/api-client";
import { isUiLocale } from "../../i18n";
import { useConfigurationStore } from "./configuration-store";

/**
 * Загружает конфигурацию с бэкенда и применяет язык интерфейса по умолчанию.
 *
 * Список культур живёт на бэкенде, а не дублируется здесь: он обязан совпадать
 * с RequestLocalizationOptions и resx-ресурсами, а две копии разъедутся на первом
 * же добавленном языке.
 */
export function useAppConfiguration() {
  const store = useConfigurationStore();
  const { locale } = useI18n();

  const general = useGetApiV1ConfigurationsClient();

  watch(
    general.data,
    (configuration) => {
      if (!configuration) {
        return;
      }

      store.setConfiguration(configuration);

      const defaultCulture = configuration.cultures?.defaultCulture;
      if (typeof defaultCulture === "string" && isUiLocale(defaultCulture)) {
        locale.value = defaultCulture;
      }
    },
    { immediate: true },
  );

  return { store };
}
