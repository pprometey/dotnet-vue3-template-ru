import { http, HttpResponse } from "msw";
import type { ConfigurationGetResult } from "../../../generated/models";

// Override GET /api/v1/Configurations/client: детерминированный список культур
// интерфейса для Storybook и демо.
export default http.get("*/api/v1/Configurations/client", () =>
  HttpResponse.json<ConfigurationGetResult>({
    cultures: {
      defaultCulture: "ru",
      supportedCultures: ["ru", "en", "kk"],
    },
  }),
);
