import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";
import tsconfigPaths from "vite-tsconfig-paths";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  root: here,
  plugins: [vue(), tsconfigPaths({ loose: true })],
  test: {
    environment: "happy-dom",
    globals: false,
    include: ["src/**/*.{test,spec}.ts"],
    setupFiles: ["src/test/msw-setup.ts"],
    // element-plus тянет .scss из исходников: без инлайна Node-загрузчик падает
    // на расширении .scss. Инлайн отдаёт импорты стилей Vitest, где css: false
    // заменяет их пустыми модулями.
    css: false,
    server: {
      deps: {
        inline: ["element-plus"],
      },
    },
    coverage: {
      provider: "v8",
      reporter: ["text", "html"],
      reportsDirectory: "../../../coverage/apps/frontend/web",
    },
  },
});
