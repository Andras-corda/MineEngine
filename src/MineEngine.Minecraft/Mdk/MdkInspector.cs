using System.Text.RegularExpressions;

namespace MineEngine.Minecraft.Mdk;

/// <summary>Ce que l'inspection d'un dossier de MDK a permis de déterminer.</summary>
public sealed class MdkInspection
{
    public MdkInspection(ModLoaderKind loader, MinecraftVersion minecraftVersion, string loaderVersion, int javaVersion, string? gradleVersion)
    {
        Loader = loader;
        MinecraftVersion = minecraftVersion;
        LoaderVersion = loaderVersion;
        JavaVersion = javaVersion;
        GradleVersion = gradleVersion;
    }

    public ModLoaderKind Loader { get; }

    public MinecraftVersion MinecraftVersion { get; }

    public string LoaderVersion { get; }

    public int JavaVersion { get; }

    public string? GradleVersion { get; }
}

/// <summary>
/// Reconnaît un MDK à partir de ses fichiers : gradle.properties (loader et versions),
/// build.gradle (version de Java) et le Gradle wrapper.
/// </summary>
public sealed class MdkInspector
{
    public const string MarkerFile = "gradlew.bat";

    private static readonly Regex JavaToolchain = new(@"JavaLanguageVersion\.of\(\s*(\d+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex GradleDistribution = new(@"gradle-([0-9][0-9.]*[0-9])-(bin|all)\.zip", RegexOptions.Compiled);

    /// <summary>Vrai si le dossier ressemble à la racine d'un projet Gradle de mod.</summary>
    public bool LooksLikeMdk(string directory) =>
        File.Exists(Path.Combine(directory, MarkerFile)) && File.Exists(Path.Combine(directory, "gradle.properties"));

    /// <summary>Cherche la racine du MDK dans un dossier extrait (à la racine ou un niveau en dessous).</summary>
    public string? FindRoot(string directory)
    {
        if (LooksLikeMdk(directory))
        {
            return directory;
        }

        return Directory.EnumerateDirectories(directory).FirstOrDefault(LooksLikeMdk);
    }

    public MdkInspection Inspect(string directory)
    {
        if (!LooksLikeMdk(directory))
        {
            throw new InvalidDataException(
                $"'{directory}' n'est pas un MDK : les fichiers {MarkerFile} et gradle.properties sont introuvables.");
        }

        Dictionary<string, string> properties = ReadProperties(Path.Combine(directory, "gradle.properties"));

        ModLoaderKind loader;
        string loaderVersion;
        if (properties.TryGetValue("neo_version", out string? neoVersion))
        {
            loader = ModLoaderKind.NeoForge;
            loaderVersion = neoVersion;
        }
        else if (properties.TryGetValue("forge_version", out string? forgeVersion))
        {
            loader = ModLoaderKind.Forge;
            loaderVersion = forgeVersion;
        }
        else if (properties.TryGetValue("loader_version", out string? fabricVersion))
        {
            loader = ModLoaderKind.Fabric;
            loaderVersion = fabricVersion;
        }
        else
        {
            throw new InvalidDataException("Loader non reconnu : gradle.properties ne contient ni neo_version, ni forge_version, ni loader_version.");
        }

        if (!properties.TryGetValue("minecraft_version", out string? minecraftText)
            || !MinecraftVersion.TryParse(minecraftText, out MinecraftVersion? minecraftVersion))
        {
            throw new InvalidDataException("gradle.properties ne contient pas de minecraft_version valide.");
        }

        int javaVersion = ReadJavaVersion(directory) ?? minecraftVersion!.RequiredJavaVersion;
        return new MdkInspection(loader, minecraftVersion!, loaderVersion, javaVersion, ReadGradleVersion(directory));
    }

    /// <summary>Lecture simple d'un fichier .properties (clé=valeur, commentaires ignorés).</summary>
    public static Dictionary<string, string> ReadProperties(string file)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string rawLine in File.ReadLines(file))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or '!')
            {
                continue;
            }

            int separator = line.IndexOfAny(['=', ':']);
            if (separator > 0)
            {
                result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return result;
    }

    private static int? ReadJavaVersion(string directory)
    {
        string buildFile = Path.Combine(directory, "build.gradle");
        if (!File.Exists(buildFile))
        {
            return null;
        }

        Match match = JavaToolchain.Match(File.ReadAllText(buildFile));
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string? ReadGradleVersion(string directory)
    {
        string wrapper = Path.Combine(directory, "gradle", "wrapper", "gradle-wrapper.properties");
        if (!File.Exists(wrapper))
        {
            return null;
        }

        Match match = GradleDistribution.Match(File.ReadAllText(wrapper));
        return match.Success ? match.Groups[1].Value : null;
    }
}
