namespace DotnetVue3TemplateRu.Api.RateLimiting;

/// <summary>
/// Настройки политики <see cref="RateLimitPolicies.Public"/> из секции конфигурации
/// RateLimiting. Дефолты - на случай отсутствия секции.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Сколько запросов разрешено в пределах одного окна (на партицию/IP).</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Длина окна в секундах.</summary>
    public int WindowSeconds { get; set; } = 60;
}
