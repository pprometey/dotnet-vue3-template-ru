namespace DotnetVue3TemplateRu.Core.Application.Notes.Queries.GetNote;

// Запрос чтения заметки (CQRS). Результат - тот же NoteResult, что и у команды создания.
public record GetNoteQuery(Guid Id);
