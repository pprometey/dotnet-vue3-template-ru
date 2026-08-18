namespace DotnetVue3TemplateRu.Core.Application.UserContext.Queries.SessionContextGet;

/// <summary>
/// Читает уже заполненный scoped IUserContext (без БД - всё в токене) и проецирует
/// идентичность фронту.
/// </summary>
public static class SessionContextGetQueryHandler
{
    public static SessionContextGetResult Handle(SessionContextGetQuery query, IUserContext userContext)
        => new(userContext.UserId);
}
