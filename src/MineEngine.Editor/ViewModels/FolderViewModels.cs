using MineEngine.Core.Assets;

namespace MineEngine.Editor.ViewModels;

/// <summary>Un dossier du Dashboard affiché dans la liste ou la grille, avec le nombre d'éléments qu'il contient.</summary>
public sealed class FolderItemViewModel
{
    public FolderItemViewModel(string path, int assetCount, int folderCount)
    {
        Path = path;
        AssetCount = assetCount;
        FolderCount = folderCount;
    }

    /// <summary>Chemin complet ("Armes/Épées").</summary>
    public string Path { get; }

    public string Name => AssetFolders.Name(Path);

    /// <summary>Éléments contenus, sous-dossiers compris.</summary>
    public int AssetCount { get; }

    /// <summary>Sous-dossiers directs.</summary>
    public int FolderCount { get; }

    public string ContentLabel => (AssetCount, FolderCount) switch
    {
        (0, 0) => Tr.T("Vide"),
        (_, 0) => AssetCount == 1 ? Tr.T("1 élément") : Tr.F("{0} éléments", AssetCount),
        (0, _) => FolderCount == 1 ? Tr.T("1 dossier") : Tr.F("{0} dossiers", FolderCount),
        _ => Tr.F("{0} élément(s), {1} dossier(s)", AssetCount, FolderCount),
    };

    public override string ToString() => Path;
}

/// <summary>Segment du fil d'Ariane du Dashboard ("Contenu du mod › Armes › Épées").</summary>
public sealed record FolderCrumb(string Name, string Path, bool IsLast);

/// <summary>Destination proposée par "Déplacer vers" : la racine ou un dossier, indenté selon sa profondeur.</summary>
public sealed record FolderChoice(string Path, string Label);
