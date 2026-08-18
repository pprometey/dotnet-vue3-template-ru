using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotnetVue3TemplateRu.Api.Startup;

/// <summary>
/// Dev-применение миграций на старте. Локальный контейнер PostgreSQL можно пересоздать
/// в любой момент - без этого шага база окажется пустой и первый же запрос упадёт на
/// отсутствующей таблице. Только для Development и только когда приложение владеет
/// схемой: в проде её применяет пайплайн, а интеграционные тесты накатывают её сами и
/// выключают этот шаг флагом Database:RunStartupMigrations (см. Program.cs и фабрику).
///
/// Метод обходит контексты всех модулей: они делят одну БД, но у каждого своя таблица
/// истории миграций.
/// </summary>
public static class DatabaseMigrationExtensions
{
    public static async Task MigrateModuleDatabasesAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        await sp.GetRequiredService<DotnetVue3TemplateRuDbContext>().Database.MigrateAsync();
    }
}
