using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Texture : créée en important une image PNG (jamais vide). <see cref="IAssetDefinition.CreateNew"/>
/// n'est utilisé que par les tests ; l'éditeur passe par l'import de fichier.
/// </summary>
public sealed class TextureAssetDefinition : AssetDefinition<TextureAsset>
{
    public override AssetType Type => AssetType.Texture;

    public override string Label => "Texture";

    public override string DefaultResourceIdBase => "texture";

    public override string DefaultDisplayName => "Texture";

    protected override TextureAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName, string.Empty);

    protected override void ReadProperties(TextureAsset asset, JsonPropertyReader reader) =>
        asset.FilePath = reader.GetString("file") ?? string.Empty;

    protected override void WriteProperties(TextureAsset asset, JsonObject properties) =>
        properties["file"] = asset.FilePath;
}

/// <summary>Son : créé en important un fichier OGG.</summary>
public sealed class SoundAssetDefinition : AssetDefinition<SoundAsset>
{
    public override AssetType Type => AssetType.Sound;

    public override string Label => "Son";

    public override string DefaultResourceIdBase => "sound";

    public override string DefaultDisplayName => "Son";

    protected override SoundAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName, string.Empty);

    protected override void ReadProperties(SoundAsset asset, JsonPropertyReader reader)
    {
        asset.FilePath = reader.GetString("file") ?? string.Empty;
        asset.Subtitle = reader.GetString("subtitle") ?? string.Empty;
    }

    protected override void WriteProperties(SoundAsset asset, JsonObject properties)
    {
        properties["file"] = asset.FilePath;
        properties["subtitle"] = asset.Subtitle;
    }
}
