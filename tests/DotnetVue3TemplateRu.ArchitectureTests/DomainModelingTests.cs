using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotnetVue3TemplateRu.Core.Domain.SeedWork;
using CoreNote = DotnetVue3TemplateRu.Core.Domain.Notes.Models.Note;

namespace DotnetVue3TemplateRu.ArchitectureTests;

/// <summary>
/// Правила тактического DDD (SeedWork, ADR 0008). Сущность - класс с равенством по идентичности
/// (наследник Entity); value object - record со значимым равенством. Сущность не объявляется
/// record'ом: структурное равенство сломало бы идентичность и трекинг EF Core.
///
/// Список сборок растёт вместе с модулями: каждый новый Domain добавляет сюда свой якорный тип.
/// </summary>
public class DomainModelingTests
{
    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(CoreNote).Assembly,
    ];

    [Test]
    public async Task Entities_AreNotRecords()
    {
        var recordEntities = DomainAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(Entity).IsAssignableFrom(type) && !type.IsAbstract && IsRecord(type))
            .Select(type => type.FullName)
            .ToList();

        await Assert.That(recordEntities).IsEmpty();
    }

    // record (class или struct) компилятор помечает синтетическим методом-клоном "<Clone>$".
    [SuppressMessage(
        "Security Hotspot",
        "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "Метод-клон компилятор объявляет непубличным, и обращение к нему здесь - "
            + "сам предмет проверки: иначе record не отличить от класса. Тест ничего не вызывает "
            + "и не меняет, только читает наличие члена.")]
    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null;
}
