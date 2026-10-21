using MineEngine.Assets;
using MineEngine.Assets.Models;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Texture of a model for one of its keys ("0", "1"... or a texture variable of a Java model).</summary>
public sealed record IRModelTexture(string Key, IRTexture Texture);

/// <summary>Texture drawn over a mob model; an emissive layer glows in the dark (eyes).</summary>
public sealed record IRModelLayer(IRTexture Texture, bool Emissive);

/// <summary>
/// 3D model of the project used by the game: written as models/custom/&lt;id&gt;.json for items and
/// blocks and, for a Blockbench model, turned into a Java entity model for mobs.
/// </summary>
public sealed record IRModel(Guid SourceAssetId, ResourceId Id, string SourceFile)
{
    public ModelFormat Format { get; init; } = ModelFormat.JavaJson;

    /// <summary>The Blockbench project read from <see cref="SourceFile"/>, or null for a Java JSON model.</summary>
    public BlockbenchModel? Blockbench { get; init; }

    /// <summary>
    /// Textures written with the model. A Blockbench model has all of its textures (bound, embedded or
    /// placeholder); a Java JSON model only the bound ones, the others keep their reference.
    /// </summary>
    public IReadOnlyList<IRModelTexture> Textures { get; init; } = [];

    /// <summary>Other textures for the whole model; a mob takes the first texture or one of them at random.</summary>
    public IReadOnlyList<IRTexture> Variants { get; init; } = [];

    public IReadOnlyList<IRModelLayer> Layers { get; init; } = [];
}
