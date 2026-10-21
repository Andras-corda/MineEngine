using System.Text;
using MineEngine.Core.IO;

namespace MineEngine.Core.Theming;

/// <summary>
/// Thèmes disponibles : ceux livrés avec Mine Engine et ceux importés par l'utilisateur, copiés
/// dans un dossier (un fichier .json par thème, dont le nom sert d'identifiant).
/// </summary>
public sealed class ThemeLibrary
{
    private readonly string _directory;

    public ThemeLibrary(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    public string Directory => _directory;

    /// <summary>Thèmes livrés puis thèmes importés (par nom) ; un fichier illisible est ignoré.</summary>
    public IReadOnlyList<ThemeDefinition> All()
    {
        var themes = new List<ThemeDefinition>(BuiltInThemes.All);
        if (!System.IO.Directory.Exists(_directory))
        {
            return themes;
        }

        var imported = new List<ThemeDefinition>();
        foreach (string file in System.IO.Directory.EnumerateFiles(_directory, "*.json"))
        {
            if (TryRead(file) is { } theme)
            {
                imported.Add(theme);
            }
        }

        themes.AddRange(imported.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase));
        return themes;
    }

    /// <summary>Thème d'après son identifiant, ou null s'il n'existe plus.</summary>
    public ThemeDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        ThemeDefinition? builtIn = BuiltInThemes.All.FirstOrDefault(t => t.Id == id);
        if (builtIn is not null)
        {
            return builtIn;
        }

        string file = FileOf(id);
        return File.Exists(file) ? TryRead(file) : null;
    }

    /// <summary>Vérifie un fichier de thème puis le copie dans la bibliothèque ; renvoie le thème ajouté.</summary>
    public ThemeDefinition Import(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        string json = File.ReadAllText(sourceFile);
        ThemeDefinition read = ThemeFileSerializer.Read(json, "import", Path.GetFileName(sourceFile));
        string id = UniqueId(read.Name);
        ThemeDefinition theme = read with { Id = id };
        System.IO.Directory.CreateDirectory(_directory);
        AtomicFile.WriteAllText(FileOf(id), ThemeFileSerializer.Write(theme));
        return theme;
    }

    /// <summary>Retire un thème importé ; les thèmes livrés ne peuvent pas être retirés.</summary>
    public void Remove(string id)
    {
        if (BuiltInThemes.All.Any(t => t.Id == id))
        {
            throw new InvalidOperationException(Tr.T("Themes shipped with Mine Engine cannot be deleted."));
        }

        string file = FileOf(id);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>Exporte un thème complet (avec l'accent choisi s'il y en a un).</summary>
    public static void Export(ThemeDefinition theme, string targetFile, RgbColor? accent = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFile);
        AtomicFile.WriteAllText(targetFile, ThemeFileSerializer.Write(theme, accent));
    }

    private ThemeDefinition? TryRead(string file)
    {
        try
        {
            return ThemeFileSerializer.Read(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file), Path.GetFileName(file));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    private string FileOf(string id) => Path.Combine(_directory, id + ".json");

    /// <summary>Identifiant tiré du nom (minuscules, chiffres, tirets), sans conflit avec un thème existant.</summary>
    private string UniqueId(string name)
    {
        var slug = new StringBuilder();
        foreach (char c in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(char.ToLowerInvariant(c));
            }
            else if ((c == ' ' || c == '-' || c == '_') && slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        string baseId = slug.ToString().Trim('-');
        if (baseId.Length == 0)
        {
            baseId = "theme";
        }

        string id = baseId;
        for (int index = 2; BuiltInThemes.All.Any(t => t.Id == id) || File.Exists(FileOf(id)); index++)
        {
            id = $"{baseId}-{index}";
        }

        return id;
    }
}
