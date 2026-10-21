using MineEngine.Core.GameData;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Les nœuds disponibles dans l'éditeur nodal (V0.4, volontairement réduit) : quatre
/// événements, des conditions, des actions et des valeurs. Le générateur Java traduit
/// chacun d'eux ; ajouter un nœud revient à le décrire ici puis à le traduire.
/// </summary>
public static class NodeCatalog
{
    private static readonly PinInfo Player = new("player", Tr.T("Player"), PinType.Player);
    private static readonly PinInfo Position = new("position", Tr.T("Position"), PinType.Position);

    private static readonly PinInfo NbtName = new("name", Tr.T("NBT name"), PinType.Text, "my_value");
    private static readonly PinInfo PropertyName = new("property", Tr.T("Property"), PinType.Text, "lit");
    private static readonly PinInfo Direction = new("direction", Tr.T("Direction"), PinType.Text, "north");
    private static readonly PinInfo Stack = new("stack", Tr.T("Item stack"), PinType.ItemStack);
    private static readonly PinInfo Enchantment = new("enchantment", Tr.T("Enchantment"), PinType.Text, "minecraft:sharpness");
    private static readonly PinInfo NumberOutput = new("value", Tr.T("Number"), PinType.Number);
    private static readonly PinInfo BooleanOutput = new("result", Tr.T("Result"), PinType.Boolean);
    private static readonly PinInfo StackOutput = new("stack", Tr.T("Item stack"), PinType.ItemStack);

    private static readonly Dictionary<GraphNodeKind, NodeInfo> Nodes = new NodeInfo[]
    {
        // ----- Événements -----
        new(GraphNodeKind.OnRightClickBlock, NodeCategory.Event, Tr.T("Right-click on a block"),
            Tr.T("A player right-clicks a block (main hand)."),
            outputs: [Player, new PinInfo("position", Tr.T("Block position"), PinType.Position)]) { IsCancellable = true },
        new(GraphNodeKind.OnUseItem, NodeCategory.Event, Tr.T("Item use"),
            Tr.T("A player right-clicks in the air with an item in hand."),
            outputs: [Player, new PinInfo("position", Tr.T("Player position"), PinType.Position)]) { IsCancellable = true },
        new(GraphNodeKind.OnPlayerJoin, NodeCategory.Event, Tr.T("Player joins"),
            Tr.T("A player joins the world or the server."),
            outputs: [Player, new PinInfo("position", Tr.T("Player position"), PinType.Position)]),
        new(GraphNodeKind.OnBlockBroken, NodeCategory.Event, Tr.T("Block broken"),
            Tr.T("A player breaks a block."),
            outputs: [Player, new PinInfo("position", Tr.T("Block position"), PinType.Position)]) { IsCancellable = true },

        // ----- Événements d'un item du mod -----
        ItemEvent(GraphNodeKind.OnItemRightClick, ItemEventKind.RightClick, Tr.T("Item: right-click"),
            Tr.T("The player right-clicks in the air with the item."), Tr.T("Player position")),
        ItemEvent(GraphNodeKind.OnItemUseOnBlock, ItemEventKind.UseOnBlock, Tr.T("Item: used on a block"),
            Tr.T("The player right-clicks a block with the item."), Tr.T("Block position")),
        ItemEvent(GraphNodeKind.OnItemHitEntity, ItemEventKind.HitEntity, Tr.T("Item: hits a creature"),
            Tr.T("The player hits a creature with the item."), Tr.T("Creature position")),
        ItemEvent(GraphNodeKind.OnItemBlockMined, ItemEventKind.BlockMined, Tr.T("Item: block broken with"),
            Tr.T("The player breaks a block while holding the item."), Tr.T("Block position")),
        ItemEvent(GraphNodeKind.OnItemConsumed, ItemEventKind.Consumed, Tr.T("Item: eaten or drunk"),
            Tr.T("The player finishes eating or drinking the item (food item)."), Tr.T("Player position")),
        ItemEvent(GraphNodeKind.OnItemCrafted, ItemEventKind.Crafted, Tr.T("Item: crafted"),
            Tr.T("The player crafts the item."), Tr.T("Player position")),
        ItemEvent(GraphNodeKind.OnItemHeldTick, ItemEventKind.HeldTick, Tr.T("Item: held in hand"),
            Tr.T("Every tick (20 times per second) while the player holds the item."), Tr.T("Player position")),

        // ----- Commands and keybinds (V0.6.2) -----
        new(GraphNodeKind.OnCommand, NodeCategory.Event, Tr.T("On command"),
            Tr.T("A player runs a command of the mod. Gives the argument typed after it: a number, a text or a target player."),
            inputs: [new PinInfo("command", Tr.T("Command (identifier)"), PinType.Text, string.Empty, Connectable: false)],
            outputs:
            [
                Player, new PinInfo("position", Tr.T("Player position"), PinType.Position),
                new PinInfo("number", Tr.T("Number argument"), PinType.Number), new PinInfo("text", Tr.T("Text argument"), PinType.Text),
                new PinInfo("target", Tr.T("Target player"), PinType.Player),
            ]) { DirectEvent = DirectEventKind.Command },
        new(GraphNodeKind.OnKeyPressed, NodeCategory.Event, Tr.T("On key pressed"),
            Tr.T("A player presses a keybind of the mod (outside menus). Runs on the server."),
            inputs: [new PinInfo("key", Tr.T("Keybind (identifier)"), PinType.Text, string.Empty, Connectable: false)],
            outputs: [Player, new PinInfo("position", Tr.T("Player position"), PinType.Position)]) { DirectEvent = DirectEventKind.Key },

        // ----- Conditions -----
        new(GraphNodeKind.Branch, NodeCategory.Condition, Tr.T("If"),
            Tr.T("Continues through True or False depending on a condition."),
            inputs: [new PinInfo("condition", Tr.T("Condition"), PinType.Boolean, "true")]),
        new(GraphNodeKind.HasItem, NodeCategory.Condition, Tr.T("Has item"),
            Tr.T("True if the player's inventory contains at least the given quantity."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantity"), PinType.Number, "1")]),
        new(GraphNodeKind.HoldsItem, NodeCategory.Condition, Tr.T("Holds item"),
            Tr.T("True if the player holds this item in their main hand."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:stick")]),
        new(GraphNodeKind.IsSneaking, NodeCategory.Condition, Tr.T("Is sneaking"),
            Tr.T("True if the player is sneaking (Shift key)."),
            inputs: [Player]),
        new(GraphNodeKind.Chance, NodeCategory.Condition, Tr.T("Chance"),
            Tr.T("Randomly true, with the given probability as a percentage."),
            inputs: [new PinInfo("percent", Tr.T("Percentage"), PinType.Number, "50")]),

        // ----- Actions -----
        new(GraphNodeKind.SpawnItem, NodeCategory.Action, Tr.T("Spawn an item"),
            Tr.T("Spawns an item on the ground, just above the position."),
            inputs: [Position, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantity"), PinType.Number, "1")]),
        new(GraphNodeKind.GiveItem, NodeCategory.Action, Tr.T("Give an item"),
            Tr.T("Adds an item to the player's inventory (it drops on the ground if the inventory is full)."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantity"), PinType.Number, "1")]),
        new(GraphNodeKind.RemoveItem, NodeCategory.Action, Tr.T("Remove an item"),
            Tr.T("Removes at most the given quantity from the player's inventory."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantity"), PinType.Number, "1")]),
        new(GraphNodeKind.SendMessage, NodeCategory.Action, Tr.T("Send a message"),
            Tr.T("Shows a message in the player's chat."),
            inputs: [Player, new PinInfo("message", Tr.T("Message"), PinType.Text, Tr.T("Hello!"))]),
        new(GraphNodeKind.PlaySound, NodeCategory.Action, Tr.T("Play a sound"),
            Tr.T("Plays a sound at the position, heard by nearby players."),
            inputs: [Position, new PinInfo("sound", Tr.T("Sound"), PinType.Sound, "minecraft:entity.experience_orb.pickup")]),
        new(GraphNodeKind.SetBlock, NodeCategory.Action, Tr.T("Place a block"),
            Tr.T("Replaces the block at the position."),
            inputs: [Position, new PinInfo("block", Tr.T("Block"), PinType.Block, "minecraft:stone")]),
        new(GraphNodeKind.Explosion, NodeCategory.Action, Tr.T("Explosion"),
            Tr.T("Causes an explosion at the position (TNT: power 4)."),
            inputs: [Position, new PinInfo("power", Tr.T("Power"), PinType.Number, "2")]),
        new(GraphNodeKind.CancelEvent, NodeCategory.Action, Tr.T("Cancel event"),
            Tr.T("Prevents the player's action (the block is not broken, the click has no effect).")),

        // ----- Valeurs -----
        new(GraphNodeKind.NumberValue, NodeCategory.Variable, Tr.T("Number"), Tr.T("A whole number."),
            inputs: [new PinInfo("value", Tr.T("Value"), PinType.Number, "0", Connectable: false)],
            outputs: [new PinInfo("value", Tr.T("Number"), PinType.Number)]),
        new(GraphNodeKind.TextValue, NodeCategory.Variable, Tr.T("Text"), Tr.T("A text."),
            inputs: [new PinInfo("value", Tr.T("Value"), PinType.Text, string.Empty, Connectable: false)],
            outputs: [new PinInfo("value", Tr.T("Text"), PinType.Text)]),
        new(GraphNodeKind.BooleanValue, NodeCategory.Variable, Tr.T("Boolean"), Tr.T("True or false."),
            inputs: [new PinInfo("value", Tr.T("Value"), PinType.Boolean, "true", Connectable: false)],
            outputs: [new PinInfo("value", Tr.T("Boolean"), PinType.Boolean)]),
        new(GraphNodeKind.PlayerPosition, NodeCategory.Variable, Tr.T("Player position"),
            Tr.T("The block at the player's feet."),
            inputs: [Player],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.PlayerName, NodeCategory.Variable, Tr.T("Player name"), Tr.T("The player's username."),
            inputs: [Player],
            outputs: [new PinInfo("name", Tr.T("Name"), PinType.Text)]),
        new(GraphNodeKind.OffsetPosition, NodeCategory.Variable, Tr.T("Offset a position"),
            Tr.T("Adds an offset in blocks (Y points up)."),
            inputs: [Position, new PinInfo("dx", "X", PinType.Number, "0"), new PinInfo("dy", "Y", PinType.Number, "1"), new PinInfo("dz", "Z", PinType.Number, "0")],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.RandomNumber, NodeCategory.Variable, Tr.T("Random number"),
            Tr.T("A random whole number between the minimum and the maximum, inclusive."),
            inputs: [new PinInfo("min", Tr.T("Minimum"), PinType.Number, "1"), new PinInfo("max", Tr.T("Maximum"), PinType.Number, "6")],
            outputs: [new PinInfo("value", Tr.T("Number"), PinType.Number)]),
        new(GraphNodeKind.JoinText, NodeCategory.Variable, Tr.T("Join texts"),
            Tr.T("Puts two texts end to end (a number is converted to text)."),
            inputs: [new PinInfo("first", Tr.T("First text"), PinType.Text, string.Empty), new PinInfo("second", Tr.T("Second text"), PinType.Text, string.Empty)],
            outputs: [new PinInfo("text", Tr.T("Text"), PinType.Text)]),
        new(GraphNodeKind.GreaterThan, NodeCategory.Variable, Tr.T("Greater than (>)"),
            Tr.T("True if A is strictly greater than B."),
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Result"), PinType.Boolean)]),
        new(GraphNodeKind.NumbersEqual, NodeCategory.Variable, Tr.T("Equal to (==)"),
            Tr.T("True if both numbers are equal."),
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Result"), PinType.Boolean)]),
        Arithmetic(GraphNodeKind.Add, Tr.T("Addition (+)"), Tr.T("A plus B.")),
        Arithmetic(GraphNodeKind.Subtract, Tr.T("Subtraction (-)"), Tr.T("A minus B.")),
        Arithmetic(GraphNodeKind.Multiply, Tr.T("Multiplication (×)"), Tr.T("A times B.")),
        new(GraphNodeKind.LessThan, NodeCategory.Variable, Tr.T("Less than (<)"),
            Tr.T("True if A is strictly less than B."),
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Result"), PinType.Boolean)]),
        new(GraphNodeKind.Not, NodeCategory.Variable, Tr.T("Not (!)"),
            Tr.T("Inverts a condition."),
            inputs: [new PinInfo("value", Tr.T("Condition"), PinType.Boolean, "true")],
            outputs: [new PinInfo("result", Tr.T("Result"), PinType.Boolean)]),

        // ----- Blocks (V0.6.2) -----
        new(GraphNodeKind.RemoveBlock, NodeCategory.Action, Tr.T("Remove the block"),
            Tr.T("Replaces the block at the position with air, without drops."),
            inputs: [Position]),
        new(GraphNodeKind.ReplaceBlock, NodeCategory.Action, Tr.T("Replace the block"),
            Tr.T("Replaces the block at the position, optionally keeping its state (direction, properties) and its data (inventory, NBT)."),
            inputs: [Position, new PinInfo("block", Tr.T("Block"), PinType.Block, "minecraft:stone"),
                new PinInfo("keepState", Tr.T("Keep the state"), PinType.Boolean, "true"), new PinInfo("keepData", Tr.T("Keep the data (NBT, inventory)"), PinType.Boolean, "false")]),
        new(GraphNodeKind.UpdateBlock, NodeCategory.Action, Tr.T("Update the block"),
            Tr.T("Notifies the neighbors and the clients that the block changed (redstone, rendering)."),
            inputs: [Position]),
        new(GraphNodeKind.BreakBlock, NodeCategory.Action, Tr.T("Break the block"),
            Tr.T("Breaks the block like a player would (particles, sound), with or without drops."),
            inputs: [Position, new PinInfo("drop", Tr.T("Drop items"), PinType.Boolean, "true")]),
        new(GraphNodeKind.SetBlockNbtNumber, NodeCategory.Action, Tr.T("Set block NBT number"),
            Tr.T("Stores a number in the data of the block entity at the position (chest, furnace, block of the mod with data)."),
            inputs: [Position, NbtName, new PinInfo("value", Tr.T("Value"), PinType.Number, "0")]),
        new(GraphNodeKind.SetBlockNbtText, NodeCategory.Action, Tr.T("Set block NBT text"),
            Tr.T("Stores a text in the data of the block entity at the position."),
            inputs: [Position, NbtName, new PinInfo("value", Tr.T("Value"), PinType.Text, string.Empty)]),
        new(GraphNodeKind.SetBlockNbtBoolean, NodeCategory.Action, Tr.T("Set block NBT boolean"),
            Tr.T("Stores true or false in the data of the block entity at the position."),
            inputs: [Position, NbtName, new PinInfo("value", Tr.T("Value"), PinType.Boolean, "true")]),
        new(GraphNodeKind.SetBlockProperty, NodeCategory.Action, Tr.T("Set block property"),
            Tr.T("Changes a property of the block state (\"lit\", \"powered\", \"age\"...) when the block has it."),
            inputs: [Position, PropertyName, new PinInfo("value", Tr.T("Value"), PinType.Text, "true")]),
        new(GraphNodeKind.UseBlock, NodeCategory.Action, Tr.T("Simulate a right-click on the block"),
            Tr.T("The player uses the block as with an empty hand (opens a chest, presses a button...)."),
            inputs: [Player, Position]),
        new(GraphNodeKind.SetBlockDirection, NodeCategory.Action, Tr.T("Set block direction"),
            Tr.T("Turns the block towards a direction (north, south, east, west, up, down) when it can face one."),
            inputs: [Position, Direction]),
        new(GraphNodeKind.GetBlockAt, NodeCategory.Variable, Tr.T("Block at"),
            Tr.T("The block at the position."),
            inputs: [Position], outputs: [new PinInfo("block", Tr.T("Block"), PinType.Block)]),
        new(GraphNodeKind.GetBlockProperty, NodeCategory.Variable, Tr.T("Block property"),
            Tr.T("Value of a property of the block state, as text (empty when the block does not have it)."),
            inputs: [Position, PropertyName], outputs: [new PinInfo("value", Tr.T("Value"), PinType.Text)]),
        new(GraphNodeKind.GetBlockDirection, NodeCategory.Variable, Tr.T("Block direction"),
            Tr.T("Direction the block faces (north, south...), empty when it has none."),
            inputs: [Position], outputs: [new PinInfo("direction", Tr.T("Direction"), PinType.Text)]),
        new(GraphNodeKind.GetBlockOpacity, NodeCategory.Variable, Tr.T("Block opacity"),
            Tr.T("Light blocked by the block, from 0 (transparent) to 15 (opaque)."),
            inputs: [Position], outputs: [NumberOutput]),
        new(GraphNodeKind.GetBlockHardness, NodeCategory.Variable, Tr.T("Block hardness"),
            Tr.T("Hardness of the block, rounded (stone: 2, obsidian: 50, unbreakable: -1)."),
            inputs: [Position], outputs: [NumberOutput]),
        new(GraphNodeKind.GetLightLevel, NodeCategory.Variable, Tr.T("Light level"),
            Tr.T("Light level at the position, from 0 to 15."),
            inputs: [Position], outputs: [NumberOutput]),
        new(GraphNodeKind.GetEnchantPowerBonus, NodeCategory.Variable, Tr.T("Enchanting power bonus"),
            Tr.T("Bonus given to a nearby enchanting table (bookshelf: 1)."),
            inputs: [Position], outputs: [NumberOutput]),
        new(GraphNodeKind.IsBlockSolid, NodeCategory.Variable, Tr.T("Is the block solid"),
            Tr.T("True if the block at the position is solid."),
            inputs: [Position], outputs: [BooleanOutput]),
        new(GraphNodeKind.GetBlockNbtNumber, NodeCategory.Variable, Tr.T("Block NBT number"),
            Tr.T("Number stored in the data of the block entity (0 if there is none)."),
            inputs: [Position, NbtName], outputs: [NumberOutput]),
        new(GraphNodeKind.GetBlockNbtText, NodeCategory.Variable, Tr.T("Block NBT text"),
            Tr.T("Text stored in the data of the block entity (empty if there is none)."),
            inputs: [Position, NbtName], outputs: [new PinInfo("value", Tr.T("Text"), PinType.Text)]),
        new(GraphNodeKind.GetBlockNbtBoolean, NodeCategory.Variable, Tr.T("Block NBT boolean"),
            Tr.T("True or false stored in the data of the block entity (false if there is none)."),
            inputs: [Position, NbtName], outputs: [BooleanOutput]),

        // ----- Items (V0.6.2) -----
        new(GraphNodeKind.SetItemCooldown, NodeCategory.Action, Tr.T("Set item cooldown"),
            Tr.T("The player cannot use the item again before the given number of ticks (20 ticks = 1 second)."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:ender_pearl"), new PinInfo("ticks", Tr.T("Ticks"), PinType.Number, "20")]),
        new(GraphNodeKind.AddEnchantment, NodeCategory.Action, Tr.T("Add an enchantment"),
            Tr.T("Enchants the item stack (identifier such as minecraft:sharpness) at the given level."),
            inputs: [Stack, Enchantment, new PinInfo("level", Tr.T("Level"), PinType.Number, "1")]),
        new(GraphNodeKind.RemoveEnchantment, NodeCategory.Action, Tr.T("Remove an enchantment"),
            Tr.T("Removes one enchantment from the item stack."),
            inputs: [Stack, Enchantment]),
        new(GraphNodeKind.RemoveAllEnchantments, NodeCategory.Action, Tr.T("Remove all enchantments"),
            Tr.T("Removes every enchantment from the item stack."),
            inputs: [Stack]),
        new(GraphNodeKind.SetItemDisplayName, NodeCategory.Action, Tr.T("Set item name"),
            Tr.T("Gives the item stack a custom name (like an anvil)."),
            inputs: [Stack, new PinInfo("name", Tr.T("Name"), PinType.Text, Tr.T("My item"))]),
        new(GraphNodeKind.MainHandItem, NodeCategory.Variable, Tr.T("Item in main hand"),
            Tr.T("The item stack the player holds in their main hand (it can be empty)."),
            inputs: [Player], outputs: [StackOutput]),
        new(GraphNodeKind.NewItemStack, NodeCategory.Variable, Tr.T("New item stack"),
            Tr.T("A new stack of the item, with the given count."),
            inputs: [new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Count"), PinType.Number, "1")],
            outputs: [StackOutput]),
        new(GraphNodeKind.ItemOfStack, NodeCategory.Variable, Tr.T("Item of the stack"),
            Tr.T("The kind of item of an item stack."),
            inputs: [Stack], outputs: [new PinInfo("item", Tr.T("Item"), PinType.Item)]),
        new(GraphNodeKind.ItemCooldownPercent, NodeCategory.Variable, Tr.T("Cooldown remaining (%)"),
            Tr.T("Remaining part of the cooldown of the item for the player, from 0 to 100."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:ender_pearl")], outputs: [NumberOutput]),
        new(GraphNodeKind.IsItemOnCooldown, NodeCategory.Variable, Tr.T("Is the item on cooldown"),
            Tr.T("True if the player must wait before using the item again."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:ender_pearl")], outputs: [BooleanOutput]),
        new(GraphNodeKind.CopyItemStack, NodeCategory.Variable, Tr.T("Copy of the item stack"),
            Tr.T("An independent copy: changing it does not change the original."),
            inputs: [Stack], outputs: [StackOutput]),
        StackNumber(GraphNodeKind.ItemMaxStackSize, Tr.T("Max stack size"), Tr.T("How many of this item fit in one slot (usually 64).")),
        StackNumber(GraphNodeKind.ItemCount, Tr.T("Item count"), Tr.T("Number of items in the stack.")),
        StackBoolean(GraphNodeKind.IsItemStackable, Tr.T("Is stackable"), Tr.T("True if several of these items fit in one slot.")),
        StackNumber(GraphNodeKind.ItemDamage, Tr.T("Item damage"), Tr.T("Damage taken by the item (0 = new).")),
        StackNumber(GraphNodeKind.ItemMaxDamage, Tr.T("Item durability"), Tr.T("Damage the item can take before breaking (0 if it has no durability).")),
        StackBoolean(GraphNodeKind.IsItemDamageable, Tr.T("Has durability"), Tr.T("True if the item can be damaged (tool, weapon, armor).")),
        StackBoolean(GraphNodeKind.IsItemDamaged, Tr.T("Is damaged"), Tr.T("True if the item has taken damage.")),
        new(GraphNodeKind.EnchantmentLevel, NodeCategory.Variable, Tr.T("Enchantment level"),
            Tr.T("Level of an enchantment on the item stack (0 if it does not have it)."),
            inputs: [Stack, Enchantment], outputs: [NumberOutput]),
        new(GraphNodeKind.HasEnchantment, NodeCategory.Variable, Tr.T("Has the enchantment"),
            Tr.T("True if the item stack has the enchantment."),
            inputs: [Stack, Enchantment], outputs: [BooleanOutput]),
        StackBoolean(GraphNodeKind.IsItemEnchantable, Tr.T("Is enchantable"), Tr.T("True if the item can be enchanted on an enchanting table.")),
        StackBoolean(GraphNodeKind.IsItemEnchanted, Tr.T("Is enchanted"), Tr.T("True if the item stack has at least one enchantment.")),
        StackNumber(GraphNodeKind.ItemFoodValue, Tr.T("Food value"), Tr.T("Hunger points restored by the item (0 if it is not food).")),
        StackNumber(GraphNodeKind.ItemSaturation, Tr.T("Saturation (x10)"), Tr.T("Saturation given by the item, multiplied by 10 (bread: 6 for 0.6).")),
        StackBoolean(GraphNodeKind.IsItemFood, Tr.T("Is food"), Tr.T("True if the item can be eaten or drunk.")),
        new(GraphNodeKind.CanHarvestBlock, NodeCategory.Variable, Tr.T("Can harvest the block"),
            Tr.T("True if the item is the right tool to get the block's drops (a pickaxe for stone)."),
            inputs: [Stack, new PinInfo("block", Tr.T("Block"), PinType.Block, "minecraft:stone")], outputs: [BooleanOutput]),
        new(GraphNodeKind.ItemHasRarity, NodeCategory.Variable, Tr.T("Has the rarity"),
            Tr.T("True if the item has this rarity (common, uncommon, rare, epic)."),
            inputs: [Stack, new PinInfo("rarity", Tr.T("Rarity"), PinType.Text, "rare")], outputs: [BooleanOutput]),
        new(GraphNodeKind.ItemRarity, NodeCategory.Variable, Tr.T("Item rarity"),
            Tr.T("Rarity of the item: common, uncommon, rare or epic."),
            inputs: [Stack], outputs: [new PinInfo("rarity", Tr.T("Rarity"), PinType.Text)]),
        new(GraphNodeKind.ItemDisplayName, NodeCategory.Variable, Tr.T("Item name"),
            Tr.T("Name shown for the item stack (its custom name if it has one)."),
            inputs: [Stack], outputs: [new PinInfo("name", Tr.T("Name"), PinType.Text)]),
        new(GraphNodeKind.ItemRegistryName, NodeCategory.Variable, Tr.T("Item identifier"),
            Tr.T("Identifier of the item, such as minecraft:diamond."),
            inputs: [Stack], outputs: [new PinInfo("id", Tr.T("Identifier"), PinType.Text)]),
        new(GraphNodeKind.CanBeSmelted, NodeCategory.Variable, Tr.T("Can be smelted"),
            Tr.T("True if a furnace recipe accepts the item."),
            inputs: [Stack], outputs: [BooleanOutput]),

        // ----- Logic (V0.6.5) -----
        Logic(GraphNodeKind.And, Tr.T("And"), Tr.T("True if both conditions are true."), PinType.Boolean, "true"),
        Logic(GraphNodeKind.Or, Tr.T("Or"), Tr.T("True if at least one of the conditions is true."), PinType.Boolean, "false"),
        Logic(GraphNodeKind.NumbersNotEqual, Tr.T("Different (≠)"), Tr.T("True if the two numbers are different."), PinType.Number, "0"),
        Logic(GraphNodeKind.GreaterOrEqual, Tr.T("Greater or equal (≥)"), Tr.T("True if A is greater than or equal to B."), PinType.Number, "0"),
        Logic(GraphNodeKind.LessOrEqual, Tr.T("Less or equal (≤)"), Tr.T("True if A is less than or equal to B."), PinType.Number, "0"),
        Logic(GraphNodeKind.TextEquals, Tr.T("Same text"), Tr.T("True if the two texts are the same (case counts)."), PinType.Text, string.Empty),

        // ----- Loops (V0.6.5) -----
        new(GraphNodeKind.Repeat, NodeCategory.Action, Tr.T("Repeat"),
            Tr.T("Runs the Loop body the given number of times, then continues with Completed. Gives the number of the current turn (1, 2, 3...)."),
            inputs: [new PinInfo("count", Tr.T("Times"), PinType.Number, "10")],
            outputs: [new PinInfo("index", Tr.T("Turn"), PinType.Number)], flow: NodeFlow.Loop),
        new(GraphNodeKind.While, NodeCategory.Action, Tr.T("While"),
            Tr.T("Runs the Loop body as long as the condition is true (10,000 turns at most, so that the game does not freeze), then continues with Completed."),
            inputs: [new PinInfo("condition", Tr.T("Condition"), PinType.Boolean, "false")], flow: NodeFlow.Loop),
        new(GraphNodeKind.ForEachPlayer, NodeCategory.Action, Tr.T("For each player"),
            Tr.T("Runs the Loop body once for each player of the world, then continues with Completed."),
            outputs: [new PinInfo("each", Tr.T("Player"), PinType.Player)], flow: NodeFlow.Loop),

        // ----- Math (V0.6.5) -----
        Arithmetic(GraphNodeKind.Divide, Tr.T("Division (÷)"), Tr.T("A divided by B, rounded toward zero (0 if B is 0).")),
        Arithmetic(GraphNodeKind.Modulo, Tr.T("Modulo (%)"), Tr.T("Remainder of the division of A by B (0 if B is 0).")),
        Arithmetic(GraphNodeKind.Power, Tr.T("Power"), Tr.T("A raised to the power B.")),
        Arithmetic(GraphNodeKind.Minimum, Tr.T("Minimum"), Tr.T("The smaller of the two numbers.")),
        Arithmetic(GraphNodeKind.Maximum, Tr.T("Maximum"), Tr.T("The larger of the two numbers.")),
        OneNumber(GraphNodeKind.SquareRoot, Tr.T("Square root"), Tr.T("Square root of the number, rounded.")),
        OneNumber(GraphNodeKind.Absolute, Tr.T("Absolute value"), Tr.T("The number without its sign (distance to zero).")),

        // ----- Text (V0.6.5) -----
        new(GraphNodeKind.TextLength, NodeCategory.Variable, Tr.T("Text length"), Tr.T("Number of characters of the text."),
            inputs: [TextIn("text", Tr.T("Text"), "Hello")], outputs: [NumberOutput]),
        new(GraphNodeKind.TextContains, NodeCategory.Variable, Tr.T("Text contains"), Tr.T("True if the text contains the part (case counts)."),
            inputs: [TextIn("text", Tr.T("Text"), "Hello world"), TextIn("part", Tr.T("Part"), "world")], outputs: [BooleanOutput]),
        new(GraphNodeKind.ReplaceText, NodeCategory.Variable, Tr.T("Replace text"), Tr.T("The text where every occurrence of the search is replaced."),
            inputs: [TextIn("text", Tr.T("Text"), "Hello world"), TextIn("search", Tr.T("Search"), "world"), TextIn("replacement", Tr.T("Replacement"), "player")],
            outputs: [TextOut]),
        new(GraphNodeKind.TextToNumber, NodeCategory.Variable, Tr.T("Text to number"), Tr.T("The number written in the text (\"42\"), or 0 if it is not a number."),
            inputs: [TextIn("text", Tr.T("Text"), "42")], outputs: [NumberOutput]),
        new(GraphNodeKind.NumberToText, NodeCategory.Variable, Tr.T("Number to text"), Tr.T("The number written as a text."),
            inputs: [new PinInfo("number", Tr.T("Number"), PinType.Number, "0")], outputs: [TextOut]),

        // ----- World (V0.6.5) -----
        new(GraphNodeKind.Dimension, NodeCategory.Variable, Tr.T("Dimension"),
            Tr.T("Identifier of the dimension of the event: minecraft:overworld, minecraft:the_nether, minecraft:the_end..."), outputs: [TextOut]),
        new(GraphNodeKind.WorldTime, NodeCategory.Variable, Tr.T("Time of day"),
            Tr.T("Time of the day, from 0 to 23999 (0: sunrise, 6000: noon, 13000: night, 18000: midnight)."), outputs: [NumberOutput]),
        new(GraphNodeKind.IsDay, NodeCategory.Variable, Tr.T("Is it day"), Tr.T("True during the day."), outputs: [BooleanOutput]),
        new(GraphNodeKind.IsRaining, NodeCategory.Variable, Tr.T("Is it raining"), Tr.T("True when it rains (or snows)."), outputs: [BooleanOutput]),
        new(GraphNodeKind.IsThundering, NodeCategory.Variable, Tr.T("Is there a storm"), Tr.T("True during a thunderstorm."), outputs: [BooleanOutput]),
        new(GraphNodeKind.SetWorldTime, NodeCategory.Action, Tr.T("Set the time"), Tr.T("Changes the time of the day (like /time set)."),
            inputs: [new PinInfo("time", Tr.T("Time"), PinType.Number, "1000")]),
        new(GraphNodeKind.SetWeather, NodeCategory.Action, Tr.T("Set the weather"), Tr.T("Clear weather, rain or thunderstorm for some seconds (like /weather)."),
            inputs:
            [
                new PinInfo("rain", Tr.T("Rain"), PinType.Boolean, "true"), new PinInfo("thunder", Tr.T("Thunder"), PinType.Boolean, "false"),
                new PinInfo("seconds", Tr.T("Seconds"), PinType.Number, "300"),
            ]),
        new(GraphNodeKind.SpawnEntity, NodeCategory.Action, Tr.T("Spawn a creature"), Tr.T("Makes a creature appear at the position (minecraft:pig, or a mob of the mod)."),
            inputs: [TextIn("entity", Tr.T("Creature (identifier)"), "minecraft:pig"), Position]),
        new(GraphNodeKind.SpawnParticles, NodeCategory.Action, Tr.T("Spawn particles"), Tr.T("Shows particles around the position (minecraft:flame, minecraft:heart...)."),
            inputs: [TextIn("particle", Tr.T("Particle (identifier)"), "minecraft:flame"), Position, new PinInfo("count", Tr.T("Count"), PinType.Number, "10")]),

        // ----- Players (V0.6.5) -----
        PlayerNumber(GraphNodeKind.PlayerHealth, Tr.T("Health"), Tr.T("Health points of the player (20 = 10 hearts).")),
        PlayerNumber(GraphNodeKind.PlayerMaxHealth, Tr.T("Maximum health"), Tr.T("Maximum health points of the player.")),
        PlayerNumber(GraphNodeKind.PlayerFood, Tr.T("Food level"), Tr.T("Food level of the player, from 0 to 20.")),
        new(GraphNodeKind.IsPlayerAlive, NodeCategory.Variable, Tr.T("Is alive"), Tr.T("True if the player is alive."), inputs: [Player], outputs: [BooleanOutput]),
        new(GraphNodeKind.SetPlayerHealth, NodeCategory.Action, Tr.T("Set health"), Tr.T("Sets the health points of the player."),
            inputs: [Player, new PinInfo("health", Tr.T("Health"), PinType.Number, "20")]),
        new(GraphNodeKind.HealPlayer, NodeCategory.Action, Tr.T("Heal"), Tr.T("Gives health points back to the player."),
            inputs: [Player, new PinInfo("amount", Tr.T("Amount"), PinType.Number, "4")]),
        new(GraphNodeKind.DamagePlayer, NodeCategory.Action, Tr.T("Deal damage"), Tr.T("Hurts the player (armor reduces the damage)."),
            inputs: [Player, new PinInfo("amount", Tr.T("Amount"), PinType.Number, "2")]),
        new(GraphNodeKind.TeleportPlayer, NodeCategory.Action, Tr.T("Teleport"), Tr.T("Moves the player to the position, in the same dimension."),
            inputs: [Player, Position]),
        new(GraphNodeKind.ArmorItem, NodeCategory.Variable, Tr.T("Armor piece"), Tr.T("The item worn on a part of the body: head, chest, legs or feet."),
            inputs: [Player, TextIn("slot", Tr.T("Slot"), "head")], outputs: [StackOutput]),
        new(GraphNodeKind.OffhandItem, NodeCategory.Variable, Tr.T("Item in off hand"), Tr.T("The item stack the player holds in the off hand."),
            inputs: [Player], outputs: [StackOutput]),
        new(GraphNodeKind.LookedAtBlock, NodeCategory.Variable, Tr.T("Block looked at"),
            Tr.T("Position of the block the player looks at (5 blocks at most), or the position of the player."),
            inputs: [Player], outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),

        // ----- Inventory and item data (V0.6.5) -----
        new(GraphNodeKind.ItemInSlot, NodeCategory.Variable, Tr.T("Item in slot"),
            Tr.T("The item stack in a slot of the inventory (0 to 8: hotbar, 9 to 35: inventory)."),
            inputs: [Player, new PinInfo("slot", Tr.T("Slot"), PinType.Number, "0")], outputs: [StackOutput]),
        new(GraphNodeKind.ClearSlot, NodeCategory.Action, Tr.T("Empty a slot"), Tr.T("Removes the items of a slot of the inventory."),
            inputs: [Player, new PinInfo("slot", Tr.T("Slot"), PinType.Number, "0")]),
        new(GraphNodeKind.CountItemInInventory, NodeCategory.Variable, Tr.T("Count in inventory"), Tr.T("How many of the item the player has in the inventory."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond")], outputs: [NumberOutput]),
        new(GraphNodeKind.SetItemNbtNumber, NodeCategory.Action, Tr.T("Set item data (number)"), Tr.T("Stores a number in the custom data of the item stack."),
            inputs: [Stack, NbtName, new PinInfo("value", Tr.T("Value"), PinType.Number, "0")]),
        new(GraphNodeKind.SetItemNbtText, NodeCategory.Action, Tr.T("Set item data (text)"), Tr.T("Stores a text in the custom data of the item stack."),
            inputs: [Stack, NbtName, TextIn("value", Tr.T("Value"), string.Empty)]),
        new(GraphNodeKind.GetItemNbtNumber, NodeCategory.Variable, Tr.T("Item data (number)"), Tr.T("Number stored in the custom data of the item stack (0 if none)."),
            inputs: [Stack, NbtName], outputs: [NumberOutput]),
        new(GraphNodeKind.GetItemNbtText, NodeCategory.Variable, Tr.T("Item data (text)"), Tr.T("Text stored in the custom data of the item stack (empty if none)."),
            inputs: [Stack, NbtName], outputs: [TextOut]),

        // ----- Positions (V0.6.5) -----
        Coordinate(GraphNodeKind.PositionX, "X"),
        Coordinate(GraphNodeKind.PositionY, "Y"),
        Coordinate(GraphNodeKind.PositionZ, "Z"),
        new(GraphNodeKind.MakePosition, NodeCategory.Variable, Tr.T("Position from X, Y, Z"), Tr.T("The position at these coordinates."),
            inputs: [new PinInfo("x", "X", PinType.Number, "0"), new PinInfo("y", "Y", PinType.Number, "64"), new PinInfo("z", "Z", PinType.Number, "0")],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.Distance, NodeCategory.Variable, Tr.T("Distance"), Tr.T("Distance in blocks between the two positions, rounded."),
            inputs: [new PinInfo("a", "A", PinType.Position), new PinInfo("b", "B", PinType.Position)], outputs: [NumberOutput]),
        new(GraphNodeKind.PositionsEqual, NodeCategory.Variable, Tr.T("Same position"), Tr.T("True if the two positions are the same block."),
            inputs: [new PinInfo("a", "A", PinType.Position), new PinInfo("b", "B", PinType.Position)], outputs: [BooleanOutput]),
        new(GraphNodeKind.IsInArea, NodeCategory.Variable, Tr.T("Is in area"), Tr.T("True if the position is in the box between the two corners (included)."),
            inputs: [Position, new PinInfo("from", Tr.T("Corner 1"), PinType.Position), new PinInfo("to", Tr.T("Corner 2"), PinType.Position)], outputs: [BooleanOutput]),
        new(GraphNodeKind.OffsetInDirection, NodeCategory.Variable, Tr.T("Move in a direction"),
            Tr.T("The position moved by some blocks toward a direction: up, down, north, south, east or west."),
            inputs: [Position, Direction, new PinInfo("distance", Tr.T("Distance"), PinType.Number, "1")],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),

        // ----- Events (V0.6.5) -----
        new(GraphNodeKind.OnPlayerLeave, NodeCategory.Event, Tr.T("Player leaves"), Tr.T("A player leaves the world."),
            outputs: [Player, new PinInfo("position", Tr.T("Player position"), PinType.Position)]),
        new(GraphNodeKind.OnPlayerTick, NodeCategory.Event, Tr.T("Every tick (player)"),
            Tr.T("Every tick (20 times per second), for each player. Keep it light: it runs very often."),
            outputs: [Player, new PinInfo("position", Tr.T("Player position"), PinType.Position)]),
        new(GraphNodeKind.OnPlayerHurt, NodeCategory.Event, Tr.T("Player hurt"), Tr.T("A player takes damage. Cancel the event to prevent it."),
            outputs: [Player, new PinInfo("position", Tr.T("Player position"), PinType.Position), new PinInfo("damage", Tr.T("Damage"), PinType.Number)])
        { IsCancellable = true },
        new(GraphNodeKind.OnEntitySpawn, NodeCategory.Event, Tr.T("Creature appears"),
            Tr.T("A creature (or an item, an arrow...) appears in the world. Cancel the event to prevent it."),
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position), new PinInfo("entity", Tr.T("Creature (identifier)"), PinType.Text)])
        { IsCancellable = true },
        new(GraphNodeKind.OnEntityDeath, NodeCategory.Event, Tr.T("Creature dies"), Tr.T("A creature or a player dies."),
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position), new PinInfo("entity", Tr.T("Creature (identifier)"), PinType.Text)]),

        // ----- Game rules (V0.6.3) -----
        new(GraphNodeKind.GetGameRuleBoolean, NodeCategory.Variable, Tr.T("Game rule (true/false)"),
            Tr.T("Value of a true/false game rule of Minecraft or of the mod (keepInventory, doDaylightCycle...)."),
            inputs: [RuleName("keepInventory")], outputs: [BooleanOutput]),
        new(GraphNodeKind.GetGameRuleNumber, NodeCategory.Variable, Tr.T("Game rule (number)"),
            Tr.T("Value of a number game rule of Minecraft or of the mod (randomTickSpeed, spawnRadius...)."),
            inputs: [RuleName("randomTickSpeed")], outputs: [NumberOutput]),
        new(GraphNodeKind.SetGameRuleBoolean, NodeCategory.Action, Tr.T("Set game rule (true/false)"),
            Tr.T("Changes a true/false game rule, like /gamerule."),
            inputs: [RuleName("keepInventory"), new PinInfo("value", Tr.T("Value"), PinType.Boolean, "true")]),
        new(GraphNodeKind.SetGameRuleNumber, NodeCategory.Action, Tr.T("Set game rule (number)"),
            Tr.T("Changes a number game rule, like /gamerule."),
            inputs: [RuleName("randomTickSpeed"), new PinInfo("value", Tr.T("Value"), PinType.Number, "3")]),

        // ----- Effects (V0.6.3) -----
        new(GraphNodeKind.GiveEffect, NodeCategory.Action, Tr.T("Give an effect"),
            Tr.T("Gives a status effect of Minecraft or of the mod to the player, for some seconds and at a level."),
            inputs:
            [
                Player, EffectName, new PinInfo("seconds", Tr.T("Seconds"), PinType.Number, "30"),
                new PinInfo("level", Tr.T("Level"), PinType.Number, "1"),
            ]),
        new(GraphNodeKind.RemoveEffect, NodeCategory.Action, Tr.T("Remove an effect"),
            Tr.T("Removes a status effect from the player."),
            inputs: [Player, EffectName]),
        new(GraphNodeKind.HasEffect, NodeCategory.Variable, Tr.T("Has the effect"),
            Tr.T("True if the player has the status effect."),
            inputs: [Player, EffectName], outputs: [BooleanOutput]),
    }.ToDictionary(n => n.Kind);

    private static PinInfo TextIn(string name, string label, string value) => new(name, label, PinType.Text, value);

    private static PinInfo TextOut => new("text", Tr.T("Text"), PinType.Text);

    private static NodeInfo Logic(GraphNodeKind kind, string title, string description, PinType type, string value) =>
        new(kind, NodeCategory.Variable, title, description,
            inputs: [new PinInfo("a", "A", type, value), new PinInfo("b", "B", type, value)], outputs: [BooleanOutput]);

    private static NodeInfo OneNumber(GraphNodeKind kind, string title, string description) =>
        new(kind, NodeCategory.Variable, title, description,
            inputs: [new PinInfo("a", "A", PinType.Number, "0")], outputs: [new PinInfo("result", Tr.T("Result"), PinType.Number)]);

    private static NodeInfo PlayerNumber(GraphNodeKind kind, string title, string description) =>
        new(kind, NodeCategory.Variable, title, description, inputs: [Player], outputs: [NumberOutput]);

    private static NodeInfo Coordinate(GraphNodeKind kind, string axis) =>
        new(kind, NodeCategory.Variable, Tr.F("Coordinate {0}", axis), Tr.F("The {0} coordinate of the position.", axis),
            inputs: [Position], outputs: [new PinInfo("value", axis, PinType.Number)]);

    private static PinInfo RuleName(string example) => new("rule", Tr.T("Rule"), PinType.Text, example);

    // A property, not a field: the catalog above is built before the fields declared after it.
    private static PinInfo EffectName => new("effect", Tr.T("Effect (identifier)"), PinType.Text, "minecraft:speed");

    /// <summary>Plus grande valeur acceptée pour un nombre saisi sur un nœud.</summary>
    public const int MaxNumber = 1_000_000;

    /// <summary>Nœuds du catalogue ; les nœuds de programmation (appels, objets...) sont décrits par ProgramModel.</summary>
    public static IReadOnlyCollection<NodeInfo> All => Nodes.Values;

    public static NodeInfo Get(GraphNodeKind kind) =>
        Nodes.TryGetValue(kind, out NodeInfo? info) ? info : throw new ArgumentOutOfRangeException(nameof(kind), kind, Tr.T("Unknown node."));

    /// <summary>Description d'un nœud du catalogue, ou null pour un nœud de programmation.</summary>
    public static NodeInfo? Find(GraphNodeKind kind) => Nodes.GetValueOrDefault(kind);

    /// <summary>Nœud dont la définition n'existe plus : sans broches, il est signalé en erreur.</summary>
    public static NodeInfo Missing(GraphNodeKind kind, string title) =>
        new(kind, NodeCategory.Object, title, Tr.T("The definition used by this node was deleted or is not accessible here."),
            flow: NodeFlow.Pure) { IsMissing = true };

    /// <summary>Nœud de l'événement d'un item, par sorte d'événement.</summary>
    public static NodeInfo ForItemEvent(ItemEventKind kind) => Nodes.Values.First(n => n.ItemEvent == kind);

    private static NodeInfo StackNumber(GraphNodeKind kind, string title, string description) =>
        new(kind, NodeCategory.Variable, title, description, inputs: [Stack], outputs: [NumberOutput]);

    private static NodeInfo StackBoolean(GraphNodeKind kind, string title, string description) =>
        new(kind, NodeCategory.Variable, title, description, inputs: [Stack], outputs: [BooleanOutput]);

    /// <summary>Domain of a node in the Nodes window; nodes of the program (calls, objects, variables) are in Program.</summary>
    public static NodeGroup GroupOf(NodeInfo info) => info.Kind switch
    {
        _ when info.Category == NodeCategory.Event => NodeGroup.Events,
        GraphNodeKind.Branch or GraphNodeKind.Not or GraphNodeKind.Chance or GraphNodeKind.BooleanValue or GraphNodeKind.CancelEvent => NodeGroup.Logic,
        GraphNodeKind.NumberValue or GraphNodeKind.RandomNumber or GraphNodeKind.GreaterThan or GraphNodeKind.LessThan or GraphNodeKind.NumbersEqual
            or GraphNodeKind.Add or GraphNodeKind.Subtract or GraphNodeKind.Multiply => NodeGroup.Math,
        GraphNodeKind.TextValue or GraphNodeKind.JoinText => NodeGroup.Text,
        GraphNodeKind.HasItem or GraphNodeKind.HoldsItem or GraphNodeKind.IsSneaking or GraphNodeKind.GiveItem or GraphNodeKind.RemoveItem
            or GraphNodeKind.SendMessage or GraphNodeKind.PlayerPosition or GraphNodeKind.PlayerName or GraphNodeKind.MainHandItem => NodeGroup.Player,
        GraphNodeKind.SpawnItem or GraphNodeKind.PlaySound or GraphNodeKind.Explosion or GraphNodeKind.OffsetPosition => NodeGroup.World,
        >= GraphNodeKind.RemoveBlock and <= GraphNodeKind.GetBlockNbtBoolean => NodeGroup.Block,
        GraphNodeKind.SetBlock => NodeGroup.Block,
        >= GraphNodeKind.SetItemCooldown and <= GraphNodeKind.CanBeSmelted => NodeGroup.Item,
        >= GraphNodeKind.GetGameRuleBoolean and <= GraphNodeKind.SetGameRuleNumber => NodeGroup.World,
        >= GraphNodeKind.GiveEffect and <= GraphNodeKind.HasEffect => NodeGroup.Player,
        >= GraphNodeKind.And and <= GraphNodeKind.ForEachPlayer => NodeGroup.Logic,
        >= GraphNodeKind.Divide and <= GraphNodeKind.Absolute => NodeGroup.Math,
        >= GraphNodeKind.TextLength and <= GraphNodeKind.NumberToText => NodeGroup.Text,
        >= GraphNodeKind.Dimension and <= GraphNodeKind.SpawnParticles => NodeGroup.World,
        >= GraphNodeKind.PlayerHealth and <= GraphNodeKind.LookedAtBlock => NodeGroup.Player,
        >= GraphNodeKind.ItemInSlot and <= GraphNodeKind.GetItemNbtText => NodeGroup.Item,
        >= GraphNodeKind.PositionX and <= GraphNodeKind.OffsetInDirection => NodeGroup.World,
        _ => NodeGroup.Program,
    };

    public static string GroupLabel(NodeGroup group) => group switch
    {
        NodeGroup.Events => Tr.T("Events"),
        NodeGroup.Logic => Tr.T("Logic"),
        NodeGroup.Math => Tr.T("Math"),
        NodeGroup.Text => Tr.T("Text"),
        NodeGroup.Player => Tr.T("Player"),
        NodeGroup.Item => Tr.T("Items"),
        NodeGroup.Block => Tr.T("Blocks"),
        NodeGroup.World => Tr.T("World"),
        _ => Tr.T("Program"),
    };

    private static NodeInfo Arithmetic(GraphNodeKind kind, string title, string description) =>
        new(kind, NodeCategory.Variable, title, description,
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Result"), PinType.Number)]);

    private static NodeInfo ItemEvent(GraphNodeKind kind, ItemEventKind itemEvent, string title, string description, string positionLabel) =>
        new(kind, NodeCategory.Event, title, description,
            inputs: [new PinInfo("item", Tr.T("Mod item"), PinType.Item, string.Empty, Connectable: false)],
            outputs: [Player, new PinInfo("position", positionLabel, PinType.Position)]) { ItemEvent = itemEvent };

    public static string CategoryLabel(NodeCategory category) => category switch
    {
        NodeCategory.Event => Tr.T("Events"),
        NodeCategory.Condition => Tr.T("Conditions"),
        NodeCategory.Action => Tr.T("Actions"),
        NodeCategory.Variable => Tr.T("Values"),
        NodeCategory.Function => Tr.T("Functions"),
        NodeCategory.Object => Tr.T("Objects and variables"),
        _ => category.ToString(),
    };
}
