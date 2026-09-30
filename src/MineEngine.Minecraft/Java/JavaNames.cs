using System.Globalization;
using System.Text;
using MineEngine.Core.Identifiers;

namespace MineEngine.Minecraft.Java;

/// <summary>Conversion de noms libres en identifiants Java valides.</summary>
public static class JavaNames
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class", "const", "continue",
        "default", "do", "double", "else", "enum", "extends", "final", "finally", "float", "for", "goto", "if",
        "implements", "import", "instanceof", "int", "interface", "long", "native", "new", "package", "private",
        "protected", "public", "return", "short", "static", "strictfp", "super", "switch", "synchronized", "this",
        "throw", "throws", "transient", "try", "void", "volatile", "while", "true", "false", "null", "var",
        "record", "yield", "sealed", "permits", "module", "exports", "requires", "opens",
    };

    /// <summary>"magic_sword" devient "MAGIC_SWORD".</summary>
    public static string ToConstantName(ResourceId id) => id.Value.ToUpperInvariant();

    /// <summary>Segment de package tiré de l'identifiant du mod, en évitant les mots réservés.</summary>
    public static string ToPackageSegment(ModId modId) =>
        Keywords.Contains(modId.Value) ? modId.Value + "_mod" : modId.Value;

    /// <summary>"Épée magique 2" devient "EpeeMagique2" ; <paramref name="fallback"/> si rien n'est utilisable.</summary>
    public static string ToTypeName(string? text, string fallback)
    {
        string normalized = (text ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        bool capitalizeNext = true;

        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (builder.Length == 0 && char.IsAsciiDigit(c))
                {
                    continue;
                }

                builder.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        return builder.Length > 0 ? builder.ToString() : fallback;
    }

    /// <summary>Littéral de chaîne Java entre guillemets, caractères spéciaux échappés.</summary>
    public static string StringLiteral(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (char c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when c < 0x20 || c > 0x7E => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }

        return builder.Append('"').ToString();
    }

    /// <summary>Littéral float Java ("1.5f").</summary>
    public static string FloatLiteral(float value) =>
        value.ToString("0.0#######", CultureInfo.InvariantCulture) + "f";
}
