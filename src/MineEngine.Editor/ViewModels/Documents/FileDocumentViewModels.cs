using System.Text;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.IO;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>
/// Fichier texte du disque ouvert dans un onglet. Les fichiers générés et Project.json
/// sont en lecture seule (ils sont réécrits par Mine Engine) ; les autres s'éditent et
/// s'enregistrent avec leur encodage d'origine.
/// </summary>
public sealed class FileDocumentViewModel : CodeDocumentViewModel
{
    /// <summary>Au-delà, le fichier est ouvert avec l'application de Windows plutôt que dans un onglet.</summary>
    public const long MaxEditableSize = 4 * 1024 * 1024;

    private readonly string? _readOnlyReason;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private string? _saveError;

    public FileDocumentViewModel(string fullPath, ProjectLayout layout)
        : base("file:" + Path.GetFullPath(fullPath).ToLowerInvariant(), layout.ToRelativePath(fullPath), Path.GetExtension(fullPath))
    {
        FullPath = Path.GetFullPath(fullPath);
        _readOnlyReason = ReadOnlyReason(FullPath, layout);
        Reload();
    }

    public string FullPath { get; }

    public override bool IsReadOnly => _readOnlyReason is not null;

    public override string? Banner => _saveError ?? _readOnlyReason;

    public override string EncodingLabel => _encoding.GetPreamble().Length > 0 ? $"{_encoding.WebName.ToUpperInvariant()} avec BOM" : _encoding.WebName.ToUpperInvariant();

    public string SizeLabel
    {
        get
        {
            long bytes = File.Exists(FullPath) ? new FileInfo(FullPath).Length : 0;
            return bytes < 1024 ? $"{bytes} octets" : $"{bytes / 1024.0:0.#} Ko";
        }
    }

    /// <summary>Faux pour un fichier binaire ou trop gros : il s'ouvre alors avec Windows.</summary>
    public static bool CanOpen(string fullPath)
    {
        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length > MaxEditableSize)
            {
                return false;
            }

            Span<byte> head = stackalloc byte[(int)Math.Min(info.Length, 8000)];
            using FileStream stream = info.OpenRead();
            int read = stream.Read(head);
            return !head[..read].Contains((byte)0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Relit le fichier (les modifications non enregistrées sont perdues).</summary>
    public void Reload()
    {
        byte[] bytes = File.Exists(FullPath) ? File.ReadAllBytes(FullPath) : [];
        _encoding = DetectEncoding(bytes);
        int preamble = _encoding.GetPreamble().Length;
        LoadText(_encoding.GetString(bytes, preamble, bytes.Length - preamble));
        OnPropertyChanged(nameof(EncodingLabel));
        OnPropertyChanged(nameof(SizeLabel));
    }

    public override bool Save()
    {
        if (IsReadOnly)
        {
            return false;
        }

        try
        {
            byte[] content = [.. _encoding.GetPreamble(), .. _encoding.GetBytes(Document.Text)];
            AtomicFile.WriteAllBytes(FullPath, content);
            _saveError = null;
            MarkSaved();
            OnPropertyChanged(nameof(SizeLabel));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _saveError = "Impossible d'enregistrer : " + exception.Message;
        }

        OnPropertyChanged(nameof(Banner));
        return _saveError is null;
    }

    private static Encoding DetectEncoding(byte[] bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        [0xFF, 0xFE, ..] => Encoding.Unicode,
        [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode,
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    private static string? ReadOnlyReason(string fullPath, ProjectLayout layout)
    {
        if (IsUnder(fullPath, layout.GeneratedDirectory))
        {
            return "Fichier généré : il est réécrit à chaque compilation. Modifiez le contenu du mod dans Mine Engine.";
        }

        if (string.Equals(fullPath, layout.ProjectFile, StringComparison.OrdinalIgnoreCase))
        {
            return "Paramètres du projet : modifiez-les dans Projet > Paramètres du projet.";
        }

        return IsUnder(fullPath, layout.BuildDirectory) ? "Fichier produit par la compilation." : null;
    }

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Fichier d'un asset, affiché tel qu'il sera enregistré et mis à jour à chaque
/// modification. Lecture seule : l'asset se modifie dans le panneau Détails.
/// </summary>
public sealed class AssetDocumentViewModel : CodeDocumentViewModel, IDisposable
{
    private readonly Asset _asset;
    private readonly AssetSerializer _serializer;

    public AssetDocumentViewModel(Asset asset, AssetSerializer serializer, ProjectLayout layout)
        : base("asset:" + asset.Id.ToString("N"), layout.ToRelativePath(layout.GetAssetFilePath(asset)), ".json")
    {
        _asset = asset;
        _serializer = serializer;
        _asset.Changed += OnAssetChanged;
        LoadText(_serializer.Serialize(_asset));
    }

    public Guid AssetId => _asset.Id;

    public override bool IsReadOnly => true;

    public override string? Banner => "Aperçu de l'asset tel qu'il sera enregistré. Modifiez-le dans le panneau Détails ; ce texte suit les modifications.";

    public override string DetailsBadge => "APERÇU";

    public override bool Save() => false;

    public void Dispose() => _asset.Changed -= OnAssetChanged;

    private void OnAssetChanged(object? sender, AssetChangedEventArgs e) => LoadText(_serializer.Serialize(_asset));
}
