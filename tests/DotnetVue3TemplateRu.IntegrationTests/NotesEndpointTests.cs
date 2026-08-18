using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using VerifyTUnit;

namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Эталонный интеграционный тест: HTTP -> БД -> ответ, схема Arrange-Act-Assert.
///
/// Text локализован (translation-table, ADR 0025): создание принимает все локали
/// сразу (культура -> текст), чтение разрешает текст по культуре запроса
/// (Accept-Language) с фолбэком на инлайн-дефолт.
///
/// Два подхода к верификации ответа - разработчик выбирает нужный:
///
///   1. Fluent-ассерты (Assert.That) - проверяем конкретные поля явно.
///      Подходит когда важна конкретная пара значений, а не общая форма ответа.
///
///   2. Snapshot-тест (Verify) - сравниваем весь объект с saved snapshot.
///      Подходит для фиксации формы ответа (имена полей, структура JSON),
///      когда проверить каждое поле вручную избыточно.
///
/// [Before(Test)] сбрасывает данные через Respawn перед каждым тестом,
/// обеспечивая изоляцию от других тестовых классов.
/// </summary>
[ClassDataSource<DotnetVue3TemplateRuApiFactory>(Shared = SharedType.PerTestSession)]
[NotInParallel]
public class NotesEndpointTests(DotnetVue3TemplateRuApiFactory factory)
{
    [Before(Test)]
    public Task ResetAsync() => factory.ResetDatabaseAsync();

    // --- Подход 1: Fluent-ассерты ---

    [Test]
    public async Task CreateNote_WithValidData_Returns201_AndPersistsToDb()
    {
        // Arrange
        HttpClient client = factory.CreateClient();
        string text = new Faker().Lorem.Sentence();

        // Act: без Accept-Language используется дефолтная культура ru.
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/notes",
            new { texts = new Dictionary<string, string> { ["ru"] = text } });

        // Assert: HTTP-статус
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        // Assert: тело ответа - текст, разрешённый по дефолтной культуре
        NoteResponse? body = await response.Content.ReadFromJsonAsync<NoteResponse>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Text).IsEqualTo(text);
        await Assert.That(body.Id).IsNotEqualTo(Guid.Empty);

        // Assert: инлайн-дефолт реально появился в БД
        using IServiceScope scope = factory.Services.CreateScope();
        DotnetVue3TemplateRuDbContext db = scope.ServiceProvider.GetRequiredService<DotnetVue3TemplateRuDbContext>();
        Note? saved = await db.Notes.FindAsync(body.Id);
        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Text).IsEqualTo(text);
    }

    [Test]
    public async Task CreateNote_WithEmptyTexts_Returns400()
    {
        // Arrange
        HttpClient client = factory.CreateClient();

        // Act: пустой набор локалей
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/notes",
            new { texts = new Dictionary<string, string>() });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // --- Локализация: выбор перевода по культуре запроса ---

    [Test]
    public async Task GetNote_ResolvesTextByRequestCulture_WithFallbackToDefault()
    {
        // Arrange: заметка с переводами на обе поддерживаемые культуры.
        HttpClient client = factory.CreateClient();
        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/notes",
            new { texts = new Dictionary<string, string> { ["ru"] = "Заметка", ["kk"] = "Жазба" } });
        NoteResponse? note = await created.Content.ReadFromJsonAsync<NoteResponse>();

        // Act + Assert: культура запроса выбирает перевод.
        await Assert.That(await GetNoteText(client, note!.Id, "kk")).IsEqualTo("Жазба");
        await Assert.That(await GetNoteText(client, note.Id, "ru")).IsEqualTo("Заметка");

        // Неподдерживаемая культура - фолбэк на инлайн-дефолт ru.
        await Assert.That(await GetNoteText(client, note.Id, "en")).IsEqualTo("Заметка");
    }

    // --- Подход 2: Snapshot-тест ---

    [Test]
    public async Task CreateNote_ResponseShape_MatchesSnapshot()
    {
        // Фиксированный текст - случайный менялся бы при каждом запуске.
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync(
                "/api/v1/notes",
                new { texts = new Dictionary<string, string> { ["ru"] = "Snapshot test note" } });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        NoteResponse? content = await response.Content.ReadFromJsonAsync<NoteResponse>();

        // Id и CreatedAt нестабильны между запусками - скрабируем.
        // Text ("Snapshot test note") фиксирован и проверяется как есть.
        // При первом запуске тест упадёт и запишет *.received.txt.
        // Проверьте файл и примите: verify accept (dotnet tool install -g verify.tool).
        //
        // UseFileName задаёт короткое имя файла снапшота. Без него Verify.TUnit
        // добавляет в имя параметр конструктора из ClassDataSource (factory=...),
        // делая имя длинным и нечитаемым. Папку задаёт DerivePathInfo (VerifyConfig).
        await Verify(content)
            .ScrubMembers<NoteResponse>(_ => _.Id, _ => _.CreatedAt)
            .UseFileName("CreateNote_ResponseShape");
    }

    // --- Версия 2: тот же ресурс, расширенный контракт (TextLength) ---

    [Test]
    public async Task GetNoteV2_ReturnsExtendedContract_WithTextLength()
    {
        // Arrange: создаём заметку через v1.
        HttpClient client = factory.CreateClient();
        const string text = "Versioned note";
        HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/v1/notes",
            new { texts = new Dictionary<string, string> { ["ru"] = text } });
        NoteResponse? note = await created.Content.ReadFromJsonAsync<NoteResponse>();

        // Act: читаем её через v2.
        HttpResponseMessage response = await client.GetAsync($"/api/v2/notes/{note!.Id}");

        // Assert: v2 добавляет TextLength (длина разрешённого по культуре текста).
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        NoteV2Response? body = await response.Content.ReadFromJsonAsync<NoteV2Response>();
        await Assert.That(body).IsNotNull();
        await Assert.That(body!.Text).IsEqualTo(text);
        await Assert.That(body.TextLength).IsEqualTo(text.Length);
    }

    private async Task<string> GetNoteText(HttpClient client, Guid id, string acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notes/{id}");
        request.Headers.Add("Accept-Language", acceptLanguage);

        HttpResponseMessage response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        NoteResponse? body = await response.Content.ReadFromJsonAsync<NoteResponse>();
        return body!.Text;
    }

    internal record NoteResponse(Guid Id, string Text, DateTimeOffset CreatedAt);

    internal record NoteV2Response(Guid Id, string Text, DateTimeOffset CreatedAt, int TextLength);
}
