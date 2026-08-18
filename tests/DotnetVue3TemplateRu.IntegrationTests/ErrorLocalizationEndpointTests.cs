namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Локализация ошибок валидации на границе: домен и валидаторы отдают только код,
/// GlobalExceptionHandler резолвит текст по коду на текущей культуре запроса
/// (Accept-Language) и кладёт errorCode(s) в ProblemDetails. Проверяется через
/// POST /api/v1/notes (валидатор бракует пустой/слишком длинный текст). См. ADR 0024.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class ErrorLocalizationEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    [Test]
    public async Task EmptyText_DefaultCulture_ReturnsRussianMessageAndCode()
    {
        ValidationProblem body = await PostNote(text: "", acceptLanguage: null);

        await Assert.That(body.Errors!.Values.SelectMany(v => v)).IsEquivalentTo(["Текст заметки обязателен."]);
        await Assert.That(body.ErrorCodes!.Values.SelectMany(v => v)).IsEquivalentTo(["note.text.required"]);
    }

    [Test]
    public async Task EmptyText_KazakhCulture_ReturnsKazakhMessageAndCode()
    {
        ValidationProblem body = await PostNote(text: "", acceptLanguage: "kk");

        await Assert.That(body.Errors!.Values.SelectMany(v => v)).IsEquivalentTo(["Жазба мәтіні міндетті."]);
        await Assert.That(body.ErrorCodes!.Values.SelectMany(v => v)).IsEquivalentTo(["note.text.required"]);
    }

    [Test]
    public async Task TranslationWithoutDefaultCulture_ReturnsFieldError()
    {
        // Набор без значения дефолтной культуры бракует валидатор, а не домен.
        // Разница видна клиенту: валидатор отдаёт словарь errors, по которому форма
        // показывает сообщение, а доменный отказ - только detail, и форма молчит.
        HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notes")
        {
            Content = JsonContent.Create(new
            {
                texts = new Dictionary<string, string> { ["en"] = "Note" },
            }),
        };

        HttpResponseMessage response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        ValidationProblem? body = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Errors!.Values.SelectMany(v => v)).IsEquivalentTo(["Текст заметки обязателен."]);
        await Assert.That(body.ErrorCodes!.Values.SelectMany(v => v)).IsEquivalentTo(["note.text.required"]);
    }

    [Test]
    public async Task TooLongText_KazakhCulture_ResolvesPlaceholderFromValidator()
    {
        ValidationProblem body = await PostNote(text: new string('a', 1001), acceptLanguage: "kk");

        await Assert.That(body.Errors!.Values.SelectMany(v => v)).IsEquivalentTo(["Жазба мәтіні 1000 таңбадан аспауы тиіс."]);
        await Assert.That(body.ErrorCodes!.Values.SelectMany(v => v)).IsEquivalentTo(["note.text.too_long"]);
    }

    [Test]
    public async Task NotFound_DefaultCulture_ReturnsRussianMessageWithIdAndCode()
    {
        var noteId = Guid.NewGuid();

        ProblemWithCode problem = await GetMissingNote(noteId, acceptLanguage: null);

        await Assert.That(problem.Detail).IsEqualTo($"Заметка '{noteId}' не найдена.");
        await Assert.That(problem.ErrorCode).IsEqualTo("note.not_found");
    }

    [Test]
    public async Task NotFound_KazakhCulture_ReturnsKazakhMessageWithIdAndCode()
    {
        var noteId = Guid.NewGuid();

        ProblemWithCode problem = await GetMissingNote(noteId, acceptLanguage: "kk");

        await Assert.That(problem.Detail).IsEqualTo($"'{noteId}' жазбасы табылмады.");
        await Assert.That(problem.ErrorCode).IsEqualTo("note.not_found");
    }

    private async Task<ProblemWithCode> GetMissingNote(Guid noteId, string? acceptLanguage)
    {
        HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notes/{noteId}");
        if (acceptLanguage is not null)
        {
            request.Headers.Add("Accept-Language", acceptLanguage);
        }

        HttpResponseMessage response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        ProblemWithCode? body = await response.Content.ReadFromJsonAsync<ProblemWithCode>();
        await Assert.That(body).IsNotNull();
        return body!;
    }

    private async Task<ValidationProblem> PostNote(string text, string? acceptLanguage)
    {
        HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notes")
        {
            // Один перевод на дефолтной культуре - валидатор бракует его по коду,
            // текст резолвится на границе по культуре запроса (Accept-Language).
            Content = JsonContent.Create(new { texts = new Dictionary<string, string> { ["ru"] = text } }),
        };
        if (acceptLanguage is not null)
        {
            request.Headers.Add("Accept-Language", acceptLanguage);
        }

        HttpResponseMessage response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        ValidationProblem? body = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        await Assert.That(body).IsNotNull();
        return body!;
    }

    private sealed record ValidationProblem(
        Dictionary<string, string[]>? Errors,
        Dictionary<string, string[]>? ErrorCodes);

    private sealed record ProblemWithCode(int? Status, string? Detail, string? ErrorCode);
}
