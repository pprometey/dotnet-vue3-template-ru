import { http, HttpResponse } from "msw";
import type { NoteResultV2 } from "../../../generated/models";

// Override GET /api/v2/Notes/{id}: контракт v2 с textLength (long идёт строкой).
export default http.get("*/api/v2/Notes/:id", ({ params }) =>
  HttpResponse.json<NoteResultV2>({
    id: String(params.id),
    text: "Demo note",
    createdAt: "2026-06-18T09:00:00Z",
    textLength: 9,
  }),
);
