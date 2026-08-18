import { defineStore } from "pinia";
import { ref } from "vue";
import type { SessionContextGetResult } from "@dotnet-vue3-template-ru/api-client";

/**
 * Хранилище идентичности текущего пользователя, пришедшей с бэкенда.
 *
 * Прав здесь нет: доступ решает сервер в обработчике операции, а не клиент
 * по списку из токена (ADR 0023).
 */
export const useSessionStore = defineStore("sessionStore", () => {
  const sessionContext = ref<SessionContextGetResult>();

  const setSessionContext = (value: SessionContextGetResult) => {
    sessionContext.value = value;
  };

  return { sessionContext, setSessionContext };
});
