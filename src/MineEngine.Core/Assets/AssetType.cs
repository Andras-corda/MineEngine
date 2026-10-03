namespace MineEngine.Core.Assets;

public enum AssetType
{
    Item,
    Block,
    Mob,
    Recipe,
    Texture,
    Sound,
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
}

public static class AssetTypeExtensions
{
    public static AssetIdScope IdScope(this AssetType type) => type switch
    {
        AssetType.Recipe => AssetIdScope.Recipe,
        AssetType.Texture => AssetIdScope.Texture,
        AssetType.Sound => AssetIdScope.Sound,
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
