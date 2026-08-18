namespace DotnetVue3TemplateRu.Core.Domain.Localization;

/// <summary>
/// Базовый тип строки перевода (translation-table) - переиспользуемое ядро локализации.
/// Конкретные типы модулей наследуют его и добавляют локализуемые поля; на культуру -
/// одна строка. Значение культуры по умолчанию дополнительно хранится инлайн на самой
/// сущности. См. ADR 0025 и docs/guides/entity-localization.md.
/// </summary>
public abstract class LocalizationEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    // FK на владельца (internal Guid Id агрегата).
    public Guid RelationId { get; protected set; }

    // Культура строки - CultureInfo.Name (напр. "ru-RU", "kk-KZ").
    public string Culture { get; protected set; } = null!;

    // Для EF.
    protected LocalizationEntity() { }

    protected LocalizationEntity(Guid relationId, string culture)
    {
        RelationId = relationId;
        Culture = culture;
    }
}
