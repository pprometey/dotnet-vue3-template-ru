using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace DotnetVue3TemplateRu.Api.Startup;

/// <summary>
/// JWT Bearer как resource server: приложение проверяет предъявленный токен и не
/// выпускает свои. Открытые ключи приезжают по JWKS из discovery-документа провайдера
/// и обновляются сами при ротации, поэтому симметричного секрета в конфигурации нет
/// ни в одной среде (ADR 0023).
/// </summary>
public static class AuthenticationExtensions
{
    /// <param name="isOpenApiExport">
    /// Признак build-time экспорта OpenAPI: он поднимает хост без внешних зависимостей,
    /// поэтому требовать настроенного провайдера там нельзя - документ должен собираться
    /// одинаково в любой среде сборки. Вычисляется в Program.cs тем же способом, что и
    /// профиль обмена, чтобы условие было одно на всё приложение.
    /// </param>
    public static WebApplicationBuilder AddJwtAuthentication(
        this WebApplicationBuilder builder,
        bool isOpenApiExport)
    {
        string? authority = builder.Configuration["Jwt:Authority"];
        string? audience = builder.Configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(authority) && !isOpenApiExport)
        {
            throw new InvalidOperationException(
                "Не задан 'Jwt:Authority' - адрес провайдера идентичности (OIDC). " +
                "Локально его подставляет Aspire из ресурса Logto; на стенде задайте переменной окружения.");
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;

                // Локальный провайдер поднимается по http, поэтому требовать https
                // от метаданных можно только когда адрес не локальный.
                options.RequireHttpsMetadata =
                    !string.IsNullOrWhiteSpace(authority)
                    && !authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

                // Без этого ASP.NET переименует стандартные claims в длинные URI, и "sub"
                // станет ClaimTypes.NameIdentifier. Резолвер, ищущий claim "sub", тогда
                // молча вернёт пустой UserId: запрос отработает с кодом 200 и пустой
                // идентичностью вместо ошибки (ADR 0023).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles",
                };
            });

        return builder;
    }
}
