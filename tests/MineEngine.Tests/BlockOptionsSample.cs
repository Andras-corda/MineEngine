using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Advanced settings of blocks (like the block editor of MCreator): a machine that turns and can
/// be placed on walls and ceilings, holds water, has a shape of boxes, properties of its own,
/// emits redstone, burns and colors the beam of a beacon; a falling block; a glass that faces
/// every direction; and an ore generated in the world. Added after everything else, so that the
/// end-to-end builds compile them.
/// </summary>
internal static class BlockOptionsSample
{
    public static IReadOnlyList<BlockAsset> AddTo(ModProject project)
    {
        Guid texture = project.Assets.FindByResourceId(ResourceId.Parse("goblin"), Core.Assets.AssetType.Texture)?.Id
                       ?? project.Assets.OfType<TextureAsset>().First().Id;
        var machine = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("rune_machine"), "Machine runique")
        {
            Options = new BlockOptions
            {
                Rotation = BlockRotation.Horizontal,
                HasPitchRotation = true,
                Waterloggable = true,
                Emissive = true,
                MapColor = "COLOR_PURPLE",
                Instrument = "BELL",
                PushReaction = BlockPushReaction.Block,
                EnchantPowerBonus = 1.5f,
                IgnitedByLava = true,
                Flammability = 20,
                FireSpreadSpeed = 5,
                RedstoneConnects = true,
                EmitsRedstone = true,
                RedstonePower = 9,
                BeaconColor = "MAGENTA",
                ItemTextureId = texture,
                ParticleTextureId = texture,
                Boxes = [new BlockBox(0, 0, 0, 16, 8, 16), new BlockBox(4, 8, 4, 12, 14, 12), new BlockBox(6, 0, 6, 10, 4, 10, Subtract: true)],
                Properties =
                [
                    new BlockCustomProperty("powered", BlockPropertyKind.Boolean),
                    new BlockCustomProperty("charge", BlockPropertyKind.Integer, 0, 3),
                    new BlockCustomProperty("rune", BlockPropertyKind.Choice) { Values = ["fire", "ice", "storm"] },
                ],
            },
            Appearances = [new AssetAppearance(Guid.NewGuid(), "Allumée", texture, null)],
        };
        var sand = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("star_sand"), "Sable étoilé")
        {
            Sound = BlockSoundType.Sand,
            Options = new BlockOptions { HasGravity = true, PlantsCanSustain = true, Replaceable = false, RandomOffset = BlockOffset.XZ },
        };
        var glass = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("rune_glass"), "Verre runique")
        {
            RenderType = BlockRenderType.Translucent,
            Options = new BlockOptions
            {
                Rotation = BlockRotation.AllDirections,
                CustomLightOpacity = true,
                LightOpacity = 0,
                ConnectedSides = true,
                Ladder = true,
                WalkThrough = false,
                Boxes = [new BlockBox(0, 0, 0, 16, 16, 4)],
            },
        };
        // V0.7.11: only a diamond pickaxe harvests the ore, and only where its function says yes.
        GraphAsset canDrop = EntitySample.Function(project, "ore_can_drop", DataType.Position, DataType.Boolean, "true");
        var ore = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("moon_ore"), "Minerai lunaire")
        {
            ExperienceMin = 1,
            ExperienceMax = 3,
            HarvestTool = HarvestTool.Pickaxe,
            RequiresCorrectTool = true,
            RequiredTool = ContentReference.ToId(NamespacedId.TryParse("minecraft:diamond_pickaxe", out NamespacedId? pickaxe) ? pickaxe! : throw new InvalidOperationException()),
            DropCondition = canDrop.Id,
            Options = new BlockOptions
            {
                Ore = new BlockOreGeneration
                {
                    Enabled = true,
                    Replace = ["#minecraft:stone_ore_replaceables", "minecraft:deepslate"],
                    Biomes = ["minecraft:desert", "badlands"],
                    Shape = OreShape.Triangle,
                    GroupsPerChunk = 6,
                    GroupSize = 8,
                    MinHeight = -32,
                    MaxHeight = 48,
                },
                Properties = [new BlockCustomProperty("cracked", BlockPropertyKind.Boolean)],
            },
        };
        BlockAsset[] blocks = [machine, sand, glass, ore];
        foreach (BlockAsset block in blocks)
        {
            block.TextureId = texture;
            project.Assets.Add(block);
        }

        return blocks;
    }
}
