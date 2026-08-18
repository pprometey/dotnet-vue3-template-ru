using DotnetVue3TemplateRu.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Проверяет политику rate limiting "public" на публичном эндпоинте.
///
/// Лимит переопределяется на маленький (PermitLimit = 2) через изолированную
/// фабрику: WithWebHostBuilder поднимает собственный хост (отдельный экземпляр
/// лимитера), а ConfigureTestServices переопределяет RateLimitingOptions через DI
/// (политика читает IOptions per-request). Так тест не зависит от боевого значения
/// и не влияет на общий factory; контейнер PostgreSQL переиспользуется из DotnetVue3TemplateRuApiFactory.
///
/// Отказ отдаётся как 429 в формате ProblemDetails (RFC 9457) + заголовок Retry-After.
/// Текст отказа резолвится по коду ошибки на культуре запроса, как у остальных ошибок.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class RateLimitingEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task Ping_OverLimit_Returns429_AsProblemDetails_WithRetryAfter()
    {
        using WebApplicationFactory<Program> limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitingOptions>(options => options.PermitLimit = 2)));
        HttpClient client = limited.CreateClient();

        HttpResponseMessage first = await client.GetAsync("/api/v1/ping");
        HttpResponseMessage second = await client.GetAsync("/api/v1/ping");
        HttpResponseMessage third = await client.GetAsync("/api/v1/ping");

        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(third.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);

        ProblemResponse? problem = await third.Content.ReadFromJsonAsync<ProblemResponse>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(429);
        await Assert.That(problem.Title).IsEqualTo("Too Many Requests");
        await Assert.That(problem.Detail).IsEqualTo("Слишком много запросов. Повторите попытку позже.");
        await Assert.That(problem.ErrorCode).IsEqualTo("common.rate_limit_exceeded");
        // Значение привязки к запросу меняется от прогона к прогону, поэтому проверяем,
        // что оно есть: без него отказ не связать с записью в логе.
        await Assert.That(problem.TraceId).IsNotNullOrEmpty();

        await Assert.That(third.Headers.Contains("Retry-After")).IsTrue();
    }

    [Test]
    public async Task Ping_OverLimit_LocalizesDetail_ByRequestCulture()
    {
        using WebApplicationFactory<Program> limited = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitingOptions>(options => options.PermitLimit = 1)));
        HttpClient client = limited.CreateClient();

        await client.GetAsync("/api/v1/ping");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add("Accept-Language", "kk");
        HttpResponseMessage rejected = await client.SendAsync(request);

        await Assert.That(rejected.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);

        ProblemResponse? problem = await rejected.Content.ReadFromJsonAsync<ProblemResponse>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Detail).IsEqualTo("Сұраулар тым көп. Кейінірек қайталап көріңіз.");
        await Assert.That(problem.ErrorCode).IsEqualTo("common.rate_limit_exceeded");
    }

    private sealed record ProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? ErrorCode,
        string? TraceId);
}
