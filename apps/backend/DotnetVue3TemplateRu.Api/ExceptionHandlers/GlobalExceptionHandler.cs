using System.Diagnostics;
using System.Globalization;
using DotnetVue3TemplateRu.Api.Localization;
using DotnetVue3TemplateRu.Core.Application.Exceptions;
using DotnetVue3TemplateRu.Core.Domain.Errors;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace DotnetVue3TemplateRu.Api.ExceptionHandlers;

/// <summary>
/// Единый обработчик необработанных исключений. Преобразует исключение в ответ
/// в формате ProblemDetails (RFC 9457), чтобы фронтенд-клиент (генерируется
/// Orval из OpenAPI) разбирал ошибки единообразно.
///
/// Маппинг известных исключений в статус-код:
///   ValidationException (FluentValidation) -> 400 со словарём errors
///       (ValidationProblemDetails) - провал валидаторов команд Wolverine;
///   DomainException    -> 400 (нарушение доменного инварианта);
///   NotFoundException  -> 404;
///   ArgumentException  -> 400 (прочие доменные/BCL-инварианты);
///   UpstreamUnavailableException -> 502 (смежный сервис не дал выполнить операцию);
///   всё остальное      -> 500.
///
/// Тексты доменных ошибок и ошибок валидации локализуются здесь по коду ошибки
/// (ErrorCode) через IStringLocalizer на текущей культуре запроса - домен и
/// валидаторы отдают только код (+параметры), не текст. В ответ кладётся errorCode
/// (для валидации - словарь errorCodes по полям), чтобы клиент ветвился по коду.
/// См. ADR 0024. Детали 5xx скрываются в Production (только общий текст).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _logger = logger;
        _localizer = localizer;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Валидация: отдаём 400 со словарём errors (ValidationProblemDetails),
        // а не только detail - фронт разбирает ошибки по полям.
        if (exception is ValidationException validationException)
        {
            return await WriteValidationProblemAsync(httpContext, validationException);
        }

        // Доменный инвариант: 400 с локализованным текстом по коду + errorCode.
        if (exception is DomainException domainException)
        {
            return await WriteDomainProblemAsync(httpContext, domainException);
        }

        // Сущность не найдена: 404 с локализованным по коду Detail.
        if (exception is NotFoundException notFoundException)
        {
            return await WriteNotFoundProblemAsync(httpContext, notFoundException);
        }

        // Оптимистичная блокировка: строку изменили между чтением и записью (rowversion).
        if (exception is DbUpdateConcurrencyException)
        {
            return await WriteConcurrencyProblemAsync(httpContext, exception);
        }

        // Смежный сервис не дал выполнить операцию: 502 с локализованным по коду Detail.
        if (exception is UpstreamUnavailableException upstreamException)
        {
            return await WriteUpstreamProblemAsync(httpContext, upstreamException);
        }

        (int status, string? title) = Map(exception);
        bool isServerError = status >= StatusCodes.Status500InternalServerError;

        // 4xx - ожидаемые ошибки клиента, не шумим в логах; 5xx логируем как Error.
        if (isServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        // Клиенту не уходит английский технический текст: в проде - обобщённое локализованное
        // сообщение по коду; в dev - исходный текст исключения для диагностики.
        string code = isServerError ? ErrorCodes.Common.UnexpectedError : ErrorCodes.Common.BadRequest;
        string detail = _environment.IsDevelopment() ? exception.Message : ResolveMessage(code, []);

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
                    ["errorCode"] = code,
                },
            },
        });
    }

    private async ValueTask<bool> WriteValidationProblemAsync(
        HttpContext httpContext,
        ValidationException exception)
    {
        IGrouping<string, ValidationFailure>[] byProperty = exception.Errors.GroupBy(e => e.PropertyName).ToArray();
        Dictionary<string, string[]> errors = byProperty.ToDictionary(
            g => g.Key,
            g => g.Select(ResolveValidationMessage).ToArray());
        Dictionary<string, string[]> errorCodes = byProperty.ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorCode).Where(c => !string.IsNullOrEmpty(c)).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
                    ["errorCodes"] = errorCodes,
                },
            },
        });
    }

    private async ValueTask<bool> WriteDomainProblemAsync(
        HttpContext httpContext,
        DomainException exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = ResolveDomainMessage(exception),
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
                    ["errorCode"] = exception.ErrorCode,
                },
            },
        });
    }

    private async ValueTask<bool> WriteNotFoundProblemAsync(HttpContext httpContext, NotFoundException exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = ResolveMessage(exception.ErrorCode, exception.Args),
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
                    ["errorCode"] = exception.ErrorCode,
                },
            },
        });
    }

    private async ValueTask<bool> WriteConcurrencyProblemAsync(HttpContext httpContext, Exception exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ResolveMessage(ErrorCodes.Common.ConcurrencyConflict, []),
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.Common.ConcurrencyConflict,
                },
            },
        });
    }

    private async ValueTask<bool> WriteUpstreamProblemAsync(
        HttpContext httpContext,
        UpstreamUnavailableException exception)
    {
        // Ответ 5xx, поэтому логируем как остальные 5xx: причину отказа пишет то место, которое
        // общалось со смежным сервисом, а здесь остаётся привязка к запросу.
        _logger.LogError(exception, "Upstream service unavailable ({ErrorCode})", exception.ErrorCode);

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Upstream service unavailable",
                Detail = ResolveMessage(exception.ErrorCode, exception.Args),
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier,
                    ["errorCode"] = exception.ErrorCode,
                },
            },
        });
    }

    // Текст ошибки валидации по её коду (WithErrorCode); плейсхолдеры {Name}
    // подставляются из значений FluentValidation. Фолбэк на текст правила, если
    // кода нет или для него нет ресурса.
    private string ResolveValidationMessage(ValidationFailure failure)
    {
        if (string.IsNullOrEmpty(failure.ErrorCode))
        {
            return failure.ErrorMessage;
        }

        LocalizedString localized = _localizer[failure.ErrorCode];
        if (localized.ResourceNotFound)
        {
            return failure.ErrorMessage;
        }

        return ApplyPlaceholders(localized.Value, failure.FormattedMessagePlaceholderValues);
    }

    // Текст доменной ошибки по её коду; позиционные параметры ({0}, {1}, ...) - из DomainException.Args.
    private string ResolveDomainMessage(DomainException exception) =>
        ResolveMessage(exception.ErrorCode, exception.Args);

    // Локализует сообщение по коду ошибки на текущей культуре запроса; позиционные параметры
    // ({0}, {1}, ...) подставляются из args. Фолбэк на сам код, если ресурса нет.
    private string ResolveMessage(string code, IReadOnlyList<object?> args)
    {
        LocalizedString localized = _localizer[code];
        string text = localized.ResourceNotFound ? code : localized.Value;

        return args.Count > 0
            ? string.Format(CultureInfo.CurrentCulture, text, args.ToArray())
            : text;
    }

    private static string ApplyPlaceholders(string template, IDictionary<string, object>? values)
    {
        if (values is null)
        {
            return template;
        }

        foreach ((string? key, object? value) in values)
        {
            template = template.Replace(
                $"{{{key}}}",
                Convert.ToString(value, CultureInfo.CurrentCulture),
                StringComparison.Ordinal);
        }

        return template;
    }

    private static (int Status, string Title) Map(Exception exception) => exception switch
    {
        ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
    };
}
