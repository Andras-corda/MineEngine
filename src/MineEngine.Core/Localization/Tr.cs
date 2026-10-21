using System.Globalization;

namespace MineEngine.Core.Localization;

/// <summary>
/// Access point to the interface translations: Tr.T("Text") for a text, Tr.F("{0} item(s)", n)
/// for a text with values. Texts are written in English. The language is chosen once at startup
/// (changing it requires a restart); until one is chosen, texts stay in English.
/// </summary>
public static class Tr
{
    public static TextCatalog Catalog { get; private set; } = TextCatalog.Source;

    public static UiLanguage Language => Catalog.Language;

    /// <summary>Chooses the interface language and the matching culture (dates, numbers).</summary>
    public static void Use(UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);
        Catalog = TextCatalog.For(language);
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(language.CultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo(UiLanguage.English.CultureName);
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Translation of an interface text.</summary>
    public static string T(string text) => Catalog.Translate(text);

    /// <summary>
    /// Translation of a composite text ({0}, {1}...), then the values are inserted. A malformed
    /// translation (missing brace in an imported language file) falls back to the source text.
    /// </summary>
    public static string F(string format, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, Catalog.Translate(format), args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
    }
}
