import { http, HttpResponse } from "msw";
import type { CreateNoteRequest, NoteResult } from "../../../generated/models";

// Override POST /api/v2/Notes: эхо переданного текста (первая локаль из texts), статус 201.
export default http.post("*/api/v2/Notes", async ({ request }) => {
  const body = (await request.json()) as CreateNoteRequest;
  const text = Object.values(body.texts)[0] ?? "";
  return HttpResponse.json<NoteResult>(
    {
      id: "22222222-2222-2222-2222-222222222222",
      text,
      createdAt: "2026-06-18T09:00:00Z",
    },
    { status: 201 },
  );
});
