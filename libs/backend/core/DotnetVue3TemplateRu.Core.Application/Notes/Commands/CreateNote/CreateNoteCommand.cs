namespace DotnetVue3TemplateRu.Core.Application.Notes.Commands.CreateNote;

// Контракт команды (CQRS). Handler - в Application; пишет через write-репозиторий
// агрегата (контракт в Domain), Application не зависит от EF Core.
//
// Text локализован: команда несёт все локали сразу (культура -> текст, напр.
// { "ru-RU": "...", "kk-KZ": "..." }); домен раскладывает их в строки переводов и
// дублирует дефолтную культуру инлайн (см. ADR 0025).
public record CreateNoteCommand(IReadOnlyDictionary<string, string> Texts);
