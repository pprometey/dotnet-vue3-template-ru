import { readonly, ref } from "vue";

/**
 * Признак наличия действующего токена. Отдельно от OIDC-клиента, потому что его
 * читают синхронно и реактивно: роутер решает, пускать ли на маршрут, а запросы
 * TanStack Query включаются только при наличии токена.
 *
 * Значение обновляет composition root после проверки сессии; сам флаг ничего
 * не знает про провайдера.
 */
const authenticated = ref(false);

/**
 * Почта пользователя для показа в шапке. Берётся из ID-токена, а не из ответа
 * бэкенда: стандарт OIDC адресует профиль клиентскому приложению, и в токене
 * доступа, который уходит в API, профильных claims может не быть вовсе.
 */
const email = ref<string>();

export const isAuthenticated = readonly(authenticated);

export const userEmail = readonly(email);

export function setAuthenticated(value: boolean): void {
  authenticated.value = value;
}

export function setUserEmail(value: string | undefined): void {
  email.value = value;
}
