using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Asset qui possède une texture. En V0.1, la texture est un fichier PNG du
/// dossier Textures/ du projet, désigné par un chemin relatif ; elle deviendra
/// un asset à part entière en V0.3.
/// </summary>
public abstract class TexturedAsset : Asset
{
    private string? _texturePath;

    protected TexturedAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    /// <summary>Chemin relatif au dossier du projet (séparateur '/'), ou null.</summary>
    public string? TexturePath
    {
        get => _texturePath;
        set => SetField(ref _texturePath, string.IsNullOrWhiteSpace(value) ? null : value.Replace('\\', '/'));
    }

    public bool HasTexture => TexturePath is not null;

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (!HasTexture)
        {
            diagnostics.Warning("Aucune texture ; une texture de remplacement sera utilisée.", ToString());
        }
    }
}
