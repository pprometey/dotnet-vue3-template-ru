namespace DotnetVue3TemplateRu.Core.Application.Configuration.Queries.ConfigurationGet;

public record ConfigurationGetResult(CulturesResult Cultures);

public record CulturesResult(string DefaultCulture, string[] SupportedCultures);
