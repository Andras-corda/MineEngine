using System.Windows;
using MineEngine.Editor.Settings;
using ModernWpf;

namespace MineEngine.Editor.Services;

/// <summary>Applique un thème à toute l'interface.</summary>
public interface IThemeService
{
    /// <summary>Thème choisi (Identique à Windows, Clair ou Sombre).</summary>
    AppTheme Current { get; }

    /// <summary>Vrai si l'interface est actuellement affichée en sombre.</summary>
    bool IsDark { get; }

    /// <summary>Déclenché quand l'interface passe effectivement de clair à sombre ou inversement.</summary>
    event EventHandler? AppearanceChanged;

    void Apply(AppTheme theme);
}

/// <summary>
/// Thème basé sur ModernWpf : son ThemeManager redessine les contrôles en clair ou
/// en sombre (ou suit Windows) ; la palette propre à Mine Engine (titres, badges,
/// couleurs de la console) est échangée en même temps.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    private static readonly Uri LightPalette = new("pack://application:,,,/MineEngine.Editor;component/Themes/LightPalette.xaml");
    private static readonly Uri DarkPalette = new("pack://application:,,,/MineEngine.Editor;component/Themes/DarkPalette.xaml");

    /// <summary>Couleur d'accent de Mine Engine (lilas), indépendante de celle de Windows.</summary>
    private static readonly System.Windows.Media.Color AccentColor = System.Windows.Media.Color.FromRgb(0xA8, 0x55, 0xB5);

    private readonly Application _application;
    private ResourceDictionary? _currentPalette;

    public WpfThemeService(Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        ThemeManager.Current.AccentColor = AccentColor;
        ThemeManager.Current.ActualApplicationThemeChanged += (_, _) => ApplyPalette();
    }

    public event EventHandler? AppearanceChanged;

    public AppTheme Current { get; private set; } = AppTheme.System;

    public bool IsDark => ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark;

    public void Apply(AppTheme theme)
    {
        Current = theme;
        ThemeManager.Current.ApplicationTheme = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => null,
        };

        ApplyPalette();
    }

    private void ApplyPalette()
    {
        var palette = new ResourceDictionary { Source = IsDark ? DarkPalette : LightPalette };
        if (_currentPalette is not null)
        {
            _application.Resources.MergedDictionaries.Remove(_currentPalette);
        }

        _application.Resources.MergedDictionaries.Add(palette);
        _currentPalette = palette;
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
