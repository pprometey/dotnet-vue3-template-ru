namespace DotnetVue3TemplateRu.Core.Application.Exceptions;

/// <summary>
/// Операцию нельзя выполнить, потому что смежный сервис не ответил или отказал, а без его ответа
/// продолжать нечем. Несёт стабильный код ошибки (ErrorCode из каталога ErrorCodes) и опциональные
/// параметры плейсхолдеров (Args); текст не хранится - его локализует по коду GlobalExceptionHandler
/// и отдаёт ответом 502 ProblemDetails (RFC 9457) с полем errorCode. См. ADR 0024.
///
/// Отличается от NotFoundException адресатом причины: там запрос корректен, но данных нет, здесь
/// запрос корректен, а внешняя система не дала выполнить его до конца - повтор имеет смысл.
/// </summary>
public class UpstreamUnavailableException : Exception
{
    public string ErrorCode { get; }

    // Параметры для плейсхолдеров сообщения ({0}, {1}, ...); пусто, если их нет.
    public IReadOnlyList<object?> Args { get; }

    public UpstreamUnavailableException(string errorCode, params object?[] args)
        : base(errorCode)
    {
        ErrorCode = errorCode;
        Args = args ?? [];
    }
}
