using MineEngine.Core.Theming;

namespace MineEngine.Editor.Settings;

/// <summary>Préférences de l'éditeur, communes à tous les projets.</summary>
public sealed class EditorSettings
{
    /// <summary>Pseudo-thème qui suit le réglage clair ou sombre de Windows.</summary>
    public const string SystemThemeId = "system";

    public const int MaxAutoSaveMinutes = 120;
    public const int MaxBackupsToKeep = 100;
    public const double MinUiScale = 0.8;
    public const double MaxUiScale = 1.5;
    public const int MinCodeFontSize = 9;
    public const int MaxCodeFontSize = 28;

    private string _themeId = SystemThemeId;
    private string _languageCode = UiLanguage.Default.Code;
    private int _autoSaveMinutes = 5;
    private int _backupsToKeep = 10;
    private double _uiScale = 1;
    private int _codeFontSize = 13;

    /// <summary>Thème de l'interface : <see cref="SystemThemeId"/>, un thème livré ou un thème importé.</summary>
    public string ThemeId
    {
        get => _themeId;
        set => _themeId = string.IsNullOrWhiteSpace(value) ? SystemThemeId : value.Trim().ToLowerInvariant();
    }

    /// <summary>Couleur d'accent choisie ; null garde celle du thème.</summary>
    public RgbColor? AccentColor { get; set; }

    /// <summary>Code de la langue de l'interface (anglais par défaut), appliquée au démarrage.</summary>
    public string LanguageCode
    {
        get => _languageCode;
        set => _languageCode = string.IsNullOrWhiteSpace(value) ? UiLanguage.Default.Code : value.Trim().ToLowerInvariant();
    }

    /// <summary>Taille de l'interface (1 = 100 %).</summary>
    public double UiScale
    {
        get => _uiScale;
        set => _uiScale = double.IsNaN(value) ? 1 : Math.Round(Math.Clamp(value, MinUiScale, MaxUiScale), 2);
    }

    /// <summary>Taille du texte dans l'éditeur de code, en points.</summary>
    public int CodeFontSize
    {
        get => _codeFontSize;
        set => _codeFontSize = Math.Clamp(value, MinCodeFontSize, MaxCodeFontSize);
    }

    /// <summary>Retour automatique à la ligne dans l'éditeur de code.</summary>
    public bool CodeWordWrap { get; set; }

    /// <summary>Grille affichée derrière les graphes.</summary>
    public bool GraphShowGrid { get; set; } = true;

    /// <summary>Les nœuds déplacés se calent sur la grille.</summary>
    public bool GraphSnapToGrid { get; set; }

    /// <summary>Rouvre au démarrage le dernier projet ouvert.</summary>
    public bool ReopenLastProject { get; set; }

    /// <summary>
    /// Raccourcis choisis par l'utilisateur, par identifiant de commande (voir ShortcutCatalog) ;
    /// une valeur vide retire le raccourci. Les commandes absentes gardent leur raccourci par défaut.
    /// </summary>
    public IReadOnlyDictionary<string, string> Shortcuts { get; set; } = new Dictionary<string, string>();

    /// <summary>Programmes proposés dans « Ouvrir avec » (onglet Programmes des paramètres).</summary>
    public IReadOnlyList<ExternalProgram> ExternalPrograms { get; set; } = [];

    /// <summary>Barre latérale de l'accueil réduite à ses icônes.</summary>
    public bool HubSidebarCollapsed { get; set; }

    /// <summary>Intervalle de la sauvegarde automatique, en minutes ; 0 la désactive.</summary>
    public int AutoSaveMinutes
    {
        get => _autoSaveMinutes;
        set => _autoSaveMinutes = Math.Clamp(value, 0, MaxAutoSaveMinutes);
    }

    /// <summary>Nombre de sauvegardes de secours conservées par projet.</summary>
    public int BackupsToKeep
    {
        get => _backupsToKeep;
        set => _backupsToKeep = Math.Clamp(value, 1, MaxBackupsToKeep);
    }

    public EditorSettings Clone()
    {
        var clone = (EditorSettings)MemberwiseClone();
        clone.Shortcuts = new Dictionary<string, string>(Shortcuts);
        clone.ExternalPrograms = [.. ExternalPrograms];
        return clone;
    }
}
