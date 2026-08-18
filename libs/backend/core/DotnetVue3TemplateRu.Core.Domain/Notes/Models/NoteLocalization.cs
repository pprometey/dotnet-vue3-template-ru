using DotnetVue3TemplateRu.Core.Domain.Localization;

namespace DotnetVue3TemplateRu.Core.Domain.Notes.Models;

/// <summary>
/// Строка перевода демо-сущности Note (translation-table): наследует базовый
/// LocalizationEntity (Id / RelationId / Culture) и добавляет локализуемое поле Text.
/// На культуру - одна строка; значение дефолтной культуры дополнительно хранится
/// инлайн в Note.Text. См. docs/guides/entity-localization.md.
/// </summary>
public class NoteLocalization : LocalizationEntity
{
    public string Text { get; private set; } = null!;

    // Для EF Core.
    private NoteLocalization() { }

    public NoteLocalization(Guid relationId, string culture, string text)
        : base(relationId, culture)
    {
        Text = text;
    }
}
