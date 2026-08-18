namespace DotnetVue3TemplateRu.Core.Domain.Errors;

/// <summary>
/// Каталог кодов ошибок модуля Core - стабильные идентификаторы ошибок как
/// C#-константы. На них ссылаются и доменные инварианты (DomainException), и
/// валидаторы FluentValidation (WithErrorCode); текст сообщения по коду резолвится
/// на границе из ресурсов (см. ADR 0018). Namespaced-нотация "модуль.поле.правило".
/// Каждый модуль объявляет свой ErrorCodes в собственном Domain-слое. Тест-страж
/// полноты проверяет, что у каждого кода есть перевод на каждую поддерживаемую культуру.
/// </summary>
public static class ErrorCodes
{
    // Сквозные коды: локализуют ответы, не привязанные к сущности модуля - конфликт
    // оптимистичной блокировки (409), непредвиденная ошибка (500), прочий 400 и отказ
    // ограничителя частоты (429).
    public static class Common
    {
        public const string BadRequest = "common.bad_request";
        public const string ConcurrencyConflict = "common.concurrency_conflict";
        public const string RateLimitExceeded = "common.rate_limit_exceeded";
        public const string UnexpectedError = "common.unexpected_error";
    }

    public static class Note
    {
        public const string TextRequired = "note.text.required";
        public const string TextTooLong = "note.text.too_long";
        public const string NotFound = "note.not_found";
    }
}
