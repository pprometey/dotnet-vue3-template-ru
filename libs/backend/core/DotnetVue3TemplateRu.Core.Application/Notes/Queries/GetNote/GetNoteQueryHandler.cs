using DotnetVue3TemplateRu.Core.Application.Exceptions;
using DotnetVue3TemplateRu.Core.Application.Notes.Models;
using DotnetVue3TemplateRu.Core.Domain.Errors;

namespace DotnetVue3TemplateRu.Core.Application.Notes.Queries.GetNote;

/// <summary>
/// Query-handler: читает через read-порт (проекция в БД), минуя доменную сущность.
/// Отсутствие записи - NotFoundException, которую GlobalExceptionHandler мапит в 404.
/// </summary>
public static class GetNoteQueryHandler
{
    public static async Task<NoteResult> Handle(
        GetNoteQuery query,
        INoteQueryRepository repository,
        CancellationToken ct)
        => await repository.GetByIdAsync(query.Id, ct)
           ?? throw new NotFoundException(ErrorCodes.Note.NotFound, query.Id);
}
