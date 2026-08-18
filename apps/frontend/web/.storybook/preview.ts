import type { Preview } from "@storybook/vue3-vite";
import { setup } from "@storybook/vue3-vite";
import { initialize, mswLoader } from "msw-storybook-addon";
import { createPinia } from "pinia";
import { VueQueryPlugin } from "@tanstack/vue-query";
import ElementPlus from "element-plus";
import { handlers } from "@dotnet-vue3-template-ru/api-client/mocks";
import { createAppI18n } from "../src/core/i18n";
import "../src/core/theme/index.scss";

initialize({ onUnhandledRequest: "bypass" });

setup((app) => {
  app.use(createPinia());
  app.use(VueQueryPlugin);
  app.use(ElementPlus);
  app.use(createAppI18n());
});

const preview: Preview = {
  parameters: {
    msw: { handlers },
  },
  loaders: [mswLoader],
};

export default preview;
