using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using DotnetVue3TemplateRu.Core.Domain.Notes.Repositories;

namespace DotnetVue3TemplateRu.Core.Domain.Notes.Repositories;

/// <summary>
/// Write-репозиторий агрегата Note - контракт в Domain (часть доменного языка).
/// Реализация - в Infrastructure: добавляет и сохраняет агрегат (DbContext - его
/// unit of work).
/// </summary>
public interface INoteRepository
{
    Task AddAsync(Note note, CancellationToken ct = default);
}
