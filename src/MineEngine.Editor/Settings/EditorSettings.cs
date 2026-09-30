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
    public AppTheme Theme { get; set; } = AppTheme.System;

    public EditorSettings Clone() => new() { Theme = Theme };
}
