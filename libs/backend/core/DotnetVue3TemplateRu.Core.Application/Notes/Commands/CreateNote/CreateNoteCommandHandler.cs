using System.Globalization;
using DotnetVue3TemplateRu.Core.Application.Configuration;
using DotnetVue3TemplateRu.Core.Application.Notes.Models;
using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using DotnetVue3TemplateRu.Core.Domain.Notes.Repositories;
using Microsoft.Extensions.Options;

namespace DotnetVue3TemplateRu.Core.Application.Notes.Commands.CreateNote;

/// <summary>
/// Wolverine-handler команды создания заметки. Пишет через write-репозиторий
/// агрегата (контракт в Domain), не касаясь DbContext - Application не зависит от
/// EF Core. Репозиторий сохраняет изменение (DbContext - его unit of work).
///
/// Дефолтную культуру (в неё пишется инлайн-значение) берёт из CultureOptions;
/// домен раскладывает переводы команды по культурам. Возвращает текст, разрешённый
/// по текущей культуре запроса (фолбэк - дефолтная культура).
/// </summary>
public static class CreateNoteCommandHandler
{
    public static async Task<NoteResult> Handle(
        CreateNoteCommand command,
        INoteRepository repository,
        IOptions<CultureOptions> options,
        CancellationToken ct)
    {
        string defaultCulture = options.Value.DefaultCulture;

        var note = new Note(defaultCulture, command.Texts);
        await repository.AddAsync(note, ct);

        string text = command.Texts.GetValueOrDefault(CultureInfo.CurrentCulture.Name)
                   ?? command.Texts[defaultCulture];

        return new NoteResult(note.Id, text, note.CreatedAt);
    }
}
