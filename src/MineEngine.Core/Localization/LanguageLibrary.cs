using MineEngine.Core.IO;

namespace MineEngine.Core.Localization;

/// <summary>
/// Langues disponibles : celles livrées avec Mine Engine et celles importées par l'utilisateur,
/// copiées dans un dossier sous la forme &lt;code&gt;.json.
/// </summary>
public sealed class LanguageLibrary
{
    private readonly string _directory;

    public LanguageLibrary(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <summary>Langues livrées puis langues importées (par nom) ; un fichier illisible est ignoré.</summary>
    public IReadOnlyList<UiLanguage> All()
    {
        var languages = new List<UiLanguage>(UiLanguage.All);
        if (!Directory.Exists(_directory))
        {
            return languages;
        }

        var imported = new List<UiLanguage>();
        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            if (TryRead(file) is { } language && !UiLanguage.All.Any(l => l.Code == language.Code))
            {
                imported.Add(language);
            }
        }

        languages.AddRange(imported.OrderBy(l => l.NativeName, StringComparer.CurrentCultureIgnoreCase));
        return languages;
    }

    /// <summary>Langue d'après son code, livrée ou importée ; l'anglais si elle n'existe plus.</summary>
    public UiLanguage Find(string? code) =>
        All().FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? UiLanguage.Default;

    /// <summary>
    /// Vérifie un fichier de langue puis le copie dans la bibliothèque (il remplace une langue
    /// importée de même code) ; renvoie le contenu lu, avec le nombre de traductions écartées.
    /// </summary>
    public LanguageFile Import(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        LanguageFile read = LanguageFileSerializer.Read(File.ReadAllText(sourceFile), Path.GetFileName(sourceFile));
        if (UiLanguage.All.Any(l => l.Code == read.Language.Code))
        {
            throw new InvalidDataException(Tr.F("La langue « {0} » est déjà livrée avec Mine Engine : choisissez un autre code.", read.Language.Code));
        }

        if (read.Translations.Count == 0)
        {
            throw new InvalidDataException(Tr.F("{0} ne contient aucune traduction utilisable.", Path.GetFileName(sourceFile)));
        }

        string target = FileOf(read.Language.Code);
        Directory.CreateDirectory(_directory);
        AtomicFile.WriteAllText(target, LanguageFileSerializer.Write(read.Language, read.Translations));
        return read with { Language = read.Language with { FilePath = target } };
    }

    /// <summary>Retire une langue importée ; les langues livrées ne peuvent pas être retirées.</summary>
    public void Remove(UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (language.IsBuiltIn)
        {
            throw new InvalidOperationException(Tr.T("Les langues livrées avec Mine Engine ne peuvent pas être supprimées."));
        }

        if (File.Exists(language.FilePath))
        {
            File.Delete(language.FilePath);
        }
    }

    /// <summary>
    /// Écrit un modèle de traduction : chaque texte de l'interface avec sa version anglaise,
    /// à remplacer par la traduction dans la nouvelle langue.
    /// </summary>
    public static void ExportTemplate(string targetFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFile);
        TextCatalog english = TextCatalog.For(UiLanguage.English);
        var template = new UiLanguage("xx", "Language name", "en-US");
        AtomicFile.WriteAllText(targetFile, LanguageFileSerializer.Write(
            template, english.TranslatedTexts.Select(t => KeyValuePair.Create(t, english.Translate(t)))));
    }

    /// <summary>Part des textes de l'interface traduits dans une langue (de 0 à 1).</summary>
    public static double Completeness(UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (language.IsBuiltIn)
        {
            return 1;
        }

        TextCatalog english = TextCatalog.For(UiLanguage.English);
        LanguageFile file = LanguageFileSerializer.Read(File.ReadAllText(language.FilePath!), Path.GetFileName(language.FilePath!));
        int translated = english.TranslatedTexts.Count(file.Translations.ContainsKey);
        return english.Count == 0 ? 1 : (double)translated / english.Count;
    }

    private static UiLanguage? TryRead(string file)
    {
        try
        {
            return LanguageFileSerializer.Read(File.ReadAllText(file), Path.GetFileName(file), file).Language;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    private string FileOf(string code) => Path.Combine(_directory, code + ".json");
}
