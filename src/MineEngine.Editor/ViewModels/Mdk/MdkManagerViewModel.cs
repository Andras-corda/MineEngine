using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using MineEngine.Core.Logging;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Editor.ViewModels.Mdk;

/// <summary>
/// Gestionnaire de MDK : liste des MDK installés, téléchargement des MDK
/// officiels, import d'une archive ou d'un dossier, suppression.
/// </summary>
public sealed class MdkManagerViewModel : ObservableObject, IDisposable
{
    private readonly MdkServices _mdks;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly SynchronizationContext _uiContext;
    private readonly int _uiThreadId;

    private MdkItemViewModel? _selectedItem;
    private MdkDownloadViewModel? _selectedDownload;
    private bool _isBusy;
    private string _statusText = string.Empty;

    public MdkManagerViewModel(MdkServices mdks, IDialogService dialogs, IShellService shell)
    {
        _mdks = mdks ?? throw new ArgumentNullException(nameof(mdks));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Le gestionnaire de MDK doit être créé sur le thread de l'interface.");
        _uiThreadId = Environment.CurrentManagedThreadId;

        ImportArchiveCommand = new RelayCommand(ImportArchive, () => !IsBusy);
        ImportFolderCommand = new RelayCommand(ImportFolder, () => !IsBusy);
        DownloadCommand = new AsyncRelayCommand(DownloadSelectedAsync, () => !IsBusy && SelectedDownload is { IsInstalled: false });
        RemoveCommand = new RelayCommand(RemoveSelected, () => !IsBusy && SelectedItem is not null);
        RefreshCommand = new RelayCommand(Refresh, () => !IsBusy);
        OpenLibraryFolderCommand = new RelayCommand(() => _shell.OpenFolder(LibraryDirectory));

        _mdks.Library.Changed += OnLibraryChanged;
        Rebuild();
    }

    public ObservableCollection<MdkItemViewModel> Items { get; } = [];

    public ObservableCollection<MdkDownloadViewModel> Downloads { get; } = [];

    public MdkItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public MdkDownloadViewModel? SelectedDownload
    {
        get => _selectedDownload;
        set => SetProperty(ref _selectedDownload, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string LibraryDirectory => _mdks.Library.RootDirectory;

    public string SupportDescription => _mdks.Backends.SupportDescription;

    public bool IsEmpty => Items.Count == 0;

    public ICommand ImportArchiveCommand { get; }

    public ICommand ImportFolderCommand { get; }

    public ICommand DownloadCommand { get; }

    public ICommand RemoveCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand OpenLibraryFolderCommand { get; }

    public void Dispose() => _mdks.Library.Changed -= OnLibraryChanged;

    private void ImportArchive()
    {
        string? archive = _dialogs.AskMdkArchive();
        if (archive is not null)
        {
            Execute("Import du MDK", () => _mdks.Library.ImportArchive(archive, "Archive importée : " + archive));
        }
    }

    private void ImportFolder()
    {
        string? folder = _dialogs.AskFolder("Dossier du MDK (il contient gradlew.bat)");
        if (folder is not null)
        {
            Execute("Import du MDK", () => _mdks.Library.ImportDirectory(folder));
        }
    }

    private void RemoveSelected()
    {
        MdkItemViewModel? item = SelectedItem;
        if (item is null
            || !_dialogs.Confirm("Supprimer le MDK", $"Supprimer {item.Descriptor.DisplayName} et son dossier ?\n\n{item.Directory}\n\n" +
                                                      "Les projets qui l'utilisent devront en choisir un autre."))
        {
            return;
        }

        try
        {
            _mdks.Library.Remove(item.Descriptor);
            StatusText = "MDK supprimé : " + item.Descriptor.DisplayName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError("Supprimer le MDK", exception.Message);
        }
    }

    private void Refresh()
    {
        _mdks.Library.Refresh(_mdks.Log);
        StatusText = $"{Items.Count} MDK installé(s).";
    }

    private async Task DownloadSelectedAsync()
    {
        MdkDownloadViewModel? selected = SelectedDownload;
        if (selected is null)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Téléchargement de {selected.DisplayName}...";
        try
        {
            MdkDescriptor mdk = await _mdks.Catalog.DownloadAsync(selected.Download, _mdks.Library, _mdks.Log, CancellationToken.None);
            StatusText = "MDK installé : " + mdk.DisplayName;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException
                                              or TaskCanceledException or UnauthorizedAccessException)
        {
            _mdks.Log.Error("Téléchargement du MDK : " + exception.Message);
            StatusText = "Échec du téléchargement.";
            _dialogs.ShowError("Téléchargement du MDK", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Execute(string operation, Func<MdkDescriptor> action)
    {
        try
        {
            MdkDescriptor mdk = action();
            _mdks.Log.Success("MDK installé : " + mdk.DisplayName);
            StatusText = "MDK installé : " + mdk.DisplayName;
            SelectedItem = Items.FirstOrDefault(i => i.Descriptor.Id == mdk.Id);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(operation, exception.Message);
        }
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            Rebuild();
        }
        else
        {
            _uiContext.Post(_ => Rebuild(), null);
        }
    }

    private void Rebuild()
    {
        string? selectedId = SelectedItem?.Descriptor.Id;

        Items.Clear();
        foreach (MdkDescriptor mdk in _mdks.Library.Installed)
        {
            Items.Add(new MdkItemViewModel(mdk, _mdks.Backends.Supports(mdk)));
        }

        Downloads.Clear();
        foreach (MdkDownload download in _mdks.Catalog.Downloads)
        {
            Downloads.Add(new MdkDownloadViewModel(download, _mdks.Library.Find(download.Id) is not null));
        }

        SelectedItem = Items.FirstOrDefault(i => i.Descriptor.Id == selectedId);
        OnPropertyChanged(nameof(IsEmpty));
        CommandManager.InvalidateRequerySuggested();
    }
}
