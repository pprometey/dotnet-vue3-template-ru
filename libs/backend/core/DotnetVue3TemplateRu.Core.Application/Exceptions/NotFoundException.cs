namespace DotnetVue3TemplateRu.Core.Application.Exceptions;

/// <summary>
/// Запрошенная сущность не найдена. Несёт стабильный код ошибки (ErrorCode из каталога ErrorCodes)
/// и опциональные параметры плейсхолдеров (Args); текст не хранится. Локализация по коду выполняется
/// в одной точке на границе - GlobalExceptionHandler по текущей культуре запроса преобразует это в
/// ответ 404 ProblemDetails (RFC 9457) с локализованным Detail и полем errorCode. Так Application
/// остаётся без зависимости от локализации. См. ADR 0024.
/// </summary>
public class NotFoundException : Exception
{
    public string ErrorCode { get; }

    // Параметры для плейсхолдеров сообщения ({0}, {1}, ...); пусто, если их нет.
    public IReadOnlyList<object?> Args { get; }

    public NotFoundException(string errorCode, params object?[] args)
        : base(errorCode)
    {
        ErrorCode = errorCode;
        Args = args ?? [];
    }
}
