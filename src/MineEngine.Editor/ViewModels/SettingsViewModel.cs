using System.Windows.Input;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Settings;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Fenêtre Paramètres de l'éditeur. Le thème choisi est appliqué immédiatement
/// en aperçu ; il n'est enregistré qu'à la validation, sinon l'ancien revient.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly EditorPreferences _preferences;
    private readonly EditorSettings _draft;
    private bool _saved;

    public SettingsViewModel(EditorPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _draft = preferences.Current.Clone();
        SaveCommand = new RelayCommand(Save);
    }

    /// <summary>Demande de fermeture de la fenêtre (vrai = paramètres enregistrés).</summary>
    public event EventHandler<bool>? CloseRequested;

    public ICommand SaveCommand { get; }

    public bool IsSystemTheme
    {
        get => _draft.Theme == AppTheme.System;
        set => SelectThemeIf(value, AppTheme.System);
    }

    public bool IsLightTheme
    {
        get => _draft.Theme == AppTheme.Light;
        set => SelectThemeIf(value, AppTheme.Light);
    }

    public bool IsDarkTheme
    {
        get => _draft.Theme == AppTheme.Dark;
        set => SelectThemeIf(value, AppTheme.Dark);
    }

    /// <summary>Minutes entre deux sauvegardes automatiques ; 0 les désactive (double pour le NumberBox).</summary>
    public double AutoSaveMinutes
    {
        get => _draft.AutoSaveMinutes;
        set
        {
            _draft.AutoSaveMinutes = double.IsNaN(value) ? 0 : (int)Math.Round(value);
            OnPropertyChanged();
        }
    }

    public double BackupsToKeep
    {
        get => _draft.BackupsToKeep;
        set
        {
            _draft.BackupsToKeep = double.IsNaN(value) ? 1 : (int)Math.Round(value);
            OnPropertyChanged();
        }
    }

    public int MaxAutoSaveMinutes => EditorSettings.MaxAutoSaveMinutes;

    public int MaxBackupsToKeep => EditorSettings.MaxBackupsToKeep;

    /// <summary>À appeler à la fermeture : annule l'aperçu si rien n'a été enregistré.</summary>
    public void DiscardPreview()
    {
        if (!_saved && _preferences.Themes.Current != _preferences.Current.Theme)
        {
            _preferences.Themes.Apply(_preferences.Current.Theme);
        }
    }

    private void SelectThemeIf(bool selected, AppTheme theme)
    {
        if (!selected || _draft.Theme == theme)
        {
            return;
        }

        _draft.Theme = theme;
        _preferences.Themes.Apply(theme);
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void Save()
    {
        _preferences.Update(_draft);
        _saved = true;
        CloseRequested?.Invoke(this, true);
    }
}
