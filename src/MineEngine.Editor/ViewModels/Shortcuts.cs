using System.Globalization;
using System.Windows.Input;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels;

/// <summary>
/// Commande de l'éditeur qui peut recevoir un raccourci clavier. <see cref="DefaultGesture"/>
/// est écrit au format invariant de WPF ("Ctrl+Shift+B", "F5") ; vide : pas de raccourci par défaut.
/// </summary>
public sealed record ShortcutDefinition(string Id, string Category, string Label, string DefaultGesture, Func<MainViewModel, ICommand> Command);

/// <summary>
/// Raccourcis modifiables (onglet Raccourcis des paramètres). Ctrl+Z / Ctrl+Y, Ctrl+F et les
/// raccourcis de disposition (Ctrl+B, Ctrl+J, Ctrl+Alt+B) restent fixes : ils dépendent de
/// l'élément qui a le focus.
/// </summary>
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("file.newProject", Tr.T("Fichier"), Tr.T("Nouveau projet"), "Ctrl+N", m => m.NewProjectCommand),
        new("file.openProject", Tr.T("Fichier"), Tr.T("Ouvrir un projet"), "Ctrl+O", m => m.OpenProjectCommand),
        new("file.saveAll", Tr.T("Fichier"), Tr.T("Enregistrer tout"), "Ctrl+S", m => m.SaveProjectCommand),
        new("file.saveFile", Tr.T("Fichier"), Tr.T("Enregistrer le fichier"), string.Empty, m => m.SaveDocumentCommand),
        new("file.closeTab", Tr.T("Fichier"), Tr.T("Fermer l'onglet"), "Ctrl+W", m => m.CloseActiveDocumentCommand),
        new("file.closeProject", Tr.T("Fichier"), Tr.T("Fermer le projet"), string.Empty, m => m.CloseProjectCommand),

        new("edit.edit", Tr.T("Édition"), Tr.T("Éditer l'élément sélectionné"), "Ctrl+E", m => m.EditSelectedAssetCommand),
        new("edit.duplicate", Tr.T("Édition"), Tr.T("Dupliquer l'élément sélectionné"), "Ctrl+D", m => m.DuplicateAssetCommand),
        new("edit.delete", Tr.T("Édition"), Tr.T("Supprimer l'élément sélectionné"), string.Empty, m => m.DeleteAssetCommand),
        new("edit.openGraph", Tr.T("Édition"), Tr.T("Ouvrir le graphe sélectionné"), string.Empty, m => m.OpenSelectedGraphCommand),
        new("edit.viewJava", Tr.T("Édition"), Tr.T("Voir le code Java"), "Ctrl+Shift+J", m => m.OpenAssetJavaCommand),

        new("workspace.settings", Tr.T("Espace de travail"), Tr.T("Paramètres du projet"), string.Empty, m => m.EditProjectSettingsCommand),
        new("workspace.newFolder", Tr.T("Espace de travail"), Tr.T("Nouveau dossier"), "Ctrl+Shift+N", m => m.NewFolderCommand),
        new("workspace.validate", Tr.T("Espace de travail"), Tr.T("Valider le projet"), "F7", m => m.ValidateCommand),

        new("assets.addItem", Tr.T("Éléments"), Tr.T("Ajouter un item"), string.Empty, m => m.AddItemCommand),
        new("assets.addBlock", Tr.T("Éléments"), Tr.T("Ajouter un bloc"), string.Empty, m => m.AddBlockCommand),
        new("assets.addMob", Tr.T("Éléments"), Tr.T("Ajouter un mob"), string.Empty, m => m.AddMobCommand),
        new("assets.addRecipe", Tr.T("Éléments"), Tr.T("Ajouter une recette"), string.Empty, m => m.AddRecipeCommand),
        new("assets.importTextures", Tr.T("Éléments"), Tr.T("Importer des textures"), string.Empty, m => m.ImportTexturesCommand),
        new("assets.importSounds", Tr.T("Éléments"), Tr.T("Importer des sons"), string.Empty, m => m.ImportSoundsCommand),
        new("assets.export", Tr.T("Éléments"), Tr.T("Exporter l'élément sélectionné"), string.Empty, m => m.ExportAssetCommand),

        new("build.build", Tr.T("Compilation et lancement"), Tr.T("Compiler le mod"), "Ctrl+Shift+B", m => m.BuildCommand),
        new("build.export", Tr.T("Compilation et lancement"), Tr.T("Exporter le mod (.jar)"), "Ctrl+Shift+E", m => m.ExportModCommand),
        new("build.run", Tr.T("Compilation et lancement"), Tr.T("Lancer Minecraft"), "F5", m => m.RunClientCommand),
        new("build.reloadGradle", Tr.T("Compilation et lancement"), Tr.T("Recharger Gradle"), string.Empty, m => m.ReloadGradleCommand),
        new("build.stop", Tr.T("Compilation et lancement"), Tr.T("Arrêter"), "Shift+F5", m => m.CancelBuildCommand),

        new("window.dashboard", Tr.T("Fenêtre"), Tr.T("Dashboard"), "Ctrl+1", m => m.ShowDashboardCommand),
        new("window.resources", Tr.T("Fenêtre"), Tr.T("Ressources"), "Ctrl+2", m => m.ShowResourcesCommand),
        new("window.languages", Tr.T("Fenêtre"), Tr.T("Langues du mod"), "Ctrl+3", m => m.ShowLanguagesCommand),

        new("tools.settings", Tr.T("Outils"), Tr.T("Paramètres"), "Ctrl+OemComma", m => m.OpenSettingsCommand),
        new("tools.mdk", Tr.T("Outils"), Tr.T("Gestionnaire de MDK"), string.Empty, m => m.OpenMdkManagerCommand),
    ];

    /// <summary>Raccourcis fixes, affichés pour mémoire dans les paramètres.</summary>
    public static IReadOnlyList<(string Label, string Gesture)> Fixed { get; } =
    [
        (Tr.T("Annuler"), "Ctrl+Z"),
        (Tr.T("Rétablir"), "Ctrl+Y"),
        (Tr.T("Rechercher"), "Ctrl+F"),
        (Tr.T("Afficher ou masquer l'Explorateur"), "Ctrl+B"),
        (Tr.T("Afficher ou masquer Sortie et diagnostics"), "Ctrl+J"),
        (Tr.T("Afficher ou masquer Détails / Inspecteur"), "Ctrl+Alt+B"),
    ];

    private static readonly KeyGestureConverter Converter = new();

    /// <summary>Raccourci lu au format invariant, ou null s'il est vide ou invalide.</summary>
    public static KeyGesture? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return Converter.ConvertFromInvariantString(text) as KeyGesture;
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Texte invariant d'un raccourci (enregistré dans settings.json).</summary>
    public static string Format(KeyGesture gesture) => Converter.ConvertToInvariantString(gesture) ?? string.Empty;

    /// <summary>Texte affiché à l'utilisateur ("Ctrl+Maj+B" en français).</summary>
    public static string Display(string? text) =>
        Parse(text) is { } gesture ? gesture.GetDisplayStringForCulture(CultureInfo.CurrentUICulture) : string.Empty;

    /// <summary>Raccourci effectif d'une commande : celui choisi dans les paramètres, sinon celui par défaut.</summary>
    public static string Effective(ShortcutDefinition definition, IReadOnlyDictionary<string, string> overrides) =>
        overrides.TryGetValue(definition.Id, out string? custom) ? custom : definition.DefaultGesture;
}

/// <summary>
/// Raccourcis en cours, pour l'affichage dans les menus : <c>{Binding Shortcuts[build.run]}</c>
/// donne « F5 » (ou le raccourci choisi par l'utilisateur).
/// </summary>
public sealed class ShortcutMap : ObservableObject
{
    private IReadOnlyDictionary<string, string> _overrides = new Dictionary<string, string>();

    public string this[string id] =>
        ShortcutCatalog.All.FirstOrDefault(d => d.Id == id) is { } definition
            ? ShortcutCatalog.Display(ShortcutCatalog.Effective(definition, _overrides))
            : string.Empty;

    public IReadOnlyDictionary<string, string> Overrides => _overrides;

    public void Update(IReadOnlyDictionary<string, string> overrides)
    {
        _overrides = overrides ?? new Dictionary<string, string>();
        OnPropertyChanged("Item[]");
    }
}
