using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using Microsoft.EntityFrameworkCore;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

public class DotnetVue3TemplateRuDbContext : DbContext
{
    public DotnetVue3TemplateRuDbContext(DbContextOptions<DotnetVue3TemplateRuDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(builder =>
        {
            builder.HasKey(n => n.Id);
            builder.Property(n => n.Text).IsRequired().HasMaxLength(Note.MaxTextLength);
            builder.Property(n => n.CreatedAt);
        });

        // Локализация Text: таблица переводов NoteLocalizations (translation-table).
        // Хелпер ставит таблицу, FK RelationId (cascade), уникальный индекс
        // (RelationId, Culture) и длину Culture; длину локализуемого поля - отдельно.
        modelBuilder.ConfigureLocalization<Note, NoteLocalization>(n => n.Localizations);
        modelBuilder.Entity<NoteLocalization>(builder =>
            builder.Property(l => l.Text).IsRequired().HasMaxLength(Note.MaxTextLength));
    }
}
