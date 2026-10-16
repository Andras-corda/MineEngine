using System.Globalization;

namespace MineEngine.Core.Localization;

/// <summary>
/// Point d'accès aux traductions de l'interface : Tr.T("Texte") pour un texte, Tr.F("{0} élément(s)", n)
/// pour un texte avec des valeurs. La langue est choisie une fois au démarrage (changer de langue
/// demande de relancer l'application) ; tant qu'aucune n'est choisie, les textes restent en français.
/// </summary>
public static class Tr
{
    public static TextCatalog Catalog { get; private set; } = TextCatalog.Source;

    public static UiLanguage Language => Catalog.Language;

    /// <summary>Choisit la langue de l'interface et la culture (dates, nombres) qui va avec.</summary>
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

    /// <summary>Traduction d'un texte de l'interface.</summary>
    public static string T(string text) => Catalog.Translate(text);

    /// <summary>
    /// Traduction d'un texte composite ({0}, {1}...), puis remplacement des valeurs. Une traduction
    /// mal formée (accolade oubliée dans un fichier de langue importé) retombe sur le texte source.
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
