using System.Windows;
using System.Windows.Media;
using MineEngine.Core.Theming;
using MineEngine.Editor.Settings;
using ModernWpf;

namespace MineEngine.Editor.Services;

/// <summary>Applique un thème à toute l'interface.</summary>
public interface IThemeService
{
    /// <summary>Thèmes livrés et importés.</summary>
    ThemeLibrary Library { get; }

    /// <summary>Identifiant du thème choisi (<see cref="EditorSettings.SystemThemeId"/> pour suivre Windows).</summary>
    string CurrentThemeId { get; }

    /// <summary>Accent choisi ; null garde celui du thème.</summary>
    RgbColor? CurrentAccent { get; }

    /// <summary>Thème effectivement affiché (celui de Windows pour le thème système).</summary>
    ThemeDefinition ActualTheme { get; }

    /// <summary>Vrai si l'interface est actuellement affichée en sombre.</summary>
    bool IsDark { get; }

    /// <summary>Déclenché après chaque changement de couleurs.</summary>
    event EventHandler? AppearanceChanged;

    /// <summary>Applique un thème ; un identifiant inconnu (thème supprimé) suit Windows.</summary>
    void Apply(string themeId, RgbColor? accent);
}

/// <summary>
/// Thème basé sur ModernWpf : son ThemeManager redessine les contrôles en clair ou en sombre
/// selon la base du thème ; les couleurs propres à Mine Engine (titres, badges, console, sélections)
/// sont construites à partir des données du thème et remplacées d'un bloc.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    private readonly Application _application;
    private ResourceDictionary? _currentPalette;

    public WpfThemeService(Application application, ThemeLibrary library)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        Library = library ?? throw new ArgumentNullException(nameof(library));
        ThemeManager.Current.ActualApplicationThemeChanged += (_, _) =>
        {
            if (CurrentThemeId == EditorSettings.SystemThemeId)
            {
                ApplyPalette();
            }
        };
    }

    public event EventHandler? AppearanceChanged;

    public ThemeLibrary Library { get; }

    public string CurrentThemeId { get; private set; } = EditorSettings.SystemThemeId;

    public RgbColor? CurrentAccent { get; private set; }

    public ThemeDefinition ActualTheme { get; private set; } = BuiltInThemes.Dark;

    public bool IsDark => ActualTheme.IsDark;

    public void Apply(string themeId, RgbColor? accent)
    {
        ThemeDefinition? theme = themeId == EditorSettings.SystemThemeId ? null : Library.Find(themeId);
        CurrentThemeId = theme?.Id ?? EditorSettings.SystemThemeId;
        CurrentAccent = accent;
        ThemeManager.Current.ApplicationTheme = theme is null ? null : theme.IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplyPalette();
    }

    private void ApplyPalette()
    {
        ActualTheme = CurrentThemeId == EditorSettings.SystemThemeId
            ? ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Light ? BuiltInThemes.Light : BuiltInThemes.Dark
            : Library.Find(CurrentThemeId) ?? BuiltInThemes.Dark;

        IReadOnlyDictionary<string, string> colors = ActualTheme.Resolve(CurrentAccent);
        var palette = new ResourceDictionary();
        foreach ((string key, string value) in colors)
        {
            RgbColor color = RgbColor.Parse(value);
            var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            palette[key] = brush;
        }

        RgbColor accent = AccentColors.Of(colors);
        ThemeManager.Current.AccentColor = Color.FromRgb(accent.R, accent.G, accent.B);

        if (_currentPalette is not null)
        {
            _application.Resources.MergedDictionaries.Remove(_currentPalette);
        }

        _application.Resources.MergedDictionaries.Add(palette);
        _currentPalette = palette;
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
