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
public sealed partial class BlockResourcesEmitter : ResourceEmitter
{
    public BlockResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => Tr.T("Block resources");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRBlock block in context.Ir.Content.Blocks)
        {
            if (block.Plant is { } plant)
            {
                EmitPlant(context, block, plant);
                continue;
            }

            if (block.Settings.Model.IsBase())
            {
                EmitBase(context, block);
                continue;
            }

            string blockModel = paths.Reference($"block/{block.Id}");
            // A block shown with a 3D model takes the textures of the model (see ModelsEmitter).
            IReadOnlyList<BlockTextureSlot> slots = block.CustomModel is null ? UsedSlots(block.Settings.Model) : [];

            foreach (BlockTextureSlot slot in slots.Where(s => s == BlockTextureSlot.Main || block.ExtraTextures.ContainsKey(s)))
            {
                WriteTexture(context, block.GetTexture(slot), paths.BlockTexture(block.Id, Suffix(slot)));
            }

            JsonObject baseModel = CreateBlockModel(block, paths);
            WriteJson(context, paths.BlockState(block.Id), CreateBlockState(block, blockModel, paths));
            WriteJson(context, paths.BlockModel(block.Id), baseModel);
            WriteAppearances(context, block, baseModel);

            if (block.ItemTexture is { } itemTexture)
            {
                WriteTexture(context, itemTexture, paths.BlockTexture(block.Id, "_item"));
            }

            if (block.ParticleTexture is { } particle)
            {
                WriteTexture(context, particle, paths.BlockTexture(block.Id, "_particle"));
            }

            if (block.ItemForm is not null && block.ItemTexture is not null)
            {
                // Its own texture in the inventory and in hand, like an item.
                WriteJson(context, paths.ItemModel(block.Id), new JsonObject
                {
                    ["parent"] = "minecraft:item/generated",
                    ["textures"] = new JsonObject { ["layer0"] = paths.Reference($"block/{block.Id}_item") },
                });
            }
            else if (block.ItemForm is not null)
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

    /// <summary>
    /// Plant (V0.7.0): a cross (flower, growing plant), two crosses (bottom and top of a tall plant)
    /// or one model per stage of a crop; its item is a flat texture (the seeds of a crop, the top of a
    /// tall plant); a tall plant drops itself from its bottom half, a ripe crop its produce and seeds.
    /// </summary>
    private void EmitPlant(GenerationContext context, IRBlock block, MineEngine.IR.Model.IRPlant plant)
    {
        ResourcePaths paths = context.Paths;
        string id = block.Id.Value;
        JsonObject Cross(string texture)
        {
            var model = new JsonObject { ["parent"] = "minecraft:block/cross", ["textures"] = new JsonObject { ["cross"] = texture } };
            AddRenderType(block, model);
            return model;
        }

        WriteTexture(context, block.Texture, paths.BlockTexture(block.Id));
        string itemTexture = paths.Reference($"block/{id}");
        JsonObject blockState;
        JsonObject? lootTable = CreateLootTable(block, paths);
        switch (plant.Kind)
        {
            case MineEngine.Assets.PlantKind.Tall:
            {
                WriteTexture(context, plant.TopTexture ?? block.Texture, paths.BlockTexture(block.Id, "_top"));
                WriteJson(context, paths.BlockModel(block.Id), Cross(paths.Reference($"block/{id}")));
                WriteJson(context, paths.BlockModel(MineEngine.Core.Identifiers.ResourceId.Parse(id + "_top")), Cross(paths.Reference($"block/{id}_top")));
                blockState = new JsonObject
                {
                    ["variants"] = new JsonObject
                    {
                        ["half=lower"] = new JsonObject { ["model"] = paths.Reference($"block/{id}") },
                        ["half=upper"] = new JsonObject { ["model"] = paths.Reference($"block/{id}_top") },
                    },
                };
                itemTexture = paths.Reference($"block/{id}_top");
                lootTable = new JsonObject
                {
                    ["type"] = "minecraft:block",
                    ["pools"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["rolls"] = 1,
                            ["entries"] = new JsonArray { new JsonObject { ["type"] = "minecraft:item", ["name"] = paths.Reference(id) } },
                            ["conditions"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["condition"] = "minecraft:block_state_property",
                                    ["block"] = paths.Reference(id),
                                    ["properties"] = new JsonObject { ["half"] = "lower" },
                                },
                                new JsonObject { ["condition"] = "minecraft:survives_explosion" },
                            },
                        },
                    },
                };
                break;
            }

            case MineEngine.Assets.PlantKind.Crop:
            {
                IReadOnlyList<IRTexture> stages = plant.Stages.Count > 0 ? plant.Stages : [block.Texture];
                for (int i = 0; i < stages.Count; i++)
                {
                    string stage = $"{id}_stage{i}";
                    WriteTexture(context, stages[i], paths.BlockTexture(MineEngine.Core.Identifiers.ResourceId.Parse(stage)));
                    var model = new JsonObject { ["parent"] = "minecraft:block/crop", ["textures"] = new JsonObject { ["crop"] = paths.Reference($"block/{stage}") } };
                    AddRenderType(block, model);
                    WriteJson(context, paths.BlockModel(MineEngine.Core.Identifiers.ResourceId.Parse(stage)), model);
                }

                // The 8 ages of a crop share the stages: age 7 is always the last (ripe) one.
                var variants = new JsonObject();
                for (int age = 0; age <= 7; age++)
                {
                    int stage = age == 7 ? stages.Count - 1 : Math.Min(stages.Count - 1, age * stages.Count / 8);
                    variants[$"age={age}"] = new JsonObject { ["model"] = paths.Reference($"block/{id}_stage{stage}") };
                }

                blockState = new JsonObject { ["variants"] = variants };
                lootTable = CropLootTable(block, plant, paths);
                break;
            }

            default:
                WriteJson(context, paths.BlockModel(block.Id), Cross(paths.Reference($"block/{id}")));
                blockState = new JsonObject { ["variants"] = new JsonObject { [string.Empty] = new JsonObject { ["model"] = paths.Reference($"block/{id}") } } };
                break;
        }

        WriteJson(context, paths.BlockState(block.Id), blockState);
        if (block.ItemForm is not null)
        {
            WriteJson(context, paths.ItemModel(block.Id), new JsonObject
            {
                ["parent"] = "minecraft:item/generated",
                ["textures"] = new JsonObject { ["layer0"] = itemTexture },
            });
        }

        if (lootTable is not null)
        {
            WriteJson(context, paths.BlockLootTable(block.Id), lootTable);
        }
    }

    /// <summary>A ripe crop (age 7) drops its produce and seeds; a young one its seeds only.</summary>
    private static JsonObject CropLootTable(IRBlock block, MineEngine.IR.Model.IRPlant plant, ResourcePaths paths)
    {
        JsonObject Ripe() => new()
        {
            ["condition"] = "minecraft:block_state_property",
            ["block"] = paths.Reference(block.Id.Value),
            ["properties"] = new JsonObject { ["age"] = "7" },
        };

        var pools = new JsonArray
        {
            new JsonObject
            {
                ["rolls"] = 1,
                ["entries"] = new JsonArray { new JsonObject { ["type"] = "minecraft:item", ["name"] = paths.Reference(block.Id.Value) } },
            },
            new JsonObject
            {
                ["rolls"] = 1,
                ["entries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "minecraft:item",
                        ["name"] = paths.Reference(block.Id.Value),
                        ["functions"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["function"] = "minecraft:set_count",
                                ["count"] = new JsonObject { ["type"] = "minecraft:uniform", ["min"] = 0, ["max"] = 2 },
                            },
                        },
                    },
                },
                ["conditions"] = new JsonArray { Ripe() },
            },
        };
        if (plant.Produce is { } produce)
        {
            JsonNode count = plant.ProduceMin == plant.ProduceMax
                ? JsonValue.Create(plant.ProduceMin)
                : new JsonObject { ["type"] = "minecraft:uniform", ["min"] = plant.ProduceMin, ["max"] = plant.ProduceMax };
            pools.Add(new JsonObject
            {
                ["rolls"] = 1,
                ["entries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "minecraft:item",
                        ["name"] = produce.ToString(),
                        ["functions"] = new JsonArray { new JsonObject { ["function"] = "minecraft:set_count", ["count"] = count } },
                    },
                },
                ["conditions"] = new JsonArray { Ripe() },
            });
        }

        return new JsonObject { ["type"] = "minecraft:block", ["pools"] = pools };
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

    /// <summary>
    /// Blockstate: one variant per value of the "appearance" property (0: the normal model) and per
    /// orientation: axis of a column (turned like a log), facing of a block that turns (and its face:
    /// floor, wall, ceiling). The other properties (water, those of the user) do not change the model.
    /// </summary>
    private static JsonObject CreateBlockState(IRBlock block, string blockModel, ResourcePaths paths)
    {
        int count = block.Appearances.Count;
        string ModelFor(int number) => number == 0 ? blockModel : paths.Reference($"block/{IRAppearance.FileId(block.Id, number)}");
        string Key(string orientation, int number) => string.Join(",", new[] { orientation, count == 0 ? string.Empty : $"appearance={number}" }.Where(p => p.Length > 0));

        var variants = new JsonObject();
        for (int number = 0; number <= count; number++)
        {
            foreach ((string orientation, int x, int y) in Orientations(block))
            {
                var variant = new JsonObject { ["model"] = ModelFor(number) };
                if (x != 0)
                {
                    variant["x"] = x;
                }

                if (y != 0)
                {
                    variant["y"] = y;
                }

                variants[Key(orientation, number)] = variant;
            }
        }

        return new JsonObject { ["variants"] = variants };
    }

    private static readonly (string Name, int Y)[] Horizontal = [("north", 0), ("east", 90), ("south", 180), ("west", 270)];

    /// <summary>Orientations of the block: the key of the variant and its rotation (x, y) in degrees.</summary>
    private static IEnumerable<(string Key, int X, int Y)> Orientations(IRBlock block)
    {
        if (block.Settings.Model == BlockModelKind.Column)
        {
            return [("axis=x", 90, 90), ("axis=y", 0, 0), ("axis=z", 90, 0)];
        }

        return block.Options.Rotation switch
        {
            MineEngine.Assets.BlockRotation.Horizontal when block.Options.HasPitchRotation =>
            [
                .. Horizontal.Select(h => ($"face=floor,facing={h.Name}", 0, h.Y)),
                .. Horizontal.Select(h => ($"face=wall,facing={h.Name}", 90, h.Y)),
                .. Horizontal.Select(h => ($"face=ceiling,facing={h.Name}", 180, (h.Y + 180) % 360)),
            ],
            MineEngine.Assets.BlockRotation.Horizontal => [.. Horizontal.Select(h => ($"facing={h.Name}", 0, h.Y))],
            MineEngine.Assets.BlockRotation.AllDirections => [.. Horizontal.Select(h => ($"facing={h.Name}", 0, h.Y)), ("facing=up", 270, 0), ("facing=down", 90, 0)],
            _ => [(string.Empty, 0, 0)],
        };
    }

    /// <summary>
    /// Model of each appearance: its 3D model, or the shape of the block covered with its texture
    /// (every face); without either, the normal model.
    /// </summary>
    private void WriteAppearances(GenerationContext context, IRBlock block, JsonObject baseModel)
    {
        ResourcePaths paths = context.Paths;
        for (int number = 1; number <= block.Appearances.Count; number++)
        {
            IRAppearance appearance = block.Appearances[number - 1];
            MineEngine.Core.Identifiers.ResourceId fileId = IRAppearance.FileId(block.Id, number);
            string texture = paths.Reference($"block/{fileId}");
            JsonObject model;
            if (appearance.Model is { } custom)
            {
                model = new JsonObject { ["parent"] = paths.CustomModelReference(custom) };
            }
            else if (appearance.Texture is { } image)
            {
                WriteTexture(context, image, paths.BlockTexture(fileId));
                model = block.Settings.Model switch
                {
                    BlockModelKind.Cross => new JsonObject { ["parent"] = "minecraft:block/cross", ["textures"] = new JsonObject { ["cross"] = texture } },
                    BlockModelKind.Column => new JsonObject
                    {
                        ["parent"] = "minecraft:block/cube_column",
                        ["textures"] = new JsonObject { ["end"] = texture, ["side"] = texture },
                    },
                    _ => new JsonObject { ["parent"] = "minecraft:block/cube_all", ["textures"] = new JsonObject { ["all"] = texture } },
                };
            }
            else
            {
                model = (JsonObject)baseModel.DeepClone();
            }

            AddRenderType(block, model);
            WriteJson(context, paths.BlockModel(fileId), model);
        }
    }

    /// <summary>Forge et NeoForge lisent le type de rendu directement dans le modèle.</summary>
    private static void AddRenderType(IRBlock block, JsonObject model)
    {
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
    }

    private static JsonObject CreateBlockModel(IRBlock block, ResourcePaths paths)
    {
        string Ref(BlockTextureSlot slot) => TextureReference(block, slot, paths);

        JsonObject model = block.Settings.Model switch
        {
            BlockModelKind.Custom when block.CustomModel is { } custom => new JsonObject { ["parent"] = paths.CustomModelReference(custom) },
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

        AddRenderType(block, model);
        if (block.ParticleTexture is not null)
        {
            JsonObject textures = model["textures"] as JsonObject ?? [];
            textures["particle"] = paths.Reference($"block/{block.Id}_particle");
            model["textures"] = textures;
        }

        return model;
    }

    /// <summary>The block survives the explosion; with a required tool, it is mined with that one (V0.7.11).</summary>
    private static JsonArray DropConditions(IRBlock block)
    {
        var conditions = new JsonArray { new JsonObject { ["condition"] = "minecraft:survives_explosion" } };
        if (block.RequiredTool is { } tool)
        {
            // A list of items is read by 1.20.1 and by the item sets of 1.20.5 and later.
            conditions.Add(new JsonObject
            {
                ["condition"] = "minecraft:match_tool",
                ["predicate"] = new JsonObject { ["items"] = new JsonArray { tool.ToString() } },
            });
        }

        return conditions;
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
                    ["conditions"] = DropConditions(block),
                },
            },
        };
    }
}
