using System.Linq.Expressions;
using DotnetVue3TemplateRu.Core.Domain.Localization;
using Microsoft.EntityFrameworkCore;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

/// <summary>
/// Универсальный маппинг таблицы переводов (translation-table): для сущности с коллекцией
/// строк перевода ставит FK RelationId (cascade),
/// уникальный индекс (RelationId, Culture) и ограничение длины Culture. Одинаково для
/// любой локализуемой сущности. См. ADR 0021 и docs/guides/entity-localization.md.
/// </summary>
public static class ModelBuilderLocalizationExtensions
{
    public static ModelBuilder ConfigureLocalization<TEntity, TLocalization>(
        this ModelBuilder modelBuilder,
        Expression<Func<TEntity, IEnumerable<TLocalization>?>> localizations)
        where TEntity : class
        where TLocalization : LocalizationEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasMany(localizations)
            .WithOne()
            .HasForeignKey(l => l.RelationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TLocalization>(builder =>
        {
            builder.ToTable(TableName(typeof(TLocalization)));
            builder.HasKey(l => l.Id);
            // Ключ задаётся доменом (Guid в конструкторе LocalizationEntity), не БД. Без этого
            // EF по умолчанию считает Guid-PK ValueGeneratedOnAdd и при добавлении новой строки
            // перевода к УЖЕ существующему агрегату (напр. новый язык у старой заметки) трактует
            // непустой ключ как существующую строку -> UPDATE вместо INSERT (0 строк).
            builder.Property(l => l.Id).ValueGeneratedNever();
            builder.Property(l => l.Culture).IsRequired().HasMaxLength(16);
            builder.HasIndex(l => new { l.RelationId, l.Culture }).IsUnique();
        });

        return modelBuilder;
    }

    /// <summary>
    /// Имя таблицы переводов: множественное число в snake_case (NoteLocalization ->
    /// note_localizations).
    ///
    /// Имя приходится собирать здесь, а не отдавать конвенции. Множественное число EF
    /// берёт из имени свойства DbSet, а у таблицы переводов своего DbSet нет - она
    /// доступна только через коллекцию агрегата. Без явного имени вышло бы
    /// note_localization в единственном числе рядом с notes во множественном.
    /// Конвенция snake_case (ADR 0007) явное имя не переписывает, поэтому регистр
    /// задаётся тут же - иначе в схеме появилась бы одна таблица в PascalCase.
    /// </summary>
    private static string TableName(Type localizationType)
    {
        string name = localizationType.Name;
        var result = new System.Text.StringBuilder(name.Length + 8);

        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                if (i > 0)
                {
                    result.Append('_');
                }

                result.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                result.Append(name[i]);
            }
        }

        return result.Append('s').ToString();
    }
}
