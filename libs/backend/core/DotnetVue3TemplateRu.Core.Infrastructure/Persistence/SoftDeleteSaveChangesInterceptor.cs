using DotnetVue3TemplateRu.Core.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

/// <summary>
/// Переводит физическое удаление сущностей <see cref="ISoftDeletable"/> в мягкое: на сохранении
/// каждая помеченная к удалению строка возвращается в Modified, и ей проставляется DeletedAtUtc.
/// Остальные значения (в т.ч. OriginalValues rowversion) не трогаются, поэтому оптимистичная
/// блокировка `UPDATE ... WHERE Version = original` продолжает работать. Если в проекте
/// появится интерцептор аудита, этот регистрируется ПЕРЕД ним: иначе мягкое удаление
/// попадёт в журнал как физическое.
/// </summary>
public sealed class SoftDeleteSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public SoftDeleteSaveChangesInterceptor(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        SoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach (EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.CurrentValues[nameof(ISoftDeletable.DeletedAtUtc)] = now;
        }
    }
}
