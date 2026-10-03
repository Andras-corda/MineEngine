namespace MineEngine.Editor.Settings;

/// <summary>Thème de l'interface.</summary>
public enum AppTheme
{
    /// <summary>Suit le réglage clair ou sombre de Windows.</summary>
    System,
    Light,
    Dark,
}

/// <summary>Préférences de l'éditeur, communes à tous les projets.</summary>
public sealed class EditorSettings
{
    public const int MaxAutoSaveMinutes = 120;
    public const int MaxBackupsToKeep = 100;

    private int _autoSaveMinutes = 5;
    private int _backupsToKeep = 10;

    public AppTheme Theme { get; set; } = AppTheme.System;

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

    public EditorSettings Clone() => new()
    {
        Theme = Theme,
        AutoSaveMinutes = AutoSaveMinutes,
        BackupsToKeep = BackupsToKeep,
    };
}
