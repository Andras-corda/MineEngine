using System.Text.Json;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Formats of the 3D models that can be imported into a project.</summary>
public enum ModelFormat
{
    /// <summary>Minecraft Java block or item model (.json), exported by Blockbench as "Java Block/Item".</summary>
    JavaJson,

    /// <summary>Blockbench project (.bbmodel).</summary>
    Blockbench,

    /// <summary>Bedrock or GeckoLib geometry (.geo.json).</summary>
    BedrockGeometry,

    /// <summary>Wavefront OBJ (.obj).</summary>
    Obj,

    /// <summary>glTF (.gltf or .glb).</summary>
    Gltf,
}

/// <summary>Texture of a model: one per texture of the model file ("0", "1"... or a name of a Java model).</summary>
/// <param name="Key">Key of the texture in the model file.</param>
/// <param name="Name">Name shown to the user (file name of the texture in Blockbench).</param>
/// <param name="TextureId">Texture of the project bound to it, or null to keep the texture of the file.</param>
public sealed record ModelTextureSlot(string Key, string Name, Guid? TextureId);

/// <summary>Another texture for the whole model (a cat with several coats): a mob takes one of them at random.</summary>
public sealed record ModelTextureVariant(string Name, Guid? TextureId);

/// <summary>A texture drawn over the model with the same UVs (clothes, saddle, glowing eyes when emissive).</summary>
public sealed record ModelTextureLayer(string Name, Guid? TextureId, bool Emissive);

/// <summary>
/// 3D model of the project, imported into the Models folder. Blockbench projects and Minecraft
/// Java JSON models are used by the game: written into the mod (models/custom/&lt;id&gt;.json)
/// for items and blocks, and, for Blockbench, turned into a Java entity model for mobs. The
/// other formats are kept in the project as sources (GeckoLib, OBJ, glTF).
/// Each texture of the model can be bound to a texture of the project; variants and layers are
/// used by mobs.
/// </summary>
public sealed class ModelAsset : FileAsset
{
    private IReadOnlyList<ModelTextureSlot> _slots = [];
    private IReadOnlyList<ModelTextureVariant> _variants = [];
    private IReadOnlyList<ModelTextureLayer> _layers = [];

    public ModelAsset(Guid id, ResourceId resourceId, string displayName, string filePath)
        : base(id, resourceId, displayName, filePath)
    {
    }

    public override AssetType Type => AssetType.Model;

    public ModelFormat Format => ModelFormats.FromPath(FilePath) ?? ModelFormat.JavaJson;

    /// <summary>True if the model is written into the mod (Blockbench and Java JSON models).</summary>
    public bool IsUsedByTheGame => Format is ModelFormat.JavaJson or ModelFormat.Blockbench;

    /// <summary>True if a mob can use the model (it becomes a Java entity model).</summary>
    public bool CanBeEntityModel => Format == ModelFormat.Blockbench;

    /// <summary>Textures of the model file and the textures of the project bound to them.</summary>
    public IReadOnlyList<ModelTextureSlot> Slots
    {
        get => _slots;
        set => SetList(ref _slots, value, nameof(Slots));
    }

    /// <summary>Other textures for the whole model; with variants, a mob takes the first slot or one of them at random.</summary>
    public IReadOnlyList<ModelTextureVariant> Variants
    {
        get => _variants;
        set => SetList(ref _variants, value, nameof(Variants));
    }

    /// <summary>Textures drawn over the model, in order.</summary>
    public IReadOnlyList<ModelTextureLayer> Layers
    {
        get => _layers;
        set => SetList(ref _layers, value, nameof(Layers));
    }

    /// <summary>Binds a texture of the project to a texture of the model (null: keep the texture of the file).</summary>
    public void SetSlotTexture(string key, Guid? textureId)
    {
        int index = _slots.ToList().FindIndex(s => s.Key == key);
        if (index >= 0 && _slots[index].TextureId != textureId)
        {
            Slots = [.. _slots.Select((s, i) => i == index ? s with { TextureId = textureId } : s)];
        }
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (ModelTextureSlot slot in _slots.Where(s => s.TextureId is not null))
        {
            yield return new AssetReference(slot.TextureId!.Value, AssetReferenceKind.Texture, Tr.F("texture \"{0}\"", slot.Name));
        }

        foreach (ModelTextureVariant variant in _variants.Where(v => v.TextureId is not null))
        {
            yield return new AssetReference(variant.TextureId!.Value, AssetReferenceKind.Texture, Tr.F("variant \"{0}\"", variant.Name));
        }

        foreach (ModelTextureLayer layer in _layers.Where(l => l.TextureId is not null))
        {
            yield return new AssetReference(layer.TextureId!.Value, AssetReferenceKind.Texture, Tr.F("layer \"{0}\"", layer.Name));
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (FilePath.Length > 0 && !IsUsedByTheGame)
        {
            diagnostics.Info(
                Tr.F("{0} model: kept in the project but not used by the mod (only Blockbench and Minecraft Java JSON models are).", ModelFormats.Label(Format)),
                ToString(),
                Id);
        }

        foreach (ModelTextureVariant variant in _variants.Where(v => v.TextureId is null))
        {
            diagnostics.Warning(Tr.F("Variant \"{0}\" has no texture: it is left out.", variant.Name), ToString(), Id);
        }

        foreach (ModelTextureLayer layer in _layers.Where(l => l.TextureId is null))
        {
            diagnostics.Warning(Tr.F("Layer \"{0}\" has no texture: it is left out.", layer.Name), ToString(), Id);
        }
    }

    private void SetList<T>(ref IReadOnlyList<T> field, IReadOnlyList<T>? value, string propertyName)
    {
        IReadOnlyList<T> list = value is null ? [] : [.. value];
        if (!field.SequenceEqual(list))
        {
            field = list;
            RaiseChanged(propertyName);
        }
    }
}

/// <summary>Recognition of the imported model files.</summary>
public static class ModelFormats
{
    /// <summary>Filter of the file dialog.</summary>
    public const string DialogPattern = "*.bbmodel;*.json;*.obj;*.gltf;*.glb";

    /// <summary>Format from the file name, or null if the extension is not a model.</summary>
    public static ModelFormat? FromPath(string path)
    {
        string name = Path.GetFileName(path ?? string.Empty).ToLowerInvariant();
        return name switch
        {
            _ when name.EndsWith(".geo.json", StringComparison.Ordinal) => ModelFormat.BedrockGeometry,
            _ when name.EndsWith(".json", StringComparison.Ordinal) => ModelFormat.JavaJson,
            _ when name.EndsWith(".bbmodel", StringComparison.Ordinal) => ModelFormat.Blockbench,
            _ when name.EndsWith(".obj", StringComparison.Ordinal) => ModelFormat.Obj,
            _ when name.EndsWith(".gltf", StringComparison.Ordinal) || name.EndsWith(".glb", StringComparison.Ordinal) => ModelFormat.Gltf,
            _ => null,
        };
    }

    /// <summary>Extension kept when the file is copied into the project.</summary>
    public static string Extension(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        return name.EndsWith(".geo.json", StringComparison.Ordinal) ? ".geo.json" : Path.GetExtension(name);
    }

    /// <summary>
    /// Textures used by a model file, with no texture of the project bound yet: the textures of a
    /// Blockbench project ("0", "1"...), or the texture variables of a Java JSON model.
    /// </summary>
    public static IReadOnlyList<ModelTextureSlot> ReadSlots(string file)
    {
        try
        {
            switch (FromPath(file))
            {
                case ModelFormat.Blockbench:
                    return [.. Models.BlockbenchModel.Load(file).Textures.Select(t => new ModelTextureSlot(
                        t.Index.ToString(System.Globalization.CultureInfo.InvariantCulture), t.Name, null))];

                case ModelFormat.JavaJson:
                    using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(file)))
                    {
                        return document.RootElement.TryGetProperty("textures", out JsonElement textures) && textures.ValueKind == JsonValueKind.Object
                            ? [.. textures.EnumerateObject().Where(p => p.Name != "particle").Select(p => new ModelTextureSlot(p.Name, p.Value.ToString(), null))]
                            : [];
                    }

                default:
                    return [];
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Slots of a new file of the model, keeping the textures bound to the keys that still exist.</summary>
    public static IReadOnlyList<ModelTextureSlot> MergeSlots(IReadOnlyList<ModelTextureSlot> fresh, IReadOnlyList<ModelTextureSlot> previous) =>
        [.. fresh.Select(s => s with { TextureId = previous.FirstOrDefault(p => p.Key == s.Key)?.TextureId })];

    public static string Label(ModelFormat format) => format switch
    {
        ModelFormat.JavaJson => Tr.T("Minecraft Java JSON"),
        ModelFormat.Blockbench => "Blockbench",
        ModelFormat.BedrockGeometry => Tr.T("Bedrock / GeckoLib geometry"),
        ModelFormat.Obj => "OBJ",
        _ => "glTF",
    };

    /// <summary>
    /// Reason why a file is not a model of its format (wrong extension, JSON that does not look
    /// like a model, empty OBJ...), or null if it can be imported.
    /// </summary>
    public static string? DescribeProblem(string file)
    {
        if (!File.Exists(file))
        {
            return Tr.F("'{0}' does not exist.", Path.GetFileName(file));
        }

        if (FromPath(file) is not { } format)
        {
            return Tr.F("'{0}' is not a 3D model (.json, .bbmodel, .geo.json, .obj, .gltf or .glb).", Path.GetFileName(file));
        }

        try
        {
            switch (format)
            {
                case ModelFormat.JavaJson or ModelFormat.Blockbench or ModelFormat.BedrockGeometry or ModelFormat.Gltf
                    when !file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase):
                    using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(file)))
                    {
                        JsonElement root = document.RootElement;
                        bool valid = root.ValueKind == JsonValueKind.Object && format switch
                        {
                            ModelFormat.JavaJson => root.TryGetProperty("elements", out _) || root.TryGetProperty("parent", out _),
                            ModelFormat.Blockbench => root.TryGetProperty("meta", out _),
                            ModelFormat.BedrockGeometry => root.TryGetProperty("minecraft:geometry", out _) || root.TryGetProperty("format_version", out _),
                            _ => root.TryGetProperty("asset", out _),
                        };
                        return valid ? null : Tr.F("'{0}' is not a valid {1} model.", Path.GetFileName(file), Label(format));
                    }

                case ModelFormat.Gltf:
                    using (FileStream stream = File.OpenRead(file))
                    {
                        var header = new byte[4];
                        return stream.Read(header, 0, 4) == 4 && header.AsSpan().SequenceEqual("glTF"u8)
                            ? null
                            : Tr.F("'{0}' is not a valid {1} model.", Path.GetFileName(file), Label(format));
                    }

                default:
                    return File.ReadLines(file).Any(l => l.StartsWith("v ", StringComparison.Ordinal))
                        ? null
                        : Tr.F("'{0}' is not a valid {1} model.", Path.GetFileName(file), Label(format));
            }
        }
        catch (JsonException)
        {
            return Tr.F("'{0}' is not a valid {1} model.", Path.GetFileName(file), Label(format));
        }
    }
}
