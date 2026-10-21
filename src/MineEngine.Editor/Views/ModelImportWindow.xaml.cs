using System.Windows;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.Views;

public partial class ModelImportWindow : Window
{
    public ModelImportWindow(ModelImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
    }
}
