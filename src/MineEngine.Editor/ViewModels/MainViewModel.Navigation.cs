using System.Windows.Input;
using System.Windows.Threading;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// A page of the work area: an open tab, or a view of the project (Dashboard in one of its
/// folders, Resources, Languages).
/// </summary>
/// <param name="DocumentId">Tab shown, or null for a view of the project.</param>
/// <param name="View">View of the project when no tab is shown (MainViewModel.DashboardTabId...).</param>
/// <param name="Folder">Folder open in the Dashboard.</param>
public sealed record NavigationEntry(string? DocumentId, string? View, string Folder);

/// <summary>
/// Back and Forward, like a web browser: the pages shown in the work area are remembered, and
/// going back shows the previous one again (a closed tab is skipped). Showing a new page clears
/// the pages ahead.
/// </summary>
public sealed partial class MainViewModel
{
    private const int MaxHistory = 50;

    private readonly List<NavigationEntry> _backHistory = [];
    private readonly List<NavigationEntry> _forwardHistory = [];
    private NavigationEntry? _currentPage;
    private NavigationEntry? _navigationTarget;
    private DateTime _navigationDeadline;
    private bool _recordPending;
    private ICommand? _goBackCommand;
    private ICommand? _goForwardCommand;

    public ICommand GoBackCommand => _goBackCommand ??= new RelayCommand(GoBack, () => _backHistory.Exists(IsAvailable));

    public ICommand GoForwardCommand => _goForwardCommand ??= new RelayCommand(GoForward, () => _forwardHistory.Exists(IsAvailable));

    /// <summary>
    /// The page shown changed. The window changes the tab and the view one after the other: the
    /// page is read once both are set.
    /// </summary>
    private void RecordNavigation()
    {
        if (_recordPending)
        {
            return;
        }

        _recordPending = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            _recordPending = false;
            NavigationEntry? page = CurrentPage();

            // While Back or Forward shows a page, the window goes through other pages: they are not recorded.
            if (_navigationTarget is { } target)
            {
                if (page == target || DateTime.UtcNow > _navigationDeadline)
                {
                    _navigationTarget = null;
                }

                return;
            }

            if (page is null || page == _currentPage)
            {
                return;
            }

            if (_currentPage is not null)
            {
                _backHistory.Add(_currentPage);
                if (_backHistory.Count > MaxHistory)
                {
                    _backHistory.RemoveAt(0);
                }
            }

            _forwardHistory.Clear();
            _currentPage = page;
            CommandManager.InvalidateRequerySuggested();
        }, DispatcherPriority.Background);
    }

    /// <summary>A new project (or none) starts a new history.</summary>
    private void ResetNavigation()
    {
        _backHistory.Clear();
        _forwardHistory.Clear();
        _currentPage = null;
        CommandManager.InvalidateRequerySuggested();
    }

    private NavigationEntry? CurrentPage()
    {
        if (_project is null)
        {
            return null;
        }

        return _activeDocument is { } document
            ? new NavigationEntry(document.Id, null, string.Empty)
            : new NavigationEntry(null, ActiveWorkspaceView ?? DashboardTabId, ActiveWorkspaceView is null or DashboardTabId ? _project.CurrentFolder : string.Empty);
    }

    private void GoBack() => Navigate(_backHistory, _forwardHistory);

    private void GoForward() => Navigate(_forwardHistory, _backHistory);

    /// <summary>Shows the nearest available page of <paramref name="from"/>; the current page goes to <paramref name="to"/>.</summary>
    private void Navigate(List<NavigationEntry> from, List<NavigationEntry> to)
    {
        while (from.Count > 0)
        {
            NavigationEntry page = from[^1];
            from.RemoveAt(from.Count - 1);
            if (!IsAvailable(page))
            {
                continue;
            }

            if (_currentPage is not null)
            {
                to.Add(_currentPage);
            }

            _currentPage = page;
            Show(page);
            break;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private bool IsAvailable(NavigationEntry page) =>
        _project is not null && (page.DocumentId is not { } id || Documents.Any(d => d.Id == id));

    private void Show(NavigationEntry page)
    {
        _navigationTarget = page;
        _navigationDeadline = DateTime.UtcNow.AddSeconds(1);
        {
            if (page.DocumentId is { } id)
            {
                DocumentActivationRequested?.Invoke(this, Documents.First(d => d.Id == id));
            }
            else if (page.View is ResourcesTabId or LanguagesTabId or GlobalsTabId or TagsTabId)
            {
                ProjectTabRequested?.Invoke(this, page.View);
            }
            else
            {
                if (_project is not null && _project.CurrentFolder != page.Folder)
                {
                    _project.CurrentFolder = page.Folder;
                }

                DocumentActivationRequested?.Invoke(this, null);
            }
        }

        RecordNavigation();
    }
}
