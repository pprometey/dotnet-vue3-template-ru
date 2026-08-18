import type { StorybookConfig } from "@storybook/vue3-vite";

const config: StorybookConfig = {
  stories: ["../src/**/*.stories.@(ts|tsx)"],
  addons: ["@storybook/addon-a11y"],
  framework: { name: "@storybook/vue3-vite", options: {} },
  // Service worker MSW лежит рядом с приложением: одна копия на проект, чтобы
  // её версия не разъезжалась с версией пакета.
  staticDirs: ["../public"],
};

export default config;
