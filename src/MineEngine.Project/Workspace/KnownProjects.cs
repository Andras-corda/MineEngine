using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.IO;
using MineEngine.Core.Json;

namespace MineEngine.Project.Workspace;

/// <summary>Projet présent dans la liste de l'accueil.</summary>
/// <param name="RootDirectory">Dossier du projet (celui qui contient Project.json).</param>
/// <param name="LastOpened">Dernière ouverture dans l'éditeur, ou null s'il n'a jamais été ouvert.</param>
public sealed record KnownProject(string RootDirectory, DateTimeOffset? LastOpened);

/// <summary>
/// Liste des projets connus de l'éditeur (accueil "Mes projets"), enregistrée dans un
/// fichier projects.json. Un fichier illisible donne une liste vide.
/// </summary>
public sealed class KnownProjectsStore
{
    private readonly string _file;

    public KnownProjectsStore(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        _file = Path.GetFullPath(file);
    }

    /// <summary>Faux tant que la liste n'a jamais été enregistrée (premier lancement).</summary>
    public bool Exists => File.Exists(_file);

    public IReadOnlyList<KnownProject> Load()
    {
        if (!Exists)
        {
            return [];
        }

        try
        {
            JsonObject root = JsonFormatting.ParseObject(File.ReadAllText(_file), "projects.json");
            var projects = new List<KnownProject>();
            foreach (JsonObject entry in (root["projects"] as JsonArray ?? []).OfType<JsonObject>())
            {
                string? path = entry["path"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                DateTimeOffset? lastOpened = DateTimeOffset.TryParse(
                    entry["lastOpened"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset date)
                    ? date
                    : null;
                projects.Add(new KnownProject(path, lastOpened));
            }

            return Distinct(projects);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or JsonException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<KnownProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        var array = new JsonArray();
        foreach (KnownProject project in Distinct(projects))
        {
            array.Add(new JsonObject
            {
                ["path"] = project.RootDirectory,
                ["lastOpened"] = project.LastOpened?.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        AtomicFile.WriteAllText(_file, JsonFormatting.ToText(new JsonObject { ["projects"] = array }));
    }

    /// <summary>Un même dossier n'apparaît qu'une fois (la plus récente ouverture l'emporte).</summary>
    private static IReadOnlyList<KnownProject> Distinct(IEnumerable<KnownProject> projects) =>
        projects
            .Select(p => p with { RootDirectory = Normalize(p.RootDirectory) })
            .GroupBy(p => p.RootDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.LastOpened ?? DateTimeOffset.MinValue).First())
            .ToList();

    public static string Normalize(string directory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
}
