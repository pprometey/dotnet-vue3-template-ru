using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

/// <summary>
/// Фабрика контекста для инструментов EF Core (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef dbcontext script</c>).
///
/// Без неё EF поднимает приложение целиком через его хост, а значит требует всё, без
/// чего приложение не стартует: настроенного провайдера идентичности и доступной БД.
/// Для генерации миграции не нужно ни то, ни другое - достаточно модели. Фабрика
/// отвязывает инструменты от старта приложения: <c>migrations add</c> работает вообще
/// без запущенной БД.
///
/// Строка подключения нужна только командам, которые реально ходят в базу
/// (<c>database update</c>); её берут из переменной окружения, а прочерк ниже - чтобы
/// провайдер согласился построить модель.
/// </summary>
public sealed class DotnetVue3TemplateRuDbContextFactory : IDesignTimeDbContextFactory<DotnetVue3TemplateRuDbContext>
{
    public DotnetVue3TemplateRuDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__dotnet-vue3-template-ru-db")
            ?? "Host=localhost;Port=5432;Database=dotnet-vue3-template-ru-db;Username=postgres;Password=postgres";

        DbContextOptions<DotnetVue3TemplateRuDbContext> options = new DbContextOptionsBuilder<DotnetVue3TemplateRuDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new DotnetVue3TemplateRuDbContext(options);
    }
}
