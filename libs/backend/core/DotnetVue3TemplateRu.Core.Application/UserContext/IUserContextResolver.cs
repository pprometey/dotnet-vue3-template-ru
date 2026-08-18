using System.Security.Claims;

namespace DotnetVue3TemplateRu.Core.Application.UserContext;

/// <summary>
/// Разбор claims токена в снимок идентичности. Точка подмены под конкретного
/// провайдера: даже минимальная идентичность выглядит у провайдеров по-разному -
/// почта приезжает то в "email", то в "preferred_username", то в claim с собственным
/// префиксом (см. ADR 0023).
///
/// Принимает System.Security.Claims.Claim (BCL), чтобы Application не зависел
/// от ASP.NET, а разбор проверялся юнит-тестом без поднятия хоста. Правило
/// охраняется архитектурным тестом.
/// </summary>
public interface IUserContextResolver
{
    UserContextSnapshot Resolve(IEnumerable<Claim> claims);
}
