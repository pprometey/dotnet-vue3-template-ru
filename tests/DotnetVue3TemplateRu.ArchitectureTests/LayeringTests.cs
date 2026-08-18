using System.Reflection;
using DotnetVue3TemplateRu.Core.Application.Notes.Commands.CreateNote;
using DotnetVue3TemplateRu.Core.Domain.Notes.Models;
using DotnetVue3TemplateRu.Core.Infrastructure;

namespace DotnetVue3TemplateRu.ArchitectureTests;

// Правило зависимостей Clean Architecture, которое иначе осталось бы соглашением:
//   Api -> Application -> Domain, Infrastructure -> Application/Domain.
// См. docs/adr/0015-architecture-tests.md. Фронтенд обеспечивает симметричные
// границы через eslint-plugin-boundaries (ADR 0028); эти тесты делают то же для backend.
//
// Каждый новый модуль добавляет свой файл <Module>LayeringTests с теми же четырьмя
// правилами - общий базовый класс не заводится, чтобы список проверяемых сборок
// оставался виден в файле, а не собирался рефлексией.
public class LayeringTests
{
    private static readonly Assembly Domain = typeof(Note).Assembly;
    private static readonly Assembly Application = typeof(CreateNoteCommand).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Test]
    public async Task Domain_DoesNotDependOnOuterLayers()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotnetVue3TemplateRu.Core.Application",
                "DotnetVue3TemplateRu.Core.Infrastructure",
                "DotnetVue3TemplateRu.Api")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }

    [Test]
    public async Task Application_DoesNotDependOnInfrastructureOrApi()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "DotnetVue3TemplateRu.Core.Infrastructure",
                "DotnetVue3TemplateRu.Api")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }

    [Test]
    public async Task Application_DoesNotDependOnEfCore()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }

    // Application принимает System.Security.Claims.Claim из BCL, а не HttpContext,
    // именно затем, чтобы разбор идентичности проверялся юнит-тестом без поднятия
    // хоста (ADR 0023). Без этого правила зависимость на ASP.NET заползла бы туда
    // первым же удобным случаем и обесценила шов.
    [Test]
    public async Task Application_DoesNotDependOnAspNetCore()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }

    [Test]
    public async Task Infrastructure_DoesNotDependOnApi()
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("DotnetVue3TemplateRu.Api")
            .GetResult();

        await Assert.That(result.FailingTypeNames ?? []).IsEmpty();
    }
}
