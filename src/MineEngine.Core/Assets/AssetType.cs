namespace MineEngine.Core.Assets;

public enum AssetType
{
    Item,
    Block,
    Mob,
    Recipe,
    Texture,
    Sound,

    /// <summary>Graphe nodal : comportements déclenchés par des événements du jeu.</summary>
    Graph,

    /// <summary>Arbre de comportement Puppeteer : l'intelligence d'un mob, en nœuds.</summary>
    BehaviorTree,

    /// <summary>3D model imported into the project (Java JSON, Blockbench, GeckoLib, OBJ, glTF).</summary>
    Model,

    /// <summary>Chat command of the mod ("/heal"), run by event graphs (V0.6.2).</summary>
    Command,

    /// <summary>Tab of the creative inventory, with the items chosen for it (V0.6.2).</summary>
    CreativeTab,

    /// <summary>Key of the controls that triggers event graphs (V0.6.2).</summary>
    Keybind,

    /// <summary>Enchantment of the mod (V0.6.3).</summary>
    Enchantment,

    /// <summary>Fluid of the mod: its source and flowing fluid, its block and its bucket (V0.6.3).</summary>
    Fluid,

    /// <summary>Game rule of the mod ("/gamerule") (V0.6.3).</summary>
    GameRule,

    /// <summary>Status effect of the mod, with an optional potion (V0.6.3).</summary>
    Effect,
}

/// <summary>
/// Espace dans lequel l'identifiant d'un asset doit être unique. Items, blocs et mobs
/// partagent le même (ils se côtoient dans les registres et les onglets du jeu) ;
/// une texture peut porter le même nom que l'item qui l'utilise.
/// </summary>
public enum AssetIdScope
{
    GameContent,
    Recipe,
    Texture,
    Sound,
    Graph,
    BehaviorTree,
    Model,
    Command,
    CreativeTab,
    Keybind,
    Enchantment,
    GameRule,
    Effect,
}

public static class AssetTypeExtensions
{
    public static AssetIdScope IdScope(this AssetType type) => type switch
    {
        AssetType.Recipe => AssetIdScope.Recipe,
        AssetType.Texture => AssetIdScope.Texture,
        AssetType.Sound => AssetIdScope.Sound,
        AssetType.Graph => AssetIdScope.Graph,
        AssetType.BehaviorTree => AssetIdScope.BehaviorTree,
        AssetType.Model => AssetIdScope.Model,
        AssetType.Command => AssetIdScope.Command,
        AssetType.CreativeTab => AssetIdScope.CreativeTab,
        AssetType.Keybind => AssetIdScope.Keybind,
        AssetType.Enchantment => AssetIdScope.Enchantment,
        AssetType.GameRule => AssetIdScope.GameRule,
        AssetType.Effect => AssetIdScope.Effect,
        _ => AssetIdScope.GameContent,
    };
}

public sealed class AssetChangedEventArgs : EventArgs
{
    public AssetChangedEventArgs(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
