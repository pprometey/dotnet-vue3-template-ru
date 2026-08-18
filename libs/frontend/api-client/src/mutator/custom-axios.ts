import Axios, { type AxiosError, type AxiosRequestConfig } from "axios";

export const AXIOS_INSTANCE = Axios.create();

// Базовый URL API задаёт сборка SPA (Vite). Пусто = тот же origin: так работает
// прод за общим reverse proxy, где /api проксируется на backend (ADR 0027).
// Оператор ?. нужен потому, что import.meta.env существует только под Vite;
// в чистом Node (например, при прогоне без vite-конфига) его нет.
const BASE_URL = import.meta.env?.VITE_API_BASE_URL ?? "";

// Токен приложение отдаёт провайдером, зарегистрированным один раз в main.ts.
// Через функцию, а не через значение: токен появляется после входа и обновляется
// автоматическим продлением сессии, то есть меняется в течение жизни приложения.
type AccessTokenProvider = () =>
  | string
  | undefined
  | Promise<string | undefined>;

let accessTokenProvider: AccessTokenProvider = () => undefined;

export function setAccessTokenProvider(provider: AccessTokenProvider): void {
  accessTokenProvider = provider;
}

// Однократная попытка продлить сессию и повторить запрос при 401. Перевыпуск делает
// OIDC-клиент приложения, а не мутатор: здесь только точка, куда его подключают.
type SessionRenewer = () => Promise<boolean>;

let renewSession: SessionRenewer | undefined;

export function setSessionRenewer(renewer: SessionRenewer): void {
  renewSession = renewer;
}

export const customAxios = <T>(config: AxiosRequestConfig): Promise<T> => {
  const source = Axios.CancelToken.source();

  const run = async (): Promise<T> => {
    const token = await accessTokenProvider();

    try {
      const response = await AXIOS_INSTANCE({
        ...config,
        baseURL: BASE_URL,
        cancelToken: source.token,
        headers: token
          ? { ...config.headers, Authorization: `Bearer ${token}` }
          : config.headers,
      });
      return response.data;
    } catch (error) {
      const status = (error as AxiosError)?.response?.status;
      if (status !== 401 || !renewSession) {
        throw error;
      }

      // Повтор ровно один раз: если продление не помогло, второй заход дал бы
      // бесконечный цикл на протухшей сессии.
      const renewed = await renewSession();
      if (!renewed) {
        throw error;
      }

      const renewedToken = await accessTokenProvider();
      const response = await AXIOS_INSTANCE({
        ...config,
        baseURL: BASE_URL,
        cancelToken: source.token,
        headers: renewedToken
          ? { ...config.headers, Authorization: `Bearer ${renewedToken}` }
          : config.headers,
      });
      return response.data;
    }
  };

  // TanStack Query вызывает .cancel() при демонтировании компонента: без этого
  // в полёте остаются запросы после ухода с экрана.
  const promise = run() as Promise<T> & { cancel?: () => void };
  promise.cancel = () => source.cancel("Query was cancelled");

  return promise;
};

export default customAxios;
