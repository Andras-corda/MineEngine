using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MineEngine.Editor.ViewModels.Creation;

namespace MineEngine.Editor.Views;

public partial class NewElementWindow : Window
{
    public NewElementWindow(NewElementViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
        Loaded += (_, _) =>
        {
            NameBox.SelectAll();
            NameBox.Focus();
        };
    }

    /// <summary>A double click on a choice creates it with the typed name.</summary>
    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(EntryList, source) is ListBoxItem
            && DataContext is NewElementViewModel { ConfirmCommand: { } confirm } && confirm.CanExecute(null))
        {
            confirm.Execute(null);
        }
    }
}
