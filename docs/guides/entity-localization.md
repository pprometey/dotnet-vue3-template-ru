# Guide: локализация контента сущности

Локализуемые текстовые поля сущности (названия, описания, справочные значения) хранятся по паттерну translation-table: значение культуры по умолчанию лежит инлайн в главной таблице, переводы прочих культур - строками в дочерней таблице `<Entity>Localizations`, язык выбирается на чтении по культуре запроса. Механизм переиспользуемый: базовый тип `LocalizationEntity` и EF-хелпер `ConfigureLocalization` живут в `Core`, модуль добавляет только свой тип перевода. Почему так - [ADR: Локализация контента сущностей](../adr/0021-entity-content-localization.md). Это не то же, что локализация текстов ошибок (статические строки в resx, [ADR-0018](../adr/0018-domain-errors-codes-and-localization.md)).

Сквозной пример - демо-сущность `Note` (`libs/backend/core/DotnetVue3TemplateRu.Core.Domain/Notes/Models/Note.cs`), у которой локализуется поле `Text`.

## Доменная модель: тип перевода и сущность

Объявить тип перевода, наследуя `LocalizationEntity` (`Id`/`RelationId`/`Culture` уже в базе) и добавив локализуемые поля:

```csharp
public class NoteLocalization : LocalizationEntity
{
    public string Text { get; private set; } = null!;

    private NoteLocalization() { } // для EF

    public NoteLocalization(Guid relationId, string culture, string text)
        : base(relationId, culture)
    {
        Text = text;
    }
}
```

В самой сущности локализуемое поле остаётся инлайн (значение дефолтной культуры), рядом - коллекция переводов; конструктор раскладывает входящие локали в строки и синхронизирует инлайн-дефолт:

```csharp
public class Note
{
    private readonly List<NoteLocalization> _localizations = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Text { get; private set; } = string.Empty; // дефолтная культура инлайн
    public IReadOnlyCollection<NoteLocalization> Localizations => _localizations;

    private Note() { } // для EF

    public Note(string defaultCulture, IReadOnlyDictionary<string, string> texts)
    {
        if (!texts.TryGetValue(defaultCulture, out var defaultText) || string.IsNullOrWhiteSpace(defaultText))
        {
            throw new DomainException(ErrorCodes.Note.TextRequired);
        }

        Text = defaultText;

        foreach (var (culture, text) in texts)
        {
            _localizations.Add(new NoteLocalization(Id, culture, text));
        }
    }
}
```

Инвариант - наличие непустого значения дефолтной культуры (последняя линия защиты, дублирует валидатор команды, [ADR-0018](../adr/0018-domain-errors-codes-and-localization.md)). Домен зависит только от `Core.Domain` (базовый `LocalizationEntity`), не от инфраструктуры.

## Маппинг (EF Core)

В `OnModelCreating` вызвать общий хелпер и отдельно задать длины локализуемых полей (хелпер настраивает только `Culture`):

```csharp
modelBuilder.ConfigureLocalization<Note, NoteLocalization>(n => n.Localizations);
modelBuilder.Entity<NoteLocalization>(builder =>
    builder.Property(l => l.Text).IsRequired().HasMaxLength(1000));
```

Хелпер (`Core.Infrastructure/Persistence/ModelBuilderLocalizationExtensions.cs`) ставит таблицу `<Entity>Localizations` (`NoteLocalizations`), FK `RelationId` с каскадным удалением, уникальный индекс `(RelationId, Culture)` и длину `Culture`.

## Чтение: разрешение по культуре

Локализуемое поле разрешается в проекции квери-репозитория (строгий CQRS): перевод текущей культуры, при его отсутствии - инлайн-дефолт. Текущая культура - `CultureInfo.CurrentCulture.Name` (её ставит `RequestLocalizationMiddleware`, значение `AsyncLocal` доступно и в хендлерах):

```csharp
var culture = CultureInfo.CurrentCulture.Name;

return _db.Notes
    .Where(n => n.Id == id)
    .Select(n => new NoteResult(
        n.Id,
        n.Localizations
            .Where(l => l.Culture == culture)
            .Select(l => l.Text)
            .FirstOrDefault() ?? n.Text,
        n.CreatedAt))
    .FirstOrDefaultAsync(ct);
```

Фронту отдаётся уже разрешённая строка - выбор культуры на чтении, не в контракте DTO.

## Запись: раскладка локалей

Команда несёт все локали сразу (культура -> текст); хендлер берёт дефолтную культуру из `CultureOptions.DefaultCulture` и отдаёт набор в доменный конструктор, который раскладывает его в строки `Localizations` и дублирует дефолт инлайн:

```csharp
public record CreateNoteCommand(IReadOnlyDictionary<string, string> Texts);

// handler
var defaultCulture = options.Value.DefaultCulture;
var note = new Note(defaultCulture, command.Texts);
await repository.AddAsync(note, ct);
```

Валидатор команды проверяет непустоту набора и каждого перевода (длина, непустой текст), неся только код ошибки (`WithErrorCode`); текст резолвится на границе ([ADR-0018](../adr/0018-domain-errors-codes-and-localization.md)):

```csharp
RuleFor(x => x.Texts).NotEmpty().WithErrorCode(ErrorCodes.Note.TextRequired);
RuleForEach(x => x.Texts.Values)
    .NotEmpty().WithErrorCode(ErrorCodes.Note.TextRequired)
    .MaximumLength(1000).WithErrorCode(ErrorCodes.Note.TextTooLong);
```

Плоский источник (значение по одной культуре запроса) раскладывается так же: строка перевода на эту культуру, и синхронно инлайн-поле, если это дефолтная культура.

## Несколько локализуемых полей

Локализуемое поле в контракте - карта `culture -> значение` (`IReadOnlyDictionary<string, string>`), в примере это `Texts`; такая форма называется `LocalizedText`. У сущности с несколькими локализуемыми полями таких свойств несколько - по одному на поле (например, `Names` и `Descriptions`), каждое со своей картой культур, покрытие языков у полей независимо. Домен раскладывает их в одну строку `<Entity>Localization` на культуру (строка несёт колонки всех полей), а проекция чтения собирает карты обратно по полям. На отображении поле отдаётся разрешённой строкой (как `Text` в `NoteResult`); на редактировании - полной картой всех культур, чтобы редактор показал каждый язык.

## Миграция

Таблица переводов - новая, нужна миграция модуля:

```bash
dotnet ef migrations add NoteLocalization \
  --project libs/backend/core/DotnetVue3TemplateRu.Core.Infrastructure \
  --startup-project apps/backend/DotnetVue3TemplateRu.Api
```

Создаётся `NoteLocalizations` (`Id` PK, `RelationId` FK cascade, `Culture`, локализуемые поля, уникальный индекс `(RelationId, Culture)`); инлайн-колонка в главной таблице остаётся как денормализованный дефолт.

## Добавление языка

Добавить культуру в `SupportedCultures` (секция `Cultures`, см. `Program.cs`) и писать строки перевода в `<Entity>Localizations`. Изменение схемы БД не требуется.

## Проверка

```bash
dotnet build DotnetVue3TemplateRu.slnx
dotnet test tests/DotnetVue3TemplateRu.IntegrationTests
```

End-to-end (`yarn dev`): создать заметку с переводами `{ "ru": ..., "kk": ... }`, затем `GET` с разными `Accept-Language` - `kk` отдаёт казахский перевод, `ru` русский, неподдерживаемая культура - фолбэк на инлайн-дефолт.
