using System.Net;
using System.Text.RegularExpressions;

namespace MineEngine.Tests;

/// <summary>
/// Interface texts are written in English (Tr.T, Tr.F, {l:Tr} in XAML) and each one must have
/// its French translation in Localization/fr.json.
/// </summary>
public sealed partial class LocalizationTests
{
    [Fact]
    public void English_is_the_default_language()
    {
        Assert.Equal(UiLanguage.English, UiLanguage.Default);
        Assert.Equal(UiLanguage.English, UiLanguage.FromCode("xx"));
        Assert.Equal(UiLanguage.French, UiLanguage.FromCode("FR"));
        Assert.True(UiLanguage.English.IsSource);
    }

    [Fact]
    public void English_needs_no_translation_and_French_translates()
    {
        Assert.Equal("New item", TextCatalog.For(UiLanguage.English).Translate("New item"));
        Assert.Equal("Nouvel item", TextCatalog.For(UiLanguage.French).Translate("New item"));
        Assert.Equal("Unknown text", TextCatalog.For(UiLanguage.French).Translate("Unknown text"));
    }

    [Fact]
    public void Language_files_keyed_by_former_French_texts_get_English_keys()
    {
        Dictionary<string, string> converted = TextCatalog.ToEnglishKeys([KeyValuePair.Create("Nouvel item", "Neues Item")]);

        Assert.Equal("Neues Item", converted["New item"]);
    }

    [Fact]
    public void Every_interface_text_has_a_French_translation()
    {
        TextCatalog french = TextCatalog.For(UiLanguage.French);
        List<string> texts = [.. SourceTexts().Distinct()];
        Assert.True(texts.Count > 1000, $"Only {texts.Count} texts found in src: the search is broken.");
        List<string> missing = [.. texts.Where(t => !french.Contains(t)).Order()];
        Assert.True(missing.Count == 0, "Texts without a French translation:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Translations_keep_the_same_placeholders()
    {
        TextCatalog french = TextCatalog.For(UiLanguage.French);
        List<string> wrong = [.. french.TranslatedTexts.Where(t => Placeholders(t) != Placeholders(french.Translate(t)))];
        Assert.True(wrong.Count == 0, "Different {0}, {1}... values:" + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    private static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order());

    /// <summary>Texts passed to Tr.T / Tr.F in the code and to {l:Tr '...'} in XAML.</summary>
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

        throw new DirectoryNotFoundException("MineEngine.sln not found.");
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
