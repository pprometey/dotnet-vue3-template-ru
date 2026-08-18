using System.Security.Claims;
using DotnetVue3TemplateRu.Core.Application.UserContext;

namespace DotnetVue3TemplateRu.Core.Infrastructure.UserContext;

/// <summary>
/// Разбор стандартного claim OIDC "sub" как идентификатора пользователя. Это
/// регистрируемый IANA claim, его выдают все распространённые провайдеры, поэтому
/// реализация не привязана ни к одному из них (ADR 0023).
///
/// Требует MapInboundClaims = false на схеме JWT. Иначе ASP.NET переименует "sub"
/// в длинный URI ClaimTypes.NameIdentifier, и разбор молча вернёт пустой UserId -
/// запрос отработает с кодом 200 и пустой идентичностью вместо ошибки. Запасное
/// чтение ClaimTypes.NameIdentifier оставлено на случай, если маппинг всё же
/// включат: пустая идентичность - худший из отказов, она не видна в ответе.
/// </summary>
public sealed class StandardClaimsUserContextResolver : IUserContextResolver
{
    public UserContextSnapshot Resolve(IEnumerable<Claim> claims)
    {
        IReadOnlyCollection<Claim> list = claims as IReadOnlyCollection<Claim> ?? [.. claims];

        string userId = Find(list, "sub") ?? Find(list, ClaimTypes.NameIdentifier) ?? string.Empty;

        return new UserContextSnapshot(userId);
    }

    private static string? Find(IEnumerable<Claim> claims, string type)
    {
        string? value = claims.FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.Ordinal))?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
