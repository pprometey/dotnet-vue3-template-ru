using DotnetVue3TemplateRu.Core.Application.UserContext;
using DotnetVue3TemplateRu.Core.Infrastructure.UserContext;

namespace DotnetVue3TemplateRu.Api.Middlewares;

/// <summary>
/// Заполняет IUserContext из claims аутентифицированного запроса: прогоняет claims
/// через резолвер и кладёт снимок в HttpContext.Items. Сам по себе не защищает -
/// защиту дают UseAuthentication/UseAuthorization выше; для анонимных запросов это
/// no-op. Ошибки разбора пробрасываются в GlobalExceptionHandler (RFC 9457).
/// </summary>
public sealed class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next) => _next = next;

    // Ключ, под которым заполненный контекст лежит в HttpContext.Items. Через него его
    // читает фабрика IUserContext (см. Program.cs). Причина: Wolverine исполняет хендлеры
    // в отдельном DI-скоупе, а HttpContext (IHttpContextAccessor на AsyncLocal) общий на
    // весь запрос - так контекст доезжает до хендлера, а не теряется в чужом скоупе.
    public const string ItemKey = "DotnetVue3TemplateRu.UserContext";

    public Task InvokeAsync(HttpContext context, IUserContextResolver resolver)
    {
        if (context.User.Identity is { IsAuthenticated: true })
        {
            var userContext = new RequestUserContext();
            userContext.Initialize(resolver.Resolve(context.User.Claims));
            context.Items[ItemKey] = userContext;
        }

        return _next(context);
    }
}
