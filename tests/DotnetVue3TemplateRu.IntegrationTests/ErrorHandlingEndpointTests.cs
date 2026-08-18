namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Проверяет единый формат ошибок (RFC 9457) и CORS-политику.
///
/// Ошибки: необработанные исключения мапятся GlobalExceptionHandler в ответ
/// ProblemDetails - NotFoundException -> 404, доменный ArgumentException -> 400.
///
/// CORS: политика SpaCors разрешает origin из конфигурации
/// (appsettings.Development.json -> http://localhost:5173), чужой origin не
/// получает заголовок Access-Control-Allow-Origin.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class ErrorHandlingEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    private const string AllowedOrigin = "http://localhost:5173";

    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task GetNote_WhenMissing_Returns404_AsProblemDetails()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/notes/{Guid.NewGuid()}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        ProblemResponse? problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(404);
        await Assert.That(problem.Title).IsNotNull();
    }

    [Test]
    public async Task CreateNote_WithEmptyText_Returns400_AsProblemDetails()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/notes", new { texts = new Dictionary<string, string>() });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        ProblemResponse? problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(400);
        await Assert.That(problem.Title).IsNotNull();
    }

    [Test]
    public async Task CreateNote_WithEmptyText_ReturnsValidationErrors_ForTextField()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/notes", new { texts = new Dictionary<string, string>() });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        // FluentValidation -> GlobalExceptionHandler -> ValidationProblemDetails:
        // ошибки приходят словарём errors, сгруппированным по свойству.
        ValidationProblemResponse? problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Errors).IsNotNull();
        await Assert.That(problem.Errors!.ContainsKey("Texts")).IsTrue();
    }

    [Test]
    public async Task Preflight_FromAllowedOrigin_ReturnsAllowOriginHeader()
    {
        HttpClient client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/notes");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        HttpResponseMessage response = await client.SendAsync(request);

        await Assert.That(response.Headers.Contains("Access-Control-Allow-Origin")).IsTrue();
        await Assert.That(response.Headers.GetValues("Access-Control-Allow-Origin"))
            .Contains(AllowedOrigin);
    }

    [Test]
    public async Task Preflight_FromDisallowedOrigin_HasNoAllowOriginHeader()
    {
        HttpClient client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/notes");
        request.Headers.Add("Origin", "http://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        HttpResponseMessage response = await client.SendAsync(request);

        await Assert.That(response.Headers.Contains("Access-Control-Allow-Origin")).IsFalse();
    }

    private sealed record ProblemResponse(string? Type, string? Title, int? Status, string? Detail);

    private sealed record ValidationProblemResponse(int? Status, Dictionary<string, string[]>? Errors);
}
