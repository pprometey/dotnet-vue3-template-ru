import { setupWorker } from "msw/browser";
import { handlers } from "./handlers";

// Service Worker для браузерных контекстов (dev-запуск SPA; в Storybook worker
// поднимает msw-storybook-addon). Требует mockServiceWorker.js в public-каталоге.
export const worker = setupWorker(...handlers);
