using System.Windows;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views;

public partial class ProjectPropertiesWindow : Window
{
    public ProjectPropertiesWindow(ProjectPropertiesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
    }
}
