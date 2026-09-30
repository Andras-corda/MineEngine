using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Métadonnées du mod.</summary>
public sealed class IRModInfo
{
    public IRModInfo(
        ModId modId,
        string name,
        string version,
        string authors,
        string description,
        string license,
        string website,
        string minecraftVersion,
        string loaderId)
    {
        ModId = modId ?? throw new ArgumentNullException(nameof(modId));
        Name = name;
        Version = version;
        Authors = authors;
        Description = description;
        License = license;
        Website = website;
        MinecraftVersion = minecraftVersion;
        LoaderId = loaderId;
    }

    public ModId ModId { get; }

    public string Name { get; }

    public string Version { get; }

    public string Authors { get; }

    public string Description { get; }

    public string License { get; }

    public string Website { get; }

    public string MinecraftVersion { get; }

    public string LoaderId { get; }
}
