using DotnetVue3TemplateRu.Core.Application.Notes.Models;

namespace DotnetVue3TemplateRu.Core.Application.Notes;

/// <summary>
/// Read-порт для Note (строгий CQRS): проекция в NoteResult, минуя доменную
/// сущность. Объявлен в Application, реализуется в Infrastructure.
/// </summary>
public interface INoteQueryRepository
{
    Task<NoteResult?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
