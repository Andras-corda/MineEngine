using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Asset dont le contenu est un fichier : chemin, remplacement et ouverture du fichier.</summary>
public abstract class FileAssetViewModel : AssetViewModel
{
    private readonly FileAsset _file;

    protected FileAssetViewModel(FileAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _file = model;
        ReplaceCommand = new RelayCommand(Replace);
        OpenFileCommand = new RelayCommand(() => Context.Shell.OpenFile(FullPath!), () => FullPath is not null);
    }

    public string FilePath => _file.FilePath;

    /// <summary>Chemin absolu du fichier, ou null s'il a disparu.</summary>
    public string? FullPath => AssetFileImporter.GetExistingFile(Context.Project, _file);

    public bool IsFileMissing => FullPath is null;

    public ICommand ReplaceCommand { get; }

    public ICommand OpenFileCommand { get; }

    /// <summary>Demande le fichier de remplacement ; null si l'utilisateur annule.</summary>
    protected abstract string? AskReplacementFile();

    /// <summary>Copie le fichier dans le projet et retourne son chemin relatif.</summary>
    protected abstract string CopyIntoProject(string sourceFile);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(FileAsset.FilePath))
        {
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(IsFileMissing));
        }
    }

    /// <summary>Le nouveau fichier est copié à côté de l'ancien : l'annulation retrouve l'ancien.</summary>
    private void Replace()
    {
        string? source = AskReplacementFile();
        if (source is null)
        {
            return;
        }

        try
        {
            string path = CopyIntoProject(source);
            Change("Remplacer le fichier", nameof(FileAsset.FilePath), () => _file.FilePath, v => _file.FilePath = v, path);
            Context.History.Seal();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Context.Dialogs.ShowError(TypeLabel, exception.Message);
        }
    }
}

/// <summary>Inspector d'une texture : aperçu, taille de l'image et assets qui l'utilisent.</summary>
public sealed class TextureAssetViewModel : FileAssetViewModel
{
    public TextureAssetViewModel(TextureAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    public override string? IconPath => FullPath;

    /// <summary>La texture est copiée sous le nom de chaque élément qui l'utilise : son identifiant reste interne.</summary>
    public override bool HasGameId => false;

    /// <summary>Dimensions lues dans l'en-tête PNG ("16 x 16 pixels").</summary>
    public string SizeLabel => FullPath is { } path && TryReadPngSize(path, out int width, out int height)
        ? $"{width} x {height} pixels"
        : "Fichier introuvable";

    protected override string? AskReplacementFile() => Context.Dialogs.AskTextureFile();

    protected override string CopyIntoProject(string sourceFile) =>
        Context.Importer.CopyTexture(Context.Project, sourceFile, Model.ResourceId.Value);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(FileAsset.FilePath))
        {
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    /// <summary>Largeur et hauteur sont les deux entiers big-endian qui suivent la signature et l'en-tête IHDR.</summary>
    private static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            Span<byte> header = stackalloc byte[24];
            using FileStream stream = File.OpenRead(path);
            if (stream.Read(header) < header.Length)
            {
                return false;
            }

            width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
            height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
            return width > 0 && height > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>Inspector d'un son : fichier, sous-titre, écoute.</summary>
public sealed class SoundAssetViewModel : FileAssetViewModel
{
    private readonly SoundAsset _sound;

    public SoundAssetViewModel(SoundAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _sound = model;
    }

    public string Subtitle
    {
        get => _sound.Subtitle;
        set
        {
            string text = value ?? string.Empty;
            if (text.Trim() != _sound.Subtitle)
            {
                Change("Modifier le sous-titre", nameof(SoundAsset.Subtitle), () => _sound.Subtitle, v => _sound.Subtitle = v, text);
            }
        }
    }

    /// <summary>Identifiant à utiliser dans les commandes du jeu (/playsound).</summary>
    public string PlaySoundCommand => $"/playsound {FullResourceId} master @s";

    protected override string? AskReplacementFile() => Context.Dialogs.AskSoundFiles().FirstOrDefault();

    protected override string CopyIntoProject(string sourceFile) =>
        Context.Importer.CopySound(Context.Project, sourceFile, Model.ResourceId.Value);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(SoundAsset.Subtitle):
                OnPropertyChanged(nameof(Subtitle));
                break;

            case nameof(Core.Assets.Asset.ResourceId):
                OnPropertyChanged(nameof(PlaySoundCommand));
                break;
        }
    }
}
