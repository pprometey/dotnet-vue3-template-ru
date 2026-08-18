import {
  createRouter,
  createWebHistory,
  type RouteRecordRaw,
} from "vue-router";
import {
  completeSignIn,
  completeSilentRenew,
  isAuthConfigured,
  signIn,
} from "@core/auth/auth-client";
import { isAuthenticated, setAuthenticated } from "@core/auth/session-state";
import { notesRoutes } from "@pages/notes/routes";

// Служебные маршруты входа. Они существуют потому, что authorization code
// возвращается редиректом на адрес приложения - без собственной адресной строки
// вход по OIDC невозможен (ADR 0027).
const authRoutes: RouteRecordRaw[] = [
  {
    path: "/auth/callback",
    name: "auth-callback",
    meta: { public: true },
    component: () => import("@core/auth/AuthCallbackPage.vue"),
  },
  {
    path: "/auth/silent",
    name: "auth-silent",
    meta: { public: true },
    // Страница живёт в скрытом iframe при автоматическом продлении сессии:
    // рендерить ей нечего, она только завершает обмен.
    component: {
      async setup() {
        await completeSilentRenew().catch(() => undefined);
        return () => null;
      },
    },
  },
];

const routes: RouteRecordRaw[] = [
  { path: "/", redirect: { name: "notes" } },
  ...notesRoutes,
  ...authRoutes,
  {
    path: "/:pathMatch(.*)*",
    name: "not-found",
    meta: { public: true },
    component: () => import("@core/layout/NotFoundPage.vue"),
  },
];

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

// Неаутентифицированного уводим на вход к провайдеру. Без настроенного провайдера
// (тесты, Storybook, запуск на моках) guard пропускает всех: иначе приложение
// зациклилось бы на редиректе в никуда.
router.beforeEach(async (to) => {
  if (to.meta.public || !isAuthConfigured || isAuthenticated.value) {
    return true;
  }

  await signIn();
  return false;
});

export { completeSignIn, setAuthenticated };
