using System.Runtime.CompilerServices;

namespace DotnetVue3TemplateRu.IntegrationTests;

public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Снапшоты складываем не в одну плоскую папку, а в подпапку на каждый
        // тестовый класс: Snapshots/<КлассТеста>/<Класс>.<Метод>.verified.txt.
        // Иначе при росте сьюты Snapshots/ превращается в простыню из сотен файлов.
        Verifier.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
            new PathInfo(
                directory: Path.Combine(projectDirectory, "Snapshots", type.Name),
                typeName: type.Name,
                methodName: method.Name));
    }
}
