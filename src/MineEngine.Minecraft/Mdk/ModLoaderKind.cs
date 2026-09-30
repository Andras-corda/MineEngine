namespace MineEngine.Minecraft.Mdk;

public enum ModLoaderKind
{
    Unknown,
    Forge,
    NeoForge,
    Fabric,
}

public static class ModLoaderKindExtensions
{
    /// <summary>Identifiant enregistré dans les projets ("forge", "neoforge"...).</summary>
    public static string ToId(this ModLoaderKind kind) => kind.ToString().ToLowerInvariant();

    public static string ToDisplayName(this ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.Forge => "Forge",
        ModLoaderKind.NeoForge => "NeoForge",
        ModLoaderKind.Fabric => "Fabric",
        _ => "Inconnu",
    };

    public static ModLoaderKind FromId(string? id) =>
        Enum.TryParse(id, ignoreCase: true, out ModLoaderKind kind) ? kind : ModLoaderKind.Unknown;
}
