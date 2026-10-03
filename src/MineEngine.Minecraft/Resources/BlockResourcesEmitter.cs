using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Blockstate, modèles (bloc et item), textures et loot table de chaque bloc, selon
/// sa forme : cube, cube à six textures, colonne orientable ou croix.
/// </summary>
public sealed class BlockResourcesEmitter : ResourceEmitter
{
    public BlockResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => "Ressources des blocs";

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRBlock block in context.Ir.Content.Blocks)
        {
            string blockModel = paths.Reference($"block/{block.Id}");
            IReadOnlyList<BlockTextureSlot> slots = UsedSlots(block.Settings.Model);

            foreach (BlockTextureSlot slot in slots.Where(s => s == BlockTextureSlot.Main || block.ExtraTextures.ContainsKey(s)))
            {
                WriteTexture(context, block.GetTexture(slot), paths.BlockTexture(block.Id, Suffix(slot)));
            }

            WriteJson(context, paths.BlockState(block.Id), CreateBlockState(block, blockModel));
            WriteJson(context, paths.BlockModel(block.Id), CreateBlockModel(block, paths));

            if (block.ItemForm is not null)
            {
                JsonObject itemModel = block.Settings.Model == BlockModelKind.Cross
                    ? new JsonObject
                    {
                        ["parent"] = "minecraft:item/generated",
                        ["textures"] = new JsonObject { ["layer0"] = blockModel },
                    }
                    : new JsonObject { ["parent"] = blockModel };
                WriteJson(context, paths.ItemModel(block.Id), itemModel);
            }

            JsonObject? lootTable = CreateLootTable(block, paths);
            if (lootTable is not null)
            {
                WriteJson(context, paths.BlockLootTable(block.Id), lootTable);
            }
        }
    }

    /// <summary>Emplacements de texture utilisés par chaque forme.</summary>
    private static IReadOnlyList<BlockTextureSlot> UsedSlots(BlockModelKind model) => model switch
    {
        BlockModelKind.CubeFaces =>
        [
            BlockTextureSlot.Main, BlockTextureSlot.Top, BlockTextureSlot.Bottom,
            BlockTextureSlot.North, BlockTextureSlot.South, BlockTextureSlot.East, BlockTextureSlot.West,
        ],
        BlockModelKind.Column => [BlockTextureSlot.Main, BlockTextureSlot.Top],
        _ => [BlockTextureSlot.Main],
    };

    private static string Suffix(BlockTextureSlot slot) =>
        slot == BlockTextureSlot.Main ? string.Empty : "_" + slot.ToString().ToLowerInvariant();

    /// <summary>Référence de texture d'un emplacement ; un emplacement vide reprend la texture principale.</summary>
    private static string TextureReference(IRBlock block, BlockTextureSlot slot, ResourcePaths paths)
    {
        string suffix = slot != BlockTextureSlot.Main && block.ExtraTextures.ContainsKey(slot) ? Suffix(slot) : string.Empty;
        return paths.Reference($"block/{block.Id}{suffix}");
    }

    private static JsonObject CreateBlockState(IRBlock block, string blockModel)
    {
        if (block.Settings.Model != BlockModelKind.Column)
        {
            return new JsonObject
            {
                ["variants"] = new JsonObject { [""] = new JsonObject { ["model"] = blockModel } },
            };
        }

        // Colonne : orientée selon l'axe de pose, comme une bûche.
        return new JsonObject
        {
            ["variants"] = new JsonObject
            {
                ["axis=x"] = new JsonObject { ["model"] = blockModel, ["x"] = 90, ["y"] = 90 },
                ["axis=y"] = new JsonObject { ["model"] = blockModel },
                ["axis=z"] = new JsonObject { ["model"] = blockModel, ["x"] = 90 },
            },
        };
    }

    private static JsonObject CreateBlockModel(IRBlock block, ResourcePaths paths)
    {
        string Ref(BlockTextureSlot slot) => TextureReference(block, slot, paths);

        JsonObject model = block.Settings.Model switch
        {
            BlockModelKind.CubeFaces => new JsonObject
            {
                ["parent"] = "minecraft:block/cube",
                ["textures"] = new JsonObject
                {
                    ["particle"] = Ref(BlockTextureSlot.Main),
                    ["up"] = Ref(BlockTextureSlot.Top),
                    ["down"] = Ref(BlockTextureSlot.Bottom),
                    ["north"] = Ref(BlockTextureSlot.North),
                    ["south"] = Ref(BlockTextureSlot.South),
                    ["east"] = Ref(BlockTextureSlot.East),
                    ["west"] = Ref(BlockTextureSlot.West),
                },
            },
            BlockModelKind.Column => new JsonObject
            {
                ["parent"] = "minecraft:block/cube_column",
                ["textures"] = new JsonObject
                {
                    ["end"] = Ref(BlockTextureSlot.Top),
                    ["side"] = Ref(BlockTextureSlot.Main),
                },
            },
            BlockModelKind.Cross => new JsonObject
            {
                ["parent"] = "minecraft:block/cross",
                ["textures"] = new JsonObject { ["cross"] = Ref(BlockTextureSlot.Main) },
            },
            _ => new JsonObject
            {
                ["parent"] = "minecraft:block/cube_all",
                ["textures"] = new JsonObject { ["all"] = Ref(BlockTextureSlot.Main) },
            },
        };

        // Forge et NeoForge lisent le type de rendu directement dans le modèle.
        string? renderType = block.Settings.RenderType switch
        {
            BlockRenderType.Cutout => "minecraft:cutout",
            BlockRenderType.CutoutMipped => "minecraft:cutout_mipped",
            BlockRenderType.Translucent => "minecraft:translucent",
            _ => null,
        };
        if (renderType is not null)
        {
            model["render_type"] = renderType;
        }

        return model;
    }

    private static JsonObject? CreateLootTable(IRBlock block, ResourcePaths paths)
    {
        IRBlockDrop drop = block.Drop;
        string? item = drop.Kind switch
        {
            BlockDropKind.Self => paths.Reference(block.Id.Value),
            BlockDropKind.OtherItem => drop.Item!.ToString(),
            _ => null,
        };
        if (item is null)
        {
            return null;
        }

        var entry = new JsonObject { ["type"] = "minecraft:item", ["name"] = item };
        if (drop.Kind == BlockDropKind.OtherItem && (drop.Min != 1 || drop.Max != 1))
        {
            JsonNode count = drop.Min == drop.Max
                ? JsonValue.Create(drop.Min)
                : new JsonObject { ["type"] = "minecraft:uniform", ["min"] = drop.Min, ["max"] = drop.Max };
            entry["functions"] = new JsonArray
            {
                new JsonObject { ["function"] = "minecraft:set_count", ["count"] = count },
            };
        }

        return new JsonObject
        {
            ["type"] = "minecraft:block",
            ["pools"] = new JsonArray
            {
                new JsonObject
                {
                    ["rolls"] = 1,
                    ["entries"] = new JsonArray { entry },
                    ["conditions"] = new JsonArray
                    {
                        new JsonObject { ["condition"] = "minecraft:survives_explosion" },
                    },
                },
            },
        };
    }
}
