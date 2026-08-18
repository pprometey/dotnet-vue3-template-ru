namespace DotnetVue3TemplateRu.Core.Application.UserContext.Queries.SessionContextGet;

// Проекция идентичности фронту (требует авторизации). Контекст заполняется из токена
// в composition root; здесь отдаётся то, что уже лежит в токене, - без обращения к БД.
public record SessionContextGetQuery;
