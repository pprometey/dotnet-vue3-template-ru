namespace DotnetVue3TemplateRu.Api.Localization;

/// <summary>
/// Маркер-тип ресурса локализации текстов ошибок. Тексты лежат в
/// Resources/Localization/ErrorMessages.resx (нейтральная культура - ru) и
/// ErrorMessages.kk-KZ.resx; ключ ресурса = код ошибки (ErrorCodes). Резолв текста
/// по коду - через IStringLocalizer&lt;ErrorMessages&gt; на границе (см. ADR 0024).
/// </summary>
public sealed class ErrorMessages;
