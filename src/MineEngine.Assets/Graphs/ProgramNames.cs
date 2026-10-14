using System.Globalization;
using System.Text;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Noms de programme tirés des noms saisis ("Points de vie" devient "pointsDeVie",
/// "Épée magique" devient "EpeeMagique") : sans accents ni espaces, utilisables en Java.
/// Deux noms qui donnent le même identifiant désignent la même chose (redéfinition
/// d'une méthode, doublon de variable).
/// </summary>
public static class ProgramNames
{
    /// <summary>"Points de vie" donne "pointsDeVie" (<paramref name="upperFirst"/> : "PointsDeVie") ; vide si rien n'est utilisable.</summary>
    public static string Identifier(string? text, bool upperFirst = false)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (char c in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        var result = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            bool upper = i > 0 || upperFirst;
            result.Append(upper ? char.ToUpperInvariant(word[0]) : char.ToLowerInvariant(word[0]));

            // Un mot tout en majuscules ("PV") garde ses majuscules, sinon le reste est conservé tel quel.
            result.Append(word[1..]);
        }

        string identifier = result.ToString();
        return identifier.Length > 0 && char.IsAsciiDigit(identifier[0]) ? "_" + identifier : identifier;
    }

    /// <summary>"Très rare" donne "TRES_RARE" (constante d'énumération).</summary>
    public static string Constant(string? text)
    {
        string identifier = Identifier(text, upperFirst: true);
        var builder = new StringBuilder();
        for (int i = 0; i < identifier.Length; i++)
        {
            char c = identifier[i];
            if (i > 0 && char.IsAsciiLetterUpper(c) && char.IsAsciiLetterLower(identifier[i - 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}
