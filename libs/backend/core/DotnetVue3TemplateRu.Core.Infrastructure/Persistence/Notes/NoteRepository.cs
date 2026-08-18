using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using DotnetVue3TemplateRu.Core.Domain.Notes.Repositories;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence.Notes;

public class NoteRepository : INoteRepository
{
    private readonly DotnetVue3TemplateRuDbContext _db;

    public NoteRepository(DotnetVue3TemplateRuDbContext db) => _db = db;

    // Репозиторий сохраняет агрегат: DbContext - его unit of work, коммит здесь.
    public async Task AddAsync(Note note, CancellationToken ct = default)
    {
        await _db.Notes.AddAsync(note, ct);
        await _db.SaveChangesAsync(ct);
    }
}
