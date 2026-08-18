using System.Globalization;
using Microsoft.Extensions.Options;

namespace DotnetVue3TemplateRu.Core.Application.Configuration.Queries.ConfigurationGet;

/// <summary>
/// Отдаёт фронту список культур интерфейса. Эндпоинт держится потому, что список
/// культур обязан совпадать у RequestLocalizationOptions, resx-ресурсов и SPA;
/// дублирование его в SPA гарантировало бы расхождение.
/// DefaultCulture берётся из культуры текущего запроса.
/// </summary>
public static class ConfigurationGetQueryHandler
{
    public static ConfigurationGetResult Handle(ConfigurationGetQuery query, IOptions<CultureOptions> options)
        => new(new CulturesResult(
            CultureInfo.CurrentCulture.Name,
            [.. options.Value.SupportedCultures.Select(c => c.Culture)]));
}
