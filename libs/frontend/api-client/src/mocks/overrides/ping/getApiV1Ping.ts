import { http, HttpResponse } from "msw";
import type { PongResult } from "../../../generated/models";

// Override GET /api/v1/Ping: детерминированный pong для Storybook/демо.
export default http.get("*/api/v1/Ping", () =>
  HttpResponse.json<PongResult>({
    status: "ok",
    at: "2026-06-18T09:00:00Z",
  }),
);
