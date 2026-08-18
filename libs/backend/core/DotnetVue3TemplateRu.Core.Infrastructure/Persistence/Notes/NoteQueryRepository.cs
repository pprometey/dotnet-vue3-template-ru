using System.Globalization;
using DotnetVue3TemplateRu.Core.Application.Notes;
using DotnetVue3TemplateRu.Core.Application.Notes.Models;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence.Notes;

/// <summary>
/// Реализация read-порта Note: проекция DotnetVue3TemplateRuDbContext -> NoteResult (строгий CQRS,
/// без загрузки доменной сущности). Text разрешается по текущей культуре запроса:
/// перевод из Localizations, при его отсутствии - инлайн-дефолт (выбор культуры на
/// чтении, см. ADR 0025).
/// </summary>
public class NoteQueryRepository : INoteQueryRepository
{
    private readonly DotnetVue3TemplateRuDbContext _db;

    public NoteQueryRepository(DotnetVue3TemplateRuDbContext db) => _db = db;

    public Task<NoteResult?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        string culture = CultureInfo.CurrentCulture.Name;

        return _db.Notes
            .Where(n => n.Id == id)
            .Select(n => new NoteResult(
                n.Id,
                n.Localizations
                    .Where(l => l.Culture == culture)
                    .Select(l => l.Text)
                    .FirstOrDefault() ?? n.Text,
                n.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }
}
