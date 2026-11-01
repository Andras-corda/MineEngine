using MineEngine.Core.Assets;

namespace MineEngine.Assets;

/// <summary>Sub-categories of the Resources tab (V0.7.14): what a texture, a sound or a 3D model is for.</summary>
public enum ResourceGroup
{
    Items,
    Blocks,
    Mobs,
    Interface,
    Particles,
    World,
    Graphs,
    Other,
    Unused,
}

/// <summary>
/// Finds the sub-categories of a resource from the elements that use it (a texture used through a 3D model
/// counts for the users of the model) and from its folder ("item", "block", "entity", "gui"...). A resource
/// used by an item and a block is in both; one that nothing uses is also in <see cref="ResourceGroup.Unused"/>.
/// </summary>
public static class ResourceGroups
{
    public static IReadOnlySet<ResourceGroup> Of(Asset resource, AssetRegistry assets)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(assets);
        var groups = new HashSet<ResourceGroup>();
        List<Asset> users = Users(resource, assets);
        foreach (Asset user in users)
        {
            if (user is ModelAsset model)
            {
                foreach (Asset modelUser in Users(model, assets))
                {
                    groups.Add(For(modelUser.Type));
                }
            }
            else
            {
                groups.Add(For(user.Type));
            }
        }

        if (resource is FileAsset file && FromPath(file.FilePath) is { } hint)
        {
            groups.Add(hint);
        }

        if (users.Count == 0)
        {
            groups.Add(ResourceGroup.Unused);
        }

        return groups;
    }

    /// <summary>Group of the resources used by an element of this type.</summary>
    public static ResourceGroup For(AssetType type) => type switch
    {
        AssetType.Item => ResourceGroup.Items,
        AssetType.Block or AssetType.Plant or AssetType.Fluid => ResourceGroup.Blocks,
        AssetType.Mob or AssetType.BehaviorTree => ResourceGroup.Mobs,
        AssetType.Effect or AssetType.Enchantment or AssetType.CreativeTab or AssetType.Keybind or AssetType.Command => ResourceGroup.Interface,
        AssetType.Sparkle => ResourceGroup.Particles,
        AssetType.Biome => ResourceGroup.World,
        AssetType.Graph => ResourceGroup.Graphs,
        _ => ResourceGroup.Other,
    };

    private static List<Asset> Users(Asset asset, AssetRegistry assets) =>
        [.. assets.FindReferencesTo(asset.Id).Select(r => r.Source).Where(s => s.Id != asset.Id).Distinct()];

    /// <summary>Group named by a folder of the file, the way Minecraft sorts its textures.</summary>
    private static ResourceGroup? FromPath(string path)
    {
        foreach (string segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1).Reverse())
        {
            ResourceGroup? group = segment.ToLowerInvariant() switch
            {
                "item" or "items" => ResourceGroup.Items,
                "block" or "blocks" => ResourceGroup.Blocks,
                "entity" or "entities" or "mob" or "mobs" => ResourceGroup.Mobs,
                "gui" or "ui" or "mob_effect" or "effect" or "effects" => ResourceGroup.Interface,
                "particle" or "particles" => ResourceGroup.Particles,
                _ => null,
            };
            if (group is not null)
            {
                return group;
            }
        }

        return null;
    }
}
