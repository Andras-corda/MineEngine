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

    private ProjectPropertiesViewModel(
        bool isNewProject, MdkServices mdks, Func<string?> pickFolder, Action openMdkManager, string parentDirectory)
    {
        _isNewProject = isNewProject;
        _mdks = mdks ?? throw new ArgumentNullException(nameof(mdks));
        _pickFolder = pickFolder ?? throw new ArgumentNullException(nameof(pickFolder));
        _openMdkManager = openMdkManager ?? throw new ArgumentNullException(nameof(openMdkManager));
        _parentDirectory = parentDirectory;

        _modName = "Mon mod";
        _modIdText = ModId.FromText(_modName).Value;
        _modVersion = "1.0.0";
        _authors = Environment.UserName;
        _website = string.Empty;
        _description = string.Empty;
        _license = "All Rights Reserved";

        BrowseCommand = new RelayCommand(Browse, () => IsNewProject);
        ManageMdksCommand = new RelayCommand(ManageMdks);
        ConfirmCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true), () => CanConfirm);
        ReloadMdks(preferredId: null);
    }

    /// <summary>Demande de fermeture de la fenêtre (vrai = validation).</summary>
    public event EventHandler<bool>? CloseRequested;

    public ICommand BrowseCommand { get; }

    public ICommand ManageMdksCommand { get; }

    public ICommand ConfirmCommand { get; }

    public bool IsNewProject => _isNewProject;

    public string Title => IsNewProject ? "Nouveau projet" : "Paramètres du projet";

    public string ConfirmLabel => IsNewProject ? "Créer" : "Enregistrer";

    public string ModIdRule => IsNewProject
        ? $"Définitif après la création : {ModId.RuleDescription}."
        : "L'identifiant du mod ne peut plus être modifié.";

    /// <summary>Licences proposées ; toute autre valeur peut être saisie.</summary>
    public IReadOnlyList<string> LicenseOptions { get; } =
    [
        "All Rights Reserved", "MIT", "Apache-2.0", "LGPL-3.0-only", "GPL-3.0-only", "MPL-2.0", "CC0-1.0",
    ];

    /// <summary>MDK installés et pris en charge par le générateur.</summary>
    public ObservableCollection<MdkDescriptor> AvailableMdks { get; } = [];

    public bool HasMdks => AvailableMdks.Count > 0;

    public string MdkHint => HasMdks
        ? $"Le MDK fixe le loader et la version de Minecraft. Versions prises en charge : {_mdks.Backends.SupportDescription}."
        : "Aucun MDK utilisable n'est installé. Cliquez sur « Gérer les MDK... » pour en télécharger ou en importer un.";

    public string ModName
    {
        get => _modName;
        set
        {
            if (!SetProperty(ref _modName, value ?? string.Empty))
            {
                return;
            }

            Require(nameof(ModName), _modName, "Le nom du mod est obligatoire.");
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
                    SetError(nameof(ModVersion), "Version obligatoire, sans espace (par exemple 1.0.0).");
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
                SetError(nameof(Website), "Adresse invalide : elle doit commencer par https:// (ou laissez le champ vide).");
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
                Require(nameof(License), _license, "Indiquez une licence (All Rights Reserved si vous ne savez pas).");
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
                Require(nameof(ParentDirectory), _parentDirectory, "Choisissez un dossier.");
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
        MdkServices mdks, ProjectSettings settings, Func<string?> pickFolder, Action openMdkManager)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var viewModel = new ProjectPropertiesViewModel(isNewProject: false, mdks, pickFolder, openMdkManager, string.Empty)
        {
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
            SetError(nameof(ModIdText), $"Identifiant invalide : {ModId.RuleDescription}.");
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

    private void Browse()
    {
        string? folder = _pickFolder();
        if (folder is not null)
        {
            ParentDirectory = folder;
        }
    }
}
