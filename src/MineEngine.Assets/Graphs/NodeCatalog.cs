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

    private static readonly PinInfo Entity = new("entity", Tr.T("Entity"), DataType.Entity);

    private static readonly PinInfo Living = new("entity", Tr.T("Living entity"), DataType.LivingEntity);
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

        // ----- Flow: Switch, Sequence, Flip Flop -----
        FlowNodes.Template(GraphNodeKind.SwitchOnNumber),
        FlowNodes.Template(GraphNodeKind.SwitchOnText),
        FlowNodes.Template(GraphNodeKind.Sequence),
        FlowNodes.FlipFlop(),

        // ----- Commands -----
        new(GraphNodeKind.RunCommandAsPlayer, NodeCategory.Action, Tr.T("Run a command (as the player)"),
            Tr.T("Runs a command of Minecraft or of the mod as if the player typed it (\"give @s diamond\", \"boost 5\"); the player needs the permission."),
            inputs: [Player, TextIn("command", Tr.T("Command"), "say Hello")]),
        new(GraphNodeKind.RunCommandAsServer, NodeCategory.Action, Tr.T("Run a command (as the server)"),
            Tr.T("Runs a command of Minecraft or of the mod with every permission, at the position (\"time set day\", \"summon minecraft:pig ~ ~ ~\"), without message in the chat."),
            inputs: [Position, TextIn("command", Tr.T("Command"), "weather clear")]),

        // ----- Ray casts -----
        new(GraphNodeKind.RaycastHitsBlock, NodeCategory.Variable, Tr.T("Ray cast hits a block"),
            Tr.T("True if a block is in the direction the player looks, at most at the given distance."),
            inputs: [Player, new PinInfo("distance", Tr.T("Distance (blocks)"), PinType.Number, "20"), new PinInfo("fluids", Tr.T("Stop on fluids"), PinType.Boolean, "false")],
            outputs: [BooleanOutput]),
        new(GraphNodeKind.RaycastBlockPosition, NodeCategory.Variable, Tr.T("Ray cast: block hit"),
            Tr.T("Position of the first block in the direction the player looks (or the end of the ray if there is none)."),
            inputs: [Player, new PinInfo("distance", Tr.T("Distance (blocks)"), PinType.Number, "20"), new PinInfo("fluids", Tr.T("Stop on fluids"), PinType.Boolean, "false")],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.LooksAtCreature, NodeCategory.Variable, Tr.T("Looks at a creature"),
            Tr.T("True if the player looks at a creature (not hidden by a block), at most at the given distance."),
            inputs: [Player, new PinInfo("distance", Tr.T("Distance (blocks)"), PinType.Number, "20")], outputs: [BooleanOutput]),
        new(GraphNodeKind.CreatureLookedAt, NodeCategory.Variable, Tr.T("Creature looked at"),
            Tr.T("Identifier of the creature the player looks at (minecraft:cow...), or an empty text."),
            inputs: [Player, new PinInfo("distance", Tr.T("Distance (blocks)"), PinType.Number, "20")], outputs: [TextOut]),

        // ----- Physics -----
        new(GraphNodeKind.SetVelocity, NodeCategory.Action, Tr.T("Set velocity"),
            Tr.T("Sets the speed of the player, in blocks per second on each axis (Y: up)."),
            inputs: [Player, new PinInfo("x", "X", PinType.Number, "0"), new PinInfo("y", "Y", PinType.Number, "10"), new PinInfo("z", "Z", PinType.Number, "0")]),
        new(GraphNodeKind.AddVelocity, NodeCategory.Action, Tr.T("Push"),
            Tr.T("Adds speed to the player, in blocks per second on each axis (Y: up), like a knockback."),
            inputs: [Player, new PinInfo("x", "X", PinType.Number, "0"), new PinInfo("y", "Y", PinType.Number, "5"), new PinInfo("z", "Z", PinType.Number, "0")]),
        new(GraphNodeKind.LaunchForward, NodeCategory.Action, Tr.T("Launch forward"),
            Tr.T("Launches the player forward, where the player looks, at the given speed (blocks per second)."),
            inputs: [Player, new PinInfo("speed", Tr.T("Speed"), PinType.Number, "20")]),
        new(GraphNodeKind.SetGravity, NodeCategory.Action, Tr.T("Set gravity"),
            Tr.T("Turns the gravity of the player off (the player floats) or back on."),
            inputs: [Player, new PinInfo("gravity", Tr.T("Gravity"), PinType.Boolean, "false")]),
        new(GraphNodeKind.IsOnGround, NodeCategory.Variable, Tr.T("Is on the ground"), Tr.T("True if the player stands on a block."),
            inputs: [Player], outputs: [BooleanOutput]),
        new(GraphNodeKind.FallDistance, NodeCategory.Variable, Tr.T("Fall distance"), Tr.T("Blocks fallen since the player left the ground."),
            inputs: [Player], outputs: [NumberOutput]),

        // ----- Sounds and music -----
        new(GraphNodeKind.PlaySoundAdvanced, NodeCategory.Action, Tr.T("Play a sound (volume, pitch)"),
            Tr.T("Plays a sound at the position, heard by nearby players, with its volume and its pitch in percent (100: normal)."),
            inputs:
            [
                Position, new PinInfo("sound", Tr.T("Sound"), PinType.Sound, "minecraft:block.note_block.bell"),
                new PinInfo("volume", Tr.T("Volume (%)"), PinType.Number, "100"), new PinInfo("pitch", Tr.T("Pitch (%)"), PinType.Number, "100"),
            ]),
        new(GraphNodeKind.PlaySoundToPlayer, NodeCategory.Action, Tr.T("Play a sound to the player"),
            Tr.T("Plays a sound that only this player hears, with its volume and its pitch in percent."),
            inputs:
            [
                Player, new PinInfo("sound", Tr.T("Sound"), PinType.Sound, "minecraft:entity.experience_orb.pickup"),
                new PinInfo("volume", Tr.T("Volume (%)"), PinType.Number, "100"), new PinInfo("pitch", Tr.T("Pitch (%)"), PinType.Number, "100"),
            ]),
        new(GraphNodeKind.StopSounds, NodeCategory.Action, Tr.T("Stop the sounds"), Tr.T("Stops every sound the player hears."), inputs: [Player]),
        new(GraphNodeKind.PlayMusic, NodeCategory.Action, Tr.T("Play a music"),
            Tr.T("Stops the music the player hears and plays this one (a music of the game, a music disc or a sound of the mod), with the Music volume."),
            inputs: [Player, new PinInfo("music", Tr.T("Music"), PinType.Sound, "minecraft:music_disc.cat")]),
        new(GraphNodeKind.StopMusic, NodeCategory.Action, Tr.T("Stop the music"), Tr.T("Stops the music the player hears."), inputs: [Player]),

        // ----- Appearances (textures and 3D models switched while the game runs) -----
        new(GraphNodeKind.SetItemAppearance, NodeCategory.Action, Tr.T("Set item appearance"),
            Tr.T("Shows the item stack with another look: 0 is the normal one, 1, 2... are the appearances of the item (texture or 3D model, in its tab)."),
            inputs: [Stack, new PinInfo("appearance", Tr.T("Appearance"), PinType.Number, "1")]),
        new(GraphNodeKind.ItemAppearance, NodeCategory.Variable, Tr.T("Item appearance"), Tr.T("Number of the appearance of the item stack (0: normal)."),
            inputs: [Stack], outputs: [NumberOutput]),
        new(GraphNodeKind.SetBlockAppearance, NodeCategory.Action, Tr.T("Set block appearance"),
            Tr.T("Shows the block at the position with another look: 0 is the normal one, 1, 2... are the appearances of the block (texture or 3D model, in its tab). Other blocks do not change."),
            inputs: [Position, new PinInfo("appearance", Tr.T("Appearance"), PinType.Number, "1")]),
        new(GraphNodeKind.BlockAppearance, NodeCategory.Variable, Tr.T("Block appearance"), Tr.T("Number of the appearance of the block at the position (0: normal)."),
            inputs: [Position], outputs: [NumberOutput]),
        new(GraphNodeKind.SetCreatureAppearance, NodeCategory.Action, Tr.T("Set creature appearance"),
            Tr.T("Gives another texture to the mobs of the mod around the position: 0 is the normal one, 1, 2... are the appearances of the mob (in its tab). Creature: its identifier, or empty for every mob of the mod."),
            inputs:
            [
                Position, new PinInfo("radius", Tr.T("Radius (blocks)"), PinType.Number, "8"), TextIn("creature", Tr.T("Creature (identifier)"), string.Empty),
                new PinInfo("appearance", Tr.T("Appearance"), PinType.Number, "1"),
            ]),
        new(GraphNodeKind.CreatureAppearance, NodeCategory.Variable, Tr.T("Creature appearance"),
            Tr.T("Number of the appearance of the nearest mob of the mod around the position (0: normal, or no such mob)."),
            inputs: [Position, new PinInfo("radius", Tr.T("Radius (blocks)"), PinType.Number, "8"), TextIn("creature", Tr.T("Creature (identifier)"), string.Empty)],
            outputs: [NumberOutput]),

        // ----- Decimal numbers -----
        new(GraphNodeKind.PositionToVector, NodeCategory.Variable, Tr.T("Position to vector"), Tr.T("The center of the block, as a decimal 3D vector."),
            inputs: [Position], outputs: [new PinInfo("vector", Tr.T("Vector"), DataType.Vector3f)]),
        new(GraphNodeKind.RoundDecimal, NodeCategory.Variable, Tr.T("Round"), Tr.T("The nearest whole number."),
            inputs: [new PinInfo("a", "A", DataType.Decimal, "0.0")], outputs: [NumberOutput]),
        new(GraphNodeKind.FloorDecimal, NodeCategory.Variable, Tr.T("Round down"), Tr.T("The whole number just below (2.9 gives 2, -0.5 gives -1)."),
            inputs: [new PinInfo("a", "A", DataType.Decimal, "0.0")], outputs: [NumberOutput]),
        Decimals(GraphNodeKind.DecimalAdd, Tr.T("Decimal addition (+)"), Tr.T("A plus B, with decimals."), DataType.Decimal),
        Decimals(GraphNodeKind.DecimalSubtract, Tr.T("Decimal subtraction (-)"), Tr.T("A minus B, with decimals."), DataType.Decimal),
        Decimals(GraphNodeKind.DecimalMultiply, Tr.T("Decimal multiplication (×)"), Tr.T("A times B, with decimals."), DataType.Decimal),
        Decimals(GraphNodeKind.DecimalDivide, Tr.T("Decimal division (÷)"), Tr.T("A divided by B, with decimals (0 if B is 0)."), DataType.Decimal),
        Decimals(GraphNodeKind.DecimalGreater, Tr.T("Decimal greater than (>)"), Tr.T("True if A is greater than B."), DataType.Boolean),
        Decimals(GraphNodeKind.DecimalLess, Tr.T("Decimal less than (<)"), Tr.T("True if A is less than B."), DataType.Boolean),
        new(GraphNodeKind.RandomDecimal, NodeCategory.Variable, Tr.T("Random decimal"), Tr.T("A random decimal number from 0 to 1."),
            outputs: [new PinInfo("result", Tr.T("Result"), DataType.Decimal)]),

        // ----- Directions -----
        new(GraphNodeKind.PlayerFacing, NodeCategory.Variable, Tr.T("Facing"), Tr.T("The horizontal direction the player faces (north, south, east or west)."),
            inputs: [Player], outputs: [new PinInfo("direction", Tr.T("Direction"), DataType.Direction)]),
        new(GraphNodeKind.OppositeDirection, NodeCategory.Variable, Tr.T("Opposite direction"), Tr.T("North gives south, up gives down..."),
            inputs: [new PinInfo("direction", Tr.T("Direction"), DataType.Direction, "north")], outputs: [new PinInfo("direction", Tr.T("Direction"), DataType.Direction)]),
        new(GraphNodeKind.OffsetByDirection, NodeCategory.Variable, Tr.T("Move toward a direction"), Tr.T("The position moved by some blocks toward the direction."),
            inputs: [Position, new PinInfo("direction", Tr.T("Direction"), DataType.Direction, "north"), new PinInfo("distance", Tr.T("Distance"), PinType.Number, "1")],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.DirectionName, NodeCategory.Variable, Tr.T("Direction name"), Tr.T("The name of the direction: up, down, north, south, east or west."),
            inputs: [new PinInfo("direction", Tr.T("Direction"), DataType.Direction, "north")], outputs: [TextOut]),

        // ----- Block states -----
        new(GraphNodeKind.BlockStateAt, NodeCategory.Variable, Tr.T("Block state at"), Tr.T("The block at the position, with its properties (facing, lit...)."),
            inputs: [Position], outputs: [new PinInfo("state", Tr.T("Block state"), DataType.BlockState)]),
        new(GraphNodeKind.SetBlockState, NodeCategory.Action, Tr.T("Place a block state"), Tr.T("Places the block with exactly these properties."),
            inputs: [Position, new PinInfo("state", Tr.T("Block state"), DataType.BlockState)]),
        new(GraphNodeKind.BlockOfState, NodeCategory.Variable, Tr.T("Block of the state"), Tr.T("The block, without its properties."),
            inputs: [new PinInfo("state", Tr.T("Block state"), DataType.BlockState)], outputs: [new PinInfo("block", Tr.T("Block"), PinType.Block)]),
        new(GraphNodeKind.DefaultBlockState, NodeCategory.Variable, Tr.T("Default state"), Tr.T("The block with its default properties."),
            inputs: [new PinInfo("block", Tr.T("Block"), PinType.Block, "minecraft:stone")], outputs: [new PinInfo("state", Tr.T("Block state"), DataType.BlockState)]),
        new(GraphNodeKind.StateProperty, NodeCategory.Variable, Tr.T("State property"),
            Tr.T("The value of a property of the block state, as a text (facing: north, lit: true...); empty if the block has no such property."),
            inputs: [new PinInfo("state", Tr.T("Block state"), DataType.BlockState), TextIn("property", Tr.T("Property"), "facing")], outputs: [TextOut]),
        new(GraphNodeKind.StateWithProperty, NodeCategory.Variable, Tr.T("State with a property"),
            Tr.T("The block state with another value for a property (the same state if the value is not possible)."),
            inputs: [new PinInfo("state", Tr.T("Block state"), DataType.BlockState), TextIn("property", Tr.T("Property"), "facing"), TextIn("value", Tr.T("Value"), "north")],
            outputs: [new PinInfo("state", Tr.T("Block state"), DataType.BlockState)]),

        // ----- Entities -----
        new(GraphNodeKind.EntityPosition, NodeCategory.Variable, Tr.T("Entity position"), Tr.T("The block where the entity is."),
            inputs: [Entity], outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.EntityTypeId, NodeCategory.Variable, Tr.T("Entity type"), Tr.T("Identifier of the kind of entity (minecraft:cow...); empty without entity."),
            inputs: [Entity], outputs: [TextOut]),
        new(GraphNodeKind.IsEntityAlive, NodeCategory.Variable, Tr.T("Entity is alive"), Tr.T("True if there is an entity and it is still in the world."),
            inputs: [Entity], outputs: [BooleanOutput]),
        new(GraphNodeKind.RemoveEntity, NodeCategory.Action, Tr.T("Remove the entity"), Tr.T("Removes the entity from the world, without drop (never a player)."),
            inputs: [Entity]),
        new(GraphNodeKind.NearestEntity, NodeCategory.Variable, Tr.T("Nearest entity"),
            Tr.T("The nearest entity around the position (identifier: minecraft:cow, or empty for any entity but players); none if there is no such entity."),
            inputs: [Position, new PinInfo("radius", Tr.T("Radius (blocks)"), PinType.Number, "16"), TextIn("entity", Tr.T("Entity (identifier)"), string.Empty)],
            outputs: [new PinInfo("entity", Tr.T("Entity"), DataType.Entity)]),
        new(GraphNodeKind.EntityLookedAt, NodeCategory.Variable, Tr.T("Entity looked at"), Tr.T("The entity the player looks at (not hidden by a block), or none."),
            inputs: [Player, new PinInfo("distance", Tr.T("Distance (blocks)"), PinType.Number, "20")], outputs: [new PinInfo("entity", Tr.T("Entity"), DataType.Entity)]),
        new(GraphNodeKind.TeleportEntity, NodeCategory.Action, Tr.T("Teleport the entity"), Tr.T("Moves the entity to the position, in the same dimension."),
            inputs: [Entity, Position]),
        new(GraphNodeKind.DamageEntity, NodeCategory.Action, Tr.T("Damage the entity"), Tr.T("Hurts the entity (armor reduces the damage)."),
            inputs: [Entity, new PinInfo("amount", Tr.T("Amount"), PinType.Number, "2")]),
        new(GraphNodeKind.EntityHealth, NodeCategory.Variable, Tr.T("Entity health"), Tr.T("Health points of a living entity (0 for the others)."),
            inputs: [Entity], outputs: [NumberOutput]),
        new(GraphNodeKind.EntityIsPlayer, NodeCategory.Variable, Tr.T("Entity is a player"), Tr.T("True if the entity is a player."),
            inputs: [Entity], outputs: [BooleanOutput]),

        // ----- Types of TypesVar.md (char, UUID, rich text, identifier, slots, fluids, boxes, damage, effects, NBT, worlds) -----
        new(GraphNodeKind.CharAt, NodeCategory.Variable, Tr.T("Character at"), Tr.T("The character of the text at the index (0: the first one); a space outside the text."),
            inputs: [TextIn("text", Tr.T("Text"), "Hello"), new PinInfo("index", Tr.T("Index"), PinType.Number, "0")],
            outputs: [new PinInfo("char", Tr.T("Character"), DataType.Char)]),
        new(GraphNodeKind.EntityUuid, NodeCategory.Variable, Tr.T("UUID of the entity"), Tr.T("The unique identifier of the entity or the player (kept between sessions)."),
            inputs: [Entity], outputs: [new PinInfo("uuid", Tr.T("UUID"), DataType.Uuid)]),
        new(GraphNodeKind.EntityByUuid, NodeCategory.Variable, Tr.T("Entity by UUID"), Tr.T("The entity with this UUID in this world, or none."),
            inputs: [new PinInfo("uuid", Tr.T("UUID"), DataType.Uuid)], outputs: [new PinInfo("entity", Tr.T("Entity"), DataType.Entity)]),
        new(GraphNodeKind.RandomUuid, NodeCategory.Variable, Tr.T("New UUID"), Tr.T("A new random unique identifier."),
            outputs: [new PinInfo("uuid", Tr.T("UUID"), DataType.Uuid)]),
        new(GraphNodeKind.StyledText, NodeCategory.Variable, Tr.T("Styled text"),
            Tr.T("A rich text with a color (red, gold, aqua, light_purple... as in Minecraft) and bold or italic."),
            inputs:
            [
                TextIn("text", Tr.T("Text"), "Hello"), TextIn("color", Tr.T("Color"), "gold"),
                new PinInfo("bold", Tr.T("Bold"), PinType.Boolean, "false"), new PinInfo("italic", Tr.T("Italic"), PinType.Boolean, "false"),
            ],
            outputs: [new PinInfo("component", Tr.T("Rich text"), DataType.TextComponent)]),
        new(GraphNodeKind.SendRichMessage, NodeCategory.Action, Tr.T("Send a rich message"), Tr.T("Shows a rich text (colors, bold...) in the player's chat."),
            inputs: [Player, new PinInfo("component", Tr.T("Rich text"), DataType.TextComponent)]),
        new(GraphNodeKind.IdentifierNamespace, NodeCategory.Variable, Tr.T("Namespace of the identifier"), Tr.T("\"minecraft\" for minecraft:stone."),
            inputs: [new PinInfo("id", Tr.T("Identifier"), DataType.Identifier, "minecraft:stone")], outputs: [TextOut]),
        new(GraphNodeKind.IdentifierPath, NodeCategory.Variable, Tr.T("Path of the identifier"), Tr.T("\"stone\" for minecraft:stone."),
            inputs: [new PinInfo("id", Tr.T("Identifier"), DataType.Identifier, "minecraft:stone")], outputs: [TextOut]),
        new(GraphNodeKind.EquipmentItem, NodeCategory.Variable, Tr.T("Equipment item"), Tr.T("The item stack in an equipment slot of the living entity (empty if none)."),
            inputs: [Living, new PinInfo("slot", Tr.T("Slot"), DataType.EquipmentSlot, "head")], outputs: [StackOutput]),
        new(GraphNodeKind.ItemInHand, NodeCategory.Variable, Tr.T("Item in hand"), Tr.T("The item stack held in the main hand or the off hand."),
            inputs: [Player, new PinInfo("hand", Tr.T("Hand"), DataType.Hand, "main_hand")], outputs: [StackOutput]),
        new(GraphNodeKind.SwingHand, NodeCategory.Action, Tr.T("Swing the hand"), Tr.T("Plays the animation of the arm of the player."),
            inputs: [Player, new PinInfo("hand", Tr.T("Hand"), DataType.Hand, "main_hand")]),
        new(GraphNodeKind.StackRarity, NodeCategory.Variable, Tr.T("Rarity of the stack"), Tr.T("Common, uncommon, rare or epic (the color of its name)."),
            inputs: [Stack], outputs: [new PinInfo("rarity", Tr.T("Rarity"), DataType.Rarity)]),
        new(GraphNodeKind.FluidAt, NodeCategory.Variable, Tr.T("Fluid at"), Tr.T("The fluid at the position (minecraft:empty if there is none)."),
            inputs: [Position], outputs: [new PinInfo("fluid", Tr.T("Fluid"), DataType.Fluid)]),
        new(GraphNodeKind.PlaceFluid, NodeCategory.Action, Tr.T("Place a fluid"), Tr.T("Places a source of the fluid at the position."),
            inputs: [Position, new PinInfo("fluid", Tr.T("Fluid"), DataType.Fluid, "minecraft:water")]),
        new(GraphNodeKind.EntityBox, NodeCategory.Variable, Tr.T("Box of the entity"), Tr.T("The box around the entity, used for its collisions."),
            inputs: [Entity], outputs: [new PinInfo("box", Tr.T("Box"), DataType.BoundingBox)]),
        new(GraphNodeKind.MakeBox, NodeCategory.Variable, Tr.T("Make a box"), Tr.T("The box between two blocks (both included)."),
            inputs: [new PinInfo("from", Tr.T("From"), PinType.Position), new PinInfo("to", Tr.T("To"), PinType.Position)],
            outputs: [new PinInfo("box", Tr.T("Box"), DataType.BoundingBox)]),
        new(GraphNodeKind.EntitiesInBox, NodeCategory.Variable, Tr.T("Entities in the box"), Tr.T("Every entity inside the box, as a list."),
            inputs: [new PinInfo("box", Tr.T("Box"), DataType.BoundingBox)], outputs: [new PinInfo("entities", Tr.T("Entities"), DataType.ListOf(DataType.Entity))]),
        new(GraphNodeKind.BoxContains, NodeCategory.Variable, Tr.T("Box contains"), Tr.T("True if the point is inside the box."),
            inputs: [new PinInfo("box", Tr.T("Box"), DataType.BoundingBox), new PinInfo("point", Tr.T("Point"), DataType.Vector3f)], outputs: [BooleanOutput]),
        new(GraphNodeKind.GenericDamage, NodeCategory.Variable, Tr.T("Generic damage"), Tr.T("A damage source without cause, for the entity that will be hurt."),
            inputs: [Entity], outputs: [new PinInfo("source", Tr.T("Damage source"), DataType.DamageSource)]),
        new(GraphNodeKind.DamageWithSource, NodeCategory.Action, Tr.T("Damage with a source"), Tr.T("Hurts the entity with this damage source."),
            inputs: [Entity, new PinInfo("source", Tr.T("Damage source"), DataType.DamageSource), new PinInfo("amount", Tr.T("Amount"), PinType.Number, "2")]),
        new(GraphNodeKind.MakeEffect, NodeCategory.Variable, Tr.T("Make an effect"), Tr.T("An effect with its duration and its level, to give later; none if the effect does not exist."),
            inputs: [EffectName, new PinInfo("seconds", Tr.T("Duration (seconds)"), PinType.Number, "10"), new PinInfo("level", Tr.T("Level"), PinType.Number, "1")],
            outputs: [new PinInfo("effect", Tr.T("Effect"), DataType.EffectInstance)]),
        new(GraphNodeKind.ApplyEffect, NodeCategory.Action, Tr.T("Apply an effect"), Tr.T("Gives the effect to the living entity."),
            inputs: [Living, new PinInfo("effect", Tr.T("Effect"), DataType.EffectInstance)]),
        new(GraphNodeKind.NewNbt, NodeCategory.Variable, Tr.T("New NBT data"), Tr.T("Empty NBT data, to fill with named values."),
            outputs: [new PinInfo("nbt", Tr.T("NBT data"), DataType.NbtTag)]),
        new(GraphNodeKind.NbtPutNumber, NodeCategory.Action, Tr.T("NBT: set a number"), Tr.T("Writes a number under the name."),
            inputs: [new PinInfo("nbt", Tr.T("NBT data"), DataType.NbtTag), NbtName, new PinInfo("value", Tr.T("Value"), PinType.Number, "0")]),
        new(GraphNodeKind.NbtPutText, NodeCategory.Action, Tr.T("NBT: set a text"), Tr.T("Writes a text under the name."),
            inputs: [new PinInfo("nbt", Tr.T("NBT data"), DataType.NbtTag), NbtName, TextIn("value", Tr.T("Value"), string.Empty)]),
        new(GraphNodeKind.NbtGetNumber, NodeCategory.Variable, Tr.T("NBT: number"), Tr.T("The number under the name (0 if missing)."),
            inputs: [new PinInfo("nbt", Tr.T("NBT data"), DataType.NbtTag), NbtName], outputs: [NumberOutput]),
        new(GraphNodeKind.NbtGetText, NodeCategory.Variable, Tr.T("NBT: text"), Tr.T("The text under the name (empty if missing)."),
            inputs: [new PinInfo("nbt", Tr.T("NBT data"), DataType.NbtTag), NbtName], outputs: [TextOut]),
        new(GraphNodeKind.CurrentWorld, NodeCategory.Variable, Tr.T("This world"), Tr.T("The world (dimension) where the graph runs."),
            outputs: [new PinInfo("world", Tr.T("World"), DataType.World)]),
        new(GraphNodeKind.WorldOf, NodeCategory.Variable, Tr.T("World of the entity"), Tr.T("The world (dimension) where the entity is."),
            inputs: [Entity], outputs: [new PinInfo("world", Tr.T("World"), DataType.World)]),
        new(GraphNodeKind.WorldDimension, NodeCategory.Variable, Tr.T("Dimension of the world"), Tr.T("Identifier of the dimension: minecraft:overworld, minecraft:the_nether..."),
            inputs: [new PinInfo("world", Tr.T("World"), DataType.World)], outputs: [TextOut]),
        new(GraphNodeKind.GameTime, NodeCategory.Variable, Tr.T("Game time"), Tr.T("Ticks since the world was created (a long number: 20 per second)."),
            outputs: [new PinInfo("time", Tr.T("Time (ticks)"), DataType.Long)]),

        // ----- Guilds of players and groups of entities (scopes player_guild and entity_group) -----
        new(GraphNodeKind.SetPlayerGuild, NodeCategory.Action, Tr.T("Set the guild of the player"),
            Tr.T("The player joins this guild (empty: no guild): the player_guild variables are then those of the guild."),
            inputs: [Player, TextIn("guild", Tr.T("Guild"), "knights")]),
        new(GraphNodeKind.PlayerGuild, NodeCategory.Variable, Tr.T("Guild of the player"), Tr.T("Name of the guild of the player (empty without guild)."),
            inputs: [Player], outputs: [TextOut]),
        new(GraphNodeKind.SetEntityGroup, NodeCategory.Action, Tr.T("Set the group of the entity"),
            Tr.T("The entity joins this group (empty: no group): the entity_group variables are then those of the group."),
            inputs: [Entity, TextIn("group", Tr.T("Group"), "pack")]),
        new(GraphNodeKind.EntityGroup, NodeCategory.Variable, Tr.T("Group of the entity"), Tr.T("Name of the group of the entity (empty without group)."),
            inputs: [Entity], outputs: [TextOut]),

        // ----- Tags (V0.7.1) -----
        new(GraphNodeKind.IsBlockInTag, NodeCategory.Condition, Tr.T("Block is in tag"),
            Tr.T("True if the block at the position is in the tag (#minecraft:logs, #mymod:ores...)."),
            inputs: [Position, TextIn("tag", Tr.T("Tag"), "#minecraft:logs")]),
        new(GraphNodeKind.IsItemInTag, NodeCategory.Condition, Tr.T("Item is in tag"),
            Tr.T("True if the item of the stack is in the tag (#minecraft:planks...)."),
            inputs: [Stack, TextIn("tag", Tr.T("Tag"), "#minecraft:planks")]),
        new(GraphNodeKind.IsEntityInTag, NodeCategory.Condition, Tr.T("Entity is in tag"),
            Tr.T("True if the type of the entity is in the tag (#minecraft:skeletons...)."),
            inputs: [Entity, TextIn("tag", Tr.T("Tag"), "#minecraft:skeletons")]),
        new(GraphNodeKind.IsBiomeInTag, NodeCategory.Condition, Tr.T("Biome is in tag"),
            Tr.T("True if the biome at the position is in the tag (#minecraft:is_forest...)."),
            inputs: [Position, TextIn("tag", Tr.T("Tag"), "#minecraft:is_forest")]),

        // ----- Camera of the player (V0.7.4) -----
        new(GraphNodeKind.SetCameraRotation, NodeCategory.Action, Tr.T("Set camera rotation"),
            Tr.T("Turns the head of the player to these angles, in degrees. Yaw: 0 south, 90 west, 180 north, -90 east. Pitch: -90 up, 0 straight ahead, 90 down."),
            inputs: [Player, new PinInfo("yaw", Tr.T("Yaw"), DataType.Decimal, "0.0"), new PinInfo("pitch", Tr.T("Pitch"), DataType.Decimal, "0.0")]),
        new(GraphNodeKind.TurnCamera, NodeCategory.Action, Tr.T("Turn the camera"),
            Tr.T("Turns the head of the player by these angles, in degrees (yaw: to the right, pitch: down), like the recoil of a weapon."),
            inputs: [Player, new PinInfo("yaw", Tr.T("Yaw"), DataType.Decimal, "0.0"), new PinInfo("pitch", Tr.T("Pitch"), DataType.Decimal, "-5.0")]),
        new(GraphNodeKind.CameraLookAt, NodeCategory.Action, Tr.T("Look at a position"), Tr.T("Turns the head of the player toward the center of the block."),
            inputs: [Player, Position]),
        new(GraphNodeKind.ViewFromEntity, NodeCategory.Action, Tr.T("View from an entity"),
            Tr.T("Shows the game through the eyes of the entity, like the spectator mode: the player follows it and sneaks to come back. No entity: the player's own eyes."),
            inputs: [Player, Entity]),
        new(GraphNodeKind.SetCameraPerspective, NodeCategory.Action, Tr.T("Set camera perspective"),
            Tr.T("Changes the view of the player, like F5: 0 first person, 1 third person from behind, 2 third person from the front."),
            inputs: [Player, new PinInfo("perspective", Tr.T("Perspective"), PinType.Number, "1")]),
        new(GraphNodeKind.SetCameraZoom, NodeCategory.Action, Tr.T("Set camera zoom"),
            Tr.T("Zooms the view of the player, in percent: 100 normal, 200 twice closer (like a spyglass), 50 wider."),
            inputs: [Player, new PinInfo("zoom", Tr.T("Zoom (%)"), PinType.Number, "200")]),
        new(GraphNodeKind.ShakeCamera, NodeCategory.Action, Tr.T("Shake the camera"),
            Tr.T("Shakes the view of the player, like an explosion nearby: the strength (in degrees) fades out during the duration."),
            inputs:
            [
                Player, new PinInfo("strength", Tr.T("Strength (degrees)"), DataType.Decimal, "3.0"),
                new PinInfo("seconds", Tr.T("Duration (seconds)"), DataType.Decimal, "1.0"),
            ]),
        new(GraphNodeKind.TiltCamera, NodeCategory.Action, Tr.T("Tilt the camera"),
            Tr.T("Tilts the view of the player sideways by this angle, in degrees (0: straight again)."),
            inputs: [Player, new PinInfo("angle", Tr.T("Angle (degrees)"), DataType.Decimal, "15.0")]),
        new(GraphNodeKind.SetHudVisible, NodeCategory.Action, Tr.T("Show the HUD"),
            Tr.T("Shows or hides the interface of the player (hotbar, health, crosshair), like F1, for cinematic views."),
            inputs: [Player, new PinInfo("visible", Tr.T("Visible"), PinType.Boolean, "false")]),
        new(GraphNodeKind.ResetCamera, NodeCategory.Action, Tr.T("Reset the camera"),
            Tr.T("Gives the player the normal camera back: first person, own eyes, no zoom, tilt or shake, interface shown."),
            inputs: [Player]),
        new(GraphNodeKind.CameraYaw, NodeCategory.Variable, Tr.T("Camera yaw"),
            Tr.T("Horizontal angle the player looks at, in degrees from -180 to 180 (0 south, 90 west, 180 north, -90 east)."),
            inputs: [Player], outputs: [new PinInfo("yaw", Tr.T("Yaw"), DataType.Decimal)]),
        new(GraphNodeKind.CameraPitch, NodeCategory.Variable, Tr.T("Camera pitch"),
            Tr.T("Vertical angle the player looks at, in degrees (-90 up, 0 straight ahead, 90 down)."),
            inputs: [Player], outputs: [new PinInfo("pitch", Tr.T("Pitch"), DataType.Decimal)]),
        new(GraphNodeKind.CameraDirection, NodeCategory.Variable, Tr.T("Camera direction"),
            Tr.T("The direction the player looks at, as a vector of length 1."),
            inputs: [Player], outputs: [new PinInfo("direction", Tr.T("Direction"), DataType.Vector3f)]),
        new(GraphNodeKind.CameraPosition, NodeCategory.Variable, Tr.T("Camera position"),
            Tr.T("The point the player looks from: the position of the eyes."),
            inputs: [Player], outputs: [new PinInfo("position", Tr.T("Position"), DataType.Vector3f)]),

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
    }
    // Collections and vectors: typed by the wire connected to them (see GenericNodes).
    .Concat(Enumerable.Range((int)GraphNodeKind.CollectionSize, GraphNodeKind.VectorToPosition - GraphNodeKind.CollectionSize + 1)
        .Select(k => GenericNodes.Describe((GraphNodeKind)k, GenericNodes.DefaultType((GraphNodeKind)k), t => t.BuiltInLabel)))
    .Concat(Enumerable.Range((int)GraphNodeKind.CastToNumber, GraphNodeKind.CastToPlayer - GraphNodeKind.CastToNumber + 1)
        .Select(k => GenericNodes.Describe((GraphNodeKind)k, GenericNodes.DefaultType((GraphNodeKind)k), t => t.BuiltInLabel)))
    .ToDictionary(n => n.Kind);

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
        GraphNodeKind.GetGlobal or GraphNodeKind.SetGlobal => NodeGroup.Globals,
        GraphNodeKind.Branch or GraphNodeKind.Not or GraphNodeKind.Chance or GraphNodeKind.BooleanValue or GraphNodeKind.CancelEvent => NodeGroup.Logic,
        GraphNodeKind.SwitchOnNumber or GraphNodeKind.SwitchOnText or GraphNodeKind.Sequence or GraphNodeKind.FlipFlop => NodeGroup.Logic,
        GraphNodeKind.RunCommandAsPlayer or GraphNodeKind.RunCommandAsServer => NodeGroup.World,
        >= GraphNodeKind.RaycastHitsBlock and <= GraphNodeKind.FallDistance => NodeGroup.Player,
        GraphNodeKind.PlaySoundAdvanced => NodeGroup.World,
        >= GraphNodeKind.PlaySoundToPlayer and <= GraphNodeKind.StopMusic => NodeGroup.Player,
        GraphNodeKind.SetItemAppearance or GraphNodeKind.ItemAppearance => NodeGroup.Item,
        GraphNodeKind.SetBlockAppearance or GraphNodeKind.BlockAppearance => NodeGroup.Block,
        GraphNodeKind.SetCreatureAppearance or GraphNodeKind.CreatureAppearance => NodeGroup.World,
        >= GraphNodeKind.CollectionSize and <= GraphNodeKind.ForEachInCollection => NodeGroup.Collections,
        >= GraphNodeKind.MakeVector and <= GraphNodeKind.PositionToVector => NodeGroup.Math,
        >= GraphNodeKind.RoundDecimal and <= GraphNodeKind.RandomDecimal => NodeGroup.Math,
        >= GraphNodeKind.PlayerFacing and <= GraphNodeKind.DirectionName => NodeGroup.World,
        >= GraphNodeKind.BlockStateAt and <= GraphNodeKind.StateWithProperty => NodeGroup.Block,
        >= GraphNodeKind.EntityPosition and <= GraphNodeKind.EntityIsPlayer => NodeGroup.Entities,
        >= GraphNodeKind.CastToNumber and <= GraphNodeKind.CastToPlayer => NodeGroup.Conversions,
        GraphNodeKind.CharAt or GraphNodeKind.StyledText or GraphNodeKind.IdentifierNamespace or GraphNodeKind.IdentifierPath => NodeGroup.Text,
        GraphNodeKind.EntityUuid or GraphNodeKind.EntityByUuid or GraphNodeKind.RandomUuid or GraphNodeKind.EntityBox or GraphNodeKind.GenericDamage
            or GraphNodeKind.DamageWithSource or GraphNodeKind.EquipmentItem or GraphNodeKind.ApplyEffect or GraphNodeKind.WorldOf => NodeGroup.Entities,
        GraphNodeKind.SendRichMessage or GraphNodeKind.ItemInHand or GraphNodeKind.SwingHand or GraphNodeKind.MakeEffect => NodeGroup.Player,
        GraphNodeKind.StackRarity => NodeGroup.Item,
        GraphNodeKind.FluidAt or GraphNodeKind.PlaceFluid => NodeGroup.Block,
        >= GraphNodeKind.MakeBox and <= GraphNodeKind.BoxContains => NodeGroup.World,
        >= GraphNodeKind.NewNbt and <= GraphNodeKind.NbtGetText => NodeGroup.Program,
        >= GraphNodeKind.CurrentWorld and <= GraphNodeKind.GameTime => NodeGroup.World,
        GraphNodeKind.SetPlayerGuild or GraphNodeKind.PlayerGuild => NodeGroup.Player,
        GraphNodeKind.SetEntityGroup or GraphNodeKind.EntityGroup => NodeGroup.Entities,
        GraphNodeKind.IsBlockInTag => NodeGroup.Block,
        GraphNodeKind.IsItemInTag => NodeGroup.Item,
        GraphNodeKind.IsEntityInTag => NodeGroup.Entities,
        GraphNodeKind.IsBiomeInTag => NodeGroup.World,
        >= GraphNodeKind.SetCameraRotation and <= GraphNodeKind.CameraPosition => NodeGroup.Camera,
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
        NodeGroup.Camera => Tr.T("Camera"),
        NodeGroup.Item => Tr.T("Items"),
        NodeGroup.Block => Tr.T("Blocks"),
        NodeGroup.World => Tr.T("World"),
        NodeGroup.Collections => Tr.T("Collections"),
        NodeGroup.Entities => Tr.T("Entities"),
        NodeGroup.Globals => Tr.T("Global variables"),
        NodeGroup.Conversions => Tr.T("Conversions"),
        _ => Tr.T("Program"),
    };

    private static NodeInfo Decimals(GraphNodeKind kind, string title, string description, DataType result) =>
        new(kind, NodeCategory.Variable, title, description,
            inputs: [new PinInfo("a", "A", DataType.Decimal, "0.0"), new PinInfo("b", "B", DataType.Decimal, "0.0")],
            outputs: [new PinInfo("result", Tr.T("Result"), result)]);

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
