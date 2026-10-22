using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Appearances (other looks switched by the graphs): an item and a cube block take a texture and a
/// 3D model, a column a texture and an empty one, a mob of the game and a Blockbench mob a texture.
/// Added to the sample content after everything else, so that the end-to-end builds compile them.
/// </summary>
internal static class AppearanceSample
{
    public static void AddTo(ModProject project)
    {
        Guid texture(string id) => project.Assets.FindByResourceId(ResourceId.Parse(id), AssetType.Texture)!.Id;
        T find<T>(string id, AssetType type) where T : TexturedAsset => (T)project.Assets.FindByResourceId(ResourceId.Parse(id), type)!;
        Guid sword = texture("magic_sword_texture");
        Guid goblin = texture("goblin");
        Guid model = project.Assets.OfType<ModelAsset>().First(m => m.IsUsedByTheGame).Id;

        find<ItemAsset>("ruby", AssetType.Item).Appearances =
        [
            new AssetAppearance(Guid.NewGuid(), "Sword", sword, null),
            new AssetAppearance(Guid.NewGuid(), "Cat", null, model),
        ];
        find<ItemAsset>("cat_figurine", AssetType.Item).Appearances = [new AssetAppearance(Guid.NewGuid(), "Flat", goblin, null)];
        find<BlockAsset>("ruby_ore", AssetType.Block).Appearances =
        [
            new AssetAppearance(Guid.NewGuid(), "Goblin", goblin, null),
            new AssetAppearance(Guid.NewGuid(), "Cat", null, model),
        ];
        find<BlockAsset>("star_log", AssetType.Block).Appearances =
        [
            new AssetAppearance(Guid.NewGuid(), "Sword", sword, null),
            AssetAppearance.Create("Empty"),
        ];
        find<MobAsset>("goblin", AssetType.Mob).Appearances =
        [
            new AssetAppearance(Guid.NewGuid(), "Sword", sword, null),
            AssetAppearance.Create("Empty"),
        ];
        find<MobAsset>("alley_cat", AssetType.Mob).Appearances = [new AssetAppearance(Guid.NewGuid(), "Goblin", goblin, null)];
    }
}
