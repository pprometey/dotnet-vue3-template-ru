namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Проверяет, что путь HTTP -> Wolverine -> handler -> ответ работает.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class PingEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task Ping_ReturnsOk_ViaWolverine()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/ping");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        PongResponse? body = await response.Content.ReadFromJsonAsync<PongResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Status).IsEqualTo("ok");
    }

    private sealed record PongResponse(string Status, DateTimeOffset At);
}
