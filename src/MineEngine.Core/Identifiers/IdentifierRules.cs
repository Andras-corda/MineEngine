using System.Globalization;
using System.Text;

namespace MineEngine.Core.Identifiers;

/// <summary>
/// Règles partagées par les identifiants Minecraft : une lettre minuscule suivie
/// de lettres minuscules, de chiffres ou de tirets bas.
/// </summary>
internal static class IdentifierRules
{
    public const string Description =
        "une lettre minuscule, puis uniquement des lettres minuscules (a-z), des chiffres ou '_'";

    public static bool IsValid(string? candidate, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.Length < minLength || candidate.Length > maxLength)
        {
            return false;
        }

        if (!IsLowerLetter(candidate[0]))
        {
            return false;
        }

        foreach (char c in candidate)
        {
            if (!IsLowerLetter(c) && !char.IsAsciiDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Transforme un texte libre ("Épée magique") en identifiant valide ("epee_magique").
    /// </summary>
    public static string Sanitize(string? text, int minLength, int maxLength, string fallback)
    {
        string normalized = (text ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        bool lastWasSeparator = false;

        foreach (char raw in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char c = char.ToLowerInvariant(raw);
            if (IsLowerLetter(c) || char.IsAsciiDigit(c))
            {
                builder.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        string result = builder.ToString().Trim('_');
        if (result.Length > 0 && !IsLowerLetter(result[0]))
        {
            result = "id_" + result;
        }

        if (result.Length > maxLength)
        {
            result = result[..maxLength].TrimEnd('_');
        }

        return IsValid(result, minLength, maxLength) ? result : fallback;
    }

    private static bool IsLowerLetter(char c) => c is >= 'a' and <= 'z';
}
