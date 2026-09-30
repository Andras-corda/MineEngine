using MineEngine.Core.Identifiers;

namespace MineEngine.Project;

/// <summary>Paramètres généraux d'un projet, enregistrés dans Project.json.</summary>
public sealed class ProjectSettings
{
    public const int CurrentFormatVersion = 1;

    private string _modName;

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

    /// <summary>Page du mod (affichée dans la liste des mods en jeu), ou vide.</summary>
    public string Website { get; set; } = string.Empty;

    /// <summary>
    /// MDK utilisé pour générer le projet Minecraft (par exemple "forge-1.20.1-47.4.10").
    /// Il détermine le loader et la version de Minecraft.
    /// </summary>
    public string? MdkId { get; set; }

    public string MinecraftVersion { get; set; } = string.Empty;

    public string LoaderId { get; set; } = string.Empty;
}
