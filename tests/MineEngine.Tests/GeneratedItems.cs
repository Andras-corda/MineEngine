namespace MineEngine.Tests;

/// <summary>
/// The Java of the items of a generated mod: ModItems, which registers them, then the class of each
/// item (package "item", V0.7.13), where their methods are.
/// </summary>
internal static class GeneratedItems
{
    /// <param name="modPackage">Folder of the package of the mod (".../com/mineengine/mods/testmod").</param>
    public static string Read(string modPackage)
    {
        string registry = File.ReadAllText(Path.Combine(modPackage, "registry/ModItems.java"));
        string classes = Path.Combine(modPackage, "item");
        return !Directory.Exists(classes)
            ? registry
            : string.Join('\n', [registry, .. Directory.GetFiles(classes, "*.java").Order(StringComparer.Ordinal).Select(File.ReadAllText)]);
    }
}
