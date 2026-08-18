using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace DotnetVue3TemplateRu.IntegrationTests;

/// <summary>
/// Выпуск токенов для интеграционных тестов. Подпись RS256 тестовым ключом -
/// той же асимметричной схемой, что и в бою; симметричного секрета в проекте нет
/// нигде (ADR 0023).
///
/// Идентичность кладётся в claim "sub" - именно его читает резолвер. Здесь это
/// важнее, чем кажется: в бою то же имя сохраняется только потому, что на схеме
/// JWT выключен MapInboundClaims.
/// </summary>
public static class TestTokens
{
    /// <summary>Фиктивный издатель: ходить по этому адресу никто не будет.</summary>
    public const string TestIssuer = "https://tests.local/oidc";

    /// <summary>Индикатор ресурса API - то же значение, что в Jwt:Audience.</summary>
    public const string TestAudience = "https://api.dotnet-vue3-template-ru.local";

    public static string Issue(SecurityKey signingKey, string subject)
    {
        var claims = new List<Claim> { new("sub", subject) };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
