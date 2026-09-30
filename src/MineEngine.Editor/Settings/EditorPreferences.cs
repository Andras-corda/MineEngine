using MineEngine.Editor.Services;

namespace MineEngine.Editor.Settings;

/// <summary>Préférences en cours d'utilisation : chargement, application et enregistrement.</summary>
public sealed class EditorPreferences
{
    private readonly EditorSettingsStore _store;

    public EditorPreferences(EditorSettingsStore store, IThemeService themes)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Themes = themes ?? throw new ArgumentNullException(nameof(themes));
    }

    public EditorSettings Current { get; private set; } = new();

    public IThemeService Themes { get; }

    /// <summary>Charge les préférences enregistrées et applique le thème.</summary>
    public void Load()
    {
        Current = _store.Load();
        Themes.Apply(Current.Theme);
    }

    /// <summary>Remplace les préférences, les applique et les enregistre.</summary>
    public void Update(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Current = settings.Clone();
        Themes.Apply(Current.Theme);
        _store.Save(Current);
    }
}
