namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Проверяет дефолтные health-эндпоинты из ServiceDefaults:
///   /alive  - liveness (только тег "live");
///   /health - readiness (включая проверку доступности БД).
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class HealthEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task Alive_ReturnsHealthy()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/alive");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Health_ReturnsHealthy_WhenDatabaseIsUp()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
