namespace DotnetVue3TemplateRu.Api.RateLimiting;

/// <summary>
/// Имена политик rate limiting. Константа делит имя между регистрацией
/// (Program.cs) и атрибутами <c>[EnableRateLimiting]</c> на контроллерах,
/// чтобы не плодить magic string.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Защита публичных эндпоинтов: fixed window, партиционирование по IP клиента.
    /// </summary>
    public const string Public = "public";
}
