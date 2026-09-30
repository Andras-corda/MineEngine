using System.Windows;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
        Closed += (_, _) => viewModel.DiscardPreview();
    }
}
