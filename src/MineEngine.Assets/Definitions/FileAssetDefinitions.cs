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

    public override string Label => Tr.T("Texture");

    public override string DefaultResourceIdBase => "texture";

    public override string DefaultDisplayName => Tr.T("Texture");

    protected override TextureAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName, string.Empty);

    protected override void ReadProperties(TextureAsset asset, JsonPropertyReader reader) =>
        asset.FilePath = reader.GetString("file") ?? string.Empty;

    protected override void WriteProperties(TextureAsset asset, JsonObject properties) =>
        properties["file"] = asset.FilePath;
}

/// <summary>3D model: created by importing a model file.</summary>
public sealed class ModelAssetDefinition : AssetDefinition<ModelAsset>
{
    public override AssetType Type => AssetType.Model;

    public override string Label => Tr.T("3D model");

    public override string DefaultResourceIdBase => "model";

    public override string DefaultDisplayName => Tr.T("3D model");

    protected override ModelAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName, string.Empty);

    protected override void ReadProperties(ModelAsset asset, JsonPropertyReader reader)
    {
        asset.FilePath = reader.GetString("file") ?? string.Empty;
        asset.Slots = [.. reader.GetObjectList("textures").Select(t => new ModelTextureSlot(
            t.GetString("key") ?? string.Empty, t.GetString("name") ?? string.Empty, t.GetGuid("texture")))];
        asset.Variants = [.. reader.GetObjectList("variants").Select(v => new ModelTextureVariant(v.GetString("name") ?? string.Empty, v.GetGuid("texture")))];
        asset.Layers = [.. reader.GetObjectList("layers").Select(l => new ModelTextureLayer(
            l.GetString("name") ?? string.Empty, l.GetGuid("texture"), l.GetBoolean("emissive", false)))];
    }

    protected override void WriteProperties(ModelAsset asset, JsonObject properties)
    {
        properties["file"] = asset.FilePath;
        properties["textures"] = new JsonArray([.. asset.Slots.Select(s => (JsonNode)new JsonObject
        {
            ["key"] = s.Key,
            ["name"] = s.Name,
            ["texture"] = s.TextureId?.ToString("D"),
        })]);
        properties["variants"] = new JsonArray([.. asset.Variants.Select(v => (JsonNode)new JsonObject
        {
            ["name"] = v.Name,
            ["texture"] = v.TextureId?.ToString("D"),
        })]);
        properties["layers"] = new JsonArray([.. asset.Layers.Select(l => (JsonNode)new JsonObject
        {
            ["name"] = l.Name,
            ["texture"] = l.TextureId?.ToString("D"),
            ["emissive"] = l.Emissive,
        })]);
    }
}

/// <summary>Son : créé en important un fichier OGG.</summary>
public sealed class SoundAssetDefinition : AssetDefinition<SoundAsset>
{
    public override AssetType Type => AssetType.Sound;

    public override string Label => Tr.T("Sound");

    public override string DefaultResourceIdBase => "sound";

    public override string DefaultDisplayName => Tr.T("Sound");

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
