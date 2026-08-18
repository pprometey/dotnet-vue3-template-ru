# Guide: добавить backend-модуль

Backend - модульный монолит: каждый модуль это три .NET-проекта по Clean
Architecture (`Domain` / `Application` / `Infrastructure`), сгруппированных в
доменную папку `libs/backend/<domain>/`. Имя папки - kebab-case имени модуля
(`Core` -> `core`, `PriceList` -> `price-list`), а сами проекты названы
`DotnetVue3TemplateRu.<Module>.<Layer>` (PascalCase). Сейчас в репозитории один модуль - `core`; предметные модули добавляются по этому гайду.
Почему так - [ADR: Модульный монолит](../adr/0006-modular-monolith.md).

Новые модули (например `Billing`, `PriceList`) добавляются скриптом
[scripts/add-new-module.sh](../../scripts/add-new-module.sh), а не вручную -
чтобы обвязка (ссылки слоёв, solution, Nx, подключение к Api) была одинаковой
и не разъезжалась.

```bash
scripts/add-new-module.sh <Module>     # из корня репозитория

scripts/add-new-module.sh Billing
scripts/add-new-module.sh PriceList
```

`<Module>` передаётся в нужном регистре для namespace
(`DotnetVue3TemplateRu.Billing.*`); папка домена и Nx-имена проектов берутся в kebab-case автоматически (`price-list/`, `price-list-domain`, `price-list-application`, `price-list-infrastructure`).

## Что делает скрипт за один проход

- создаёт три проекта `dotnet new classlib` (`net10.0`) в доменной папке
  `libs/backend/<domain>/` (kebab-case);
- проставляет ссылки слоёв: `Application -> Domain`,
  `Infrastructure -> Application`;
- добавляет все три проекта в solution `DotnetVue3TemplateRu.slnx`;
- подключает `Application` + `Infrastructure` модуля к Api
  (`apps/backend/DotnetVue3TemplateRu.Api`);
- генерит `project.json` (Nx) для каждого слоя с корректными
  `implicitDependencies`;
- падает без изменений, если модуль с таким именем уже существует.

## Остаётся сделать вручную

Зависит от модуля, автогенерации нет:

- зарегистрировать DI модуля в `Program.cs` Api (DbContext, Wolverine-handlers,
  сервисы);
- добавить нужные NuGet-пакеты в `Application` / `Infrastructure` (EF Core,
  Wolverine и т.п.) - слои создаются пустыми;
- разложить операцию по CQRS-конвенции (ADR-0010): команда/запрос, её хендлер и
  валидатор - отдельными файлами в `<Entity>/Commands/<Op>/` или
  `<Entity>/Queries/<Op>/`; имя типа = имя операции + суффикс роли
  (`CreateNoteCommand`/`CreateNoteCommandHandler`/`CreateNoteCommandValidator`).
  Read-порт `I<Entity>QueryRepository` - в корне папки сущности; разделяемые
  результаты/read-модели - в `<Entity>/Models/`;
- для команды с вводом добавить валидатор `*CommandValidator :
AbstractValidator<TCommand>` в подпапку операции команды
  (`<Entity>/Commands/<Op>/<Op>CommandValidator.cs`). Отдельная регистрация в DI не нужна - middleware
  `UseFluentValidation` находит валидаторы автоматически; провал даёт `400`
  `ValidationProblemDetails`. См. [ADR: Валидация ввода](../adr/0019-input-validation.md);
- держать команду/запрос полным входом операции (ADR-0011): идентичность текущего пользователя снимает край - контроллер - из `IUserContext` и кладёт в команду явным полем; сам хендлер контекст запроса не читает;
- версионировать контроллер: `[ApiVersion("1.0")]` +
  `[Route("api/v{version:apiVersion}/[controller]")]` (см. [ADR: Версионирование
  API](../adr/0016-api-versioning.md)). Маршрут без версии не используется. Новая
  версия эндпоинта - отдельный метод с `[MapToApiVersion("N.0")]` (плюс ещё один
  `[ApiVersion("N.0")]` на контроллере); неизменившиеся методы оставляют без
  `[MapToApiVersion]` - они обслуживают все версии. DTO разных версий называют
  по-разному (`NoteResult` / `NoteResultV2`), иначе коллизия схем в общем
  OpenAPI-документе.

## Проверка после добавления

```bash
dotnet build DotnetVue3TemplateRu.slnx    # .NET-сборка зелёная
yarn nx graph                    # новый модуль виден в графе
```

> Имена Nx-проектов имеют префикс модуля (`core-domain`, `billing-domain`, ...),
> поэтому при нескольких модулях имена не конфликтуют.
