import { defineConfig } from "orval";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));

// Путь к OpenAPI-документу - абсолютный, от расположения этого конфиг-файла, с
// принудительно заглавной буквой диска. Зачем заглавная: nx запускает команду с
// рабочим каталогом, где буква диска строчная ("d:\..."); тогда внутренний ключ
// orval (строится от cwd) расходится с ключом из swagger-parser (канонический
// "D:\...") и генерация падает "Cannot read properties of undefined (reading
// 'paths')". Прямой вызов orval из консоли даёт заглавную с обеих сторон и
// работает - поэтому баг проявляется только под nx. На Linux/CI буквы диска нет,
// регулярка не срабатывает (no-op).
//
// Версионирование API: один общий OpenAPI-документ со всеми версиями (см.
// бэкенд, ShouldInclude). Версии различимы по имени функции (getApiV1NotesId /
// getApiV2NotesId), поэтому генерируется один клиент с плоской структурой.
//
// workspace = корень сгенерированного клиента: заставляет orval создать корневой
// barrel-индекс (src/generated/index.ts), который реэкспортирует src/index.ts.
// target/schemas/mutator резолвятся относительно workspace.
const openApiSpec = resolve(
  here,
  "../../../apps/backend/DotnetVue3TemplateRu.Api/openapi/DotnetVue3TemplateRu.Api.json",
).replace(/^[a-z]:/, (drive) => drive.toUpperCase());

export default defineConfig({
  dotnetVue3TemplateRu: {
    input: {
      target: openApiSpec,
    },
    output: {
      workspace: "./src/generated",
      mode: "tags-split",
      target: "./",
      // splitByTags: схемы одного тега кладутся в подпапку этого тега
      // (models/<tag>/), общие (используемые 2+ тегами или ничем) остаются в
      // корне models/. Даёт feature-slice раскладку - модель ищется рядом со
      // своим ресурсом, а не в общей куче.
      schemas: {
        path: "./models",
        splitByTags: true,
      },
      client: "vue-query",
      httpClient: "axios",
      clean: true,
      indexFiles: true,
      prettier: true,
      // MSW-хендлеры (faker) рядом с каждым tag-split файлом: базовый слой моков
      // для Storybook / dev / тестов. Курируемые override - в src/mocks
      // (см. docs/guides/api-mocks.md).
      mock: { generators: [{ type: "msw" }] },
      override: {
        mutator: {
          path: "../mutator/custom-axios.ts",
          name: "customAxios",
        },
      },
    },
  },
});
