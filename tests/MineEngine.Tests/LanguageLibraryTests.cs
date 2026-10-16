namespace MineEngine.Tests;

/// <summary>Langues importées : format du fichier, repli sur l'anglais, modèle de traduction.</summary>
public sealed class LanguageLibraryTests
{
    private const string German = """
        { "formatVersion": 1, "code": "de", "name": "Deutsch", "culture": "de-DE",
          "translations": {
            "Nouveau bloc": "Neuer Block",
            "Déplacer {0} nœuds": "{0} Knoten verschieben",
            "Supprimer « {0} »": "Löschen",
            "Enregistrer": ""
          } }
        """;

    [Fact]
    public void Language_file_keeps_valid_translations_only()
    {
        LanguageFile file = LanguageFileSerializer.Read(German, "de.json");
        Assert.Equal("de", file.Language.Code);
        Assert.Equal("Deutsch", file.Language.NativeName);
        Assert.Equal("de-DE", file.Language.CultureName);
        Assert.Equal(2, file.Translations.Count);
        Assert.Equal(2, file.RejectedCount);
    }

    [Theory]
    [InlineData("""{ "code": "Deutsch!", "name": "Deutsch" }""")]
    [InlineData("""{ "code": "de" }""")]
    [InlineData("""{ "formatVersion": 9, "code": "de", "name": "Deutsch" }""")]
    public void Invalid_language_files_are_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => LanguageFileSerializer.Read(json, "x.json"));
    }

    [Fact]
    public void Imported_language_falls_back_to_English()
    {
        using var temp = new TemporaryDirectory();
        var library = new LanguageLibrary(Path.Combine(temp.Path, "languages"));
        string source = Path.Combine(temp.Path, "de.json");
        File.WriteAllText(source, German);

        LanguageFile imported = library.Import(source);
        UiLanguage german = library.Find("de");
        Assert.False(german.IsBuiltIn);
        Assert.Equal(imported.Language, german);
        Assert.Equal(3, library.All().Count);

        TextCatalog catalog = TextCatalog.For(german);
        Assert.Equal("Neuer Block", catalog.Translate("Nouveau bloc"));
        Assert.Equal(TextCatalog.For(UiLanguage.English).Translate("Enregistrer"), catalog.Translate("Enregistrer"));
        Assert.Equal("Texte inconnu", catalog.Translate("Texte inconnu"));

        double completeness = LanguageLibrary.Completeness(german);
        Assert.InRange(completeness, 0.0001, 0.01);
        Assert.Equal(1, LanguageLibrary.Completeness(UiLanguage.English));

        library.Remove(german);
        Assert.Equal(UiLanguage.Default, library.Find("de"));
        Assert.Throws<InvalidOperationException>(() => library.Remove(UiLanguage.French));
    }

    [Fact]
    public void Built_in_codes_cannot_be_imported()
    {
        using var temp = new TemporaryDirectory();
        var library = new LanguageLibrary(Path.Combine(temp.Path, "languages"));
        string source = Path.Combine(temp.Path, "en.json");
        File.WriteAllText(source, """{ "code": "en", "name": "English", "translations": { "Nouveau bloc": "Block" } }""");
        Assert.Throws<InvalidDataException>(() => library.Import(source));
    }

    [Fact]
    public void Template_lists_every_text_with_its_English_version()
    {
        using var temp = new TemporaryDirectory();
        string template = Path.Combine(temp.Path, "template.json");
        LanguageLibrary.ExportTemplate(template);

        LanguageFile file = LanguageFileSerializer.Read(File.ReadAllText(template), "template.json");
        TextCatalog english = TextCatalog.For(UiLanguage.English);
        Assert.Equal(english.Count, file.Translations.Count);
        Assert.Equal("New block", file.Translations["Nouveau bloc"]);
    }
}
