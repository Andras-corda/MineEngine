using MineEngine.Core.Identifiers;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Copies the icon of the mod (chosen in the project settings) to the root of the mod resources;
/// the metadata file (mods.toml or neoforge.mods.toml) points to it with logoFile, so the icon
/// appears in the in-game mod list.
/// </summary>
public sealed class ModIconEmitter : IFileEmitter
{
    public string Name => Tr.T("Mod icon");

    /// <summary>Name of the icon inside the mod jar ("magicmod_logo.png").</summary>
    public static string ResourceName(ModId modId) => modId.Value + "_logo.png";

    public void Emit(GenerationContext context)
    {
        if (context.Mod.IconFile is { } icon && File.Exists(icon))
        {
            context.Files.CopyFile(icon, $"{ResourcePaths.ResourcesRoot}/{ResourceName(context.Mod.ModId)}");
        }
    }
}
