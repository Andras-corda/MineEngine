using MineEngine.Core.Commands;
using MineEngine.Editor.Services;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Services dont un ViewModel d'asset a besoin pour éditer son asset.</summary>
public sealed class AssetEditingContext
{
    private readonly Func<Guid, bool> _selectAsset;

    public AssetEditingContext(
        ModProject project, AssetFileImporter importer, IDialogService dialogs, IShellService shell, Func<Guid, bool> selectAsset,
        EditorNavigation navigation)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Importer = importer ?? throw new ArgumentNullException(nameof(importer));
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        Shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _selectAsset = selectAsset ?? throw new ArgumentNullException(nameof(selectAsset));
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    public ModProject Project { get; }

    /// <summary>Historique dans lequel passent toutes les modifications.</summary>
    public UndoHistory History => Project.History;

    public AssetFileImporter Importer { get; }

    public IDialogService Dialogs { get; }

    public IShellService Shell { get; }

    /// <summary>Ouverture d'un autre asset dans un onglet (graphe relié à un événement...).</summary>
    public EditorNavigation Navigation { get; }

    /// <summary>Sélectionne un autre asset dans le Content Browser (lien "Utilisé par", bouton "Ouvrir").</summary>
    public bool SelectAsset(Guid id) => _selectAsset(id);
}
