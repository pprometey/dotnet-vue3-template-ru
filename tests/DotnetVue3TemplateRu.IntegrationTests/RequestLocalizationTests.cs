namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Выбор культуры запроса: RequestLocalizationMiddleware ставит CultureInfo.CurrentCulture
/// из Accept-Language (в поддерживаемых), иначе - дефолт. Проверяется через анонимный
/// GET /api/v1/configurations/client, чей хендлер отдаёт CultureInfo.CurrentCulture.Name
/// в поле cultures.defaultCulture. SupportedCultures = ["ru", "en", "kk"], дефолт ru.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class RequestLocalizationTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Test]
    public async Task Client_WithSupportedAcceptLanguage_UsesThatCulture()
    {
        ConfigResponse body = await GetClientConfig("kk");

        await Assert.That(body.Cultures.DefaultCulture).IsEqualTo("kk");
    }

    [Test]
    public async Task Client_WithoutAcceptLanguage_UsesDefaultCulture()
    {
        ConfigResponse body = await GetClientConfig(acceptLanguage: null);

        await Assert.That(body.Cultures.DefaultCulture).IsEqualTo("ru");
    }

    [Test]
    public async Task Client_WithUnsupportedAcceptLanguage_FallsBackToDefault()
    {
        ConfigResponse body = await GetClientConfig("fr-FR");

        await Assert.That(body.Cultures.DefaultCulture).IsEqualTo("ru");
    }

    private async Task<ConfigResponse> GetClientConfig(string? acceptLanguage)
    {
        HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/configurations/client");
        if (acceptLanguage is not null)
        {
            request.Headers.Add("Accept-Language", acceptLanguage);
        }

        HttpResponseMessage response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        ConfigResponse? body = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        await Assert.That(body).IsNotNull();
        return body!;
    }

    internal record ConfigResponse(string AuthenticationUrl, CulturesResponse Cultures);

    internal record CulturesResponse(string DefaultCulture, string[] SupportedCultures);
}
