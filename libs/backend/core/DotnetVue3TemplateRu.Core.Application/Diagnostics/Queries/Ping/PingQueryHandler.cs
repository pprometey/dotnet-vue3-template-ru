namespace DotnetVue3TemplateRu.Core.Application.Diagnostics.Queries.Ping;

/// <summary>
/// Демонстрация Wolverine (CQRS) end-to-end. Чистый handler без внешних
/// зависимостей - обнаруживается по соглашению (*Handler с методом Handle).
///
/// Примечание: handler-ы, пишущие в БД через scoped-сервисы (репозитории,
/// DbContext), требуют пакета WolverineFx.EntityFrameworkCore либо включения
/// service-location в codegen.
/// </summary>
public static class PingQueryHandler
{
    public static PongResult Handle(PingQuery query) => new("ok", DateTimeOffset.UtcNow);
}
