using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Identifiers;
using MineEngine.Project;
using MineEngine.Project.Localization;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.5.9 : langues du mod et traductions (onglet Langues), enregistrées avec le projet.</summary>
public sealed class ModLocalizationTests
{
    [Fact]
    public void Locales_are_validated_and_normalized()
    {
        Assert.True(ModLocalization.IsValidLocale("de_de"));
        Assert.True(ModLocalization.IsValidLocale("pt_br"));
        Assert.False(ModLocalization.IsValidLocale("Deutsch"));
        Assert.False(ModLocalization.IsValidLocale("de"));

        ModLocalization localization = ModLocalization.Default.WithLocale("DE-de");
        Assert.Equal(new[] { "en_us", "fr_fr", "de_de" }, localization.Locales.ToArray());
        Assert.Throws<ArgumentException>(() => ModLocalization.Default.WithLocale("allemand"));
    }

    [Fact]
    public void An_empty_translation_restores_the_default_text()
    {
        ModLocalization localization = ModLocalization.Default.WithTranslation("fr_fr", "item.mod.ruby", "Rubis rouge");
        Assert.Equal("Rubis rouge", localization.Find("fr_fr", "item.mod.ruby"));

        localization = localization.WithTranslation("fr_fr", "item.mod.ruby", string.Empty);
        Assert.Null(localization.Find("fr_fr", "item.mod.ruby"));
        Assert.True(localization.Translations.IsEmpty);
    }

    [Fact]
    public void Removing_a_locale_removes_its_translations()
    {
        ModLocalization localization = ModLocalization.Default.WithTranslations("de_de", [KeyValuePair.Create("item.mod.ruby", "Rubin")]);
        Assert.Contains("de_de", localization.Locales);

        localization = localization.WithoutLocale("de_de");
        Assert.DoesNotContain("de_de", localization.Locales);
        Assert.Empty(localization.TranslationsFor("de_de"));
    }

    [Fact]
    public void Languages_are_saved_with_the_project_only_when_they_differ_from_the_defaults()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("langmod"), "Lang Mod"));
        string file = Path.Combine(project.Layout.RootDirectory, ModLocalizationSerializer.FileName);
        Assert.False(File.Exists(file));

        project.Localization = project.Localization.WithLocale("es_es").WithTranslation("es_es", "itemGroup.langmod", "Mod de idiomas");
        repository.Save(project);
        Assert.True(File.Exists(file));

        ModProject reopened = repository.Open(project.Layout.RootDirectory);
        Assert.Equal(new[] { "en_us", "fr_fr", "es_es" }, reopened.Localization.Locales.ToArray());
        Assert.Equal("Mod de idiomas", reopened.Localization.Find("es_es", "itemGroup.langmod"));

        reopened.Localization = ModLocalization.Default;
        repository.Save(reopened);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Minecraft_language_files_round_trip()
    {
        string json = ModLocalizationSerializer.WriteLanguageFile([KeyValuePair.Create("item.mod.b", "B"), KeyValuePair.Create("item.mod.a", "A")]);
        Assert.True(json.IndexOf("item.mod.a", StringComparison.Ordinal) < json.IndexOf("item.mod.b", StringComparison.Ordinal));

        var read = ModLocalizationSerializer.ReadLanguageFile(json, "de_de.json");
        Assert.Equal("A", read["item.mod.a"]);
        Assert.Throws<InvalidDataException>(() => ModLocalizationSerializer.ReadLanguageFile("[1]", "x.json"));
    }

    [Fact]
    public void Translation_keys_cover_names_tooltips_spawn_eggs_and_subtitles()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("keys"), "Keys"));
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis") { TooltipLines = ["Brille", "Rare"] });
        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("golem"), "Golem") { HasSpawnEgg = true });

        IReadOnlyList<TranslationEntry> entries = ModTranslationKeys.Collect(project);

        Assert.Contains(entries, e => e.Key == "itemGroup.keys" && e.Text == "Keys");
        Assert.Contains(entries, e => e.Key == "item.keys.ruby" && e.Text == "Rubis");
        Assert.Contains(entries, e => e.Key == "tooltip.keys.ruby.1" && e.Text == "Rare");
        Assert.Contains(entries, e => e.Key == "entity.keys.golem");
        TranslationEntry egg = Assert.Single(entries, e => e.Source == TranslationSource.SpawnEgg);
        Assert.Equal("Golem Spawn Egg", egg.DefaultFor("en_us"));
        Assert.Equal("Œuf d'apparition de Golem", egg.DefaultFor("fr_fr"));
    }
}
