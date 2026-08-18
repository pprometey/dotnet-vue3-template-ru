import { http, HttpResponse } from "msw";
import type { CreateNoteRequest, NoteResult } from "../../../generated/models";

// Override POST /api/v1/Notes: эхо переданного текста (первая локаль из texts), статус 201.
export default http.post("*/api/v1/Notes", async ({ request }) => {
  const body = (await request.json()) as CreateNoteRequest;
  const text = Object.values(body.texts)[0] ?? "";
  return HttpResponse.json<NoteResult>(
    {
      id: "11111111-1111-1111-1111-111111111111",
      text,
      createdAt: "2026-06-18T09:00:00Z",
    },
    { status: 201 },
  );
});
