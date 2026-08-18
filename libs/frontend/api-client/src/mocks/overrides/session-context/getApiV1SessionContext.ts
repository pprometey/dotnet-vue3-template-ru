import { http, HttpResponse } from "msw";
import type { SessionContextGetResult } from "../../../generated/models";

// Override GET /api/v1/session-context: детерминированная идентичность пользователя.
// На бэке эндпоинт требует токена; в моках он не проверяется. userId - значение claim
// "sub" провайдера, поэтому строка, а не число.
export default http.get("*/api/v1/session-context", () =>
  HttpResponse.json<SessionContextGetResult>({
    userId: "00000000-0000-0000-0000-000000000001",
  }),
);
