using System.Collections.Concurrent;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Project;
using MineEngine.Project.Vanilla;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Image file that shows an item in the editor: the texture of an item or a block of the project,
/// or the texture of an item of Minecraft read from the game (item/, its model, then block/).
/// The files of Minecraft are looked for in the background and remembered.
/// </summary>
public static class ItemIcons
{
    private static readonly ConcurrentDictionary<string, string?> Vanilla = new(StringComparer.Ordinal);

    private static string VanillaCache =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine", "vanilla");

    /// <summary>Texture of an item or a block of the project, or null (no texture, or another reference).</summary>
    public static string? ProjectFile(ModProject project, ContentReference? reference)
    {
        if (reference is not { Kind: ContentReferenceKind.Asset } || project.Assets.Find(reference.AssetId) is not { } asset)
        {
            return null;
        }

        Guid? texture = asset switch
        {
            BlockAsset block => block.Options.ItemTextureId ?? block.TextureId,
            TexturedAsset textured => textured.TextureId,
            _ => null,
        };
        return texture is { } id && project.Assets.Find(id) is TextureAsset file ? AssetFileImporter.GetExistingFile(project, file) : null;
    }

    /// <summary>Image of the reference: at once for the project, in the background for Minecraft; null if there is none.</summary>
    public static async Task<string?> FindAsync(ModProject project, ContentReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        if (reference.Kind == ContentReferenceKind.Asset)
        {
            return ProjectFile(project, reference);
        }

        if (reference.Kind != ContentReferenceKind.Id || reference.Id is not { Namespace: "minecraft" } id)
        {
            return null;
        }

        string version = project.Settings.MinecraftVersion;
        string key = version + "|" + id.Path;
        if (Vanilla.TryGetValue(key, out string? known))
        {
            return known;
        }

        string? found = await Task.Run(() => FindVanilla(version, id.Path));
        Vanilla[key] = found;
        return found;
    }

    private static string? FindVanilla(string version, string path)
    {
        // Shipped with the editor: most items have a texture of their name.
        if ((MinecraftTextures.FileOf("item/" + path) ?? MinecraftTextures.FileOf("block/" + path)) is { } shipped)
        {
            return shipped;
        }

        try
        {
            if (VanillaAssetLibrary.For(version, VanillaCache) is not { } library)
            {
                return null;
            }

            return library.FileOf(new VanillaAsset(VanillaAssetKind.Texture, "item/" + path))
                   ?? (library.PreviewTexture(new VanillaAsset(VanillaAssetKind.Model, "item/" + path)) is { } texture ? library.FileOf(texture) : null)
                   ?? library.FileOf(new VanillaAsset(VanillaAssetKind.Texture, "block/" + path));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
