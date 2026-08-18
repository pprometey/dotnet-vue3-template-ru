# Snapshots

Эталоны snapshot-тестов (Verify). Каждый файл `*.verified.txt` - зафиксированная форма ответа эндпоинта. Тест сравнивает фактический ответ с эталоном и падает при расхождении.

## Структура

Подпапка на каждый тестовый класс - иначе при росте сьюты тут была бы плоская простыня из сотен файлов.

```text
Snapshots/
  <ТестовыйКласс>/
    <ИмяСнапшота>.verified.txt
```

Пример:

```text
Snapshots/
  NotesEndpointTests/
    CreateNote_ResponseShape.verified.txt
```

Папку (`Snapshots/<Класс>`) задаёт `DerivePathInfo` в [VerifyConfig.cs](../VerifyConfig.cs). Имя файла задаётся явно в самом тесте через `.UseFileName("...")` - без этого Verify.TUnit добавляет в имя параметр конструктора из `ClassDataSource` (`factory=...`), и имя становится длинным и нечитаемым.

## Формат файла

Verify по умолчанию пишет не строгий JSON, а свой relaxed-формат (без кавычек у имён полей):

```text
{
  Id: {Scrubbed},
  Text: Snapshot test note,
  CreatedAt: {Scrubbed}
}
```

`{Scrubbed}` - значение поля, заменённое через `ScrubMembers<T>(...)` в тесте. Скрабируются нестабильные поля (Id, даты, timestamp), иначе снапшот не совпадал бы между запусками. Остальные поля проверяются как есть.

## verified vs received

- `*.verified.txt` - принятый эталон. **Коммитится в git.**
- `*.received.txt` - фактический ответ последнего прогона. Появляется только когда тест упал. **В git не идёт** (`.gitignore`: `*.received.*`).

## Workflow

Первый запуск теста (или после намеренного изменения API) падает и пишет `*.received.txt`. Сравни его с эталоном и прими.

```bash
dotnet tool install -g verify.tool   # один раз на машину
```

### Принять снапшоты через CLI

```bash
cd tests/DotnetVue3TemplateRu.IntegrationTests   # рабочая папка

dotnet-verify accept        # обойти все *.received и спросить по каждому (выборочно)
dotnet-verify accept -y     # принять все *.received без вопросов
dotnet-verify accept -w Snapshots/NotesEndpointTests   # сузить область до одной папки
```

Отдельного флага "принять конкретный файл по имени" нет. Точечно - либо интерактивный режим (без `-y`, отвечаешь по каждому), либо сузить поиск через `-w <папка>`.

### Другие способы принять (на практике чаще)

- **Через diff-tool.** При падении снапшот-теста Verify сам открывает установленный инструмент сравнения (VS Code, Beyond Compare и т.п.) с диффом received vs verified - принимаешь прямо там, по одному файлу.
- **Руками.** Переименовать `*.received.txt` -> `*.verified.txt`. `accept` делает ровно это.

После принятия закоммить новый/изменённый `*.verified.txt`. Подробнее - [docs/guides/integration-tests.md](../../../docs/guides/integration-tests.md).
