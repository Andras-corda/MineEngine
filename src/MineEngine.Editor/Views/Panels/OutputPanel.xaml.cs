using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MineEngine.Editor.ViewModels;

namespace MineEngine.Editor.Views.Panels;

/// <summary>Console Output : le code-behind gère seulement le défilement automatique.</summary>
public partial class OutputPanel : UserControl
{
    private ObservableCollection<LogEntry>? _entries;
    private bool _scrollPending;

    public OutputPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_entries is not null)
        {
            _entries.CollectionChanged -= OnEntriesChanged;
        }

        _entries = (e.NewValue as MainViewModel)?.Output.Entries;
        if (_entries is not null)
        {
            _entries.CollectionChanged += OnEntriesChanged;
        }
    }

    /// <summary>
    /// Le défilement est reporté après le traitement de la modification par la liste :
    /// la lire pendant l'événement provoque l'erreur "ItemsControl incohérent".
    /// Les ajouts rapprochés sont regroupés.
    /// </summary>
    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, ScrollToEnd);
    }

    private void ScrollToEnd()
    {
        _scrollPending = false;
        if (_entries is { Count: > 0 } entries)
        {
            OutputList.ScrollIntoView(entries[^1]);
        }
    }
}
