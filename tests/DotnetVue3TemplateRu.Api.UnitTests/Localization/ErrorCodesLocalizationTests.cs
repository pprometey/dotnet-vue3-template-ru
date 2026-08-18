using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using DotnetVue3TemplateRu.Api.Localization;
using DotnetVue3TemplateRu.Core.Domain.Errors;

namespace DotnetVue3TemplateRu.Api.UnitTests.Localization;

/// <summary>
/// Страж полноты каталога ошибок ("один список"): каждый код из ErrorCodes имеет
/// текст в ресурсах на каждой поддерживаемой культуре. Нейтральная культура несёт
/// русский текст, плюс явные en и kk. Проверка идёт по конкретной культуре без
/// фолбэка (tryParents: false), поэтому пропущенный перевод не маскируется откатом
/// к нейтральному. См. ADR 0018.
///
/// Список каталогов растёт вместе с модулями: каждый новый Domain добавляет сюда
/// свой ErrorCodes.
/// </summary>
public class ErrorCodesLocalizationTests
{
    private const string BaseName = "DotnetVue3TemplateRu.Api.Resources.Localization.ErrorMessages";

    [Test]
    public async Task EveryErrorCode_HasText_InEverySupportedCulture()
    {
        string[] codes = AllErrorCodes();
        // Non-vacuous: каталог не пуст и содержит известные коды.
        await Assert.That(codes).Contains(ErrorCodes.Note.TextRequired);
        await Assert.That(codes).Contains(ErrorCodes.Note.TextTooLong);
        await Assert.That(codes).Contains(ErrorCodes.Common.UnexpectedError);

        var manager = new ResourceManager(BaseName, typeof(ErrorMessages).Assembly);

        // Ключи каждой культуры собираются РОВНО ОДИН раз. ResourceManager кэширует
        // набор ресурсов, а KeysFor его закрывает: повторный вызов для той же культуры
        // получил бы уже закрытый набор и упал ObjectDisposedException.
        HashSet<string> neutralKeys = KeysFor(manager, CultureInfo.InvariantCulture);
        HashSet<string> englishKeys = KeysFor(manager, new CultureInfo("en"));
        HashSet<string> kazakhKeys = KeysFor(manager, new CultureInfo("kk"));

        string[] missingNeutral = codes.Where(c => !neutralKeys.Contains(c)).ToArray();
        string[] missingEnglish = codes.Where(c => !englishKeys.Contains(c)).ToArray();
        string[] missingKazakh = codes.Where(c => !kazakhKeys.Contains(c)).ToArray();

        await Assert.That(missingNeutral).IsEmpty();
        await Assert.That(missingEnglish).IsEmpty();
        await Assert.That(missingKazakh).IsEmpty();
    }

    // Каталоги кодов каждого модуля (по одному ErrorCodes на модуль в его Domain-слое).
    private static string[] AllErrorCodes() => [.. CodesFrom(typeof(ErrorCodes))];

    private static IEnumerable<string> CodesFrom(Type catalog) =>
        catalog.GetNestedTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    private static HashSet<string> KeysFor(ResourceManager manager, CultureInfo culture)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        using ResourceSet? set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        if (set is null)
        {
            return keys;
        }

        foreach (DictionaryEntry entry in set)
        {
            keys.Add((string)entry.Key);
        }

        return keys;
    }
}
