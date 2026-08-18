using System.Linq.Expressions;
using DotnetVue3TemplateRu.Core.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

/// <summary>
/// Глобальная конвенция мягкого удаления: для каждого mapped-типа, реализующего
/// <see cref="ISoftDeletable"/>, вешает query-filter `e => e.DeletedAtUtc == null` и индекс на
/// DeletedAtUtc. Вызывается в конце OnModelCreating (после ApplyConfigurationsFromAssembly и
/// ConfigureLocalization), когда все типы уже в модели. Запросам, которым нужно видеть мягко
/// удалённые строки (просмотр истории, оживление записи), фильтр снимается через IgnoreQueryFilters().
/// </summary>
public static class ModelBuilderSoftDeleteExtensions
{
    public static ModelBuilder ApplySoftDeleteConvention(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Строим `e => e.DeletedAtUtc == null` по CLR-типу (не-дженерик HasQueryFilter).
            ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
            MemberExpression property = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAtUtc));
            LambdaExpression filter = Expression.Lambda(
                Expression.Equal(property, Expression.Constant(null, typeof(DateTimeOffset?))),
                parameter);

            EntityTypeBuilder entity = modelBuilder.Entity(entityType.ClrType);
            entity.HasQueryFilter(filter);
            entity.HasIndex(nameof(ISoftDeletable.DeletedAtUtc));
        }

        return modelBuilder;
    }
}
