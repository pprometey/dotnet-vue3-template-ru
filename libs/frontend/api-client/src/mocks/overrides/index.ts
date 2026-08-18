import { configurationsOverrides } from "./configurations";
import { notesOverrides } from "./notes";
import { pingOverrides } from "./ping";
import { sessionContextOverrides } from "./session-context";

// Курируемые детерминированные override для демо/Storybook.
export const overrideHandlers = [
  ...configurationsOverrides,
  ...notesOverrides,
  ...pingOverrides,
  ...sessionContextOverrides,
];
