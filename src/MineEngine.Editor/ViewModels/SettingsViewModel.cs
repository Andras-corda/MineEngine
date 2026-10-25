using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using MineEngine.Core.Theming;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Editor.Settings;
using MineEngine.Editor.ViewModels.Assets;

namespace MineEngine.Editor.ViewModels;

/// <summary>Thème proposé dans les paramètres, avec quatre couleurs d'aperçu (#RRGGBB).</summary>
public sealed record ThemeChoice(string Id, string Name, bool IsBuiltIn, string Background, string Card, string Text, string Accent)
{
    public static ThemeChoice From(ThemeDefinition theme, string? name = null)
    {
        IReadOnlyDictionary<string, string> colors = theme.Resolve();
        return new ThemeChoice(theme.Id, name ?? theme.DisplayName, theme.IsBuiltIn,
            colors["HubBackgroundBrush"], colors["CardBrush"], colors["StrongTextBrush"], colors["AccentBrush"]);
    }

    /// <summary>Indication sous le nom : origine du thème.</summary>
    public string Detail => Id == EditorSettings.SystemThemeId
        ? Tr.T("Light or dark, following Windows")
        : IsBuiltIn ? Tr.T("Shipped with Mine Engine") : Tr.T("Imported");
}

/// <summary>Couleur d'accent proposée ; <see cref="Color"/> null garde celle du thème.</summary>
public sealed record AccentChoice(string Name, RgbColor? Color, string Swatch)
{
    public override string ToString() => Name;
}

/// <summary>Langue proposée, avec son origine et sa part de textes traduits.</summary>
public sealed record LanguageChoice(UiLanguage Language, string Detail)
{
    public string Name => Language.NativeName;

    public bool IsBuiltIn => Language.IsBuiltIn;
}

/// <summary>
/// Fenêtre Paramètres de l'éditeur. Le thème et l'accent choisis sont appliqués immédiatement
/// en aperçu ; les réglages ne sont enregistrés qu'à la validation, sinon l'ancien thème revient.
/// Les thèmes et les langues importés ou supprimés le sont tout de suite (ce sont des fichiers).
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private const string JsonFilter = "JSON (*.json)|*.json";

    private readonly EditorPreferences _preferences;
    private readonly IDialogService _dialogs;
    private EditorSettings _draft;
    private bool _saved;
    private string? _themeMessage;
    private string? _languageMessage;
    private string? _programMessage;

    public SettingsViewModel(EditorPreferences preferences, IDialogService dialogs)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _draft = preferences.Current.Clone();

        Accents = [new AccentChoice(Tr.T("Theme color"), null, "Transparent"),
            .. AccentColors.Presets.Select(p => new AccentChoice(p.DisplayName, p.Color, p.Color.ToString()))];
        ScaleChoices = [.. new[] { 0.8, 0.9, 1.0, 1.1, 1.25, 1.5 }.Select(s => new Choice<double>(s, Tr.F("{0}%", (int)Math.Round(s * 100))))];

        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(Reset);
        ImportThemeCommand = new RelayCommand(ImportTheme);
        ExportThemeCommand = new RelayCommand(ExportTheme);
        RemoveThemeCommand = new RelayCommand(RemoveTheme, () => SelectedTheme is { IsBuiltIn: false });
        ImportLanguageCommand = new RelayCommand(ImportLanguage);
        ExportLanguageTemplateCommand = new RelayCommand(ExportLanguageTemplate);
        RemoveLanguageCommand = new RelayCommand(RemoveLanguage, () => SelectedLanguage is { IsBuiltIn: false });

        LoadThemes();
        LoadLanguages();
        LoadShortcuts();
        LoadPrograms();
        AddProgramCommand = new RelayCommand(AddProgram);
        DetectProgramsCommand = new RelayCommand(DetectPrograms);
        RemoveProgramCommand = new RelayCommand<ExternalProgramRowViewModel>(row =>
        {
            Programs.Remove(row);
            OnProgramsChanged();
        });
    }

    // ----- Programmes externes -----

    /// <summary>Programmes proposés dans « Ouvrir avec ».</summary>
    public ObservableCollection<ExternalProgramRowViewModel> Programs { get; } = [];

    public ICommand AddProgramCommand { get; }

    public ICommand DetectProgramsCommand { get; }

    public ICommand RemoveProgramCommand { get; }

    /// <summary>Résultat de la dernière détection.</summary>
    public string? ProgramMessage
    {
        get => _programMessage;
        private set => SetProperty(ref _programMessage, value);
    }

    private void LoadPrograms()
    {
        Programs.Clear();
        foreach (ExternalProgram program in _draft.ExternalPrograms)
        {
            Programs.Add(new ExternalProgramRowViewModel(program, OnProgramsChanged));
        }
    }

    private void OnProgramsChanged() => _draft.ExternalPrograms = [.. Programs.Select(p => p.ToModel())];

    private void AddProgram()
    {
        if (_dialogs.AskFileToOpen(Tr.T("Add a program"), Tr.T("Programs (*.exe)|*.exe")) is not { } exe)
        {
            return;
        }

        Programs.Add(new ExternalProgramRowViewModel(new ExternalProgram(Path.GetFileNameWithoutExtension(exe), exe, "png"), OnProgramsChanged));
        OnProgramsChanged();
        ProgramMessage = Tr.T("Enter the extensions of the files to open with this program (for example png;jpg).");
    }

    private void DetectPrograms()
    {
        var known = Programs.Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<ExternalProgram> found = [.. KnownPrograms.Detect().Where(p => !known.Contains(p.Path))];
        foreach (ExternalProgram program in found)
        {
            Programs.Add(new ExternalProgramRowViewModel(program, OnProgramsChanged));
        }

        OnProgramsChanged();
        ProgramMessage = found.Count == 0
            ? Tr.T("No new program found (Blockbench, Paint.NET, GIMP, Aseprite, Audacity, Visual Studio Code, Notepad++, IntelliJ IDEA).")
            : Tr.F("{0} program(s) added: {1}.", found.Count, string.Join(", ", found.Select(p => p.Name)));
    }

    // ----- Raccourcis -----

    /// <summary>Commandes et leur raccourci, regroupées par menu.</summary>
    public ObservableCollection<ShortcutRowViewModel> Shortcuts { get; } = [];

    /// <summary>Raccourcis fixes, rappelés sous la liste.</summary>
    public IReadOnlyList<string> FixedShortcuts { get; } =
        [.. ShortcutCatalog.Fixed.Select(f => $"{ShortcutCatalog.Display(f.Gesture)} : {f.Label}")];

    private void LoadShortcuts()
    {
        Shortcuts.Clear();
        foreach (ShortcutDefinition definition in ShortcutCatalog.All)
        {
            Shortcuts.Add(new ShortcutRowViewModel(definition, ShortcutCatalog.Effective(definition, _draft.Shortcuts), OnShortcutChanged));
        }

        UpdateConflicts();
    }

    /// <summary>Le brouillon garde seulement les raccourcis qui diffèrent de ceux par défaut.</summary>
    private void OnShortcutChanged()
    {
        _draft.Shortcuts = Shortcuts.Where(r => !r.IsDefault).ToDictionary(r => r.Definition.Id, r => r.Gesture);
        UpdateConflicts();
    }

    private void UpdateConflicts()
    {
        foreach (ShortcutRowViewModel row in Shortcuts)
        {
            ShortcutRowViewModel? other = row.Gesture.Length == 0
                ? null
                : Shortcuts.FirstOrDefault(r => !ReferenceEquals(r, row) && r.Gesture == row.Gesture);
            row.Conflict = other is null ? null : Tr.F("Also used by \"{0}\"", other.Label);
        }
    }

    /// <summary>Demande de fermeture de la fenêtre (vrai = paramètres enregistrés).</summary>
    public event EventHandler<bool>? CloseRequested;

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand ImportThemeCommand { get; }

    public ICommand ExportThemeCommand { get; }

    public ICommand RemoveThemeCommand { get; }

    public ICommand ImportLanguageCommand { get; }

    public ICommand ExportLanguageTemplateCommand { get; }

    public ICommand RemoveLanguageCommand { get; }

    /// <summary>Vrai si l'utilisateur a accepté de relancer Mine Engine pour changer de langue.</summary>
    public bool RestartRequested { get; private set; }

    // ----- Général -----

    public bool ReopenLastProject
    {
        get => _draft.ReopenLastProject;
        set
        {
            _draft.ReopenLastProject = value;
            OnPropertyChanged();
        }
    }

    /// <summary>The Dashboard opens as tiles (grid).</summary>
    public bool DashboardTileView
    {
        get => _draft.DashboardTileView;
        set
        {
            _draft.DashboardTileView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DashboardListView));
        }
    }

    /// <summary>The Dashboard opens as a list (radio button opposite to <see cref="DashboardTileView"/>).</summary>
    public bool DashboardListView
    {
        get => !_draft.DashboardTileView;
        set => DashboardTileView = !value;
    }

    /// <summary>Debug mode (V0.6.4).</summary>
    public bool DebugMode
    {
        get => _draft.DebugMode;
        set
        {
            _draft.DebugMode = value;
            OnPropertyChanged();
        }
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

    // ----- Apparence -----

    public ObservableCollection<ThemeChoice> Themes { get; } = [];

    public ThemeChoice? SelectedTheme
    {
        get => Themes.FirstOrDefault(t => t.Id == _draft.ThemeId);
        set
        {
            if (value is null || value.Id == _draft.ThemeId)
            {
                return;
            }

            _draft.ThemeId = value.Id;
            Preview();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<AccentChoice> Accents { get; }

    public AccentChoice SelectedAccent
    {
        get => Accents.FirstOrDefault(a => a.Color == _draft.AccentColor) ?? Accents[0];
        set
        {
            if (value is null || value.Color == _draft.AccentColor)
            {
                return;
            }

            _draft.AccentColor = value.Color;
            Preview();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<Choice<double>> ScaleChoices { get; }

    /// <summary>Taille de l'interface ; appliquée à l'enregistrement.</summary>
    public Choice<double> SelectedScale
    {
        get => ScaleChoices.MinBy(c => Math.Abs(c.Value - _draft.UiScale))!;
        set
        {
            if (value is not null)
            {
                _draft.UiScale = value.Value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Résultat de la dernière opération sur les thèmes (import, export).</summary>
    public string? ThemeMessage
    {
        get => _themeMessage;
        private set => SetProperty(ref _themeMessage, value);
    }

    // ----- Langue -----

    public ObservableCollection<LanguageChoice> Languages { get; } = [];

    /// <summary>Langue de l'interface ; elle s'applique au prochain démarrage.</summary>
    public LanguageChoice? SelectedLanguage
    {
        get => Languages.FirstOrDefault(l => l.Language.Code == _draft.LanguageCode);
        set
        {
            if (value is null)
            {
                return;
            }

            _draft.LanguageCode = value.Language.Code;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NeedsRestart));
        }
    }

    /// <summary>Vrai si la langue choisie n'est pas celle de l'interface affichée.</summary>
    public bool NeedsRestart => _draft.LanguageCode != Tr.Language.Code;

    /// <summary>Résultat de la dernière opération sur les langues (import, modèle).</summary>
    public string? LanguageMessage
    {
        get => _languageMessage;
        private set => SetProperty(ref _languageMessage, value);
    }

    // ----- Éditeurs -----

    public double CodeFontSize
    {
        get => _draft.CodeFontSize;
        set
        {
            _draft.CodeFontSize = double.IsNaN(value) ? 13 : (int)Math.Round(value);
            OnPropertyChanged();
        }
    }

    public int MinCodeFontSize => EditorSettings.MinCodeFontSize;

    public int MaxCodeFontSize => EditorSettings.MaxCodeFontSize;

    public bool CodeWordWrap
    {
        get => _draft.CodeWordWrap;
        set
        {
            _draft.CodeWordWrap = value;
            OnPropertyChanged();
        }
    }

    public bool GraphShowGrid
    {
        get => _draft.GraphShowGrid;
        set
        {
            _draft.GraphShowGrid = value;
            OnPropertyChanged();
        }
    }

    public bool GraphSnapToGrid
    {
        get => _draft.GraphSnapToGrid;
        set
        {
            _draft.GraphSnapToGrid = value;
            OnPropertyChanged();
        }
    }

    /// <summary>À appeler à la fermeture : annule l'aperçu si rien n'a été enregistré.</summary>
    public void DiscardPreview()
    {
        IThemeService themes = _preferences.Themes;
        EditorSettings current = _preferences.Current;
        if (!_saved && (themes.CurrentThemeId != current.ThemeId || themes.CurrentAccent != current.AccentColor))
        {
            themes.Apply(current.ThemeId, current.AccentColor);
        }
    }

    private void Preview() => _preferences.Themes.Apply(_draft.ThemeId, _draft.AccentColor);

    private void LoadThemes()
    {
        Themes.Clear();
        Themes.Add(ThemeChoice.From(BuiltInThemes.Dark, Tr.T("Same as Windows")) with { Id = EditorSettings.SystemThemeId });
        foreach (ThemeDefinition theme in _preferences.Themes.Library.All())
        {
            Themes.Add(ThemeChoice.From(theme));
        }

        if (SelectedTheme is null)
        {
            _draft.ThemeId = EditorSettings.SystemThemeId;
        }

        OnPropertyChanged(nameof(SelectedTheme));
    }

    private void LoadLanguages()
    {
        Languages.Clear();
        foreach (UiLanguage language in _preferences.Languages.All())
        {
            Languages.Add(new LanguageChoice(language, LanguageDetail(language)));
        }

        if (SelectedLanguage is null)
        {
            _draft.LanguageCode = UiLanguage.Default.Code;
        }

        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(NeedsRestart));
    }

    private static string LanguageDetail(UiLanguage language)
    {
        if (language.IsBuiltIn)
        {
            return Tr.T("Included with Mine Engine");
        }

        try
        {
            return Tr.F("Imported, {0}% of texts translated", (int)Math.Floor(LanguageLibrary.Completeness(language) * 100));
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            return Tr.T("Imported, unreadable file");
        }
    }

    private void ImportTheme()
    {
        if (_dialogs.AskFileToOpen(Tr.T("Import a theme"), JsonFilter) is not { } file)
        {
            return;
        }

        try
        {
            ThemeDefinition theme = _preferences.Themes.Library.Import(file);
            LoadThemes();
            SelectedTheme = Themes.FirstOrDefault(t => t.Id == theme.Id);
            ThemeMessage = Tr.F("Theme \"{0}\" imported.", theme.Name);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            _dialogs.ShowError(Tr.T("Import a theme"), exception.Message);
        }
    }

    private void ExportTheme()
    {
        ThemeDefinition theme = _draft.ThemeId == EditorSettings.SystemThemeId
            ? _preferences.Themes.ActualTheme
            : _preferences.Themes.Library.Find(_draft.ThemeId) ?? _preferences.Themes.ActualTheme;
        if (_dialogs.AskFileToSave(Tr.T("Export the theme"), JsonFilter, theme.DisplayName + ".json") is not { } file)
        {
            return;
        }

        try
        {
            ThemeLibrary.Export(theme, file, _draft.AccentColor);
            ThemeMessage = Tr.F("Theme exported to {0}.", file);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            _dialogs.ShowError(Tr.T("Export the theme"), exception.Message);
        }
    }

    private void RemoveTheme()
    {
        if (SelectedTheme is not { IsBuiltIn: false } theme ||
            !_dialogs.Confirm(Tr.T("Delete the theme"), Tr.F("Delete the theme \"{0}\"?", theme.Name)))
        {
            return;
        }

        try
        {
            _preferences.Themes.Library.Remove(theme.Id);
            _draft.ThemeId = EditorSettings.SystemThemeId;
            LoadThemes();
            Preview();
            ThemeMessage = Tr.F("Theme \"{0}\" deleted.", theme.Name);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            _dialogs.ShowError(Tr.T("Delete the theme"), exception.Message);
        }
    }

    private void ImportLanguage()
    {
        if (_dialogs.AskFileToOpen(Tr.T("Import a language"), JsonFilter) is not { } file)
        {
            return;
        }

        try
        {
            LanguageFile imported = _preferences.Languages.Import(file);
            LoadLanguages();
            SelectedLanguage = Languages.FirstOrDefault(l => l.Language.Code == imported.Language.Code);
            LanguageMessage = imported.RejectedCount == 0
                ? Tr.F("Language \"{0}\" imported ({1} translations).", imported.Language.NativeName, imported.Translations.Count)
                : Tr.F("Language \"{0}\" imported ({1} translations, {2} skipped: empty or without the same {{0}}, {{1}}... values).",
                    imported.Language.NativeName, imported.Translations.Count, imported.RejectedCount);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            _dialogs.ShowError(Tr.T("Import a language"), exception.Message);
        }
    }

    private void ExportLanguageTemplate()
    {
        if (_dialogs.AskFileToSave(Tr.T("Export a translation template"), JsonFilter, "mineengine-translation.json") is not { } file)
        {
            return;
        }

        try
        {
            LanguageLibrary.ExportTemplate(file);
            LanguageMessage = Tr.F("Template saved to {0}. Replace each English text with its translation, fill in the language code and name, then import the file.", file);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            _dialogs.ShowError(Tr.T("Export a translation template"), exception.Message);
        }
    }

    private void RemoveLanguage()
    {
        if (SelectedLanguage is not { IsBuiltIn: false } language ||
            !_dialogs.Confirm(Tr.T("Delete the language"), Tr.F("Delete the language \"{0}\"?", language.Name)))
        {
            return;
        }

        try
        {
            _preferences.Languages.Remove(language.Language);
            _draft.LanguageCode = UiLanguage.Default.Code;
            LoadLanguages();
            LanguageMessage = Tr.F("Language \"{0}\" deleted.", language.Name);
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            _dialogs.ShowError(Tr.T("Delete the language"), exception.Message);
        }
    }

    private void Reset()
    {
        if (!_dialogs.Confirm(Tr.T("Restore defaults"), Tr.T("Restore all editor settings to their defaults? Imported themes and languages are kept.")))
        {
            return;
        }

        bool sidebarCollapsed = _draft.HubSidebarCollapsed;
        _draft = new EditorSettings { HubSidebarCollapsed = sidebarCollapsed };
        LoadShortcuts();
        LoadPrograms();
        Preview();
        OnPropertyChanged(string.Empty);
    }

    private void Save()
    {
        _preferences.Update(_draft);
        _saved = true;
        RestartRequested = NeedsRestart && _dialogs.Confirm(
            Tr.T("Change language"),
            Tr.T("The new language will be used the next time Mine Engine starts. Restart now?"));
        CloseRequested?.Invoke(this, true);
    }

    private static bool IsFileError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or JsonException;
}
