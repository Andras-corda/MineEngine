using System.Windows;
using MineEngine.Editor.ViewModels.Mdk;

namespace MineEngine.Editor.Views;

public partial class MdkManagerWindow : Window
{
    public MdkManagerWindow(MdkManagerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }
}
