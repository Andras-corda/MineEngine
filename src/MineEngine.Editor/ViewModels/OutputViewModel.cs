using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Core.Logging;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Console Output de l'éditeur. Implémente <see cref="ILog"/> : les messages
/// peuvent venir de n'importe quel thread, ils sont ajoutés sur le thread de l'interface.
/// </summary>
public sealed class OutputViewModel : ObservableObject, ILog
{
    private const int MaxEntries = 5000;

    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;

    public OutputViewModel()
    {
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException(Tr.T("The console must be created on the UI thread."));
        _uiThreadId = Environment.CurrentManagedThreadId;
        ClearCommand = new RelayCommand(Entries.Clear, () => Entries.Count > 0);
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public ICommand ClearCommand { get; }

    public void Write(LogLevel level, string message)
    {
        // Detailed messages (timings, generated files...) are shown only in debug mode.
        if (level == LogLevel.Debug && !Core.Diagnostics.EngineDebug.Enabled)
        {
            return;
        }

        var entry = new LogEntry(DateTime.Now, level, message);
        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            Append(entry);
        }
        else
        {
            _uiContext.Post(_ => Append(entry), null);
        }
    }

    private void Append(LogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }
    }
}
