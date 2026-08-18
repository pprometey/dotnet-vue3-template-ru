using DotnetVue3TemplateRu.Core.Application.UserContext;

namespace DotnetVue3TemplateRu.Core.Infrastructure.UserContext;

/// <summary>
/// Контейнер идентичности на запрос. Заполняется один раз в composition root
/// (UserContextMiddleware) через Initialize; для анонимного запроса остаётся пустым.
/// </summary>
public sealed class RequestUserContext : IUserContext
{
    public bool IsAuthenticated { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public void Initialize(UserContextSnapshot snapshot)
    {
        UserId = snapshot.UserId;
        IsAuthenticated = true;
    }
}
