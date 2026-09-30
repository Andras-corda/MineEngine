using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Build;
using MineEngine.Build.Gradle;
using MineEngine.Build.Java;
using MineEngine.Editor.Services;
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.App;

/// <summary>
/// Seul endroit où les objets de l'application sont créés et reliés entre eux.
/// Toutes les autres classes reçoivent leurs dépendances par leur constructeur.
/// </summary>
internal sealed class CompositionRoot : IDisposable
{
    private readonly HttpClient _httpClient;

    public CompositionRoot(System.Windows.Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MineEngine/0.1");

        string localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine");
        string projectsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MineEngine Projects");

        // Préférences de l'éditeur (thème) : appliquées avant la création des fenêtres
        Preferences = new EditorPreferences(
            new EditorSettingsStore(Path.Combine(localData, "settings.json")), new WpfThemeService(application));
        Preferences.Load();

        // Journal : la console Output de l'éditeur
        Output = new OutputViewModel();

        // Données et persistance
        AssetCatalog catalog = AssetCatalog.CreateDefault();
        var repository = new ProjectRepository(new AssetSerializer(catalog), new ProjectSettingsSerializer());

        // MDK et backends
        MdkLibrary = new MdkLibrary(Path.Combine(localData, "mdks"), new MdkInspector(), new MdkManifestSerializer());
        ModLoaderBackendRegistry backends = ModLoaderBackendRegistry.CreateDefault();
        var mdks = new MdkServices(MdkLibrary, MdkCatalog.CreateDefault(_httpClient), backends, Output);

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
        var assetViewModels = new AssetViewModelFactory(catalog, new TextureImporter(), dialogs);
        MainViewModel = new MainViewModel(repository, assetViewModels, pipeline, dialogs, shell, mdks, Output);
    }

    public EditorPreferences Preferences { get; }

    public OutputViewModel Output { get; }

    public MdkLibrary MdkLibrary { get; }

    public MainViewModel MainViewModel { get; }

    public void Dispose() => _httpClient.Dispose();
}
