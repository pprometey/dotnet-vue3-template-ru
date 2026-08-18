using System.Net.Http.Headers;

namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Путь идентичности целиком: токен -> UserContextMiddleware -> резолвер ->
/// IUserContext -> обработчик -> ответ (ADR 0023). Токен подписан тестовым ключом,
/// провайдер в прогоне не поднимается (ADR 0031).
///
/// Ответ проверяется целиком, а не по одному полю: пустая идентичность выглядит
/// как успешный ответ с пустой строкой, и частичная проверка её пропустила бы.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class SessionContextEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task WithToken_ReturnsSubjectFromToken()
    {
        HttpResponseMessage response = await Get(factory.IssueToken(subject: "user-42"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        SessionContext? body = await response.Content.ReadFromJsonAsync<SessionContext>();
        await Assert.That(body).IsEqualTo(new SessionContext("user-42"));
    }

    [Test]
    public async Task WithoutToken_IsUnauthorized()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/session-context");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task WithMalformedToken_IsUnauthorized()
    {
        HttpResponseMessage response = await Get("not-a-token");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> Get(string token)
    {
        HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/session-context");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request);
    }

    private sealed record SessionContext(string UserId);
}
