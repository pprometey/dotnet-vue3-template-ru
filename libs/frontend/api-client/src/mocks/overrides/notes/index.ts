import getApiV1NotesId from "./getApiV1NotesId";
import postApiV1Notes from "./postApiV1Notes";
import postApiV2Notes from "./postApiV2Notes";
import getApiV2NotesId from "./getApiV2NotesId";

export const notesOverrides = [
  getApiV1NotesId,
  postApiV1Notes,
  postApiV2Notes,
  getApiV2NotesId,
];
