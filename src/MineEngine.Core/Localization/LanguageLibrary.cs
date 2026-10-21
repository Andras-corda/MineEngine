using MineEngine.Core.IO;

namespace MineEngine.Core.Localization;

/// <summary>
/// Available languages: those shipped with Mine Engine and those imported by the user, copied
/// into a folder as &lt;code&gt;.json.
/// </summary>
public sealed class LanguageLibrary
{
    private readonly string _directory;

    public LanguageLibrary(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    /// <summary>Shipped languages, then imported ones (by name); an unreadable file is skipped.</summary>
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

    /// <summary>Language from its code, shipped or imported; English when it no longer exists.</summary>
    public UiLanguage Find(string? code) =>
        All().FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? UiLanguage.Default;

    /// <summary>
    /// Checks a language file, then copies it into the library with English keys (it replaces an
    /// imported language with the same code); returns what was read, with the number of rejected translations.
    /// </summary>
    public LanguageFile Import(string sourceFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        LanguageFile read = LanguageFileSerializer.Read(File.ReadAllText(sourceFile), Path.GetFileName(sourceFile));
        if (UiLanguage.All.Any(l => l.Code == read.Language.Code))
        {
            throw new InvalidDataException(Tr.F("The language \"{0}\" already ships with Mine Engine: choose another code.", read.Language.Code));
        }

        if (read.Translations.Count == 0)
        {
            throw new InvalidDataException(Tr.F("{0} contains no usable translation.", Path.GetFileName(sourceFile)));
        }

        string target = FileOf(read.Language.Code);
        Directory.CreateDirectory(_directory);
        AtomicFile.WriteAllText(target, LanguageFileSerializer.Write(read.Language, TextCatalog.ToEnglishKeys(read.Translations)));
        return read with { Language = read.Language with { FilePath = target } };
    }

    /// <summary>Removes an imported language; shipped languages cannot be removed.</summary>
    public void Remove(UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (language.IsBuiltIn)
        {
            throw new InvalidOperationException(Tr.T("Languages shipped with Mine Engine cannot be deleted."));
        }

        if (File.Exists(language.FilePath))
        {
            File.Delete(language.FilePath);
        }
    }

    /// <summary>
    /// Writes a translation template: every interface text with itself (English) as value, to be
    /// replaced by the translation in the new language.
    /// </summary>
    public static void ExportTemplate(string targetFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFile);
        var template = new UiLanguage("xx", "Language name", "en-US");
        AtomicFile.WriteAllText(targetFile, LanguageFileSerializer.Write(
            template, TextCatalog.SourceTexts.Select(t => KeyValuePair.Create(t, t))));
    }

    /// <summary>Share of the interface texts translated in a language (from 0 to 1).</summary>
    public static double Completeness(UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (language.IsBuiltIn)
        {
            return 1;
        }

        LanguageFile file = LanguageFileSerializer.Read(File.ReadAllText(language.FilePath!), Path.GetFileName(language.FilePath!));
        Dictionary<string, string> translations = TextCatalog.ToEnglishKeys(file.Translations);
        List<string> texts = [.. TextCatalog.SourceTexts];
        return texts.Count == 0 ? 1 : (double)texts.Count(translations.ContainsKey) / texts.Count;
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
