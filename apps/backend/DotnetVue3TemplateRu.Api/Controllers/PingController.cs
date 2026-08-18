using Asp.Versioning;
using DotnetVue3TemplateRu.Api.RateLimiting;
using DotnetVue3TemplateRu.Core.Application.Diagnostics.Queries.Ping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace DotnetVue3TemplateRu.Api.Controllers;

/// <summary>
/// Демонстрация Wolverine (CQRS): HTTP -> IMessageBus -> handler -> ответ.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.Public)]
public class PingController : ControllerBase
{
    private readonly IMessageBus _bus;

    public PingController(IMessageBus bus) => _bus = bus;

    [HttpGet]
    [ProducesResponseType<PongResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PongResult>> Get(CancellationToken ct)
    {
        PongResult pong = await _bus.InvokeAsync<PongResult>(new PingQuery(), ct);
        return Ok(pong);
    }
}
