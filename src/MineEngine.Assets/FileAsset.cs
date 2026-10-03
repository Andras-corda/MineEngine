using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>Asset dont le contenu est un fichier copié dans le projet (image, son).</summary>
public abstract class FileAsset : Asset
{
    private string _filePath;

    protected FileAsset(Guid id, ResourceId resourceId, string displayName, string filePath)
        : base(id, resourceId, displayName)
    {
        _filePath = Normalize(filePath);
    }

    /// <summary>Chemin du fichier, relatif au dossier du projet (séparateur '/').</summary>
    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, Normalize(value));
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        if (FilePath.Length == 0)
        {
            diagnostics.Error("Aucun fichier n'est associé à cet asset.", ToString(), Id);
        }
    }

    private static string Normalize(string? path) => (path ?? string.Empty).Trim().Replace('\\', '/');
}

/// <summary>Image PNG du projet, utilisable par les items, les blocs et les mobs.</summary>
public sealed class TextureAsset : FileAsset
{
    public TextureAsset(Guid id, ResourceId resourceId, string displayName, string filePath)
        : base(id, resourceId, displayName, filePath)
    {
    }

    public override AssetType Type => AssetType.Texture;
}

/// <summary>Son OGG du projet, déclaré comme événement sonore du mod.</summary>
public sealed class SoundAsset : FileAsset
{
    private string _subtitle = string.Empty;

    public SoundAsset(Guid id, ResourceId resourceId, string displayName, string filePath)
        : base(id, resourceId, displayName, filePath)
    {
    }

    public override AssetType Type => AssetType.Sound;

    /// <summary>Sous-titre affiché quand les sous-titres du jeu sont activés ; vide pour aucun.</summary>
    public string Subtitle
    {
        get => _subtitle;
        set => SetField(ref _subtitle, value?.Trim() ?? string.Empty);
    }
}
