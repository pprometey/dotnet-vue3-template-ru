using Asp.Versioning;
using DotnetVue3TemplateRu.Core.Application.UserContext.Queries.SessionContextGet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace DotnetVue3TemplateRu.Api.Controllers;

/// <summary>
/// Проекция пользовательского контекста фронту: идентификатор пользователя (claim "sub").
/// Контекст заполняет UserContextMiddleware из claims токена (см. ADR 0023), поэтому
/// обращений в БД здесь нет - всё уже в токене. Прав контекст не несёт: их решает
/// предметный модуль. Требует авторизации.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/session-context")]
[Produces("application/json")]
[Authorize]
public class SessionContextController : ControllerBase
{
    private readonly IMessageBus _bus;

    public SessionContextController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpGet]
    [ProducesResponseType<SessionContextGetResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionContextGetResult>> Get(CancellationToken ct)
        => Ok(await _bus.InvokeAsync<SessionContextGetResult>(new SessionContextGetQuery(), ct));
}
