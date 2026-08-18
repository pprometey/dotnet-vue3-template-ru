using Asp.Versioning;
using DotnetVue3TemplateRu.Api.RateLimiting;
using DotnetVue3TemplateRu.Core.Application.Notes.Commands.CreateNote;
using DotnetVue3TemplateRu.Core.Application.Notes.Models;
using DotnetVue3TemplateRu.Core.Application.Notes.Queries.GetNote;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace DotnetVue3TemplateRu.Api.Controllers;

/// <summary>
/// Демо-эндпоинт шаблона. И запись, и чтение идут через Wolverine (CQRS):
/// команда/запрос -> handler в Application -> репозиторий -> EF Core. Типизированные
/// ответы (ActionResult&lt;T&gt; + ProducesResponseType) дают полные модели в OpenAPI
/// и сгенерированном Orval-клиенте.
///
/// Версионирование (демо стиля): один контроллер объявляет несколько версий
/// (<c>[ApiVersion]</c>); метод без <c>[MapToApiVersion]</c> обслуживает все
/// версии (см. <see cref="Create"/>), а изменившийся эндпоинт разносится по
/// версиям (<see cref="GetByIdV1"/> / <see cref="GetByIdV2"/>). Неизменившиеся
/// эндпоинты не дублируются.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public class NotesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public NotesController(IMessageBus bus) => _bus = bus;

    // Без [MapToApiVersion] - эндпоинт общий для всех версий контроллера (v1 и v2).
    // Валидацию (непустой текст, длина) делает FluentValidation-валидатор команды
    // через Wolverine-middleware внутри InvokeAsync; провал -> ValidationException
    // -> GlobalExceptionHandler -> 400 ValidationProblemDetails (со словарём errors).
    [HttpPost]
    [ProducesResponseType<NoteResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NoteResult>> Create(
        [FromBody] CreateNoteRequest request,
        CancellationToken ct)
    {
        NoteResult result = await _bus.InvokeAsync<NoteResult>(new CreateNoteCommand(request.Texts), ct);

        return CreatedAtAction(nameof(GetByIdV1), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<NoteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteResult>> GetByIdV1(Guid id, CancellationToken ct)
    {
        NoteResult result = await _bus.InvokeAsync<NoteResult>(new GetNoteQuery(id), ct);

        return Ok(result);
    }

    // Версия 2 того же эндпоинта: расширенный контракт (TextLength). Домен и
    // Application не меняем - v2-DTO собирается здесь (демо плумбинга версий).
    [HttpGet("{id:guid}")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType<NoteResultV2>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NoteResultV2>> GetByIdV2(Guid id, CancellationToken ct)
    {
        NoteResult result = await _bus.InvokeAsync<NoteResult>(new GetNoteQuery(id), ct);

        return Ok(new NoteResultV2(result.Id, result.Text, result.CreatedAt, result.Text.Length));
    }
}

// Text локализован: запрос несёт все локали сразу (культура -> текст, напр.
// { "ru-RU": "...", "kk-KZ": "..." }). См. ADR 0025.
public record CreateNoteRequest(IReadOnlyDictionary<string, string> Texts);

// Временный демо-DTO версии 2 (расширяет v1 полем TextLength).
public record NoteResultV2(Guid Id, string Text, DateTimeOffset CreatedAt, int TextLength);
