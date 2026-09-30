using MineEngine.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Tests;

public sealed class AssetRegistryTests
{
    [Fact]
    public void Unique_identifiers_get_a_numeric_suffix()
    {
        var registry = new AssetRegistry();
        registry.Add(new ItemAsset(Guid.NewGuid(), registry.CreateUniqueResourceId("new_item"), "A"));
        registry.Add(new ItemAsset(Guid.NewGuid(), registry.CreateUniqueResourceId("new_item"), "B"));

        Assert.Equal("new_item_3", registry.CreateUniqueResourceId("new_item").Value);
    }

    [Fact]
    public void Duplicate_identifier_is_rejected()
    {
        var registry = new AssetRegistry();
        registry.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby block")));
    }

    [Fact]
    public void Modifying_an_asset_raises_a_registry_change()
    {
        var registry = new AssetRegistry();
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby");
        registry.Add(item);

        AssetRegistryChange? change = null;
        registry.Changed += (_, e) => change = e.Change;
        item.MaxStackSize = 16;

        Assert.Equal(AssetRegistryChange.Modified, change);
    }

    [Fact]
    public void Stack_size_outside_limits_is_refused()
    {
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby");
        Assert.Throws<ArgumentOutOfRangeException>(() => item.MaxStackSize = 100);
    }
}
