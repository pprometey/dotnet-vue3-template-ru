# Учебник: сквозной путь запроса

Пройдём демо-срез `Notes` целиком - от HTTP-запроса до строки в PostgreSQL и обратно. Цель не в самих заметках: это единственный путь, по которому пойдёт любая ваша операция, и после него понятно, куда класть новый код.

Нужен поднятый стек: `yarn dev`.

## Шаг 1. Создать заметку

```bash
curl -s -X POST http://localhost:5249/api/v1/notes \
  -H 'Content-Type: application/json' \
  -d '{"texts":{"ru":"привет","en":"hello"}}'
```

Ответ - `201` и тело `{ "id": "...", "text": "привет", "createdAt": "..." }`.

Запрос прошёл через пять слоёв. Разберём их по порядку.

**Контроллер** (`Api/Controllers/NotesController.cs`) не содержит логики. Он собирает команду и отдаёт её шине:

```csharp
var result = await _bus.InvokeAsync<NoteResult>(new CreateNoteCommand(request.Texts), ct);
```

**Валидатор** (`Core.Application/Notes/Commands/CreateNote/CreateNoteCommandValidator.cs`) отрабатывает раньше обработчика - его вызывает middleware Wolverine. Провал даёт `400` с полями и кодами ошибок, до обработчика дело не доходит.

**Обработчик** - static-класс, зависимости приходят параметрами метода:

```csharp
public static async Task<NoteResult> Handle(
    CreateNoteCommand command,
    INoteRepository repository,
    IOptions<CultureOptions> options,
    CancellationToken ct)
```

**Домен** (`Core.Domain/Notes/Models/Note.cs`) проверяет инвариант ещё раз и бросает `DomainException` с кодом, а не с текстом. Это не дублирование валидатора: валидатор - удобство края, домен - последняя линия.

**Репозиторий** (`Core.Infrastructure/Persistence/Notes/NoteRepository.cs`) сохраняет агрегат; `DbContext` для него - unit of work.

## Шаг 2. Прочитать на другом языке

```bash
curl -s -H 'Accept-Language: en' http://localhost:5249/api/v1/notes/<id>
```

Придёт `"hello"`. Запросите `kk` - придёт `"привет"`: перевода на казахский нет, и сработал фолбэк на значение культуры по умолчанию, которое лежит инлайн в самой строке `notes`.

Так работает локализация контента: все переводы лежат в таблице `note_localizations`, а значение культуры по умолчанию дублируется в `notes.text` - оно доступно без join и служит фолбэком.

Чтение идёт не через тот репозиторий, что запись. `INoteQueryRepository` проецирует нужные колонки прямо в `NoteResult` в SQL, не поднимая агрегат.

## Шаг 3. Посмотреть версию 2

```bash
curl -s http://localhost:5249/api/v2/notes/<id>
```

В ответе добавилось `textLength`. Версия живёт в URL-сегменте; домен и Application при этом не менялись - DTO второй версии собирает контроллер.

## Шаг 4. Увидеть ошибку

```bash
curl -s -X POST http://localhost:5249/api/v1/notes \
  -H 'Content-Type: application/json' -H 'Accept-Language: en' \
  -d '{"texts":{"ru":""}}'
```

Ответ - `400` в формате RFC 9457, со словарём `errors` (локализованные тексты) и словарём `errorCodes` (стабильные коды). Смените `Accept-Language` на `ru` - изменится текст, но не код. Фронтенд ветвится по коду, человек читает текст.

## Шаг 5. Заглянуть в базу

```bash
docker exec -it $(docker ps --filter name=postgres -q) psql -U postgres -d dotnet-vue3-template-ru-db -c '\dt public.*'
```

Таблицы называются `notes` и `note_localizations` - нижний регистр с подчёркиваниями. Поэтому запрос пишется без кавычек: `select * from notes`. Рядом есть схема `wolverine` - это message store шины, прикладных данных в нём нет.

## Шаг 6. Тот же путь из интерфейса

Откройте http://localhost:5173/notes. Форма создаёт заметку, карточка её показывает. Обе фичи не знают друг о друге: идентификатор передаёт между ними страница - это работающий пример правила изоляции.

HTTP-код здесь никто не писал: `usePostApiV1Notes` и `useGetApiV2NotesId` сгенерированы Orval из OpenAPI-документа, который собрала сборка backend.

## Что дальше

- Добавить свою операцию - [how-to.md](how-to.md).
- Точный список типов и эндпоинтов - [reference.md](reference.md).
- Почему слои разложены именно так - [explanation.md](explanation.md).
