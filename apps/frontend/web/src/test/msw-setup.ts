import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "@dotnet-vue3-template-ru/api-client/mocks/server";

// Общий MSW-сервер на весь прогон: поднимается один раз, между тестами сбрасывает
// хендлеры, добавленные конкретным тестом через server.use().
beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
