import js from "@eslint/js";
import tseslint from "typescript-eslint";
import vue from "eslint-plugin-vue";
import pluginVueA11y from "eslint-plugin-vuejs-accessibility";
import boundaries from "eslint-plugin-boundaries";
import vueParser from "vue-eslint-parser";

export default tseslint.config(
  {
    ignores: [
      "**/dist/**",
      "**/node_modules/**",
      "**/.nx/**",
      "**/bin/**",
      "**/obj/**",
      "**/coverage/**",
      "**/storybook-static/**",
      // Сгенерированный Orval-клиент не линтуем.
      "libs/frontend/api-client/src/generated/**",
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...vue.configs["flat/recommended"],
  ...pluginVueA11y.configs["flat/recommended"],
  {
    files: ["**/*.vue"],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        parser: tseslint.parser,
        ecmaVersion: "latest",
        sourceType: "module",
      },
    },
  },
  {
    // Шаблонные послабления; форматирование - забота prettier, не eslint.
    rules: {
      "@typescript-eslint/no-explicit-any": "off",
      "@typescript-eslint/no-unused-vars": [
        "warn",
        { argsIgnorePattern: "^_" },
      ],
      "vue/multi-word-component-names": "off",
      "vue/singleline-html-element-content-newline": "off",
      "vue/max-attributes-per-line": "off",
      "vue/html-self-closing": "off",
    },
  },
  {
    // Изоляция SPA (ADR 0028): четыре уровня со строгим направлением импорта
    // "вниз" - app (composition root и роутер) -> core (кросс-раздельная
    // инфраструктура) -> page (раздел = ветка маршрутов) -> feature
    // (вертикальный срез внутри раздела), плюс page-shared для листьев,
    // общих у фич одного раздела.
    //
    // Страница не импортирует страницу-сестру, фича не импортирует фичу-сестру:
    // интеграция двух фич идёт на уровне страницы, двух разделов - через core
    // или маршрут. Более специфичные паттерны (features/*, shared/*) идут ПЕРЕД
    // общим pages/*: выигрывает первый совпавший. Захват имени раздела и
    // подстановка ${from.page} - то, что превращает правило "только своя
    // страница" из пожелания в проверку.
    files: ["apps/frontend/web/src/**/*.{ts,vue}"],
    plugins: { boundaries },
    settings: {
      // Резолв настроен явно двумя резолверами: typescript читает псевдонимы из
      // tsconfig paths, node добирает .vue и импорты директорий через index.ts.
      // Без alias-aware резолва цель импорта осталась бы нераспознанной, и
      // правило молча пропустилось бы - поэтому его проверяют намеренным
      // нарушением границы, а не чтением конфига.
      "import/resolver": {
        typescript: { project: "apps/frontend/web/tsconfig.json" },
        node: { extensions: [".ts", ".js", ".vue", ".mjs", ".cjs"] },
      },
      // src/test/ исключён намеренно: это обвязка прогона (поднятие MSW), а не
      // часть приложения, и уровня в иерархии у неё нет. Без явного исключения
      // файл просто не совпал бы ни с одним элементом и правило пропустило бы
      // его молча - разница в том, что теперь это записано, а не случайность.
      "boundaries/include": ["apps/frontend/web/src/**/*"],
      "boundaries/ignore": ["apps/frontend/web/src/test/**/*"],
      // По умолчанию правило смотрит только на import. Здесь этого мало по двум
      // причинам: барель-файл фичи (index.ts) состоит из re-export, и через него
      // граница обходилась бы молча; а страницы грузятся ленивым import() в
      // routes.ts - без dynamic-import маршруты вообще не проверялись бы.
      "boundaries/dependency-nodes": ["import", "export", "dynamic-import"],
      "boundaries/elements": [
        {
          type: "app",
          pattern: [
            "apps/frontend/web/src/main.ts",
            "apps/frontend/web/src/App.vue",
          ],
          mode: "file",
        },
        { type: "app", pattern: "apps/frontend/web/src/router" },
        { type: "core", pattern: "apps/frontend/web/src/core/*" },
        {
          type: "feature",
          pattern: "apps/frontend/web/src/pages/*/features/*",
          capture: ["page", "feature"],
        },
        {
          type: "page-shared",
          pattern: "apps/frontend/web/src/pages/*/shared/*",
          capture: ["page", "segment"],
        },
        {
          type: "page",
          pattern: "apps/frontend/web/src/pages/*",
          capture: ["page"],
        },
      ],
    },
    rules: {
      "boundaries/element-types": [
        "error",
        {
          default: "disallow",
          rules: [
            // app -> app: composition root импортирует корневой компонент и
            // роутер, они с ним одного уровня.
            { from: "app", allow: ["app", "core", "page"] },
            { from: "core", allow: ["core"] },
            {
              from: "page",
              allow: [
                "core",
                ["feature", { page: "${from.page}" }],
                ["page-shared", { page: "${from.page}" }],
              ],
            },
            {
              from: "feature",
              allow: ["core", ["page-shared", { page: "${from.page}" }]],
            },
            { from: "page-shared", allow: ["core"] },
          ],
        },
      ],
    },
  },
);
