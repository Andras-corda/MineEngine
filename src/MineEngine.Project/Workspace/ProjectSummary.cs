using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Json;
using MineEngine.Project.Serialization;

namespace MineEngine.Project.Workspace;

/// <summary>
/// Aperçu d'un projet affiché dans l'accueil, lu sans charger le projet : paramètres,
/// nombre d'assets par type et quelques textures pour la vignette.
/// </summary>
public sealed class ProjectSummary
{
    private const int MaxPreviewTextures = 6;

    private ProjectSummary(string rootDirectory, ProjectSettings settings, IReadOnlyDictionary<AssetType, int> counts,
        IReadOnlyList<string> previewTextures, DateTime lastModified)
    {
        RootDirectory = rootDirectory;
        Settings = settings;
        Counts = counts;
        PreviewTextures = previewTextures;
        LastModified = lastModified;
    }

    public string RootDirectory { get; }

    public ProjectSettings Settings { get; }

    /// <summary>Nombre d'assets par type (les types absents valent 0).</summary>
    public IReadOnlyDictionary<AssetType, int> Counts { get; }

    /// <summary>Chemins absolus de quelques images PNG du projet.</summary>
    public IReadOnlyList<string> PreviewTextures { get; }

    /// <summary>Dernière modification du fichier Project.json.</summary>
    public DateTime LastModified { get; }

    public int Count(AssetType type) => Counts.GetValueOrDefault(type);

    /// <summary>Lit l'aperçu d'un projet ; null si le dossier ne contient pas de projet lisible.</summary>
    public static ProjectSummary? TryRead(string rootDirectory, ProjectSettingsSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        var layout = new ProjectLayout(rootDirectory);
        if (!File.Exists(layout.ProjectFile))
        {
            return null;
        }

        try
        {
            ProjectSettings settings = serializer.Deserialize(File.ReadAllText(layout.ProjectFile), ProjectLayout.ProjectFileName);
            return new ProjectSummary(
                layout.RootDirectory, settings, CountAssets(layout), FindPreviewTextures(layout), File.GetLastWriteTime(layout.ProjectFile));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Le type est lu dans chaque fichier : marche aussi pour l'ancien rangement à plat.</summary>
    private static Dictionary<AssetType, int> CountAssets(ProjectLayout layout)
    {
        var counts = new Dictionary<AssetType, int>();
        if (!Directory.Exists(layout.ContentDirectory))
        {
            return counts;
        }

        foreach (string file in Directory.EnumerateFiles(layout.ContentDirectory, "*" + AssetSerializer.FileExtension, SearchOption.AllDirectories))
        {
            try
            {
                JsonObject root = JsonFormatting.ParseObject(File.ReadAllText(file), file);
                if (Enum.TryParse(root["type"]?.GetValue<string>(), out AssetType type))
                {
                    counts[type] = counts.GetValueOrDefault(type) + 1;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or JsonException)
            {
                // Un fichier illisible n'empêche pas d'afficher le reste de l'aperçu.
            }
        }

        return counts;
    }

    private static List<string> FindPreviewTextures(ProjectLayout layout) =>
        Directory.Exists(layout.TexturesDirectory)
            ? Directory.EnumerateFiles(layout.TexturesDirectory, "*.png").Order(StringComparer.OrdinalIgnoreCase).Take(MaxPreviewTextures).ToList()
            : [];
}
