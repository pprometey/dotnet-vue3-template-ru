import { UserManager, WebStorageStateStore, type User } from "oidc-client-ts";

/**
 * OIDC-клиент приложения: authorization code + PKCE против внешнего провайдера
 * (ADR 0023). Приложение не выпускает токены и не хранит пароли - оно только
 * отправляет пользователя на вход и получает обратно токен.
 *
 * Адрес провайдера, идентификатор клиента и индикатор ресурса API приходят
 * переменными сборки Vite: локально их подставляет Aspire из ресурса Logto.
 */
const authority = import.meta.env.VITE_OIDC_AUTHORITY ?? "";
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID ?? "";
const apiResource = import.meta.env.VITE_OIDC_RESOURCE ?? "";

export const isAuthConfigured = Boolean(authority) && Boolean(clientId);

const origin = typeof window === "undefined" ? "" : window.location.origin;

// Ленивая инициализация: без настроенного провайдера UserManager не создаётся -
// иначе он падал бы на старте в тестах и в Storybook, где входа нет вовсе.
let manager: UserManager | undefined;

function userManager(): UserManager {
  if (!isAuthConfigured) {
    throw new Error(
      "Провайдер идентичности не настроен: не заданы VITE_OIDC_AUTHORITY и VITE_OIDC_CLIENT_ID.",
    );
  }

  manager ??= new UserManager({
    authority,
    client_id: clientId,
    redirect_uri: `${origin}/auth/callback`,
    post_logout_redirect_uri: origin,
    silent_redirect_uri: `${origin}/auth/silent`,
    response_type: "code",
    scope: "openid profile email",
    // Индикатор ресурса (RFC 8707) в обоих запросах: он говорит провайдеру, для
    // какого API выпускать токен доступа. Без него Logto вернёт непрозрачную
    // строку вместо JWT, и проверка подписи на стороне API откажет с 401.
    extraQueryParams: { resource: apiResource },
    extraTokenParams: { resource: apiResource },
    // Автоматическое продление в скрытом iframe: без него сессия обрывается по
    // истечении токена посреди работы, а пользователь видит внезапный 401.
    automaticSilentRenew: true,
    // sessionStorage вместо localStorage по умолчанию: токен не переживает
    // закрытие вкладки и не разъезжается между вкладками разных пользователей.
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  });

  return manager;
}

export async function getUser(): Promise<User | null> {
  if (!isAuthConfigured) {
    return null;
  }

  return userManager().getUser();
}

export async function getAccessToken(): Promise<string | undefined> {
  const user = await getUser();
  return user?.expired ? undefined : user?.access_token;
}

export function signIn(): Promise<void> {
  return userManager().signinRedirect();
}

export function completeSignIn(): Promise<User> {
  return userManager().signinRedirectCallback();
}

export function completeSilentRenew(): Promise<void> {
  return userManager().signinSilentCallback();
}

export function signOut(): Promise<void> {
  return userManager().signoutRedirect();
}

/**
 * Однократная попытка продлить сессию. Возвращает признак успеха, а не бросает:
 * вызывающий (HTTP-мутатор при 401) должен уметь отличить "продлили, повторяем
 * запрос" от "сессия кончилась, показываем вход".
 */
export async function renewSession(): Promise<boolean> {
  if (!isAuthConfigured) {
    return false;
  }

  try {
    const user = await userManager().signinSilent();
    return Boolean(user?.access_token);
  } catch {
    return false;
  }
}
