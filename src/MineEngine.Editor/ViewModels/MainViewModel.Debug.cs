using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Logging;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Debug mode and crash reports (V0.6.4): the Debug menu (shown in debug mode), the memory used in
/// the status bar, and the reports written when Mine Engine meets an unexpected error.
/// </summary>
public sealed partial class MainViewModel
{
    private DispatcherTimer? _memoryTimer;
    private string _memoryLabel = string.Empty;
    private CrashReporter? _crashReporter;

    /// <summary>Writes the crash reports; set at startup.</summary>
    public CrashReporter? CrashReporter
    {
        get => _crashReporter;
        set
        {
            _crashReporter = value;
            _crashReporter?.SetStateProvider(EditorState);
        }
    }

    /// <summary>Folder of the log files; set at startup.</summary>
    public string? LogsDirectory { get; set; }

    public bool IsDebugMode => _preferences.Current.DebugMode;

    /// <summary>"DEBUG · 245 MB" in the status bar.</summary>
    public string MemoryLabel
    {
        get => _memoryLabel;
        private set => SetProperty(ref _memoryLabel, value);
    }

    public ICommand OpenLogsFolderCommand { get; private set; } = null!;

    public ICommand OpenCrashReportsCommand { get; private set; } = null!;

    public ICommand CopySystemInformationCommand { get; private set; } = null!;

    public ICommand WriteTestCrashReportCommand { get; private set; } = null!;

    public ICommand CollectMemoryCommand { get; private set; } = null!;

    public ICommand LogEngineStateCommand { get; private set; } = null!;

    /// <summary>Saves a report for an error the editor caught, says where it is, and returns its file.</summary>
    public string? ReportError(Exception exception, CrashKind kind = CrashKind.Error)
    {
        CrashReport? report = _crashReporter?.Report(exception, kind);
        if (report is not null)
        {
            _log.Warning(Tr.F("A crash report was saved: {0}", report.File));
        }

        return report?.File;
    }

    private void InitializeDebug()
    {
        OpenLogsFolderCommand = new RelayCommand(() => _shell.OpenFolder(LogsDirectory!), () => LogsDirectory is not null);
        OpenCrashReportsCommand = new RelayCommand(OpenCrashReports, () => _crashReporter is not null);
        CopySystemInformationCommand = new RelayCommand(CopySystemInformation, () => _crashReporter is not null);
        WriteTestCrashReportCommand = new RelayCommand(WriteTestCrashReport, () => _crashReporter is not null);
        CollectMemoryCommand = new RelayCommand(CollectMemory);
        LogEngineStateCommand = new RelayCommand(LogEngineState);
        _preferences.Changed += (_, _) => UpdateDebugMode();
        UpdateDebugMode();
    }

    private void UpdateDebugMode()
    {
        OnPropertyChanged(nameof(IsDebugMode));
        if (IsDebugMode)
        {
            _memoryTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => RefreshMemory(), Dispatcher.CurrentDispatcher);
            _memoryTimer.Start();
            RefreshMemory();
            _log.Debug(Tr.T("Debug mode is on."));
        }
        else
        {
            _memoryTimer?.Stop();
            MemoryLabel = string.Empty;
        }
    }

    private void RefreshMemory() =>
        MemoryLabel = Tr.F("DEBUG · {0} MB", Environment.WorkingSet / (1024 * 1024));

    /// <summary>State of the editor written in the crash reports and in the system information.</summary>
    private IReadOnlyList<(string Name, string Value)> EditorState()
    {
        var state = new List<(string, string)>();
        if (_project is { } project)
        {
            state.Add(("Project", $"{project.Name} ({project.Model.Layout.RootDirectory})"));
            state.Add(("Target", $"{project.Model.Settings.LoaderId} {project.Model.Settings.MinecraftVersion}, MDK {project.Model.Settings.MdkId}"));
            state.Add(("Assets", string.Join(", ", project.Model.Assets.GroupBy(a => a.Type).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"))));
        }
        else
        {
            state.Add(("Project", "none"));
        }

        state.Add(("Active tab", _activeDocument?.Title ?? ActiveWorkspaceView ?? "none"));
        state.Add(("Open tabs", Documents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        state.Add(("Busy", IsBusy ? "yes" : "no"));
        return state;
    }

    private void OpenCrashReports()
    {
        System.IO.Directory.CreateDirectory(_crashReporter!.Directory);
        _shell.OpenFolder(_crashReporter.Directory);
    }

    private void CopySystemInformation()
    {
        System.Windows.Clipboard.SetText(_crashReporter!.SystemInformation());
        StatusText = Tr.T("System information copied");
    }

    /// <summary>Checks the reporter: an error is thrown, caught and reported like a real one.</summary>
    private void WriteTestCrashReport()
    {
        try
        {
            throw new InvalidOperationException(Tr.T("Test of the crash reports (Debug menu)."));
        }
        catch (InvalidOperationException exception)
        {
            if (_crashReporter!.Report(exception, CrashKind.Test) is { } report)
            {
                _log.Info(Tr.F("Test crash report written: {0}", report.File));
                _shell.OpenFile(report.File);
            }
        }
    }

    private void CollectMemory()
    {
        long before = GC.GetTotalMemory(forceFullCollection: false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long after = GC.GetTotalMemory(forceFullCollection: true);
        _log.Info(Tr.F("Memory collected: {0} MB before, {1} MB after.", before / (1024 * 1024), after / (1024 * 1024)));
        RefreshMemory();
    }

    /// <summary>Writes the state of the engine in the Output: project, caches, documents, memory.</summary>
    private void LogEngineState()
    {
        var text = new StringBuilder(Tr.T("State of the engine:"));
        foreach ((string name, string value) in EditorState())
        {
            text.Append("\n  ").Append(name).Append(": ").Append(value);
        }

        if (_project is { } project)
        {
            text.Append("\n  ").Append(project.Model.Assets.DebugSummary());
            text.Append("\n  ").Append(Tr.F("Undo history: {0} step(s)", project.History.UndoCount));
        }

        text.Append("\n  ").Append(Tr.F("Memory: {0} MB (managed {1} MB)", Environment.WorkingSet / (1024 * 1024), GC.GetTotalMemory(false) / (1024 * 1024)));
        _log.Info(text.ToString());
    }
}
