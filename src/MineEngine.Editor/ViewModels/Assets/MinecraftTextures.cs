namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Textures of Minecraft shipped with the editor (folder MinecraftContent/textures next to the
/// application, filled from import-MinecraftContent): read at once, without the jar of the game.
/// A texture is named by its path under textures/, without extension ("item/diamond").
/// </summary>
public static class MinecraftTextures
{
    private static readonly Lazy<IReadOnlyList<string>> Paths = new(ReadPaths);

    /// <summary>Folder of the textures, next to the application.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "MinecraftContent", "textures");

    /// <summary>Every texture ("block/stone", "entity/cow/cow"...), sorted.</summary>
    public static IReadOnlyList<string> All => Paths.Value;

    /// <summary>The PNG file of a texture ("item/diamond"), or null if it is not shipped.</summary>
    public static string? FileOf(string path)
    {
        string file = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar) + ".png");
        return File.Exists(file) ? file : null;
    }

    /// <summary>First folder of a texture: "block", "item", "entity"...</summary>
    public static string TopFolder(string path) => path.Contains('/') ? path[..path.IndexOf('/')] : string.Empty;

    private static IReadOnlyList<string> ReadPaths()
    {
        if (!Directory.Exists(Root))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(Root, "*.png", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f)[..^4].Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)];
    }
}
