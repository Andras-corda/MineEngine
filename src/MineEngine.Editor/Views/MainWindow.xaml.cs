using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views;

/// <summary>
/// Fenêtre principale. Le code-behind se limite à ce qui relève de la vue :
/// défilement automatique de la console et confirmation à la fermeture.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _scrollPending;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Output.Entries.CollectionChanged += OnOutputChanged;
    }

    /// <summary>
    /// Le défilement est reporté après le traitement de la modification par la liste :
    /// la lire pendant l'événement, avant qu'elle ait intégré la nouvelle ligne, provoque
    /// l'erreur "ItemsControl incohérent". Les ajouts rapprochés sont regroupés.
    /// </summary>
    private void OnOutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, ScrollOutputToEnd);
    }

    private void ScrollOutputToEnd()
    {
        _scrollPending = false;
        var entries = _viewModel.Output.Entries;
        if (entries.Count > 0)
        {
            OutputList.ScrollIntoView(entries[^1]);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmShutdown())
        {
            e.Cancel = true;
        }
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
