# Гайд: моделирование домена (сущности, value object, инварианты)

Как в этом проекте писать доменный слой: тактические типы (SeedWork), value object на `record`, размещение инвариантов. Почему так - [ADR-0008](../adr/0008-tactical-ddd-seedwork.md); коды ошибок и локализация - [ADR-0018](../adr/0018-domain-errors-codes-and-localization.md).

## Сущность / агрегат

Сущность - `class`, наследует `Entity` (равенство по идентичности, `Id`). Корень с оптимистичной блокировкой наследует `AggregateRoot` (добавляет rowversion `Version`). Оба - в `Core.Domain/SeedWork/`.

Раскладка папки-агрегата (ADR-0009): модель (агрегат, VO, локализации, входные data-записи) - в `Models/`, интерфейсы write-репозиториев - в `Repositories/`; namespace зеркалит папку (`...<Entity>.Models` / `...<Entity>.Repositories`). Группы VO без агрегата (например, `Scheduling/` с периодичностью и сроком) и ядро (`SeedWork/`, `Errors/`, `Localization/`) остаются плоскими.

Правила:

- Свойства - `{ get; private set; }`; никаких публичных сеттеров. Коллекция - приватный backing-list, наружу `IReadOnlyCollection<T>` над ним.
- Приватный конструктор без параметров - только для EF Core.
- Создание - статическая фабрика (`Create(...)`) или публичный валидирующий конструктор; объект-инициализатор наружу недоступен.
- Инвариант проверяется внутри (конструктор/фабрика/метод-поведение) броском `DomainException` с кодом.
- Поведение - методы, меняющие состояние с сохранением инвариантов, а не публичные сеттеры.

```csharp
public class Document : AggregateRoot   // Id и Version - из SeedWork
{
    private readonly List<DocumentLine> _lines = [];

    public const int MinLines = 1;
    public const int MaxLines = 30;

    public Guid OwnerId { get; private set; }
    public IReadOnlyCollection<DocumentLine> Lines => _lines;

    private Document() { }   // Для EF Core.

    public static Document Create(Guid ownerId, /* ... */) { /* фабрика */ }

    public void ReplaceLines(IReadOnlyList<DocumentLineDraft> lines)
    {
        // Инвариант - последняя линия защиты: проверяем до мутации, чтобы объект оставался валидным.
        if (lines.Count is < MinLines or > MaxLines)
        {
            throw new DomainException(ErrorCodes.Document.LineCountOutOfRange);
        }
        // ...
    }
}
```

Не наследуй `Entity`/`AggregateRoot` руками через повтор `Id`/`Version` - они уже в базе. Сущность не объявляй `record` (арх-тест это запретит): равенство сущности - по идентичности, а не по значению.

## Value object

VO - `sealed record` с валидирующим конструктором и get-only свойствами. Так `with` не обходит инвариант (изменить поле нельзя), а равенство по значению даётся из коробки. Вложенная коллекция - `EquatableArray<T>` (`Core.Domain/SeedWork/`), чтобы равенство шло по содержимому, а не по ссылке списка.

```csharp
public sealed record TimeRange
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeRange(TimeOnly start, TimeOnly end)
    {
        if (!IsValid(start, end))
        {
            throw new DomainException(ErrorCodes.Schedule.InvalidTimeRange);
        }
        Start = start;
        End = end;
    }

    // Предикат правила над примитивами - его же зовёт валидатор команды.
    public static bool IsValid(TimeOnly start, TimeOnly end) => start < end;
}

public sealed record DaySchedule
{
    public EquatableArray<TimeRange> Breaks { get; }   // равенство по содержимому
    // ...
}
```

Позиционный record (`record TimeRange(TimeOnly Start, TimeOnly End)`) для VO не используется: его `init`-сеттеры пускают `with`, собирающий невалидный экземпляр.

Хранение VO с коллекциями: одна JSON-колонка через `ValueConverter` + `ValueComparer`; сериализация через суррогатные DTO со списками (стабильная форма), маппинг в доменный VO явный. Конвертер живёт рядом с `DbContext` своего модуля, в `Persistence/`.

Идентификатор из внешней системы тоже заворачивают в VO, а не носят голой строкой. Ближайший случай - `sub` пользователя от провайдера идентичности: домен и интерфейсы write-репозиториев говорят типом, а `string` остаётся только на транспортных краях (команды, запросы, контракты), и заворачивание происходит на краю - в хендлере или адаптере. В БД такой VO лежит той же строковой колонкой через `ValueConverter`: равенство по конвертируемому свойству EF транслирует в SQL, включая `IN`-списки.

## Инвариант и его зеркало в валидаторе

Правило живёт в домене. Если у операции есть валидатор команды, он зеркалит правило тем же кодом ради пофайлового `400` и fail-fast до хендлера - но не является единственным местом правила. Проверка, общая обеим сторонам, описывается один раз статическим guard-предикатом над примитивами: домен зовёт его и бросает, валидатор зовёт как `bool`.

```csharp
// Валидатор команды (Application) зеркалит доменный предикат тем же кодом.
day.RuleFor(d => d.Work!)
    .Must(w => TimeRange.IsValid(w.Start, w.End))
    .When(d => d.Work is not null)
    .WithErrorCode(ErrorCodes.Schedule.InvalidTimeRange);
```

Перед сдачей: ни одно правило не должно жить только в валидаторе (или нигде). Каждое правило валидатора зеркалит доменный инвариант с тем же `ErrorCode`.

Новый код ошибки - константа в `ErrorCodes` модуля + запись в `ErrorMessages.resx` и `ErrorMessages.kk.resx` (ключ = код); страж полноты `ErrorCodesLocalizationTests` падает, если перевод забыт (ADR-0024).

## Чек-лист перед сдачей

Пять анти-паттернов, в которые особенно легко скатывается доменный код (в том числе сгенерированный ИИ). Прогнать перед сдачей/ревью:

1. Анемичная модель - бизнес-логика вынесена из доменных объектов в хендлеры/сервисы, сущность осталась мешком данных.
2. Primitive obsession - примитив вместо доменного типа (голая строка-идентичность, необёрнутое число с правилом).
3. Смещение валидации - правило живёт только в валидаторе Application (или нигде), а не в домене.
4. Публичные сеттеры - изменяемое состояние без инкапсуляции.
5. Data bag - класс-контейнер без поведения.

## Проверка

- `dotnet build DotnetVue3TemplateRu.slnx` - сборка.
- `nx test architecture-tests` - арх-тесты, включая "сущность не record".
- Интеграционные тесты (`tests/DotnetVue3TemplateRu.IntegrationTests`) - round-trip VO и инварианты end-to-end.
