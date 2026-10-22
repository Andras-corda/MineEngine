using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views;

/// <summary>Asset browser (textures, 3D models): a double-click on a tile chooses it.</summary>
public partial class AssetBrowserWindow : Window
{
    public AssetBrowserWindow(AssetBrowserViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
    }

    private void OnTileDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: AssetBrowserItem } && DataContext is AssetBrowserViewModel { AcceptCommand: { } accept }
            && accept.CanExecute(null))
        {
            accept.Execute(null);
        }
    }
}
