using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views.Panels;

/// <summary>Onglet Ressources (voir ResourcesViewModel).</summary>
public partial class ResourcesPanel : UserControl
{
    public ResourcesPanel()
    {
        InitializeComponent();
    }

    /// <summary>Double-clic : l'élément s'ouvre dans son onglet (éditeur nodal pour un graphe).</summary>
    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: AssetViewModel asset } && DataContext is MainViewModel main
            && main.OpenAssetEditorCommand.CanExecute(asset))
        {
            main.OpenAssetEditorCommand.Execute(asset);
            e.Handled = true;
        }
    }
}
