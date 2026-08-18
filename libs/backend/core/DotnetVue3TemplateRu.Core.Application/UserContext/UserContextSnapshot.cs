namespace DotnetVue3TemplateRu.Core.Application.UserContext;

/// <summary>
/// Чистый результат разбора токена: набор полей, которыми заполняется IUserContext.
/// Возвращается IUserContextResolver - границей между разбором claims конкретного
/// провайдера и общим контейнером контекста (см. ADR 0023).
/// </summary>
public sealed record UserContextSnapshot(string UserId);
