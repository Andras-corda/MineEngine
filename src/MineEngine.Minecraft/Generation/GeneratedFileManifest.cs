using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.IO;

namespace MineEngine.Minecraft.Generation;

/// <summary>
/// Liste des fichiers produits par la dernière génération, rangée dans
/// .mineengine/generated.json. Elle distingue le code généré (réécrit à chaque
/// compilation, supprimé quand il n'est plus produit) du code écrit par l'utilisateur,
/// auquel Mine Engine ne touche jamais.
/// </summary>
public sealed class GeneratedFileManifest
{
    public const string RelativePath = ".mineengine/generated.json";

    private readonly HashSet<string> _files;

    public GeneratedFileManifest(IEnumerable<string> files)
    {
        _files = new HashSet<string>((files ?? []).Select(Normalize), StringComparer.OrdinalIgnoreCase);
    }

    public static GeneratedFileManifest Empty { get; } = new([]);

    /// <summary>Chemins relatifs au workspace, avec des '/'.</summary>
    public IReadOnlyCollection<string> Files => _files;

    public bool Contains(string relativePath) => _files.Contains(Normalize(relativePath));

    public static string PathIn(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Lit la liste d'un workspace ; vide si elle n'existe pas ou est illisible.</summary>
    public static GeneratedFileManifest Load(string workspaceDirectory)
    {
        string file = PathIn(workspaceDirectory);
        if (!File.Exists(file))
        {
            return Empty;
        }

        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllText(file));
            return root?["files"] is JsonArray files
                ? new GeneratedFileManifest(files.Select(f => f?.GetValue<string>()).OfType<string>())
                : Empty;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            return Empty;
        }
    }

    public void Save(string workspaceDirectory)
    {
        string file = PathIn(workspaceDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var root = new JsonObject
        {
            ["files"] = new JsonArray([.. _files.Order(StringComparer.Ordinal).Select(f => (JsonNode?)JsonValue.Create(f))]),
        };
        AtomicFile.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string Normalize(string path) => path.Replace('\\', '/').Trim().TrimStart('/');
}
