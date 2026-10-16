using MineEngine.Core.Theming;

namespace MineEngine.Tests;

/// <summary>Thèmes livrés, couleur d'accent, fichiers de thème et bibliothèque des thèmes importés.</summary>
public sealed class ThemingTests
{
    [Fact]
    public void Built_in_themes_resolve_every_color()
    {
        foreach (ThemeDefinition theme in BuiltInThemes.All)
        {
            IReadOnlyDictionary<string, string> colors = theme.Resolve();
            Assert.All(ThemeColorKeys.All, key => Assert.True(colors.ContainsKey(key), $"{theme.Id} : {key} manquant"));
            Assert.All(colors.Values, value => Assert.True(RgbColor.TryParse(value, out _), $"{theme.Id} : {value} invalide"));
        }

        Assert.Equal(ThemeColorKeys.All.Count, BuiltInThemes.Dark.Colors.Count);
        Assert.Equal(ThemeColorKeys.All.Count, BuiltInThemes.Light.Colors.Count);
        Assert.Equal(BuiltInThemes.All.Count, BuiltInThemes.All.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void Partial_theme_takes_missing_colors_from_its_base()
    {
        IReadOnlyDictionary<string, string> contrast = BuiltInThemes.HighContrast.Resolve();
        Assert.Equal("#000000", contrast["HubBackgroundBrush"]);
        Assert.Equal(BuiltInThemes.Dark.Colors["LogSuccessBrush"], contrast["LogSuccessBrush"]);
    }

    [Fact]
    public void Palette_themes_use_their_own_colors_and_accent()
    {
        IReadOnlyDictionary<string, string> gold = BuiltInThemes.Gold.Resolve();
        Assert.Equal("#19171B", gold["HubBackgroundBrush"]);
        Assert.Equal("#2F2921", gold["CardBrush"]);
        Assert.Equal("#9E8123", gold["AccentBrush"]);
        Assert.Equal(ThemeColorKeys.All.Count, BuiltInThemes.Purple.Colors.Count);
        Assert.NotEqual(BuiltInThemes.Red.Colors["NavSelectedBrush"], BuiltInThemes.Blue.Colors["NavSelectedBrush"]);
    }

    [Fact]
    public void Colors_parse_and_mix()
    {
        Assert.True(RgbColor.TryParse("#2f7fd8", out RgbColor color));
        Assert.Equal("#2F7FD8", color.ToString());
        Assert.True(RgbColor.TryParse("FFFFFF", out _));
        Assert.False(RgbColor.TryParse("#12345", out _));
        Assert.False(RgbColor.TryParse("#GGGGGG", out _));
        Assert.Equal(new RgbColor(128, 128, 128), new RgbColor(0, 0, 0).Mix(new RgbColor(255, 255, 255), 0.5));
    }

    [Fact]
    public void Accent_replaces_every_accent_color()
    {
        var green = new RgbColor(0x2E, 0xA0, 0x5A);
        IReadOnlyDictionary<string, string> colors = BuiltInThemes.Dark.Resolve(green);
        Assert.Equal("#2EA05A", colors["AccentBrush"]);
        Assert.Equal("#2EA05A", colors["AccentButtonBackground"]);
        Assert.All(ThemeColorKeys.Accent, key => Assert.NotEqual(BuiltInThemes.Dark.Colors[key], colors[key]));
        Assert.Equal(BuiltInThemes.Dark.Colors["HubBackgroundBrush"], colors["HubBackgroundBrush"]);

        // L'accent par défaut redonne à peu près les couleurs d'origine du thème sombre.
        IReadOnlyDictionary<string, string> blue = BuiltInThemes.Dark.Resolve(AccentColors.Default);
        Assert.Equal(BuiltInThemes.Dark.Colors["AccentBrush"], blue["AccentBrush"]);
    }

    [Fact]
    public void Theme_file_round_trips_and_ignores_unknown_keys()
    {
        string json = """
            { "formatVersion": 1, "name": "Forêt", "base": "light",
              "colors": { "AccentBrush": "#2ea05a", "UnknownBrush": "#000000" } }
            """;
        ThemeDefinition theme = ThemeFileSerializer.Read(json, "foret", "foret.json");
        Assert.Equal("Forêt", theme.Name);
        Assert.Equal(ThemeBase.Light, theme.Base);
        Assert.Equal("#2EA05A", theme.Colors["AccentBrush"]);
        Assert.False(theme.Colors.ContainsKey("UnknownBrush"));

        ThemeDefinition again = ThemeFileSerializer.Read(ThemeFileSerializer.Write(theme), "foret", "foret.json");
        Assert.Equal(theme.Resolve().OrderBy(c => c.Key), again.Resolve().OrderBy(c => c.Key));
        Assert.Equal(ThemeColorKeys.All.Count, again.Colors.Count);
    }

    [Theory]
    [InlineData("""{ "base": "dark" }""")]
    [InlineData("""{ "name": "X", "base": "blue" }""")]
    [InlineData("""{ "name": "X", "colors": { "AccentBrush": "bleu" } }""")]
    [InlineData("""{ "formatVersion": 99, "name": "X" }""")]
    [InlineData("""[1, 2]""")]
    public void Invalid_theme_files_are_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => ThemeFileSerializer.Read(json, "x", "x.json"));
    }

    [Fact]
    public void Library_imports_lists_and_removes_themes()
    {
        using var temp = new TemporaryDirectory();
        var library = new ThemeLibrary(Path.Combine(temp.Path, "themes"));
        Assert.Equal(BuiltInThemes.All.Count, library.All().Count);

        string source = Path.Combine(temp.Path, "source.json");
        File.WriteAllText(source, """{ "name": "Sombre", "base": "dark", "colors": { "CardBrush": "#101010" } }""");
        ThemeDefinition first = library.Import(source);
        ThemeDefinition second = library.Import(source);

        Assert.Equal("sombre", first.Id);
        Assert.Equal("sombre-2", second.Id);
        Assert.False(first.IsBuiltIn);
        Assert.Equal("#101010", library.Find("sombre")!.Colors["CardBrush"]);
        Assert.Equal(BuiltInThemes.All.Count + 2, library.All().Count);

        File.WriteAllText(Path.Combine(temp.Path, "themes", "broken.json"), "{ not json");
        Assert.Equal(BuiltInThemes.All.Count + 2, library.All().Count);

        library.Remove("sombre");
        Assert.Null(library.Find("sombre"));
        Assert.Throws<InvalidOperationException>(() => library.Remove(BuiltInThemes.DarkId));

        string exported = Path.Combine(temp.Path, "export.json");
        ThemeLibrary.Export(BuiltInThemes.Blue, exported, new RgbColor(0xD9, 0x45, 0x45));
        ThemeDefinition read = ThemeFileSerializer.Read(File.ReadAllText(exported), "e", "export.json");
        Assert.Equal("#D94545", read.Colors["AccentBrush"]);
        Assert.Equal("#0D1433", read.Colors["HubBackgroundBrush"]);
    }
}
