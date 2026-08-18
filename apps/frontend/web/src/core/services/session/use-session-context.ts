import { computed, watch } from "vue";
import { useGetApiV1SessionContext } from "@dotnet-vue3-template-ru/api-client";
import { isAuthenticated } from "../../auth/session-state";
import { useSessionStore } from "./session-store";

/**
 * Загружает идентичность пользователя с бэкенда и кладёт в session-store.
 *
 * Эндпоинт требует токена, поэтому запрос включается только когда сессия есть -
 * иначе первый рендер приложения гарантированно даёт 401 в консоли.
 */
export function useSessionContext() {
  const store = useSessionStore();

  const session = useGetApiV1SessionContext({
    query: { enabled: computed(() => isAuthenticated.value) },
  });

  watch(
    session.data,
    (context) => {
      if (context) {
        store.setSessionContext(context);
      }
    },
    { immediate: true },
  );

  return { store };
}
