using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;

namespace MineEngine.Project;

/// <summary>
/// Organisation d'un projet sur le disque. Comme un workspace MCreator, le dossier du
/// projet est lui-même le projet Gradle du mod :
/// <code>
/// MonMod/
///   MonMod.mineengine   paramètres du projet (dossiers du Dashboard, fichiers verrouillés...)
///   elements/           un fichier .asset.json par élément, rangé par type (items/, mobs/, graphs/...)
///   Textures/           images PNG importées
///   Sounds/             sons OGG importés
///   src/main/java       code Java : celui généré par Mine Engine et celui écrit à la main
///   src/main/resources  ressources générées (modèles, langues, recettes...)
///   build.gradle...     fichiers Gradle recopiés depuis le MDK
///   build/libs/         mods .jar produits
///   run/                dossier de jeu de Minecraft (mondes de test)
///   .mineengine/        cache local : liste des fichiers générés, sauvegardes
/// </code>
/// Mine Engine ne réécrit que les fichiers qu'il a lui-même générés et que l'utilisateur
/// n'a pas verrouillés ; les autres fichiers de src/ sont le code de l'utilisateur.
/// </summary>
public sealed class ProjectLayout
{
    /// <summary>Extension du fichier projet ("MonMod.mineengine").</summary>
    public const string ProjectFileExtension = ".mineengine";

    /// <summary>Fichier projet des versions antérieures à la 0.4.2, converti à l'ouverture.</summary>
    public const string LegacyProjectFileName = "Project.json";

    public const string ElementsFolder = "elements";

    public const string JavaSourceFolder = "src/main/java";

    public const string ResourcesFolder = "src/main/resources";

    public ProjectLayout(string rootDirectory, string? projectFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        ProjectFile = Path.Combine(RootDirectory, projectFileName ?? FindProjectFileName(RootDirectory));
    }

    public string RootDirectory { get; }

    /// <summary>Fichier projet ("MonMod.mineengine").</summary>
    public string ProjectFile { get; }

    public string ProjectFileName => Path.GetFileName(ProjectFile);

    /// <summary>Fichier Project.json d'un projet pas encore converti.</summary>
    public string LegacyProjectFile => Path.Combine(RootDirectory, LegacyProjectFileName);

    public string ElementsDirectory => Path.Combine(RootDirectory, ElementsFolder);

    public string TexturesDirectory => Path.Combine(RootDirectory, "Textures");

    public string SoundsDirectory => Path.Combine(RootDirectory, "Sounds");

    /// <summary>Imported 3D models.</summary>
    public string ModelsDirectory => Path.Combine(RootDirectory, "Models");

    public string SourceDirectory => Path.Combine(RootDirectory, "src");

    public string JavaSourceDirectory => ToAbsolutePath(JavaSourceFolder);

    public string ResourcesDirectory => ToAbsolutePath(ResourcesFolder);

    /// <summary>Dossier de travail de Gradle (classes compilées, .jar).</summary>
    public string GradleBuildDirectory => Path.Combine(RootDirectory, "build");

    /// <summary>Mods .jar produits par la compilation.</summary>
    public string BuildDirectory => Path.Combine(GradleBuildDirectory, "libs");

    /// <summary>Dossier de jeu utilisé par "Lancer Minecraft".</summary>
    public string RunDirectory => Path.Combine(RootDirectory, "run");

    public string CacheDirectory => Path.Combine(RootDirectory, ".mineengine");

    /// <summary>Dossier où le mod est généré : le projet lui-même.</summary>
    public string WorkspaceDirectory => RootDirectory;

    /// <summary>Vrai si le dossier contient un projet (au format actuel ou ancien).</summary>
    public static bool ContainsProject(string directory) =>
        Directory.Exists(directory)
        && (Directory.EnumerateFiles(directory, "*" + ProjectFileExtension).Any()
            || File.Exists(Path.Combine(directory, LegacyProjectFileName)));

    /// <summary>Nom du fichier projet d'un dossier : le fichier .mineengine existant, sinon "Dossier.mineengine".</summary>
    public static string FindProjectFileName(string rootDirectory)
    {
        if (Directory.Exists(rootDirectory))
        {
            string? existing = Directory.EnumerateFiles(rootDirectory, "*" + ProjectFileExtension)
                .Order(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (existing is not null)
            {
                return Path.GetFileName(existing);
            }
        }

        return DefaultProjectFileName(Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory))));
    }

    /// <summary>"Mon mod" donne "Mon mod.mineengine".</summary>
    public static string DefaultProjectFileName(string projectName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new string((projectName ?? string.Empty).Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return (cleaned.Length > 0 ? cleaned : Tr.T("Project")) + ProjectFileExtension;
    }

    public void EnsureDirectories()
    {
        foreach (string directory in new[]
                 {
                     RootDirectory, ElementsDirectory, TexturesDirectory, SoundsDirectory, JavaSourceDirectory,
                 })
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string GetAssetFilePath(Asset asset) =>
        Path.Combine(ElementsDirectory, AssetFolder(asset.Type), asset.ResourceId.Value + AssetSerializer.FileExtension);

    /// <summary>Sous-dossier de elements/ propre à chaque type d'asset.</summary>
    public static string AssetFolder(AssetType type) => type switch
    {
        AssetType.Item => "items",
        AssetType.Block => "blocks",
        AssetType.Mob => "mobs",
        AssetType.Recipe => "recipes",
        AssetType.Texture => "textures",
        AssetType.Sound => "sounds",
        AssetType.Graph => "graphs",
        AssetType.BehaviorTree => "ai",
        AssetType.Model => "models",
        AssetType.Command => "commands",
        AssetType.CreativeTab => "tabs",
        AssetType.Keybind => "keybinds",
        AssetType.Enchantment => "enchantments",
        AssetType.Fluid => "fluids",
        AssetType.GameRule => "gamerules",
        AssetType.Effect => "effects",
        AssetType.GlobalVariable => "globals",
        _ => type.ToString().ToLowerInvariant(),
    };

    /// <summary>Vrai si le chemin est le dossier lui-même ou se trouve dedans.</summary>
    public static bool IsWithin(string path, string directory)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Convertit un chemin relatif au projet ("Textures/a.png") en chemin absolu.</summary>
    public string ToAbsolutePath(string projectRelativePath) =>
        Path.GetFullPath(Path.Combine(RootDirectory, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Convertit un chemin absolu situé dans le projet en chemin relatif avec des '/'.</summary>
    public string ToRelativePath(string absolutePath) =>
        Path.GetRelativePath(RootDirectory, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
}
