namespace MineEngine.Core.Assets;

public enum AssetType
{
    Item,
    Block,
}

public sealed class AssetChangedEventArgs : EventArgs
{
    public AssetChangedEventArgs(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
