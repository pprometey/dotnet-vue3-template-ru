import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import tsconfigPaths from "vite-tsconfig-paths";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  root: here,
  plugins: [
    vue(),
    // Алиасы путей (@core/*, @pages/*, @dotnet-vue3-template-ru/api-client) берутся из
    // tsconfig.json - единый источник, чтобы список не расходился между сборкой,
    // тестами и линтом. loose нужен, чтобы алиасы применялись и к импортам .vue.
    tsconfigPaths({ loose: true }),
  ],
  css: {
    preprocessorOptions: {
      scss: {
        // Переопределения переменных Element Plus обязаны попасть в каждый .scss
        // ДО его собственных правил, иначе тема молча не применится.
        additionalData: `@use "core/theme/element-vars.scss" as *;`,
        // Псевдонимы путей TypeScript sass не читает - импорты в .scss он
        // резолвит своими loadPaths. Отсюда корень src в списке и путь выше
        // без @core.
        loadPaths: [resolve(here, "src")],
        api: "modern-compiler",
      },
    },
  },
  server: {
    // Порт закреплён жёстко: его знают CORS-политика API и список redirectUris
    // в realm провайдера. Случайный порт молча ломает вход и запросы из браузера.
    port: 5173,
    strictPort: true,
  },
  preview: {
    port: 5173,
    strictPort: true,
  },
});
