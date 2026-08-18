import { getConfigurationsMock } from "../generated/configurations/configurations.msw";
import { getNotesMock } from "../generated/notes/notes.msw";
import { getPingMock } from "../generated/ping/ping.msw";
import { getSessionContextMock } from "../generated/session-context/session-context.msw";
import { overrideHandlers } from "./overrides";

// Итоговый список хендлеров MSW. Курируемые override идут ПЕРВЫМИ: в MSW
// выигрывает первый подходящий хендлер, поэтому они перекрывают faker. Faker из
// Orval остаётся фолбэком для эндпоинтов, не покрытых вручную.
export const handlers = [
  ...overrideHandlers,
  ...getConfigurationsMock(),
  ...getNotesMock(),
  ...getPingMock(),
  ...getSessionContextMock(),
];
