namespace MineEngine.Core.Assets;

public static class AssetFolders
{
    public const char Separator = '/'; // pour separer les noms dans le dir

    private static readonly char[] Forbidden = ['\\', ':', '*', '?', '"', '<', '>', '|']; // listes les characteres interdit dans un dir

    // dir nettoyé, null si un nom est invalide
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string[] segments = path.Split(
            Separator,
            (StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        );

        // Si le nom est invalide alors revois null
        foreach (string segment in segments)
        {
            if (!IsValidName(segment))
            {
                return null;
            }
        }

        return string.Join(Separator, segments);
    }

    // Checks if a folder name is valid
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name.Trim() != name)
            return false;

        if (name.IndexOfAny(Forbidden) >= 0)
            return false;

        if (name.Contains(Separator))
            return false;

        if (name == "." || name == "..")
            return false;

        if (name.Length > 64)
            return false;

        return true;
    }

    public static string Combine(string parent, string name) {
        if(parent.Length == 0){
            return name
        } else {
            return parent + Separator + name;
        }
    }

    // Parent du dir
    public static string Parent(string path)
    {
        int index = path.LastIndexOf(Separator);

        if (index < 0)
        {
            return string.Empty;
        }
        else
        {
            return path.Substring(0, index);
        }
    }

    // Dernier segment du dir 
    public static string Name(string path) 
    {
        return path[(path.LastIndexOf(Separator) + 1)..];
    }

    // verifie si un dir se trouve dans un dossier ou correspond au dossier lui meme
    public static bool IsWithin(string path, string folder)
    {
        // si le dossier est vide, tous les chemins sont consideres comme etant dedans
        if (folder.Length == 0)
            return true;

        // verifie si le chemin est egal au dossier
        if (string.Equals(path, folder, StringComparison.Ordinal))
            return true;

        // verifie si le chemin commence par le nom du dossier + separateur
        if (path.StartsWith(folder + Separator, StringComparison.Ordinal))
            return true;

        // le chemin n'est pas dedans
        return false;
    }

    // remplace le début d'un dir 
    public static string Rebase(string path, string oldFolder, string newFolder)
    {
        // verifie si le dir se trouve dans l'ancien dossier
        if (IsWithin(path, oldFolder))
        {
            return newFolder + path[oldFolder.Length..];
        }

        // Si le dir n'est pas dans l'ancien dossier, on le retourne sans modification
        return path;
    }

    // un dir et tous ses dossiers parents (A/B/C -> A, A/B, A/B/C)
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