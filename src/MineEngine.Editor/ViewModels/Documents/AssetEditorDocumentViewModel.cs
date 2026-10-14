using System.ComponentModel;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>
/// Onglet d'édition d'un élément du mod (item, bloc, mob, recette, texture, son). Il
/// affiche l'éditeur complet de l'asset ; le panneau Détails n'en garde qu'un résumé.
/// Les modifications passent par l'historique du projet et s'enregistrent avec lui.
/// </summary>
public sealed class AssetEditorDocumentViewModel : DocumentViewModel, IDisposable
{
    public AssetEditorDocumentViewModel(AssetViewModel asset, string displayPath)
        : base(IdFor(asset.Model.Id), displayPath)
    {
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Asset.PropertyChanged += OnAssetPropertyChanged;
    }

    /// <summary>L'éditeur de l'asset (le même ViewModel que dans le Dashboard).</summary>
    public AssetViewModel Asset { get; }

    public Guid AssetId => Asset.Model.Id;

    public override string FileName => Asset.Header;

    public override bool IsReadOnly => false;

    public override string DetailsTitle => Asset.DetailsTitle;

    public override string DetailsBadge => Asset.DetailsBadge;

    public static string IdFor(Guid assetId) => "edit:" + assetId.ToString("N");

    /// <summary>Les modifications sont enregistrées avec le projet (Ctrl+S).</summary>
    public override bool Save() => true;

    public override void Navigate(OutlineItem item)
    {
    }

    public void Dispose() => Asset.PropertyChanged -= OnAssetPropertyChanged;

    private void OnAssetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetViewModel.Header) or nameof(AssetViewModel.DetailsTitle))
        {
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(DetailsTitle));
        }
    }
}
