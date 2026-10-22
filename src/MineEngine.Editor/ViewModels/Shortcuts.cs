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
        new("file.newProject", Tr.T("File"), Tr.T("New project"), "Ctrl+N", m => m.NewProjectCommand),
        new("file.openProject", Tr.T("File"), Tr.T("Open a project"), "Ctrl+O", m => m.OpenProjectCommand),
        new("file.saveAll", Tr.T("File"), Tr.T("Save all"), "Ctrl+S", m => m.SaveProjectCommand),
        new("file.saveFile", Tr.T("File"), Tr.T("Save the file"), string.Empty, m => m.SaveDocumentCommand),
        new("file.closeTab", Tr.T("File"), Tr.T("Close the tab"), "Ctrl+W", m => m.CloseActiveDocumentCommand),
        new("file.closeProject", Tr.T("File"), Tr.T("Close the project"), string.Empty, m => m.CloseProjectCommand),

        new("edit.edit", Tr.T("Edit"), Tr.T("Edit the selected element"), "Ctrl+E", m => m.EditSelectedAssetCommand),
        new("edit.duplicate", Tr.T("Edit"), Tr.T("Duplicate the selected element"), "Ctrl+D", m => m.DuplicateAssetCommand),
        new("edit.delete", Tr.T("Edit"), Tr.T("Delete the selected element"), string.Empty, m => m.DeleteAssetCommand),
        new("edit.openGraph", Tr.T("Edit"), Tr.T("Open the selected graph"), string.Empty, m => m.OpenSelectedGraphCommand),
        new("edit.viewJava", Tr.T("Edit"), Tr.T("View the Java code"), "Ctrl+Shift+J", m => m.OpenAssetJavaCommand),

        new("workspace.settings", Tr.T("Workspace"), Tr.T("Project Settings"), string.Empty, m => m.EditProjectSettingsCommand),
        new("workspace.newFolder", Tr.T("Workspace"), Tr.T("New folder"), "Ctrl+Shift+N", m => m.NewFolderCommand),
        new("workspace.validate", Tr.T("Workspace"), Tr.T("Validate the project"), "F7", m => m.ValidateCommand),

        new("assets.addItem", Tr.T("Assets"), Tr.T("Add an item"), string.Empty, m => m.AddItemCommand),
        new("assets.addBlock", Tr.T("Assets"), Tr.T("Add a block"), string.Empty, m => m.AddBlockCommand),
        new("assets.addMob", Tr.T("Assets"), Tr.T("Add a mob"), string.Empty, m => m.AddMobCommand),
        new("assets.addRecipe", Tr.T("Assets"), Tr.T("Add a recipe"), string.Empty, m => m.AddRecipeCommand),
        new("assets.importModels", Tr.T("Assets"), Tr.T("Import 3D models"), string.Empty, m => m.ImportModelsCommand),
        new("assets.addBehaviorTree", Tr.T("Assets"), Tr.T("Add a behavior tree (Puppeteer)"), string.Empty, m => m.AddBehaviorTreeCommand),
        new("assets.importTextures", Tr.T("Assets"), Tr.T("Import textures"), string.Empty, m => m.ImportTexturesCommand),
        new("assets.importSounds", Tr.T("Assets"), Tr.T("Import sounds"), string.Empty, m => m.ImportSoundsCommand),
        new("assets.export", Tr.T("Assets"), Tr.T("Export the selected element"), string.Empty, m => m.ExportAssetCommand),

        new("build.build", Tr.T("Build & Run"), Tr.T("Build the mod"), "Ctrl+Shift+B", m => m.BuildCommand),
        new("build.export", Tr.T("Build & Run"), Tr.T("Export the mod (.jar)"), "Ctrl+Shift+E", m => m.ExportModCommand),
        new("build.run", Tr.T("Build & Run"), Tr.T("Launch Minecraft"), "F5", m => m.RunClientCommand),
        new("build.reloadGradle", Tr.T("Build & Run"), Tr.T("Reload Gradle"), string.Empty, m => m.ReloadGradleCommand),
        new("build.stop", Tr.T("Build & Run"), Tr.T("Stop"), "Shift+F5", m => m.CancelBuildCommand),

        new("window.back", Tr.T("Window"), Tr.T("Back"), "Alt+Left", m => m.GoBackCommand),
        new("window.forward", Tr.T("Window"), Tr.T("Forward"), "Alt+Right", m => m.GoForwardCommand),
        new("window.dashboard", Tr.T("Window"), Tr.T("Dashboard"), "Ctrl+1", m => m.ShowDashboardCommand),
        new("window.resources", Tr.T("Window"), Tr.T("Resources"), "Ctrl+2", m => m.ShowResourcesCommand),
        new("window.languages", Tr.T("Window"), Tr.T("Mod languages"), "Ctrl+3", m => m.ShowLanguagesCommand),
        new("window.globals", Tr.T("Window"), Tr.T("Global variables"), "Ctrl+4", m => m.ShowGlobalsCommand),
        new("window.tags", Tr.T("Window"), Tr.T("Tags"), "Ctrl+5", m => m.ShowTagsCommand),

        new("tools.settings", Tr.T("Tools"), Tr.T("Settings"), "Ctrl+OemComma", m => m.OpenSettingsCommand),
        new("tools.mdk", Tr.T("Tools"), Tr.T("MDK Manager"), string.Empty, m => m.OpenMdkManagerCommand),
    ];

    /// <summary>Raccourcis fixes, affichés pour mémoire dans les paramètres.</summary>
    public static IReadOnlyList<(string Label, string Gesture)> Fixed { get; } =
    [
        (Tr.T("Cancel"), "Ctrl+Z"),
        (Tr.T("Redo"), "Ctrl+Y"),
        (Tr.T("Find"), "Ctrl+F"),
        (Tr.T("Show or hide the Explorer"), "Ctrl+B"),
        (Tr.T("Show or hide Output and Diagnostics"), "Ctrl+J"),
        (Tr.T("Show or hide Properties"), "Ctrl+Alt+B"),
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
