using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.RateLimiting;
using Asp.Versioning;
using DotnetVue3TemplateRu.Api.ExceptionHandlers;
using DotnetVue3TemplateRu.Api.Localization;
using DotnetVue3TemplateRu.Api.Middlewares;
using DotnetVue3TemplateRu.Api.RateLimiting;
using DotnetVue3TemplateRu.Api.Serialization;
using DotnetVue3TemplateRu.Api.Startup;
using DotnetVue3TemplateRu.Core.Application.Configuration;
using DotnetVue3TemplateRu.Core.Application.Diagnostics.Queries.Ping;
using DotnetVue3TemplateRu.Core.Application.UserContext;
using DotnetVue3TemplateRu.Core.Domain.Errors;
using DotnetVue3TemplateRu.Core.Infrastructure;
using DotnetVue3TemplateRu.Core.Infrastructure.Messaging;
using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;
using DotnetVue3TemplateRu.Core.Infrastructure.UserContext;
using DotnetVue3TemplateRu.ServiceDefaults;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.FluentValidation;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Телеметрия, service discovery, Serilog, дефолтные health-checks (self/live).
builder.AddServiceDefaults();

// Build-time экспорт OpenAPI (GetDocument.Insider) идёт без БД и без внешних сервисов:
// документ должен собираться байт-в-байт одинаково в любой среде сборки (ADR 0026).
bool isOpenApiExport = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

// Личный оверрайд разработчика поверх всего остального. appsettings.Development.json и
// переменные окружения (ими Aspire передаёт адреса контейнеров) хост подключает сам.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
}

// Единый формат ошибок (RFC 9457): AddProblemDetails включает ProblemDetails как
// дефолтный ответ для ошибок, GlobalExceptionHandler ловит необработанные
// исключения и мапит известные типы в статус-коды (см. UseExceptionHandler ниже).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// CORS: в разработке SPA и API слушают разные порты, а разные порты одного хоста -
// это разные origin для браузера. В проде за общим reverse proxy список пуст.
// Без AllowCredentials: токен идёт заголовком Authorization (Bearer), не cookie.
string[] corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("SpaCors", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// Профиль обмена Wolverine (ADR 0014): под build-time экспортом OpenAPI - InMemory
// (Solo без message store, без БД); иначе Messaging:Durability (дефолт Persistent -
// Solo + message store). Оба профиля - Solo, поэтому async доступен всегда; на билде
// отличается лишь отсутствие store, а не режим.
MessagingProfile messagingProfile = isOpenApiExport
    ? MessagingProfile.InMemory
    : builder.Configuration.GetValue("Messaging:Durability", MessagingProfile.Persistent);
string? messagingConnectionString = builder.Configuration.GetConnectionString("dotnet-vue3-template-ru-db");

// Строка подключения обязательна везде, кроме build-time экспорта OpenAPI. Проверяем
// сразу: иначе пустая строка уходит в EF и всплывает сетевой ошибкой Npgsql, по которой
// не видно, что настройка просто не доехала.
if (!isOpenApiExport && string.IsNullOrWhiteSpace(messagingConnectionString))
{
    throw new InvalidOperationException(
        "Не задана строка подключения к БД (ключ 'ConnectionStrings:dotnet-vue3-template-ru-db'). " +
        "Локально её подставляет Aspire через withReference(db); вне Aspire задайте её в " +
        "appsettings.Development.json или переменной окружения 'ConnectionStrings__dotnet-vue3-template-ru-db'.");
}

// Wolverine как медиатор/CQRS. Все handlers - в Application (команды и запросы);
// Infrastructure держит только реализации абстракций (репозитории), хендлеров нет.
builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(PingQuery).Assembly);

    // Durability по профилю (ADR 0014): всегда Solo, поэтому доступны и sync
    // (InvokeAsync), и async (PublishAsync). Persistent поднимает message store
    // (фундамент durable/outbox); InMemory (build-time экспорт / запуск без БД)
    // оставляет очереди в памяти. Локальные очереди буферные по умолчанию;
    // durable - точечно per-queue.
    options.UseDotnetVue3TemplateRuDurability(messagingProfile, messagingConnectionString);

    // Разрешаем codegen внедрять scoped-сервисы (DotnetVue3TemplateRuDbContext) через service location.
    options.ServiceLocationPolicy = ServiceLocationPolicy.AllowedButWarn;

    // EF Core transaction middleware: оборачивает обработку сообщения в транзакцию DbContext.
    options.UseEntityFrameworkCoreTransactions();

    // FluentValidation middleware: прогоняет IValidator<T> перед хендлером команды.
    // Валидаторы находятся в ассемблиах, сканируемых выше (Application). Провал ->
    // FluentValidation.ValidationException -> GlobalExceptionHandler отдаёт 400.
    options.UseFluentValidation();
});

builder.Services.AddControllers()
    // long всегда сериализуется строкой (точность int64 в JS Number теряется
    // выше 2^53-1). Парный schema-transformer ниже приводит спек к string.
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new LongAsStringJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new NullableLongAsStringJsonConverter());
    });

// Версионирование API по URL-сегменту (api/v{N}/...). Версию различает
// UrlSegmentApiVersionReader; ApiExplorer подставляет её в пути OpenAPI-документа.
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        // Запрос без явной версии трактуем как v1 (удобство для health/демо-обращений).
        options.AssumeDefaultVersionWhenUnspecified = true;
        // Заголовки api-supported-versions / api-deprecated-versions в ответах.
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        // Имена групп документов: v1, v2, ... (формат для GroupName).
        options.GroupNameFormat = "'v'VVV";
        // Подставляет конкретную версию в шаблон пути {version:apiVersion} в спеке.
        options.SubstituteApiVersionInUrl = true;
    })
    .AddMvc();

// Один общий OpenAPI-документ на все версии: ShouldInclude включает эндпоинты
// любой версии (пути /api/v1/..., /api/v2/... подставляет SubstituteApiVersionInUrl).
// Источник для Scalar и Orval.
builder.Services.AddOpenApi(options =>
{
    options.ShouldInclude = _ => true;
    options.AddSchemaTransformer<Int64AsStringSchemaTransformer>();
    // int32 приходит от веб-дефолтов STJ как union integer|string; возвращаем чистое
    // integer (Orval -> number), int64 при этом остаётся строкой (см. трансформер выше).
    options.AddSchemaTransformer<Int32AsNumberSchemaTransformer>();
});

builder.Services.AddInfrastructure(builder.Configuration);

// IUserContext доезжает до Wolverine-хендлеров через HttpContext (IHttpContextAccessor
// на AsyncLocal): Wolverine исполняет хендлер в отдельном DI-скоупе, и scoped-инстанс из
// UserContextMiddleware туда не попадает. Middleware кладёт заполненный контекст в
// HttpContext.Items; для анонимных запросов - пустой контекст по умолчанию.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext>(sp =>
{
    IDictionary<object, object?>? items = sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.Items;
    return items?[UserContextMiddleware.ItemKey] as IUserContext ?? new RequestUserContext();
});

// Локализация текстов ошибок: IStringLocalizer резолвит текст по коду ошибки из
// ресурсов Resources/Localization/ErrorMessages.*.resx на текущей культуре запроса.
// Единственная точка резолва - GlobalExceptionHandler (домен и валидаторы отдают
// только код). См. ADR 0018.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Культуры интерфейса. Секция читается дважды: типизированными Options (их потребляют
// хендлеры) и здесь, чтобы настроить RequestLocalizationOptions ещё до сборки контейнера.
builder.Services.Configure<CultureOptions>(builder.Configuration.GetSection("Cultures"));
CultureOptions cultureOptions = builder.Configuration.GetSection("Cultures").Get<CultureOptions>()
    ?? new CultureOptions
    {
        DefaultCulture = "ru",
        SupportedCultures =
        [
            new SupportedCultureOptions { Culture = "ru" },
            new SupportedCultureOptions { Culture = "en" },
            new SupportedCultureOptions { Culture = "kk" },
        ],
    };

// Локализация запроса: культура выбирается провайдерами RequestLocalizationMiddleware
// (query-string, cookie, Accept-Language) и попадает в CultureInfo.CurrentCulture
// (AsyncLocal - доступна и Wolverine-хендлерам).
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo[] supported = cultureOptions.SupportedCultures.Select(c => new CultureInfo(c.Culture)).ToArray();
    options.DefaultRequestCulture = new RequestCulture(cultureOptions.DefaultCulture);
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
});

// JWT по OIDC (ADR 0023). Глобальная политика не навязывается - публичные эндпоинты
// (health, культуры, демо) остаются доступны; защита включается атрибутом [Authorize].
builder.AddJwtAuthentication(isOpenApiExport);
builder.Services.AddAuthorization();

// Readiness: доступность БД попадает в /health (но не в /alive).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DotnetVue3TemplateRuDbContext>("database");

// Rate limiting: именованная политика "public" защищает публичные эндпоинты от
// перебора. Подключается opt-in атрибутом [EnableRateLimiting] на контроллере,
// поэтому health-эндпоинты остаются без лимита. Партиционирование per-IP
// (fixed window): отдельное окно на каждый клиентский IP. Отказ -> 429 в формате
// ProblemDetails (RFC 9457, единообразно с GlobalExceptionHandler) + Retry-After.
builder.Services.Configure<RateLimitingOptions>(
    builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.Public, httpContext =>
    {
        // Настройки читаем из IOptions (per-request, дёшево): лимит конфигурируем
        // через секцию RateLimiting и переопределяем в тестах через DI.
        RateLimitingOptions rateLimiting = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimiting.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
            });
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        // Отказ лимитера собирается здесь, а не в GlobalExceptionHandler: исключения нет,
        // перехватывать нечего. Текст всё равно резолвится по коду ошибки на культуре запроса
        // (UseRequestLocalization стоит выше по конвейеру), а errorCode уходит клиенту - так
        // ответ 429 не отличается по форме от остальных ошибок (ADR 0017, 0018).
        IStringLocalizer<ErrorMessages> localizer = context.HttpContext.RequestServices
            .GetRequiredService<IStringLocalizer<ErrorMessages>>();

        IProblemDetailsService problemDetailsService = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = localizer[ErrorCodes.Common.RateLimitExceeded].Value,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.Common.RateLimitExceeded,
                },
            },
        });
    };
});

bool isMigrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);

WebApplication app = builder.Build();

// Режим пайплайна деплоя: накатывает миграции и завершается, не поднимая веб-сервер.
// Тем же образом запускается дважды - сперва с этим аргументом, затем в обычном режиме.
if (isMigrateOnly)
{
    await app.MigrateModuleDatabasesAsync();
    return;
}

// Dev: накатываем миграции на старте - локальный контейнер эфемерный, иначе база пустая
// и первый же запрос падает на отсутствующей таблице. Не в build-time экспорте OpenAPI
// (там БД нет) и не в проде (там схему применяет пайплайн). Флаг
// Database:RunStartupMigrations (по умолчанию включён) выключают интеграционные тесты:
// там хост тоже в Development, но схему накатывает сама тест-фабрика.
if (!isOpenApiExport
    && app.Environment.IsDevelopment()
    && app.Configuration.GetValue("Database:RunStartupMigrations", true))
{
    await app.MigrateModuleDatabasesAsync();
}

// Культура запроса (query/cookie/Accept-Language) -> CultureInfo.CurrentCulture.
// Выше обработчика ошибок намеренно: GlobalExceptionHandler локализует тексты по
// текущей культуре, а культура, выставленная НИЖЕ в конвейере, ему не видна -
// значение AsyncLocal, установленное в более глубоком async-кадре, не всплывает
// наверх при развёртке стека исключения. См. ADR 0018.
app.UseRequestLocalization();

// Перехватывает исключения из всего, что ниже, и отдаёт ProblemDetails через
// GlobalExceptionHandler (культура запроса уже установлена выше).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Сырой OpenAPI-документ (/openapi/v1.json) со всеми версиями - источник
    // для Scalar и Orval.
    app.MapOpenApi();
    // Scalar: интерактивный API UI по /scalar/v1 (вместо устаревшего Swagger).
    app.MapScalarApiReference();
    // Корень бэкенда открывает UI - удобно при заходе по адресу из Aspire.
    // ExcludeFromDescription: это dev-only редирект, не часть контракта - иначе он
    // утекает в OpenAPI-документ при dev-сборке и спека дрейфует относительно CI.
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.UseSerilogRequestLogging();

// В контейнере TLS терминируется обратным прокси, HTTPS-порт не слушается -
// редирект только в dev (Aspire поднимает https-профиль).
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("SpaCors");
app.UseAuthentication();
// После UseAuthentication и ДО UseAuthorization: IUserContext заполняется для
// аутентифицированных запросов (для анонимных - no-op) и готов к моменту, когда его
// читают authorization-хендлеры политик. Формат токена скрыт за IUserContextResolver
// (см. ADR 0023).
app.UseMiddleware<UserContextMiddleware>();
app.UseAuthorization();
// После UseRouting (вставляется фреймворком) и UseCors: лимитер видит выбранный
// эндпоинт с атрибутом [EnableRateLimiting], а на ответе 429 остаются CORS-заголовки.
app.UseRateLimiter();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Делает Program доступным для WebApplicationFactory в интеграционных тестах.
public partial class Program;
