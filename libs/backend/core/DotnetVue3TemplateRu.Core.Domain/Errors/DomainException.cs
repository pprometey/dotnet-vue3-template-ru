namespace DotnetVue3TemplateRu.Core.Domain.Errors;

/// <summary>
/// Нарушение доменного инварианта. Несёт стабильный код ошибки (ErrorCode из
/// каталога ErrorCodes) и опциональные параметры сообщения (Args) для плейсхолдеров.
/// Текст не хранится: локализация по коду выполняется в одной точке на границе
/// (GlobalExceptionHandler по текущей культуре запроса). Так домен остаётся без
/// зависимости от локализации. См. ADR 0024.
/// </summary>
public class DomainException : Exception
{
    public string ErrorCode { get; }

    // Параметры для плейсхолдеров сообщения ({0}, {1}, ...); пусто, если их нет.
    public IReadOnlyList<object?> Args { get; }

    public DomainException(string errorCode, params object?[] args)
        : base(errorCode)
    {
        ErrorCode = errorCode;
        Args = args ?? [];
    }
}
