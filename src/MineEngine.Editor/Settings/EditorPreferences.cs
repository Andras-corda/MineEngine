using MineEngine.Editor.Services;

namespace MineEngine.Editor.Settings;

/// <summary>Préférences en cours d'utilisation : chargement, application et enregistrement.</summary>
public sealed class EditorPreferences
{
    private readonly EditorSettingsStore _store;

    public EditorPreferences(EditorSettingsStore store, IThemeService themes, IInterfaceService ui, LanguageLibrary languages)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Themes = themes ?? throw new ArgumentNullException(nameof(themes));
        Interface = ui ?? throw new ArgumentNullException(nameof(ui));
        Languages = languages ?? throw new ArgumentNullException(nameof(languages));
    }

    /// <summary>Déclenché après l'enregistrement de nouvelles préférences.</summary>
    public event EventHandler? Changed;

    public EditorSettings Current { get; private set; } = new();

    public IThemeService Themes { get; }

    /// <summary>Taille de l'interface et réglages des éditeurs.</summary>
    public IInterfaceService Interface { get; }

    /// <summary>Langues livrées et importées.</summary>
    public LanguageLibrary Languages { get; }

    /// <summary>
    /// Charge les préférences enregistrées, choisit la langue de l'interface et applique le thème.
    /// À appeler avant de créer les fenêtres : la langue ne change plus jusqu'au prochain démarrage.
    /// </summary>
    public void Load()
    {
        Current = _store.Load();
        Core.Diagnostics.EngineDebug.Enabled = Current.DebugMode;
        UseLanguage(Languages.Find(Current.LanguageCode));
        Themes.Apply(Current.ThemeId, Current.AccentColor);
        Interface.Apply(Current);
    }

    /// <summary>Remplace les préférences, les applique et les enregistre.</summary>
    public void Update(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings.Clone();
        Core.Diagnostics.EngineDebug.Enabled = Current.DebugMode;
        Themes.Apply(Current.ThemeId, Current.AccentColor);
        Interface.Apply(Current);
        _store.Save(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Mémorise un état de l'interface (barre latérale réduite...) sans rien réappliquer.
    /// </summary>
    public void Remember(Action<EditorSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        EditorSettings settings = Current.Clone();
        change(settings);
        Current = settings;
        _store.Save(Current);
    }

    /// <summary>Langue importée illisible (fichier modifié à la main) : l'interface reste en anglais.</summary>
    private static void UseLanguage(UiLanguage language)
    {
        try
        {
            Tr.Use(language);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Tr.Use(UiLanguage.Default);
        }
    }
}
