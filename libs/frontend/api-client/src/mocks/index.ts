// Env-агностичный вход: только данные хендлеров (без msw/browser и msw/node,
// чтобы не тянуть несовместимый рантайм в чужой контекст). Worker и server -
// в отдельных сабпутях: @dotnet-vue3-template-ru/api-client/mocks/browser и .../mocks/server.
export { handlers } from "./handlers";
export { overrideHandlers } from "./overrides";
