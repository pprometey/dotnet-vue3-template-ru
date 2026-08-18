using Asp.Versioning;
using DotnetVue3TemplateRu.Api.RateLimiting;
using DotnetVue3TemplateRu.Core.Application.Configuration.Queries.ConfigurationGet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace DotnetVue3TemplateRu.Api.Controllers;

/// <summary>
/// Отдаёт фронту список культур интерфейса. Эндпоинт анонимный: язык нужен до входа,
/// на экране, который видит неаутентифицированный пользователь.
///
/// Список живёт на бэкенде, а не дублируется в SPA, потому что он обязан совпадать
/// у RequestLocalizationOptions, resx-ресурсов и интерфейса: две копии разъедутся
/// на первом же добавленном языке.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ConfigurationsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public ConfigurationsController(IMessageBus bus) => _bus = bus;

    [HttpGet("client")]
    [EnableRateLimiting(RateLimitPolicies.Public)]
    [ProducesResponseType<ConfigurationGetResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigurationGetResult>> Get(CancellationToken ct)
        => Ok(await _bus.InvokeAsync<ConfigurationGetResult>(new ConfigurationGetQuery(), ct));
}
