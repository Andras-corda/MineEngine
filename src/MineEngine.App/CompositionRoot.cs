using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Build;
using MineEngine.Build.Gradle;
using MineEngine.Build.Java;
using MineEngine.Core.Logging;
using MineEngine.Core.Theming;
using MineEngine.Editor;
using MineEngine.Editor.Services;
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Hub;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Project;
using MineEngine.Project.Backups;
using MineEngine.Project.Serialization;
using MineEngine.Project.Workspace;

namespace MineEngine.App;

/// <summary>
/// Seul endroit où les objets de l'application sont créés et reliés entre eux.
/// Toutes les autres classes reçoivent leurs dépendances par leur constructeur.
/// </summary>
internal sealed class CompositionRoot : IDisposable
{
    private readonly HttpClient _httpClient; // HTTP client pour les requêtes réseau 
    private readonly FileLog _fileLog; // Fichier de journalisation pour enregistrer les logs

    /// <summary>
    /// Initializes a new instance of the CompositionRoot class, setting up dependencies and services for the application.
    /// </summary>
    /// <param name="application"></param>
    public CompositionRoot(System.Windows.Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _httpClient = new HttpClient { 
            Timeout = TimeSpan.FromMinutes(10) 
            };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MineEngine/" + EditorInfo.Version);

        // données locales de l'application pour stocker les préférences, les thèmes, les langues, les MDK, etc.
        string localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine");
            
        // dossier de données locales pour stocker les préférences, les thèmes, les langues, les MDK, etc.
        string projectsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MineEngine Projects");

        // Préférences de l'éditeur (appliquées avant la création des fenêtres)
        Preferences = new EditorPreferences(
            new EditorSettingsStore(Path.Combine(localData, "settings.json")),
            new WpfThemeService(application, new ThemeLibrary(Path.Combine(localData, "themes"))),
            new WpfInterfaceService(application),
            new LanguageLibrary(Path.Combine(localData, "languages")));
        Preferences.Load();

        // console Output de l'éditeur et fichier dans %LOCALAPPDATA%\MineEngine\logs
        Output = new OutputViewModel();
        _fileLog = new FileLog(Path.Combine(localData, "logs"));
        Log = new CompositeLog(Output, _fileLog);

        // Données et persistance
        AssetCatalog catalog = AssetCatalog.CreateDefault();
        var repository = new ProjectRepository(new AssetSerializer(catalog), new ProjectSettingsSerializer());

        // MDK et backends
        MdkLibrary = new MdkLibrary(Path.Combine(localData, "mdks"), new MdkInspector(), new MdkManifestSerializer());
        ModLoaderBackendRegistry backends = ModLoaderBackendRegistry.CreateDefault();
        var mdks = new MdkServices(MdkLibrary, MdkCatalog.CreateDefault(_httpClient), backends, Log);

        // Génération et build
        var pipeline = new BuildPipeline(
            ModIRBuilder.CreateDefault(),
            MdkLibrary,
            backends,
            ModGenerator.CreateDefault,
            new JdkLocator(),
            new GradleJavaCompatibility(),
            new GradleRunner());

        // Éditeur
        var shell = new WindowsShellService();
        var dialogs = new WpfDialogService(projectsDirectory, mdks, shell, Preferences);
        var assetViewModels = new AssetViewModelFactory(catalog, new AssetFileImporter(), dialogs, shell);
        DockLayout = new DockLayoutService(Path.Combine(localData, "layout.v2.xml"), Log);
        var knownProjects = new KnownProjectsStore(Path.Combine(localData, "projects.json"));
        MainViewModel = new MainViewModel(
            repository, new ProjectBackupService(), assetViewModels, pipeline, dialogs, shell, mdks, Preferences, Output, Log,
            open => new ProjectHubViewModel(knownProjects, new ProjectSettingsSerializer(), dialogs, shell, projectsDirectory, Preferences, open));
    }

    public EditorPreferences Preferences { get; } // objet contenant les préférences de l'éditeur, les thèmes, les langues, etc.

    public OutputViewModel Output { get; } // objet représentant la console Output de l'éditeur, affichant les messages de log et les erreurs.

    /// <summary>Journal de l'application : console Output et fichier.</summary>
    public ILog Log { get; } // objet représentant le journal de l'application, combinant la console Output et le fichier de log pour enregistrer les messages et les erreurs.

    public DockLayoutService DockLayout { get; } // objet représentant la disposition du dock de l'éditeur, permettant de sauvegarder et de restaurer la position des fenêtres et des panneaux.

    public MdkLibrary MdkLibrary { get; } // objet représentant la bibliothèque de MDK (Mod Development Kit) de l'éditeur, permettant de gérer les MDK installés et disponibles pour le développement de mods.

    public MainViewModel MainViewModel { get; } // objet représentant le ViewModel principal de l'éditeur, contenant les commandes et les données pour gérer les projets, les assets, les préférences, etc.

    /// <summary>
    /// Cleans up resources and disposes of the composition root when the application exits.
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
        _fileLog.Dispose();
    }
}
