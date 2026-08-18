namespace DotnetVue3TemplateRu.Core.Application.Configuration;

/// <summary>
/// Культуры интерфейса. DefaultCulture отдаётся фронту (язык по умолчанию),
/// SupportedCultures - список доступных культур.
/// </summary>
public sealed class CultureOptions
{
    public string DefaultCulture { get; set; } = null!;

    public SupportedCultureOptions[] SupportedCultures { get; set; } = [];
}

/// <summary>
/// Одна поддерживаемая культура. Обёртка вокруг одного поля, а не голая строка,
/// чтобы список в конфигурации можно было дополнить названием языка, не ломая биндинг.
/// </summary>
public sealed class SupportedCultureOptions
{
    public string Culture { get; set; } = null!;
}
