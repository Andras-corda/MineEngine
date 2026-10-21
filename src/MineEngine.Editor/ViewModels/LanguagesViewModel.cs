using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Core.Commands;
using MineEngine.Core.IO;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Project;
using MineEngine.Project.Localization;

namespace MineEngine.Editor.ViewModels;

/// <summary>Une langue du mod dans la liste de l'onglet Langues.</summary>
public sealed class LocaleItemViewModel : ObservableObject
{
    private string _progress = string.Empty;

    public LocaleItemViewModel(string code) => Code = code;

    public string Code { get; }

    /// <summary>Nom lisible de la langue ("Deutsch (Deutschland)"), d'après le code Minecraft.</summary>
    public string Name
    {
        get
        {
            try
            {
                return System.Globalization.CultureInfo.GetCultureInfo(Code.Replace('_', '-')).NativeName;
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                return Code;
            }
        }
    }

    /// <summary>"12 / 40 traduits".</summary>
    public string Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public override string ToString() => Code;
}

/// <summary>
/// Un texte du mod dans une langue : sa clé, le texte saisi sur l'élément (écrit par défaut)
/// et la traduction. Une traduction vide rend le texte par défaut.
/// </summary>
public sealed class TranslationRowViewModel : ObservableObject
{
    private readonly LanguagesViewModel _owner;
    private string _translation;

    public TranslationRowViewModel(LanguagesViewModel owner, string key, string defaultText, string sourceLabel, string translation)
    {
        _owner = owner;
        Key = key;
        DefaultText = defaultText;
        SourceLabel = sourceLabel;
        _translation = translation;
    }

    public string Key { get; }

    /// <summary>Texte écrit si la ligne n'est pas traduite (vide pour une clé ajoutée à la main).</summary>
    public string DefaultText { get; }

    /// <summary>Origine du texte : Item, Bloc, Info-bulle... ou « Clé ajoutée ».</summary>
    public string SourceLabel { get; }

    public string Translation
    {
        get => _translation;
        set
        {
            value ??= string.Empty;
            if (value != _translation)
            {
                _translation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTranslated));
                _owner.SetTranslation(Key, value);
            }
        }
    }

    public bool IsTranslated => _translation.Length > 0;

    /// <summary>Le texte a changé ailleurs (annulation, import) : la ligne suit sans réécrire l'historique.</summary>
    internal void Update(string translation)
    {
        if (translation != _translation)
        {
            _translation = translation;
            OnPropertyChanged(nameof(Translation));
            OnPropertyChanged(nameof(IsTranslated));
        }
    }
}

/// <summary>
/// Onglet Langues du projet : les langues du mod (fichiers de langue de Minecraft) et la
/// traduction de chaque texte. Ajout et suppression de langues, import d'un fichier de langue
/// Minecraft, export du fichier complet d'une langue, clés ajoutées pour les textes du code Java.
/// Chaque modification est une étape annulable du projet.
/// </summary>
public sealed class LanguagesViewModel : ObservableObject, IDisposable
{
    private const string JsonFilter = "JSON (*.json)|*.json";

    private readonly ProjectViewModel _project;
    private readonly IDialogService _dialogs;
    private readonly ListCollectionView _view;
    private LocaleItemViewModel? _selectedLocale;
    private string _searchText = string.Empty;
    private bool _untranslatedOnly;
    private string? _message;
    private bool _writing;

    private readonly Func<bool> _canEdit;

    /// <param name="canEdit">Faux pendant une compilation : les traductions ne changent pas pendant que Gradle lit les fichiers.</param>
    public LanguagesViewModel(ProjectViewModel project, IDialogService dialogs, Func<bool> canEdit)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        _view = new ListCollectionView(Rows) { Filter = Matches };

        AddLocaleCommand = new RelayCommand(AddLocale, () => CanEdit);
        RemoveLocaleCommand = new RelayCommand(RemoveLocale, () => CanEdit && _selectedLocale is not null && Locales.Count > 1);
        ImportCommand = new RelayCommand(Import, () => CanEdit);
        ExportCommand = new RelayCommand(Export, () => _selectedLocale is not null);
        AddKeyCommand = new RelayCommand(AddKey, () => CanEdit && _selectedLocale is not null);
        ClearTranslationCommand = new RelayCommand<TranslationRowViewModel>(row => row.Translation = string.Empty, _ => CanEdit);

        Model.LocalizationChanged += OnLocalizationChanged;
        Model.Assets.Changed += OnAssetsChanged;
        RebuildLocales();
    }

    public ObservableCollection<LocaleItemViewModel> Locales { get; } = [];

    public ObservableCollection<TranslationRowViewModel> Rows { get; } = [];

    /// <summary>Lignes de la langue choisie, filtrées par la recherche.</summary>
    public ICollectionView VisibleRows => _view;

    public ICommand AddLocaleCommand { get; }

    public ICommand RemoveLocaleCommand { get; }

    public ICommand ImportCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand AddKeyCommand { get; }

    public ICommand ClearTranslationCommand { get; }

    public bool CanEdit => _canEdit();

    public LocaleItemViewModel? SelectedLocale
    {
        get => _selectedLocale;
        set
        {
            if (SetProperty(ref _selectedLocale, value))
            {
                RebuildRows();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                _view.Refresh();
            }
        }
    }

    /// <summary>N'affiche que les textes sans traduction dans la langue choisie.</summary>
    public bool UntranslatedOnly
    {
        get => _untranslatedOnly;
        set
        {
            if (SetProperty(ref _untranslatedOnly, value))
            {
                _view.Refresh();
            }
        }
    }

    /// <summary>Résultat de la dernière opération (import, export...).</summary>
    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    private ModProject Model => _project.Model;

    public void Dispose()
    {
        Model.LocalizationChanged -= OnLocalizationChanged;
        Model.Assets.Changed -= OnAssetsChanged;
    }

    /// <summary>Écrit une traduction de la langue choisie (étape annulable ; les frappes rapprochées n'en font qu'une).</summary>
    internal void SetTranslation(string key, string text)
    {
        if (_selectedLocale is null)
        {
            return;
        }

        Change(Model.Localization.WithTranslation(_selectedLocale.Code, key, text), Tr.F("Translate \"{0}\"", key));
    }

    private void Change(ModLocalization value, string description)
    {
        _writing = true;
        try
        {
            ModProject model = Model;
            model.History.Execute(new PropertyChangeCommand<ModLocalization>(
                description, model, nameof(ModProject.Localization), () => model.Localization, v => model.Localization = v, value));
        }
        finally
        {
            _writing = false;
        }

        UpdateProgress();
    }

    private bool Matches(object item)
    {
        if (item is not TranslationRowViewModel row || (_untranslatedOnly && row.IsTranslated))
        {
            return false;
        }

        return _searchText.Length == 0
            || row.Key.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || row.DefaultText.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase)
            || row.Translation.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnLocalizationChanged(object? sender, EventArgs e)
    {
        if (_writing)
        {
            return;
        }

        // Annulation, import... : la liste des langues ou les textes ont changé.
        if (!Locales.Select(l => l.Code).SequenceEqual(Model.Localization.Locales))
        {
            RebuildLocales();
            return;
        }

        RefreshRows();
    }

    private void OnAssetsChanged(object? sender, EventArgs e) => RebuildRows();

    private void RebuildLocales()
    {
        string? selected = _selectedLocale?.Code;
        Locales.Clear();
        foreach (string code in Model.Localization.Locales)
        {
            Locales.Add(new LocaleItemViewModel(code));
        }

        _selectedLocale = Locales.FirstOrDefault(l => l.Code == selected) ?? Locales.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedLocale));
        RebuildRows();
    }

    /// <summary>Lignes de la langue choisie : textes du mod, puis clés ajoutées à la main.</summary>
    private void RebuildRows()
    {
        Rows.Clear();
        if (_selectedLocale is not { } locale)
        {
            return;
        }

        IReadOnlyList<TranslationEntry> entries = ModTranslationKeys.Collect(Model);
        var known = entries.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var translations = Model.Localization.TranslationsFor(locale.Code);
        foreach (TranslationEntry entry in entries)
        {
            Rows.Add(new TranslationRowViewModel(this, entry.Key, entry.DefaultFor(locale.Code), SourceLabel(entry.Source),
                translations.GetValueOrDefault(entry.Key) ?? string.Empty));
        }

        foreach ((string key, string text) in translations.Where(t => !known.Contains(t.Key)).OrderBy(t => t.Key, StringComparer.Ordinal))
        {
            Rows.Add(new TranslationRowViewModel(this, key, string.Empty, Tr.T("Added key"), text));
        }

        UpdateProgress();
    }

    /// <summary>Met à jour les traductions affichées sans recréer les lignes (le champ en cours de saisie garde le focus).</summary>
    private void RefreshRows()
    {
        if (_selectedLocale is not { } locale)
        {
            return;
        }

        var translations = Model.Localization.TranslationsFor(locale.Code);
        var shown = Rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        if (translations.Keys.Any(k => !shown.Contains(k)))
        {
            RebuildRows();
            return;
        }

        foreach (TranslationRowViewModel row in Rows)
        {
            row.Update(translations.GetValueOrDefault(row.Key) ?? string.Empty);
        }

        // Undo or import: the filters apply again. Never while typing: a refresh recreates the
        // rows, and the text box being typed in would lose the focus after each character.
        _view.Refresh();
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        int total = ModTranslationKeys.Collect(Model).Count;
        foreach (LocaleItemViewModel locale in Locales)
        {
            int translated = Model.Localization.TranslationsFor(locale.Code).Count;
            locale.Progress = Tr.F("{0} / {1} translated", translated, total);
        }
    }

    private static string SourceLabel(TranslationSource source) => source switch
    {
        TranslationSource.CreativeTab => Tr.T("Creative tab"),
        TranslationSource.Item => Tr.T("Item"),
        TranslationSource.Block => Tr.T("Block"),
        TranslationSource.Tooltip => Tr.T("Tooltip"),
        TranslationSource.Mob => Tr.T("Mob"),
        TranslationSource.SpawnEgg => Tr.T("Spawn egg"),
        TranslationSource.ProjectCreativeTab => Tr.T("Creative tab"),
        TranslationSource.Keybind or TranslationSource.KeyCategory => Tr.T("Keybind"),
        TranslationSource.Enchantment => Tr.T("Enchantment"),
        TranslationSource.Fluid => Tr.T("Fluid"),
        TranslationSource.Bucket => Tr.T("Bucket"),
        TranslationSource.GameRule or TranslationSource.GameRuleDescription => Tr.T("Game rule"),
        TranslationSource.Effect => Tr.T("Effect"),
        TranslationSource.Potion or TranslationSource.SplashPotion or TranslationSource.LingeringPotion or TranslationSource.TippedArrow => Tr.T("Potion"),
        _ => Tr.T("Sound subtitle"),
    };

    private void AddLocale()
    {
        string? code = _dialogs.AskText(Tr.T("Add a language"),
            Tr.T("Minecraft language code (examples: de_de, es_es, it_it, pt_br, ja_jp):"), string.Empty);
        if (code is null)
        {
            return;
        }

        code = ModLocalization.Normalize(code);
        if (!ModLocalization.IsValidLocale(code))
        {
            _dialogs.ShowError(Tr.T("Add a language"), Tr.F("Invalid language code: \"{0}\" (examples: de_de, es_es, pt_br).", code));
            return;
        }

        Change(Model.Localization.WithLocale(code), Tr.F("Add the {0} language", code));
        RebuildLocales();
        SelectedLocale = Locales.FirstOrDefault(l => l.Code == code);
    }

    private void RemoveLocale()
    {
        if (_selectedLocale is not { } locale
            || !_dialogs.Confirm(Tr.T("Delete the language"), Tr.F("Delete the {0} language and its translations from the mod? (Ctrl+Z to undo)", locale.Code)))
        {
            return;
        }

        Change(Model.Localization.WithoutLocale(locale.Code), Tr.F("Delete the {0} language", locale.Code));
        RebuildLocales();
    }

    /// <summary>Importe un fichier de langue Minecraft ; sa langue est tirée du nom du fichier (de_de.json), sinon demandée.</summary>
    private void Import()
    {
        if (_dialogs.AskFileToOpen(Tr.T("Import a language file"), JsonFilter) is not { } file)
        {
            return;
        }

        try
        {
            var entries = ModLocalizationSerializer.ReadLanguageFile(File.ReadAllText(file), Path.GetFileName(file));
            string code = ModLocalization.Normalize(Path.GetFileNameWithoutExtension(file));
            if (!ModLocalization.IsValidLocale(code))
            {
                code = ModLocalization.Normalize(_dialogs.AskText(Tr.T("Import a language file"),
                    Tr.T("Language of this file (Minecraft code, for example de_de):"), _selectedLocale?.Code ?? string.Empty));
                if (!ModLocalization.IsValidLocale(code))
                {
                    return;
                }
            }

            // Une valeur identique au texte par défaut n'est pas une traduction : elle n'est pas gardée.
            var defaults = ModTranslationKeys.Collect(Model).ToDictionary(e => e.Key, e => e.DefaultFor(code), StringComparer.Ordinal);
            var kept = entries.Where(e => !defaults.TryGetValue(e.Key, out string? text) || text != e.Value).ToList();
            Change(Model.Localization.WithTranslations(code, kept), Tr.F("Import the {0} language", code));
            RebuildLocales();
            SelectedLocale = Locales.FirstOrDefault(l => l.Code == code);
            Message = Tr.F("{0}: {1} translations imported into {2}.", Path.GetFileName(file), kept.Count, code);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            _dialogs.ShowError(Tr.T("Import a language file"), exception.Message);
        }
    }

    /// <summary>Exporte le fichier de langue complet (textes par défaut et traductions), tel que le mod l'utilisera.</summary>
    private void Export()
    {
        if (_selectedLocale is not { } locale
            || _dialogs.AskFileToSave(Tr.T("Export the language"), JsonFilter, locale.Code + ".json") is not { } file)
        {
            return;
        }

        try
        {
            var entries = ModTranslationKeys.Collect(Model).ToDictionary(e => e.Key, e => e.DefaultFor(locale.Code), StringComparer.Ordinal);
            foreach ((string key, string text) in Model.Localization.TranslationsFor(locale.Code))
            {
                entries[key] = text;
            }

            AtomicFile.WriteAllText(file, ModLocalizationSerializer.WriteLanguageFile(entries));
            Message = Tr.F("{0} language exported to {1}.", locale.Code, file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(Tr.T("Export the language"), exception.Message);
        }
    }

    /// <summary>Ajoute une clé que Mine Engine n'écrit pas (texte affiché par du code Java : message, écran...).</summary>
    private void AddKey()
    {
        if (_selectedLocale is not { } locale)
        {
            return;
        }

        string? key = _dialogs.AskText(Tr.T("Add a key"),
            Tr.F("Translation key (for example message.{0}.welcome):", Model.Settings.ModId), $"message.{Model.Settings.ModId}.");
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string? text = _dialogs.AskText(Tr.T("Add a key"), Tr.F("Text in {0}:", locale.Code), string.Empty);
        if (!string.IsNullOrEmpty(text))
        {
            Change(Model.Localization.WithTranslation(locale.Code, key.Trim(), text), Tr.F("Add the {0} key", key.Trim()));
            RebuildRows();
        }
    }
}
