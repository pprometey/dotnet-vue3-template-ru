using System.Security.Cryptography;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Поднимает API в памяти (WebApplicationFactory) поверх реального PostgreSQL
/// в Docker (Testcontainers). Один экземпляр на всю тестовую сессию через
/// ClassDataSource&lt;DotnetVue3TemplateRuApiFactory&gt;(Shared = SharedType.PerTestSession).
/// Схема накатывается миграциями один раз. Требует запущенного Docker.
///
/// Изоляция между тестами: каждый тестовый класс вызывает ResetDatabaseAsync()
/// в методе [Before(Test)] - Respawn удаляет строки (~10 мс) без пересоздания
/// схемы и без перезапуска контейнера.
/// </summary>
public class DotnetVue3TemplateRuApiFactory : WebApplicationFactory<Program>, IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    // Ключ подписи тестовых токенов. Асимметричный, как и в бою: симметричного
    // секрета в проекте нет нигде, включая тесты (ADR 0023). Один на сессию -
    // токены разных тестов проверяются одним и тем же открытым ключом.
    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048))
    {
        KeyId = "integration-tests",
    };

    private NpgsqlConnection? _dbConnection;
    private Respawner _respawner = null!;

    /// <summary>Выпускает токен для защищённых эндпоинтов. Подпись RS256 тестовым ключом.</summary>
    public string IssueToken(string subject = "test-user")
        => TestTokens.Issue(_signingKey, subject);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Схему накатывает сама фабрика (InitializeAsync), поэтому выключаем
                // dev-миграции на старте приложения - иначе они бы столкнулись с уже
                // созданной схемой (хост в тестах поднят как Development).
                ["Database:RunStartupMigrations"] = "false",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DotnetVue3TemplateRuDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<DotnetVue3TemplateRuDbContext>(options => options
                .UseNpgsql(_postgres.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(new SoftDeleteSaveChangesInterceptor(TimeProvider.System)));

            // Проверка токена переводится на тестовый ключ: поднимать провайдера
            // идентичности в прогоне значило бы удвоить его длительность ради
            // проверки чужого сервиса. Непроверенным остаётся ровно одно звено -
            // получение ключей по JWKS из discovery-документа провайдера (ADR 0031).
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.RequireHttpsMetadata = false;
                options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect
                    .OpenIdConnectConfiguration();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _signingKey,
                };
            });
        });
    }

    // Вызывается автоматически ClassDataSource до первого теста в сессии.
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Эти три значения кладутся в переменные окружения ДО первой сборки хоста:
        // Program читает их в top-level коде - строку подключения для message store
        // Wolverine, Authority при настройке JWT - раньше, чем применяются хуки
        // ConfigureAppConfiguration. Через хук они бы просто не доехали, и хост упал
        // бы на проверке обязательных настроек.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__dotnet-vue3-template-ru-db", _postgres.GetConnectionString());

        // Authority обязателен на старте (ADR 0023). Значение фиктивное: ходить по нему
        // никто не будет - проверку подписи подменяет тестовый ключ в ConfigureTestServices.
        Environment.SetEnvironmentVariable("Jwt__Authority", TestTokens.TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestTokens.TestAudience);

        using IServiceScope scope = Services.CreateScope();
        DotnetVue3TemplateRuDbContext db = scope.ServiceProvider.GetRequiredService<DotnetVue3TemplateRuDbContext>();
        await db.Database.MigrateAsync();

        _dbConnection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _dbConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            // Только прикладная схема. Служебные таблицы Wolverine живут в схеме
            // wolverine (ADR 0007), и вычистить их между тестами значило бы выдернуть
            // конверты из-под работающего durability agent.
            SchemasToInclude = ["public"],
        });
    }

    /// <summary>
    /// Сбрасывает данные во всех таблицах схемы public.
    /// Вызывается каждым тестовым классом в [Before(Test)]. Типичное время: 5-50 мс.
    /// </summary>
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_dbConnection!);

    // Вызывается автоматически TUnit после завершения сессии.
    public override async ValueTask DisposeAsync()
    {
        // Хост (и Wolverine) останавливаем ДО контейнера: на остановке Wolverine ещё
        // обращается к message store, поэтому БД должна быть жива.
        await base.DisposeAsync();

        // Проверка на null не формальность: если InitializeAsync упал на середине,
        // соединения ещё нет, и без неё настоящая причина падения тонет под
        // NullReferenceException из уборки.
        if (_dbConnection is not null)
        {
            await _dbConnection.CloseAsync();
        }

        await _postgres.DisposeAsync();
    }
}
