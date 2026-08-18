import { http, HttpResponse } from "msw";
import type { NoteResult } from "../../../generated/models";

// Override GET /api/v1/Notes/{id}: детерминированный ответ для Storybook/демо.
// Путь зеркалит сгенерированный Orval хендлер ("*/..." - матч независимо от origin).
export default http.get("*/api/v1/Notes/:id", ({ params }) =>
  HttpResponse.json<NoteResult>({
    id: String(params.id),
    text: "Demo note",
    createdAt: "2026-06-18T09:00:00Z",
  }),
);
