using DotnetVue3TemplateRu.Core.Domain.Localization;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DotnetVue3TemplateRu.Core.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Форма EF-модели, которую строит универсальный хелпер ConfigureLocalization
/// (Core.Infrastructure). Модель собирается офлайн (без подключения к БД) на тестовой
/// паре сущность + перевод и проверяется метаданными EF.
/// </summary>
public class LocalizationMappingTests
{
    [Test]
    public async Task ConfigureLocalization_MapsTranslationTable()
    {
        using var context = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql("Host=localhost;Database=model-only")
                .UseSnakeCaseNamingConvention()
                .Options);

        IEntityType localization = context.Model.FindEntityType(typeof(TestOwnerLocalization))!;

        // Таблица <entity>_localizations: множественное число в snake_case (ADR 0007).
        await Assert.That(localization.GetTableName()).IsEqualTo("test_owner_localizations");

        // PK = Id.
        await Assert.That(string.Join(",", localization.FindPrimaryKey()!.Properties.Select(p => p.Name)))
            .IsEqualTo("Id");

        // Culture обязателен и с ограничением длины.
        IProperty culture = localization.FindProperty(nameof(LocalizationEntity.Culture))!;
        await Assert.That(culture.IsNullable).IsFalse();
        await Assert.That(culture.GetMaxLength()).IsEqualTo(16);

        // Уникальный индекс (RelationId, Culture).
        IIndex uniqueIndex = localization.GetIndexes().Single(i => i.IsUnique);
        await Assert.That(string.Join(",", uniqueIndex.Properties.Select(p => p.Name)))
            .IsEqualTo("RelationId,Culture");

        // FK на владельца по RelationId, каскадное удаление.
        IForeignKey foreignKey = localization.GetForeignKeys().Single();
        await Assert.That(foreignKey.PrincipalEntityType.ClrType).IsEqualTo(typeof(TestOwner));
        await Assert.That(string.Join(",", foreignKey.Properties.Select(p => p.Name)))
            .IsEqualTo("RelationId");
        await Assert.That(foreignKey.DeleteBehavior).IsEqualTo(DeleteBehavior.Cascade);
    }

    // Фикстура формы модели: экземпляров этих типов тест не создаёт, ему нужны только
    // объявления, по которым EF собирает метаданные. Отсюда инициализаторы у свойств -
    // присваивать их в коде некому.
    private sealed class TestOwner
    {
        public Guid Id { get; } = Guid.Empty;
        public string Name { get; set; } = null!;
        public ICollection<TestOwnerLocalization> Localizations { get; set; } = [];
    }

    private sealed class TestOwnerLocalization : LocalizationEntity
    {
        public string Name { get; set; } = null!;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        // DbSet не объявляется намеренно: сущность попадает в модель явным вызовом
        // Entity<TestOwner>() ниже, и второй способ её зарегистрировать не нужен.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestOwner>().HasKey(o => o.Id);
            modelBuilder.ConfigureLocalization<TestOwner, TestOwnerLocalization>(o => o.Localizations);
        }
    }
}
