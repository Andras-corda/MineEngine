using System.Reflection;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace MineEngine.Editor.Views.Documents;

/// <summary>
/// Coloration syntaxique des onglets de code, adaptée au thème : les définitions
/// fournies par AvalonEdit sont relues et leurs couleurs remplacées par une palette
/// lisible en clair comme en sombre (celle d'origine est prévue pour un fond blanc).
/// </summary>
public static class CodeHighlighting
{
    private const string ResourcePrefix = "ICSharpCode.AvalonEdit.Highlighting.Resources.";

    private static readonly Dictionary<string, string> ResourceByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Java"] = "Java-Mode.xshd",
        ["Json"] = "Json.xshd",
        ["MarkDown"] = "MarkDown-Mode.xshd",
        ["XML"] = "XML-Mode.xshd",
        ["PowerShell"] = "PowerShell.xshd",
        ["Python"] = "Python-Mode.xshd",
    };

    private static readonly Dictionary<(string Name, bool Dark), IHighlightingDefinition?> Cache = [];

    private static bool _isDark = true;

    /// <summary>Déclenché quand le thème change : les éditeurs ouverts rechargent leur coloration.</summary>
    public static event EventHandler? ThemeChanged;

    public static bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark != value)
            {
                _isDark = value;
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    /// <summary>Définition pour un nom de coloration ("Java", "Json"...), ou null pour du texte brut.</summary>
    public static IHighlightingDefinition? Get(string? name)
    {
        if (name is null || !ResourceByName.TryGetValue(name, out string? resource))
        {
            return null;
        }

        if (!Cache.TryGetValue((name, _isDark), out IHighlightingDefinition? definition))
        {
            definition = Load(resource, _isDark);
            Cache[(name, _isDark)] = definition;
        }

        return definition;
    }

    private static IHighlightingDefinition? Load(string resource, bool dark)
    {
        Assembly assembly = typeof(HighlightingManager).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourcePrefix + resource);
        if (stream is null)
        {
            return null;
        }

        using var reader = new XmlTextReader(stream);
        XshdSyntaxDefinition xshd = HighlightingLoader.LoadXshd(reader);
        foreach (XshdColor color in xshd.Elements.OfType<XshdColor>())
        {
            if (ColorFor(color.Name, dark) is { } replacement)
            {
                color.Foreground = new SimpleHighlightingBrush(replacement);
                color.Background = null;
            }
        }

        return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
    }

    /// <summary>Couleur selon le rôle deviné d'après le nom de la couleur dans la définition.</summary>
    private static Color? ColorFor(string? name, bool dark)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        (string Dark, string Light)? pair = n switch
        {
            _ when n.Contains("comment") => ("#6E7681", "#6A737D"),
            _ when n.Contains("string") || n.Contains("char") || n.Contains("code") => ("#E2A8E9", "#A1508F"),
            _ when n.Contains("digit") || n.Contains("number") => ("#B5CEA8", "#098658"),
            _ when n.Contains("method") => ("#DCDCAA", "#795E26"),
            _ when n.Contains("fieldname") || n.Contains("type") || n.Contains("attribute") => ("#82AAFF", "#1E5BB8"),
            _ when n.Contains("heading") || n.Contains("keyword") || n.Contains("modifier") || n.Contains("visibility")
                   || n.Contains("this") || n.Contains("null") || n.Contains("bool") || n.Contains("literal")
                   || n.Contains("intrinsic") || n.Contains("tag") || n.Contains("preprocessor") => ("#D493DA", "#8E3F99"),
            _ when n.Contains("punctuation") || n.Contains("operator") => ("#C8C8D0", "#4B4B57"),
            _ => null,
        };

        return pair is { } colors ? (Color)ColorConverter.ConvertFromString(dark ? colors.Dark : colors.Light) : null;
    }
}
