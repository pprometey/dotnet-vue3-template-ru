using DotnetVue3TemplateRu.Core.Application.Notes;
using DotnetVue3TemplateRu.Core.Application.UserContext;
using DotnetVue3TemplateRu.Core.Domain.Notes.Repositories;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence.Notes;
using DotnetVue3TemplateRu.Core.Infrastructure.UserContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetVue3TemplateRu.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Строку подключения кладёт Aspire через withReference(db) в штатную секцию
        // ConnectionStrings, поэтому читается она штатным GetConnectionString.
        string? connectionString = configuration.GetConnectionString("dotnet-vue3-template-ru-db");

        services.AddDbContext<DotnetVue3TemplateRuDbContext>(options => options
            .UseNpgsql(connectionString)
            // Имена таблиц и колонок в snake_case: иначе PostgreSQL требует кавычек
            // в каждом ручном запросе к таблице, созданной EF Core (ADR 0007).
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new SoftDeleteSaveChangesInterceptor(TimeProvider.System)));

        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<INoteQueryRepository, NoteQueryRepository>();

        services.AddScoped<IUserContextResolver, StandardClaimsUserContextResolver>();

        // IUserContext регистрируется в композиционном корне (Api) через HttpContext:
        // Wolverine исполняет хендлеры в отдельном DI-скоупе, поэтому scoped-инстанс из
        // middleware до них не доезжает - контекст переносится через HttpContext.Items
        // (IHttpContextAccessor на AsyncLocal). RequestUserContext остаётся здесь как
        // контейнер, но заполняется и читается через HttpContext.

        return services;
    }
}
