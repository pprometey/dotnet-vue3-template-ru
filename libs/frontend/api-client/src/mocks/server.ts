import { setupServer } from "msw/node";
import { handlers } from "./handlers";

// Перехватчик для Node-контекстов (Vitest). См. libs/frontend/web/src/test.
export const server = setupServer(...handlers);
