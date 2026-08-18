# Диаграммы модуля Core

Исходники - `.puml` (C4-PlantUML), рендер - в одноимённые `.svg`. На SVG ссылается [../core-architecture.md](../core-architecture.md).

Перерисовать после правки `.puml` (из этой папки, PowerShell):

```text
docker run --rm -v "${PWD}:/work" plantuml/plantuml -tsvg -charset UTF-8 /work/<имя>.puml
```

Файлы:

- `core-components` - C3, компоненты ядра и шов разбора идентичности.
