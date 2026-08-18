using DotnetVue3TemplateRu.Core.Domain.Errors;
using DotnetVue3TemplateRu.Core.Domain.SeedWork;

namespace DotnetVue3TemplateRu.Core.Domain.Notes.Models;

/// <summary>
/// Демо-сущность шаблона. Заменяется реальными сущностями предметных модулей.
/// Существует, чтобы показать вертикальный срез: HTTP -> Application -> EF Core -> БД.
/// Заодно демонстрирует локализацию контента (translation-table): Text локализуется -
/// значение дефолтной культуры хранится инлайн, прочие переводы - в Localizations
/// (см. ADR 0025 и docs/guides/entity-localization.md).
/// </summary>
public class Note : Entity
{
    // Максимальная длина текста заметки (на культуру): единый источник для доменного
    // инварианта, валидатора и конфигурации EF.
    public const int MaxTextLength = 1000;

    private readonly List<NoteLocalization> _localizations = [];

    // Текст дефолтной культуры инлайн: доступен без join и служит фолбэком для культур
    // без перевода. Перевод текущей культуры разрешается на чтении из Localizations.
    public string Text { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Все культуры (включая дефолтную) - по строке на культуру.
    public IReadOnlyCollection<NoteLocalization> Localizations => _localizations;

    // Для EF Core.
    private Note() { }

    /// <summary>
    /// Создаёт заметку из набора переводов (культура -> текст). Значение дефолтной
    /// культуры дублируется инлайн в Text; каждая культура (включая дефолтную) - строка
    /// в Localizations.
    /// </summary>
    public Note(string defaultCulture, IReadOnlyDictionary<string, string> texts)
    {
        // Доменный инвариант - последняя линия (defense-in-depth): валидатор команды
        // отрабатывает раньше, но домен не полагается на него. Бросается код ошибки,
        // текст локализуется на границе (см. ADR 0024).
        if (!texts.TryGetValue(defaultCulture, out string? defaultText) || string.IsNullOrWhiteSpace(defaultText))
        {
            throw new DomainException(ErrorCodes.Note.TextRequired);
        }

        Text = defaultText;

        foreach ((string? culture, string? text) in texts)
        {
            if (text.Length > MaxTextLength)
            {
                throw new DomainException(ErrorCodes.Note.TextTooLong);
            }

            _localizations.Add(new NoteLocalization(Id, culture, text));
        }
    }
}
