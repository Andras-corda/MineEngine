using System.Text.RegularExpressions;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>Nature d'une entrée de la structure (pour l'icône).</summary>
public enum OutlineKind
{
    Type,
    Method,
    Field,
    Property,
    Heading,
    Section,

    /// <summary>Nœud événement d'un graphe.</summary>
    Event,
}

/// <summary>Une entrée de la section Structure de l'explorateur : symbole et endroit où il se trouve.</summary>
/// <param name="Name">Nom affiché.</param>
/// <param name="Kind">Nature du symbole.</param>
/// <param name="Line">Ligne (à partir de 1) dans un fichier de code.</param>
/// <param name="Depth">Niveau d'imbrication, pour l'indentation.</param>
/// <param name="NodeId">Nœud visé dans un graphe, sinon null.</param>
/// <param name="Hint">Texte affiché à droite à la place du numéro de ligne.</param>
public sealed record OutlineItem(string Name, OutlineKind Kind, int Line, int Depth, Guid? NodeId = null, string? Hint = null)
{
    /// <summary>Icône du symbole (clé de Themes/Icons.xaml).</summary>
    public string Icon => Kind switch
    {
        OutlineKind.Type => "ClassGeometry",
        OutlineKind.Method => "FunctionGeometry",
        OutlineKind.Field or OutlineKind.Property => "Glyph.Field",
        OutlineKind.Event => "Glyph.Event",
        _ => "Glyph.File",
    };

    public string LineLabel => Hint ?? Line.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Structure d'un fichier, extraite par des expressions simples (pas une vraie analyse
/// syntaxique) : classes et méthodes Java, clés JSON de premier niveau, titres Markdown,
/// sections TOML.
/// </summary>
public static partial class OutlineBuilder
{
    private const int MaxItems = 300;

    public static IReadOnlyList<OutlineItem> Build(string extension, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] lines = text.Split('\n');
        IEnumerable<OutlineItem> items = extension.ToLowerInvariant() switch
        {
            ".java" or ".gradle" => Java(lines),
            ".json" or ".mcmeta" => Json(lines),
            ".md" => Markdown(lines),
            ".toml" or ".ini" or ".cfg" => Toml(lines),
            ".properties" => Properties(lines),
            _ => [],
        };
        return items.Take(MaxItems).ToList();
    }

    private static IEnumerable<OutlineItem> Java(string[] lines)
    {
        int depth = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal) && !trimmed.StartsWith('*'))
            {
                if (JavaType().Match(line) is { Success: true } type)
                {
                    yield return new OutlineItem(type.Groups["name"].Value, OutlineKind.Type, i + 1, depth);
                }
                else if (depth > 0 && JavaMethod().Match(line) is { Success: true } method && !IsKeyword(method.Groups["name"].Value))
                {
                    yield return new OutlineItem(method.Groups["name"].Value + "()", OutlineKind.Method, i + 1, depth);
                }
                else if (depth == 1 && JavaField().Match(line) is { Success: true } field)
                {
                    yield return new OutlineItem(field.Groups["name"].Value, OutlineKind.Field, i + 1, depth);
                }
            }

            depth = Math.Max(0, depth + line.Count(c => c == '{') - line.Count(c => c == '}'));
        }
    }

    private static IEnumerable<OutlineItem> Json(string[] lines)
    {
        int depth = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (depth == 1 && JsonKey().Match(lines[i]) is { Success: true } key)
            {
                yield return new OutlineItem(key.Groups["name"].Value, OutlineKind.Property, i + 1, 0);
            }

            depth = Math.Max(0, depth + lines[i].Count(c => c is '{' or '[') - lines[i].Count(c => c is '}' or ']'));
        }
    }

    private static IEnumerable<OutlineItem> Markdown(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (MarkdownHeading().Match(lines[i]) is { Success: true } heading)
            {
                yield return new OutlineItem(heading.Groups["name"].Value.Trim(), OutlineKind.Heading, i + 1, heading.Groups["level"].Length - 1);
            }
        }
    }

    private static IEnumerable<OutlineItem> Toml(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (TomlSection().Match(lines[i]) is { Success: true } section)
            {
                yield return new OutlineItem(section.Groups["name"].Value, OutlineKind.Section, i + 1, 0);
            }
        }
    }

    private static IEnumerable<OutlineItem> Properties(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (PropertyKey().Match(lines[i]) is { Success: true } key)
            {
                yield return new OutlineItem(key.Groups["name"].Value, OutlineKind.Property, i + 1, 0);
            }
        }
    }

    private static bool IsKeyword(string name) => name is "if" or "for" or "while" or "switch" or "catch" or "return" or "new" or "synchronized";

    [GeneratedRegex(@"\b(class|interface|enum|record)\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex JavaType();

    [GeneratedRegex(@"^\s*(?:(?:public|protected|private|static|final|abstract|synchronized|default)\s+)*[\w<>\[\],.?\s]+\s+(?<name>[A-Za-z_]\w*)\s*\([^;]*$")]
    private static partial Regex JavaMethod();

    [GeneratedRegex(@"^\s*(?:(?:public|protected|private|static|final)\s+)+[\w<>\[\],.?\s]+\s+(?<name>[A-Za-z_]\w*)\s*(=|;)")]
    private static partial Regex JavaField();

    [GeneratedRegex("^\\s*\"(?<name>[^\"]+)\"\\s*:")]
    private static partial Regex JsonKey();

    [GeneratedRegex(@"^(?<level>#{1,6})\s+(?<name>.+)$")]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"^\s*\[+(?<name>[^\]]+)\]+")]
    private static partial Regex TomlSection();

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z_][\w.\-]*)\s*=")]
    private static partial Regex PropertyKey();
}
