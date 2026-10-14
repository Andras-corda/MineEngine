namespace MineEngine.Core.Assets;

/// <summary>
/// Chemins des dossiers du Dashboard ("Armes/Épées") : purement logiques, ils servent à
/// ranger les éléments du mod dans l'éditeur et n'ont aucun effet sur le jeu ni sur
/// l'emplacement des fichiers. "" désigne la racine.
/// </summary>
public static class AssetFolders
{
    public const char Separator = '/';

    private static readonly char[] Forbidden = ['\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>Chemin nettoyé : segments sans espaces superflus, sans segments vides ; null si un nom est invalide.</summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string[] segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(s => !IsValidName(s)) ? null : string.Join(Separator, segments);
    }

    /// <summary>Nom de dossier acceptable : non vide, sans '/', sans caractère interdit par Windows.</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim() == name && name.IndexOfAny(Forbidden) < 0
        && !name.Contains(Separator) && name is not "." and not ".." && name.Length <= 64;

    public static string Combine(string parent, string name) => parent.Length == 0 ? name : parent + Separator + name;

    /// <summary>Dossier parent ("" pour un dossier de premier niveau ou la racine).</summary>
    public static string Parent(string path)
    {
        int index = path.LastIndexOf(Separator);
        return index < 0 ? string.Empty : path[..index];
    }

    /// <summary>Dernier segment du chemin ("Épées" pour "Armes/Épées").</summary>
    public static string Name(string path) => path[(path.LastIndexOf(Separator) + 1)..];

    /// <summary>Vrai si <paramref name="path"/> est <paramref name="folder"/> ou l'un de ses sous-dossiers.</summary>
    public static bool IsWithin(string path, string folder) =>
        folder.Length == 0
        || string.Equals(path, folder, StringComparison.Ordinal)
        || path.StartsWith(folder + Separator, StringComparison.Ordinal);

    /// <summary>Remplace le début <paramref name="oldFolder"/> d'un chemin par <paramref name="newFolder"/> (renommage, déplacement).</summary>
    public static string Rebase(string path, string oldFolder, string newFolder) =>
        IsWithin(path, oldFolder) ? newFolder + path[oldFolder.Length..] : path;

    /// <summary>Un chemin et tous ses dossiers parents ("A/B/C" donne "A", "A/B", "A/B/C").</summary>
    public static IEnumerable<string> WithAncestors(string path)
    {
        if (path.Length == 0)
        {
            yield break;
        }

        string[] segments = path.Split(Separator);
        for (int i = 1; i <= segments.Length; i++)
        {
            yield return string.Join(Separator, segments[..i]);
        }
    }
}
