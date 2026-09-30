using System.Windows;
using Microsoft.Win32;
using MineEngine.Editor.Settings;

namespace MineEngine.Editor.Services;

/// <summary>Applique un thème à toute l'interface.</summary>
public interface IThemeService
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}

/// <summary>
/// Thème WPF : le thème Fluent de .NET 9 redessine les contrôles standard en clair
/// ou en sombre, et une palette propre à Mine Engine (titres, badges, couleurs de
/// la console) est échangée en même temps. En mode Système, la palette suit les
/// changements de réglage de Windows.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly Uri LightPalette = new("pack://application:,,,/MineEngine.Editor;component/Themes/LightPalette.xaml");
    private static readonly Uri DarkPalette = new("pack://application:,,,/MineEngine.Editor;component/Themes/DarkPalette.xaml");

    private readonly Application _application;
    private ResourceDictionary? _currentPalette;

    public WpfThemeService(Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public AppTheme Current { get; private set; } = AppTheme.System;

    public void Apply(AppTheme theme)
    {
        Current = theme;

#pragma warning disable WPF0001 // Le thème Fluent de WPF est encore marqué expérimental dans .NET 9.
        _application.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
#pragma warning restore WPF0001

        ApplyPalette(IsDark(theme) ? DarkPalette : LightPalette);
    }

    private void ApplyPalette(Uri source)
    {
        var palette = new ResourceDictionary { Source = source };
        if (_currentPalette is not null)
        {
            _application.Resources.MergedDictionaries.Remove(_currentPalette);
        }

        _application.Resources.MergedDictionaries.Add(palette);
        _currentPalette = palette;
    }

    private static bool IsDark(AppTheme theme) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => IsWindowsInDarkMode(),
    };

    private static bool IsWindowsInDarkMode()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int useLight && useLight == 0;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Current == AppTheme.System && e.Category == UserPreferenceCategory.General)
        {
            _application.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        }
    }
}
