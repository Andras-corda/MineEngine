using System.Net;
using System.Text.RegularExpressions;

namespace MineEngine.Tests;

/// <summary>
/// Les textes de l'interface sont écrits en français (Tr.T, Tr.F, {l:Tr} dans le XAML)
/// et chacun doit avoir sa traduction anglaise dans Localization/en.json.
/// </summary>
public sealed partial class LocalizationTests
{
    [Fact]
    public void English_is_the_default_language()
    {
        Assert.Equal(UiLanguage.English, UiLanguage.Default);
        Assert.Equal(UiLanguage.English, UiLanguage.FromCode("xx"));
        Assert.Equal(UiLanguage.French, UiLanguage.FromCode("FR"));
    }

    [Fact]
    public void French_needs_no_translation_and_English_translates()
    {
        Assert.Equal("Nouveau bloc", TextCatalog.For(UiLanguage.French).Translate("Nouveau bloc"));
        Assert.Equal("New block", TextCatalog.For(UiLanguage.English).Translate("Nouveau bloc"));
        Assert.Equal("Texte inconnu", TextCatalog.For(UiLanguage.English).Translate("Texte inconnu"));
    }

    [Fact]
    public void Every_interface_text_has_an_English_translation()
    {
        TextCatalog english = TextCatalog.For(UiLanguage.English);
        List<string> texts = [.. SourceTexts().Distinct()];
        Assert.True(texts.Count > 1000, $"Seulement {texts.Count} textes trouvés dans src : la recherche est cassée.");
        List<string> missing = [.. texts.Where(t => !english.Contains(t)).Order()];
        Assert.True(missing.Count == 0, "Textes sans traduction anglaise :\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Translations_keep_the_same_placeholders()
    {
        TextCatalog english = TextCatalog.For(UiLanguage.English);
        List<string> wrong = [.. english.TranslatedTexts.Where(t => Placeholders(t) != Placeholders(english.Translate(t)))];
        Assert.True(wrong.Count == 0, "Valeurs {0}, {1}... différentes :\n" + string.Join("\n", wrong));
    }

    private static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order());

    /// <summary>Textes passés à Tr.T / Tr.F dans le code et à {l:Tr '...'} dans le XAML.</summary>
    private static IEnumerable<string> SourceTexts()
    {
        string src = Path.Combine(RepositoryRoot(), "src");
        foreach (string file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            if (file.EndsWith(".cs", StringComparison.Ordinal) && !file.Contains("Localization"))
            {
                foreach (Match match in CodeText().Matches(File.ReadAllText(file)))
                {
                    yield return Regex.Unescape(match.Groups[1].Value);
                }
            }
            else if (file.EndsWith(".xaml", StringComparison.Ordinal))
            {
                string xaml = File.ReadAllText(file);
                foreach (Match match in XamlText().Matches(xaml))
                {
                    yield return MarkupEscape().Replace(WebUtility.HtmlDecode(match.Groups[1].Value), "$1");
                }

                foreach (Match match in XamlElementText().Matches(xaml))
                {
                    yield return WebUtility.HtmlDecode(match.Groups[1].Value);
                }
            }
        }
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MineEngine.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("MineEngine.sln introuvable.");
    }

    [GeneratedRegex("""Tr\.[TF]\(\s*"((?:[^"\\]|\\.)*)"(?=\s*[,)])""")]
    private static partial Regex CodeText();

    [GeneratedRegex(@"\{l:Tr '((?:[^'\\]|\\.)*)'\}")]
    private static partial Regex XamlText();

    [GeneratedRegex("""<l:Tr Text="([^"]*)" />""")]
    private static partial Regex XamlElementText();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex MarkupEscape();

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
