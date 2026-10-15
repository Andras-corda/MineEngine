using MineEngine.Core.Assets;
using MineEngine.Core.Commands;

namespace MineEngine.Project.Commands;

/// <summary>Remplace la liste des dossiers déclarés du projet (dossiers vides compris).</summary>
public sealed class ChangeFoldersCommand : IUndoableCommand
{
    private readonly ProjectSettings _settings;
    private readonly IReadOnlyList<string> _before;
    private readonly IReadOnlyList<string> _after;

    public ChangeFoldersCommand(ProjectSettings settings, IEnumerable<string> folders, string description)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _before = settings.Folders;
        _after = [.. folders];
        Description = description;
    }

    public string Description { get; }

    public void Execute() => _settings.Folders = _after;

    public void Undo() => _settings.Folders = _before;

    public bool TryMerge(IUndoableCommand next) => false;
}

/// <summary>
/// Opérations sur les dossiers du Dashboard. Chacune produit une seule commande
/// annulable qui met à jour la liste des dossiers et le dossier des assets concernés.
/// Les dossiers sont logiques : les fichiers restent rangés par type sur le disque.
/// </summary>
public static class FolderOperations
{
    /// <summary>Tous les dossiers : déclarés, plus ceux qui contiennent des assets, avec leurs parents.</summary>
    public static IReadOnlyList<string> AllFolders(ModProject project) =>
        [.. project.Settings.Folders.Concat(project.Assets.Select(a => a.Folder))
            .SelectMany(AssetFolders.WithAncestors)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.CurrentCultureIgnoreCase)];

    public static bool Exists(ModProject project, string path) =>
        path.Length == 0 || AllFolders(project).Contains(path, StringComparer.Ordinal);

    public static IUndoableCommand Create(ModProject project, string path) =>
        new ChangeFoldersCommand(project.Settings, project.Settings.Folders.Append(path), Tr.F("Créer le dossier « {0} »", AssetFolders.Name(path)));

    /// <summary>Renomme ou déplace un dossier : ses sous-dossiers et ses assets suivent.</summary>
    public static IUndoableCommand Move(ModProject project, string path, string newPath, string description)
    {
        if (AssetFolders.IsWithin(newPath, path))
        {
            throw new InvalidOperationException(Tr.T("Un dossier ne peut pas être déplacé dans lui-même."));
        }

        IEnumerable<string> folders = AllFolders(project)
            .Where(f => AssetFolders.IsWithin(f, path))
            .Select(f => AssetFolders.Rebase(f, path, newPath))
            .Concat(project.Settings.Folders.Where(f => !AssetFolders.IsWithin(f, path)));
        var commands = new List<IUndoableCommand> { new ChangeFoldersCommand(project.Settings, folders, description) };
        commands.AddRange(project.Assets
            .Where(a => AssetFolders.IsWithin(a.Folder, path))
            .Select(a => SetFolder(a, AssetFolders.Rebase(a.Folder, path, newPath))));
        return new CompositeCommand(description, commands);
    }

    /// <summary>Supprime un dossier : son contenu (assets et sous-dossiers) remonte d'un niveau.</summary>
    public static IUndoableCommand Dissolve(ModProject project, string path)
    {
        string parent = AssetFolders.Parent(path);
        IEnumerable<string> folders = AllFolders(project)
            .Where(f => AssetFolders.IsWithin(f, path) && f != path)
            .Select(f => AssetFolders.Rebase(f, path, parent))
            .Concat(project.Settings.Folders.Where(f => !AssetFolders.IsWithin(f, path)));
        string description = Tr.F("Supprimer le dossier « {0} »", AssetFolders.Name(path));
        var commands = new List<IUndoableCommand> { new ChangeFoldersCommand(project.Settings, folders, description) };
        commands.AddRange(project.Assets
            .Where(a => AssetFolders.IsWithin(a.Folder, path))
            .Select(a => SetFolder(a, AssetFolders.Rebase(a.Folder, path, parent))));
        return new CompositeCommand(description, commands);
    }

    public static IUndoableCommand MoveAssets(IEnumerable<Asset> assets, string folder)
    {
        List<IUndoableCommand> commands = [.. assets.Where(a => a.Folder != folder).Select(a => SetFolder(a, folder))];
        string target = folder.Length == 0 ? Tr.T("la racine") : Tr.F("« {0} »", AssetFolders.Name(folder));
        return new CompositeCommand(commands.Count == 1 ? Tr.F("Déplacer vers {0}", target) : Tr.F("Déplacer {0} éléments vers {1}", commands.Count, target), commands);
    }

    private static PropertyChangeCommand<string> SetFolder(Asset asset, string folder) =>
        new(Tr.T("Changer de dossier"), asset, nameof(Asset.Folder), () => asset.Folder, v => asset.Folder = v, folder);
}
