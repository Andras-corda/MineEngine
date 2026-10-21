using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Project;

/// <summary>Paramètres généraux d'un projet, enregistrés dans le fichier .mineengine.</summary>
public sealed class ProjectSettings
{
    public const int CurrentFormatVersion = 1;

    private string _modName;
    private IReadOnlyList<string> _folders = [];
    private IReadOnlyList<string> _lockedFiles = [];

    public ProjectSettings(ModId modId, string modName)
    {
        ModId = modId ?? throw new ArgumentNullException(nameof(modId));
        _modName = string.IsNullOrWhiteSpace(modName) ? modId.Value : modName.Trim();
    }

    /// <summary>Identifiant du mod. Il n'est pas modifiable après la création du projet.</summary>
    public ModId ModId { get; }

    public string ModName
    {
        get => _modName;
        set => _modName = string.IsNullOrWhiteSpace(value) ? ModId.Value : value.Trim();
    }

    public string ModVersion { get; set; } = "1.0.0";

    public string Authors { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string License { get; set; } = "All Rights Reserved";

    /// <summary>
    /// Icon of the mod shown in the in-game mod list: a PNG file, path relative to the project
    /// ("icon.png"), or empty for none.
    /// </summary>
    public string IconFile { get; set; } = string.Empty;

    /// <summary>Page du mod (affichée dans la liste des mods en jeu), ou vide.</summary>
    public string Website { get; set; } = string.Empty;

    /// <summary>
    /// MDK utilisé pour générer le projet Minecraft (par exemple "forge-1.20.1-47.4.10").
    /// Il détermine le loader et la version de Minecraft.
    /// </summary>
    public string? MdkId { get; set; }

    public string MinecraftVersion { get; set; } = string.Empty;

    public string LoaderId { get; set; } = string.Empty;

    /// <summary>
    /// Dossiers créés dans le Dashboard, même vides ("Armes", "Armes/Épées"). Les dossiers
    /// qui contiennent des assets existent aussi sans être listés ici.
    /// </summary>
    public IReadOnlyList<string> Folders
    {
        get => _folders;
        set => _folders = [.. (value ?? []).Select(AssetFolders.Normalize).OfType<string>().Where(f => f.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// Fichiers générés que l'utilisateur a verrouillés pour les modifier à la main
    /// (chemins relatifs au projet, avec des '/') : Mine Engine ne les réécrit plus.
    /// </summary>
    public IReadOnlyList<string> LockedFiles
    {
        get => _lockedFiles;
        set => _lockedFiles = [.. (value ?? []).Select(NormalizePath).Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)];
    }

    public bool IsLocked(string projectRelativePath) =>
        _lockedFiles.Contains(NormalizePath(projectRelativePath), StringComparer.OrdinalIgnoreCase);

    private static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Trim().Trim('/');

    /// <summary>Copie indépendante de ces paramètres.</summary>
    public ProjectSettings Clone()
    {
        var copy = new ProjectSettings(ModId, ModName);
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>Reprend toutes les valeurs d'autres paramètres du même mod.</summary>
    public void CopyFrom(ProjectSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.ModId != ModId)
        {
            throw new ArgumentException(Tr.T("The settings of another mod cannot be copied."), nameof(other));
        }

        ModName = other.ModName;
        ModVersion = other.ModVersion;
        Authors = other.Authors;
        Description = other.Description;
        License = other.License;
        IconFile = other.IconFile;
        Website = other.Website;
        MdkId = other.MdkId;
        MinecraftVersion = other.MinecraftVersion;
        LoaderId = other.LoaderId;
        Folders = other.Folders;
        LockedFiles = other.LockedFiles;
    }
}
