using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Minecraft.Mdk;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Formulaire des informations d'un projet : utilisé à la création (tous les
/// champs) et pour modifier un projet existant (identifiant et dossier figés).
/// </summary>
public sealed class ProjectPropertiesViewModel : ValidatingObservableObject
{
    private readonly MdkServices _mdks;
    private readonly Func<string?> _pickFolder;
    private readonly Action _openMdkManager;
    private readonly bool _isNewProject;

    private string _modName;
    private string _modIdText;
    private bool _modIdEditedByUser;
    private string _modVersion;
    private string _authors;
    private string _website;
    private string _description;
    private string _license;
    private string _parentDirectory;
    private MdkDescriptor? _selectedMdk;
    private string _iconFile = string.Empty;
    private Func<string?> _pickIcon = () => null;
    private string? _projectRoot;

    private ProjectPropertiesViewModel(
        bool isNewProject, MdkServices mdks, Func<string?> pickFolder, Action openMdkManager, string parentDirectory)
    {
        _isNewProject = isNewProject;
        _mdks = mdks ?? throw new ArgumentNullException(nameof(mdks));
        _pickFolder = pickFolder ?? throw new ArgumentNullException(nameof(pickFolder));
        _openMdkManager = openMdkManager ?? throw new ArgumentNullException(nameof(openMdkManager));
        _parentDirectory = parentDirectory;

        _modName = Tr.T("My mod");
        _modIdText = ModId.FromText(_modName).Value;
        _modVersion = "1.0.0";
        _authors = Environment.UserName;
        _website = string.Empty;
        _description = string.Empty;
        _license = "All Rights Reserved";

        BrowseCommand = new RelayCommand(Browse, () => IsNewProject);
        ManageMdksCommand = new RelayCommand(ManageMdks);
        ConfirmCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true), () => CanConfirm);
        ChooseIconCommand = new RelayCommand(ChooseIcon);
        RemoveIconCommand = new RelayCommand(() => IconFile = string.Empty, () => HasIcon);
        ReloadMdks(preferredId: null);
    }

    /// <summary>Demande de fermeture de la fenêtre (vrai = validation).</summary>
    public event EventHandler<bool>? CloseRequested;

    public ICommand BrowseCommand { get; }

    /// <summary>Chooses the PNG icon of the mod.</summary>
    public ICommand ChooseIconCommand { get; }

    public ICommand RemoveIconCommand { get; }

    /// <summary>
    /// Icon of the mod: path relative to the project, or the absolute path of an image just chosen
    /// (copied into the project when the settings are saved); empty for none.
    /// </summary>
    public string IconFile
    {
        get => _iconFile;
        private set
        {
            if (SetProperty(ref _iconFile, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IconPreview));
                OnPropertyChanged(nameof(HasIcon));
                OnPropertyChanged(nameof(IconLabel));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Absolute path of the icon to preview, or null.</summary>
    public string? IconPreview => ProjectIcon.Resolve(_projectRoot, _iconFile) is { } path && File.Exists(path) ? path : null;

    public bool HasIcon => _iconFile.Length > 0;

    public string IconLabel => HasIcon ? Path.GetFileName(_iconFile) : Tr.T("No icon");

    /// <summary>Gives the dialog used to choose the icon (a PNG file).</summary>
    public ProjectPropertiesViewModel WithIconPicker(Func<string?> pickIcon)
    {
        _pickIcon = pickIcon ?? throw new ArgumentNullException(nameof(pickIcon));
        return this;
    }

    public ICommand ManageMdksCommand { get; }

    public ICommand ConfirmCommand { get; }

    public bool IsNewProject => _isNewProject;

    public string Title => IsNewProject ? Tr.T("New project") : Tr.T("Project Settings");

    public string ConfirmLabel => IsNewProject ? Tr.T("Create") : Tr.T("Save");

    public string ModIdRule => IsNewProject
        ? Tr.F("Permanent after creation: {0}.", ModId.RuleDescription)
        : Tr.T("The mod identifier can no longer be changed.");

    /// <summary>Licences proposées ; toute autre valeur peut être saisie.</summary>
    public IReadOnlyList<string> LicenseOptions { get; } =
    [
        "All Rights Reserved", "MIT", "Apache-2.0", "LGPL-3.0-only", "GPL-3.0-only", "MPL-2.0", "CC0-1.0",
    ];

    /// <summary>MDK installés et pris en charge par le générateur.</summary>
    public ObservableCollection<MdkDescriptor> AvailableMdks { get; } = [];

    public bool HasMdks => AvailableMdks.Count > 0;

    public string MdkHint => HasMdks
        ? Tr.F("The MDK sets the loader and the Minecraft version. Supported versions: {0}.", _mdks.Backends.SupportDescription)
        : Tr.T("No usable MDK is installed. Click \"Manage MDKs...\" to download or import one.");

    public string ModName
    {
        get => _modName;
        set
        {
            if (!SetProperty(ref _modName, value ?? string.Empty))
            {
                return;
            }

            Require(nameof(ModName), _modName, Tr.T("The mod name is required."));
            if (IsNewProject && !_modIdEditedByUser)
            {
                UpdateModId(ModId.FromText(_modName).Value);
            }

            OnPropertyChanged(nameof(TargetDirectory));
        }
    }

    public string ModIdText
    {
        get => _modIdText;
        set
        {
            if (!IsNewProject)
            {
                return;
            }

            _modIdEditedByUser = true;
            UpdateModId(value ?? string.Empty);
        }
    }

    public string ModVersion
    {
        get => _modVersion;
        set
        {
            if (SetProperty(ref _modVersion, value?.Trim() ?? string.Empty))
            {
                if (_modVersion.Length == 0 || _modVersion.Any(char.IsWhiteSpace))
                {
                    SetError(nameof(ModVersion), Tr.T("Version required, without spaces (for example 1.0.0)."));
                }
                else
                {
                    ClearErrors(nameof(ModVersion));
                }
            }
        }
    }

    public string Authors
    {
        get => _authors;
        set => SetProperty(ref _authors, value ?? string.Empty);
    }

    public string Website
    {
        get => _website;
        set
        {
            if (!SetProperty(ref _website, value?.Trim() ?? string.Empty))
            {
                return;
            }

            bool valid = _website.Length == 0
                || (Uri.TryCreate(_website, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));
            if (valid)
            {
                ClearErrors(nameof(Website));
            }
            else
            {
                SetError(nameof(Website), Tr.T("Invalid address: it must start with https:// (or leave the field empty)."));
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value ?? string.Empty);
    }

    public string License
    {
        get => _license;
        set
        {
            if (SetProperty(ref _license, value ?? string.Empty))
            {
                Require(nameof(License), _license, Tr.T("Enter a license (All Rights Reserved if you are not sure)."));
            }
        }
    }

    public MdkDescriptor? SelectedMdk
    {
        get => _selectedMdk;
        set
        {
            if (SetProperty(ref _selectedMdk, value))
            {
                OnPropertyChanged(nameof(SelectedMdkDetails));
            }
        }
    }

    public string SelectedMdkDetails => _selectedMdk is null
        ? string.Empty
        : $"Minecraft {_selectedMdk.MinecraftVersion}, {_selectedMdk.Loader.ToDisplayName()} {_selectedMdk.LoaderVersion}, Java {_selectedMdk.JavaVersion}";

    public string ParentDirectory
    {
        get => _parentDirectory;
        set
        {
            if (SetProperty(ref _parentDirectory, value ?? string.Empty))
            {
                Require(nameof(ParentDirectory), _parentDirectory, Tr.T("Choose a folder."));
                OnPropertyChanged(nameof(TargetDirectory));
            }
        }
    }

    /// <summary>Dossier qui sera créé pour le projet.</summary>
    public string TargetDirectory =>
        string.IsNullOrWhiteSpace(ParentDirectory) || !ModId.TryParse(ModIdText, out ModId? modId)
            ? string.Empty
            : ProjectRepository.GetProjectDirectory(ParentDirectory, new ProjectSettings(modId!, ModName));

    public bool CanConfirm =>
        !HasErrors
        && !string.IsNullOrWhiteSpace(ModName)
        && ModId.IsValid(ModIdText)
        && SelectedMdk is not null
        && (!IsNewProject || !string.IsNullOrWhiteSpace(ParentDirectory));

    public static ProjectPropertiesViewModel ForNewProject(
        MdkServices mdks, string defaultParentDirectory, Func<string?> pickFolder, Action openMdkManager) =>
        new(isNewProject: true, mdks, pickFolder, openMdkManager, defaultParentDirectory);

    public static ProjectPropertiesViewModel ForExistingProject(
        MdkServices mdks, ProjectSettings settings, string projectRoot, Func<string?> pickFolder, Action openMdkManager)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var viewModel = new ProjectPropertiesViewModel(isNewProject: false, mdks, pickFolder, openMdkManager, string.Empty)
        {
            _projectRoot = projectRoot,
            _iconFile = settings.IconFile,
            _modName = settings.ModName,
            _modIdText = settings.ModId.Value,
            _modVersion = settings.ModVersion,
            _authors = settings.Authors,
            _website = settings.Website,
            _description = settings.Description,
            _license = settings.License,
        };
        viewModel.ReloadMdks(settings.MdkId, settings.LoaderId, settings.MinecraftVersion);
        return viewModel;
    }

    /// <summary>Paramètres d'un nouveau projet, d'après le formulaire.</summary>
    public ProjectSettings CreateSettings()
    {
        var settings = new ProjectSettings(ModId.Parse(ModIdText), ModName);
        ApplyTo(settings);
        return settings;
    }

    /// <summary>Recopie le formulaire dans des paramètres existants.</summary>
    public void ApplyTo(ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.ModName = ModName;
        settings.ModVersion = ModVersion;
        settings.Authors = Authors.Trim();
        settings.Website = Website;
        settings.Description = Description.Trim();
        settings.License = License.Trim();
        settings.IconFile = IconFile;
        if (SelectedMdk is not null)
        {
            settings.MdkId = SelectedMdk.Id;
            settings.LoaderId = SelectedMdk.Loader.ToId();
            settings.MinecraftVersion = SelectedMdk.MinecraftVersion.Id;
        }
    }

    private void ReloadMdks(string? preferredId, string? loaderId = null, string? minecraftVersion = null)
    {
        AvailableMdks.Clear();
        foreach (MdkDescriptor mdk in _mdks.UsableMdks)
        {
            AvailableMdks.Add(mdk);
        }

        SelectedMdk =
            AvailableMdks.FirstOrDefault(m => string.Equals(m.Id, preferredId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableMdks.FirstOrDefault(m =>
                string.Equals(m.Loader.ToId(), loaderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.MinecraftVersion.Id, minecraftVersion, StringComparison.Ordinal))
            ?? (preferredId is null && loaderId is null ? AvailableMdks.FirstOrDefault() : null);

        OnPropertyChanged(nameof(HasMdks));
        OnPropertyChanged(nameof(MdkHint));
    }

    private void ManageMdks()
    {
        string? current = SelectedMdk?.Id;
        _openMdkManager();
        ReloadMdks(current);
        SelectedMdk ??= AvailableMdks.FirstOrDefault();
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateModId(string text)
    {
        if (!SetProperty(ref _modIdText, text, nameof(ModIdText)))
        {
            return;
        }

        if (ModId.IsValid(text))
        {
            ClearErrors(nameof(ModIdText));
        }
        else
        {
            SetError(nameof(ModIdText), Tr.F("Invalid identifier: {0}.", ModId.RuleDescription));
        }

        OnPropertyChanged(nameof(TargetDirectory));
    }

    private void Require(string propertyName, string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SetError(propertyName, message);
        }
        else
        {
            ClearErrors(propertyName);
        }
    }

    /// <summary>A file that is not a PNG image is refused with a message under the field.</summary>
    private void ChooseIcon()
    {
        if (_pickIcon() is not { } file)
        {
            return;
        }

        if (!ProjectIcon.IsPng(file))
        {
            SetError(nameof(IconFile), Tr.T("The icon must be a PNG image."));
            OnPropertyChanged(nameof(IconError));
            return;
        }

        ClearErrors(nameof(IconFile));
        OnPropertyChanged(nameof(IconError));
        IconFile = file;
    }

    /// <summary>Message shown when the chosen file is not a PNG image, or empty.</summary>
    public string IconError => GetErrors(nameof(IconFile)).OfType<string>().FirstOrDefault() ?? string.Empty;

    private void Browse()
    {
        string? folder = _pickFolder();
        if (folder is not null)
        {
            ParentDirectory = folder;
        }
    }
}
