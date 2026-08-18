namespace DotnetVue3TemplateRu.Core.Application.Notes.Models;

// Результат для Note, общий для команды создания и запроса чтения. Отдаёт уже
// разрешённый по текущей культуре текст - выбор культуры на чтении, не в контракте
// (см. ADR 0025).
public record NoteResult(Guid Id, string Text, DateTimeOffset CreatedAt);
