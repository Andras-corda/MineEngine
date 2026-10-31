using System.Text.Json.Nodes;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Resources of the block bases (V0.7.11): their models made from the templates of the game, and the
/// blockstates of the game (the same rotations as the oak or stone blocks). The main texture is the
/// side; the top and bottom textures are the faces of the stairs and slabs and the halves of a door,
/// the top texture the edge of a pane.
/// </summary>
public sealed partial class BlockResourcesEmitter
{
    private static readonly (string Facing, int Yaw)[] Facings = [("east", 0), ("south", 90), ("west", 180), ("north", 270)];

    private void EmitBase(GenerationContext context, IRBlock block)
    {
        ResourcePaths paths = context.Paths;
        string id = block.Id.Value;
        foreach (BlockTextureSlot slot in new[] { BlockTextureSlot.Main, BlockTextureSlot.Top, BlockTextureSlot.Bottom }
                     .Where(s => s == BlockTextureSlot.Main || block.ExtraTextures.ContainsKey(s)))
        {
            WriteTexture(context, block.GetTexture(slot), paths.BlockTexture(block.Id, Suffix(slot)));
        }

        string side = TextureReference(block, BlockTextureSlot.Main, paths);
        string top = TextureReference(block, BlockTextureSlot.Top, paths);
        string bottom = TextureReference(block, BlockTextureSlot.Bottom, paths);
        string Ref(string suffix) => paths.Reference($"block/{id}{suffix}");
        void Model(string suffix, string parent, params (string Key, string Texture)[] textures)
        {
            var model = new JsonObject { ["parent"] = "minecraft:block/" + parent, ["textures"] = new JsonObject() };
            foreach ((string key, string texture) in textures)
            {
                model["textures"]![key] = texture;
            }

            AddRenderType(block, model);
            WriteJson(context, paths.BlockModel(ResourceId.Parse(id + suffix)), model);
        }

        JsonObject blockState;
        string? itemParent = Ref(string.Empty);
        JsonObject? lootTable = CreateLootTable(block, context);
        switch (block.Settings.Model)
        {
            case BlockModelKind.Stairs:
                Model(string.Empty, "stairs", ("bottom", bottom), ("top", top), ("side", side));
                Model("_inner", "inner_stairs", ("bottom", bottom), ("top", top), ("side", side));
                Model("_outer", "outer_stairs", ("bottom", bottom), ("top", top), ("side", side));
                blockState = StairsState(Ref);
                break;

            case BlockModelKind.Slab:
                Model(string.Empty, "slab", ("bottom", bottom), ("top", top), ("side", side));
                Model("_top", "slab_top", ("bottom", bottom), ("top", top), ("side", side));
                Model("_double", "cube_bottom_top", ("bottom", bottom), ("top", top), ("side", side));
                blockState = Variants(("type=bottom", Variant(Ref(string.Empty))), ("type=top", Variant(Ref("_top"))), ("type=double", Variant(Ref("_double"))));
                DoubleSlabDrop(lootTable, block, paths);
                break;

            case BlockModelKind.Fence:
                Model("_post", "fence_post", ("texture", side));
                Model("_side", "fence_side", ("texture", side));
                Model("_inventory", "fence_inventory", ("texture", side));
                blockState = Sides(Ref("_post"), Ref("_side"), "true");
                itemParent = Ref("_inventory");
                break;

            case BlockModelKind.FenceGate:
                Model(string.Empty, "template_fence_gate", ("texture", side));
                Model("_open", "template_fence_gate_open", ("texture", side));
                Model("_wall", "template_fence_gate_wall", ("texture", side));
                Model("_wall_open", "template_fence_gate_wall_open", ("texture", side));
                blockState = FenceGateState(Ref);
                break;

            case BlockModelKind.Wall:
                Model("_post", "template_wall_post", ("wall", side));
                Model("_side", "template_wall_side", ("wall", side));
                Model("_side_tall", "template_wall_side_tall", ("wall", side));
                Model("_inventory", "wall_inventory", ("wall", side));
                blockState = WallState(Ref);
                itemParent = Ref("_inventory");
                break;

            case BlockModelKind.Door:
                foreach (string half in new[] { "bottom", "top" })
                {
                    foreach (string hinge in new[] { "left", "right" })
                    {
                        Model($"_{half}_{hinge}", $"door_{half}_{hinge}", ("top", top), ("bottom", bottom));
                        Model($"_{half}_{hinge}_open", $"door_{half}_{hinge}_open", ("top", top), ("bottom", bottom));
                    }
                }

                blockState = DoorState(Ref);
                LowerHalfDrop(lootTable, block, paths);
                itemParent = null;
                break;

            case BlockModelKind.Trapdoor:
                Model("_bottom", "template_orientable_trapdoor_bottom", ("texture", side));
                Model("_top", "template_orientable_trapdoor_top", ("texture", side));
                Model("_open", "template_orientable_trapdoor_open", ("texture", side));
                blockState = TrapdoorState(Ref);
                itemParent = Ref("_bottom");
                break;

            case BlockModelKind.Pane:
                Model("_post", "template_glass_pane_post", ("pane", side), ("edge", top));
                Model("_side", "template_glass_pane_side", ("pane", side), ("edge", top));
                Model("_side_alt", "template_glass_pane_side_alt", ("pane", side), ("edge", top));
                Model("_noside", "template_glass_pane_noside", ("pane", side));
                Model("_noside_alt", "template_glass_pane_noside_alt", ("pane", side));
                blockState = PaneState(Ref);
                itemParent = null;
                break;

            case BlockModelKind.Button:
                Model(string.Empty, "button", ("texture", side));
                Model("_pressed", "button_pressed", ("texture", side));
                Model("_inventory", "button_inventory", ("texture", side));
                blockState = ButtonState(Ref);
                itemParent = Ref("_inventory");
                break;

            default:
                Model(string.Empty, "pressure_plate_up", ("texture", side));
                Model("_down", "pressure_plate_down", ("texture", side));
                blockState = Variants(("powered=false", Variant(Ref(string.Empty))), ("powered=true", Variant(Ref("_down"))));
                break;
        }

        WriteJson(context, paths.BlockState(block.Id), blockState);
        if (block.ItemTexture is { } itemTexture)
        {
            WriteTexture(context, itemTexture, paths.BlockTexture(block.Id, "_item"));
        }

        if (block.ItemForm is not null)
        {
            // A door and a pane are flat in the inventory, like those of the game.
            JsonObject itemModel = block.ItemTexture is not null || itemParent is null
                ? new JsonObject
                {
                    ["parent"] = "minecraft:item/generated",
                    ["textures"] = new JsonObject { ["layer0"] = block.ItemTexture is not null ? Ref("_item") : side },
                }
                : new JsonObject { ["parent"] = itemParent };
            WriteJson(context, paths.ItemModel(block.Id), itemModel);
        }

        if (lootTable is not null)
        {
            WriteJson(context, paths.BlockLootTable(block.Id), lootTable);
        }
    }

    private static JsonObject Variant(string model, int x = 0, int y = 0, bool uvlock = false)
    {
        var variant = new JsonObject { ["model"] = model };
        if (x != 0)
        {
            variant["x"] = x;
        }

        if (y != 0)
        {
            variant["y"] = y;
        }

        if (uvlock)
        {
            variant["uvlock"] = true;
        }

        return variant;
    }

    private static JsonObject Variants(params (string Key, JsonObject Variant)[] variants)
    {
        var all = new JsonObject();
        foreach ((string key, JsonObject variant) in variants)
        {
            all[key] = variant;
        }

        return new JsonObject { ["variants"] = all };
    }

    private static JsonObject Part(JsonObject apply, JsonObject? when = null)
    {
        var part = new JsonObject { ["apply"] = apply };
        if (when is not null)
        {
            part["when"] = when;
        }

        return part;
    }

    /// <summary>The stairs of the game: an outer or inner corner turns a quarter more on the left side, and on the top half on the right side.</summary>
    private static JsonObject StairsState(Func<string, string> model)
    {
        var variants = new List<(string, JsonObject)>();
        foreach ((string facing, int yaw) in Facings)
        {
            foreach (string half in new[] { "bottom", "top" })
            {
                foreach (string shape in new[] { "straight", "inner_left", "inner_right", "outer_left", "outer_right" })
                {
                    string suffix = shape.StartsWith("inner", StringComparison.Ordinal) ? "_inner" : shape.StartsWith("outer", StringComparison.Ordinal) ? "_outer" : string.Empty;
                    int turn = (half, shape.EndsWith("left", StringComparison.Ordinal), shape.EndsWith("right", StringComparison.Ordinal)) switch
                    {
                        ("bottom", true, _) => -90,
                        ("top", _, true) => 90,
                        _ => 0,
                    };
                    int y = ((yaw + turn) % 360 + 360) % 360;
                    int x = half == "top" ? 180 : 0;
                    variants.Add(($"facing={facing},half={half},shape={shape}", Variant(model(suffix), x, y, uvlock: x != 0 || y != 0)));
                }
            }
        }

        return Variants([.. variants]);
    }

    /// <summary>A post, and a side for each direction it joins (fences: "true"; walls: "low" and "tall").</summary>
    private static JsonObject Sides(string post, string side, string joined, JsonObject? postWhen = null)
    {
        var parts = new JsonArray { Part(Variant(post), postWhen) };
        foreach ((string direction, int y) in new[] { ("north", 0), ("east", 90), ("south", 180), ("west", 270) })
        {
            parts.Add(Part(Variant(side, y: y, uvlock: true), new JsonObject { [direction] = joined }));
        }

        return new JsonObject { ["multipart"] = parts };
    }

    private static JsonObject WallState(Func<string, string> model)
    {
        JsonObject state = Sides(model("_post"), model("_side"), "low", new JsonObject { ["up"] = "true" });
        foreach ((string direction, int y) in new[] { ("north", 0), ("east", 90), ("south", 180), ("west", 270) })
        {
            state["multipart"]!.AsArray().Add(Part(Variant(model("_side_tall"), y: y, uvlock: true), new JsonObject { [direction] = "tall" }));
        }

        return state;
    }

    private static JsonObject FenceGateState(Func<string, string> model)
    {
        var variants = new List<(string, JsonObject)>();
        foreach ((string facing, int y) in new[] { ("south", 0), ("west", 90), ("north", 180), ("east", 270) })
        {
            foreach (bool wall in new[] { false, true })
            {
                foreach (bool open in new[] { false, true })
                {
                    string suffix = (wall ? "_wall" : string.Empty) + (open ? "_open" : string.Empty);
                    variants.Add(($"facing={facing},in_wall={Lower(wall)},open={Lower(open)}", Variant(model(suffix), y: y, uvlock: true)));
                }
            }
        }

        return Variants([.. variants]);
    }

    /// <summary>The doors of the game: an open door turns a quarter towards its hinge.</summary>
    private static JsonObject DoorState(Func<string, string> model)
    {
        var variants = new List<(string, JsonObject)>();
        foreach ((string facing, int yaw) in Facings)
        {
            foreach (string half in new[] { "lower", "upper" })
            {
                foreach (string hinge in new[] { "left", "right" })
                {
                    foreach (bool open in new[] { false, true })
                    {
                        int y = open ? (yaw + (hinge == "left" ? 90 : 270)) % 360 : yaw;
                        string suffix = $"_{(half == "lower" ? "bottom" : "top")}_{hinge}{(open ? "_open" : string.Empty)}";
                        variants.Add(($"facing={facing},half={half},hinge={hinge},open={Lower(open)}", Variant(model(suffix), y: y)));
                    }
                }
            }
        }

        return Variants([.. variants]);
    }

    /// <summary>The trapdoors that turn with the player (the orientable templates of the game).</summary>
    private static JsonObject TrapdoorState(Func<string, string> model)
    {
        var variants = new List<(string, JsonObject)>();
        foreach ((string facing, int yaw) in new[] { ("north", 0), ("east", 90), ("south", 180), ("west", 270) })
        {
            foreach (string half in new[] { "bottom", "top" })
            {
                foreach (bool open in new[] { false, true })
                {
                    JsonObject variant = (open, half) switch
                    {
                        (false, _) => Variant(model("_" + half), y: yaw),
                        (true, "bottom") => Variant(model("_open"), y: yaw),
                        _ => Variant(model("_open"), x: 180, y: (yaw + 180) % 360),
                    };
                    variants.Add(($"facing={facing},half={half},open={Lower(open)}", variant));
                }
            }
        }

        return Variants([.. variants]);
    }

    private static JsonObject PaneState(Func<string, string> model) => new()
    {
        ["multipart"] = new JsonArray
        {
            Part(Variant(model("_post"))),
            Part(Variant(model("_side")), new JsonObject { ["north"] = "true" }),
            Part(Variant(model("_side"), y: 90), new JsonObject { ["east"] = "true" }),
            Part(Variant(model("_side_alt")), new JsonObject { ["south"] = "true" }),
            Part(Variant(model("_side_alt"), y: 90), new JsonObject { ["west"] = "true" }),
            Part(Variant(model("_noside")), new JsonObject { ["north"] = "false" }),
            Part(Variant(model("_noside_alt")), new JsonObject { ["east"] = "false" }),
            Part(Variant(model("_noside_alt"), y: 90), new JsonObject { ["south"] = "false" }),
            Part(Variant(model("_noside"), y: 270), new JsonObject { ["west"] = "false" }),
        },
    };

    /// <summary>The buttons of the game: on the floor, on a wall (turned up, the texture kept) or on the ceiling (upside down).</summary>
    private static JsonObject ButtonState(Func<string, string> model)
    {
        var variants = new List<(string, JsonObject)>();
        foreach ((string facing, int yaw) in new[] { ("north", 0), ("east", 90), ("south", 180), ("west", 270) })
        {
            foreach (string face in new[] { "floor", "wall", "ceiling" })
            {
                foreach (bool powered in new[] { false, true })
                {
                    string suffix = powered ? "_pressed" : string.Empty;
                    JsonObject variant = face switch
                    {
                        "floor" => Variant(model(suffix), y: yaw),
                        "wall" => Variant(model(suffix), x: 90, y: yaw, uvlock: true),
                        _ => Variant(model(suffix), x: 180, y: (yaw + 180) % 360),
                    };
                    variants.Add(($"face={face},facing={facing},powered={Lower(powered)}", variant));
                }
            }
        }

        return Variants([.. variants]);
    }

    /// <summary>A double slab drops two slabs.</summary>
    private static void DoubleSlabDrop(JsonObject? lootTable, IRBlock block, ResourcePaths paths)
    {
        if (lootTable?["pools"]?[0]?["entries"]?[0] is not JsonObject entry)
        {
            return;
        }

        if (entry["functions"] is not JsonArray functions)
        {
            entry["functions"] = functions = [];
        }

        functions.Add(new JsonObject
        {
            ["function"] = "minecraft:set_count",
            ["count"] = 2,
            ["add"] = false,
            ["conditions"] = new JsonArray { StateCondition(block, paths, "type", "double") },
        });
    }

    /// <summary>A door drops from its lower half only.</summary>
    private static void LowerHalfDrop(JsonObject? lootTable, IRBlock block, ResourcePaths paths)
    {
        if (lootTable?["pools"]?[0]?["conditions"] is JsonArray conditions)
        {
            conditions.Add(StateCondition(block, paths, "half", "lower"));
        }
    }

    private static JsonObject StateCondition(IRBlock block, ResourcePaths paths, string property, string value) => new()
    {
        ["condition"] = "minecraft:block_state_property",
        ["block"] = paths.Reference(block.Id.Value),
        ["properties"] = new JsonObject { [property] = value },
    };

    private static string Lower(bool value) => value ? "true" : "false";
}
