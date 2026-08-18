import { createApp } from "vue";
import { createPinia } from "pinia";
import { VueQueryPlugin } from "@tanstack/vue-query";
import ElementPlus from "element-plus";
import {
  setAccessTokenProvider,
  setSessionRenewer,
} from "@dotnet-vue3-template-ru/api-client";
import App from "./App.vue";
import { router } from "./router";
import { createAppI18n } from "@core/i18n";
import {
  getAccessToken,
  getUser,
  isAuthConfigured,
  renewSession,
} from "@core/auth/auth-client";
import { setAuthenticated, setUserEmail } from "@core/auth/session-state";
import "@core/theme/index.scss";

// Единственный composition root приложения (ADR 0027): один createApp, один Pinia,
// один QueryClient, один роутер, один i18n. Изолировать нечего - приложение на
// странице одно.
async function bootstrap() {
  // Моки поднимаются до старта приложения, иначе первые запросы уйдут мимо
  // перехватчика. Динамический импорт - чтобы msw не попал в прод-бандл.
  if (import.meta.env.VITE_API_MOCKING === "enabled") {
    const { worker } = await import(
      "@dotnet-vue3-template-ru/api-client/mocks/browser"
    );
    await worker.start({ onUnhandledRequest: "bypass" });
  }

  // Токен HTTP-клиенту отдаётся функцией, а не значением: он появляется после
  // входа и обновляется автоматическим продлением, то есть меняется в течение
  // жизни приложения.
  setAccessTokenProvider(getAccessToken);
  setSessionRenewer(renewSession);

  // Состояние сессии определяется до первого рендера: иначе guard роутера успеет
  // увести на вход пользователя, у которого сессия есть.
  if (isAuthConfigured) {
    const user = await getUser().catch(() => null);
    setAuthenticated(Boolean(user && !user.expired));
    setUserEmail(user?.profile.email);
  }

  createApp(App)
    .use(createPinia())
    .use(VueQueryPlugin)
    .use(ElementPlus)
    .use(createAppI18n())
    .use(router)
    .mount("#app");
}

void bootstrap();
