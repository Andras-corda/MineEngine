using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Texture proposée dans la liste d'un emplacement ; <see cref="Guid.Empty"/> pour "aucune".</summary>
/// <param name="Id">Asset Texture, ou Guid.Empty.</param>
/// <param name="Label">Nom affiché.</param>
public sealed record TextureOption(Guid Id, string Label);

/// <summary>
/// Un emplacement de texture dans l'Inspector : choix parmi les textures du projet,
/// import d'une nouvelle image (qui devient un asset Texture) ou retrait. La texture
/// est désignée par son guid : la renommer ne casse pas le lien.
/// </summary>
public sealed class TextureSlotViewModel : ObservableObject
{
    private readonly Asset _asset;
    private readonly AssetEditingContext _context;
    private readonly Func<Guid?> _getter;
    private readonly Action<Guid?> _setter;
    private readonly string _emptyLabel;
    private string _label;

    public TextureSlotViewModel(
        Asset asset,
        AssetEditingContext context,
        string propertyName,
        string label,
        string emptyLabel,
        Func<Guid?> getter,
        Action<Guid?> setter)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        PropertyName = propertyName;
        _label = label;
        _emptyLabel = emptyLabel;
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        ImportCommand = new RelayCommand(Import);
        ClearCommand = new RelayCommand(() => Set("Retirer une texture", null), () => _getter() is not null);
        OpenCommand = new RelayCommand(() => _context.SelectAsset(_getter()!.Value), () => Texture is not null);
    }

    /// <summary>Nom de la propriété de l'asset modifiée par cet emplacement.</summary>
    public string PropertyName { get; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public IReadOnlyList<TextureOption> Options =>
    [
        new TextureOption(Guid.Empty, _emptyLabel),
        .. _context.Project.Assets.OfType<TextureAsset>()
            .OrderBy(t => t.ResourceId)
            .Select(t => new TextureOption(t.Id, t.ResourceId.Value)),
    ];

    /// <summary>Texture choisie (Guid.Empty pour aucune) ; la modifier passe par l'historique.</summary>
    public Guid SelectedTextureId
    {
        get => _getter() ?? Guid.Empty;
        set
        {
            Guid? id = value == Guid.Empty ? null : value;
            if (id != _getter())
            {
                Set("Changer une texture", id);
            }
        }
    }

    /// <summary>Chemin absolu de l'image pour l'aperçu, ou null.</summary>
    public string? FullPath => Texture is { } texture ? AssetFileImporter.GetExistingFile(_context.Project, texture) : null;

    public string PathLabel => _getter() switch
    {
        null => _emptyLabel,
        _ when Texture is { } texture => texture.FilePath,
        _ => "Texture supprimée : choisissez-en une autre",
    };

    public ICommand ImportCommand { get; }

    public ICommand ClearCommand { get; }

    /// <summary>Sélectionne l'asset Texture dans le Content Browser.</summary>
    public ICommand OpenCommand { get; }

    private TextureAsset? Texture => _getter() is { } id ? _context.Project.Assets.Find(id) as TextureAsset : null;

    /// <summary>À appeler quand la texture de l'asset ou la liste des textures a changé.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Options));
        OnPropertyChanged(nameof(SelectedTextureId));
        OnPropertyChanged(nameof(PathLabel));
        OnPropertyChanged(nameof(FullPath));
    }

    /// <summary>Importe une image : ajout de l'asset Texture et choix de celle-ci, en une seule étape annulable.</summary>
    private void Import()
    {
        string? file = _context.Dialogs.AskTextureFile();
        if (file is null)
        {
            return;
        }

        try
        {
            ModProject project = _context.Project;
            TextureAsset texture = _context.Importer.ImportTexture(project, file);
            _context.History.Execute(new CompositeCommand(
                "Importer une texture",
                [
                    new AddAssetCommand(project.Assets, texture, "Texture"),
                    new PropertyChangeCommand<Guid?>("Changer une texture", _asset, PropertyName, _getter, _setter, texture.Id),
                ]));
            _context.History.Seal();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _context.Dialogs.ShowError("Texture", exception.Message);
        }
    }

    private void Set(string description, Guid? id)
    {
        _context.History.Execute(new PropertyChangeCommand<Guid?>(description, _asset, PropertyName, _getter, _setter, id));
        _context.History.Seal();
    }
}
