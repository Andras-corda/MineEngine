using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// The item tab of V0.7.12 (item.md): a chisel that stays in the crafting grid and wears out, glows
/// when a function says so and harvests any block; a drill of a custom material; a juice drunk slowly
/// that gives its bottle back; the cat figurine flat in the inventories. Step 2: an ember that burns
/// and composts by functions and that piglins take, and a bow, a wand, a repeater and a fishing rod.
/// Step 3: a graph that uses every new event of the items. V0.7.13: methods of the class of the
/// chisel and of the ember, in Java and in graphs of the item. Added with the samples, so that the
/// end-to-end builds compile them.
/// </summary>
internal static class ItemSample
{
    public static void AddTo(ModProject project, ItemAsset ruby)
    {
        Guid texture = project.Assets.OfType<TextureAsset>().First().Id;
        GraphAsset shines = EntitySample.Function(project, "chisel_shines", DataType.Entity, DataType.Boolean, "true");

        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby_chisel"), "Ruby chisel")
        {
            TextureId = texture,
            Durability = 64,
            Remainder = ItemRemainderKind.ItselfDamaged,
            HarvestsAnyBlock = true,
            RepairItem = ContentReference.ToTag(Name("c:gems/ruby")),
            HasGlint = true,
            GlintCondition = shines.Id,
        });
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby_drill"), "Ruby drill")
        {
            TextureId = texture,
            Kind = ItemKind.Pickaxe,
            Tier = ItemTier.Custom,
            CustomTier = new ItemCustomTier { Uses = 900, Speed = 7.5f, Enchantability = 18, MiningLevel = ItemTier.Diamond },
            AttackDamage = 1f,
            AttackSpeed = -2.8f,
            RepairItem = ContentReference.ToAsset(ruby.Id),
        });
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("berry_juice"), "Berry juice")
        {
            TextureId = texture,
            IsFood = true,
            EatSeconds = 2.4f,
            UseAnimation = ItemUseAnimation.Drink,
            EatResult = ContentReference.ToId(Name("minecraft:glass_bottle")),
            Remainder = ItemRemainderKind.Itself,
        });

        ItemAsset figurine = project.Assets.OfType<ItemAsset>().Single(i => i.ResourceId.Value == "cat_figurine");
        figurine.GuiTextureId = texture;

        AddStep2(project, ruby, texture);
    }

    private static void AddStep2(ModProject project, ItemAsset ruby, Guid texture)
    {
        GraphAsset burnTime = EntitySample.Function(project, "ember_burn_time", DataType.Entity, DataType.Decimal, "30");
        GraphAsset canBurn = EntitySample.Function(project, "ember_can_burn", DataType.Entity, DataType.Boolean, "true");
        GraphAsset canCompost = EntitySample.Function(project, "ember_can_compost", DataType.Entity, DataType.Boolean, "true");
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ember_chip"), "Ember chip")
        {
            TextureId = texture,
            BurnTime = 200,
            BurnTimeFunction = burnTime.Id,
            BurnCondition = canBurn.Id,
            CompostChance = 40,
            CompostCondition = canCompost.Id,
            PiglinCurrency = true,
        });

        void Ranged(string id, int durability, ItemRanged ranged) =>
            project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse(id), id) { TextureId = texture, Durability = durability, Ranged = ranged });

        Ranged("rune_bow", 384, new ItemRanged { Kind = RangedItemKind.Bow, Damage = 3f });
        Ranged("frost_wand", 0, new ItemRanged
        {
            Kind = RangedItemKind.Throw, Projectile = RangedProjectile.Snowball, NeedsAmmo = false, Mode = RangedFireMode.Instant, RateTicks = 10, Speed = 1.5f,
        });
        Ranged("rune_repeater", 500, new ItemRanged
        {
            Kind = RangedItemKind.Crossbow, Projectile = RangedProjectile.SpectralArrow, Ammo = ContentReference.ToAsset(ruby.Id), Mode = RangedFireMode.Automatic, RateTicks = 5,
        });
        Ranged("rune_rod", 64, new ItemRanged { Kind = RangedItemKind.FishingRod });

        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("item_events_v12"), "Item events V0.7.12") { Graph = EventsGraph(project) });
        AddMethods(project);
    }

    /// <summary>
    /// V0.7.13: the chisel glints while its holder sneaks (Java), says when it is dropped (a procedure of
    /// the item) and picked up (Java); the ember burns 45 s (Java) and composts by a function of the item.
    /// </summary>
    private static void AddMethods(ModProject project)
    {
        ItemAsset Item(string id) => project.Assets.OfType<ItemAsset>().Single(i => i.ResourceId.Value == id);
        ItemAsset chisel = Item("ruby_chisel");
        ItemAsset ember = Item("ember_chip");

        MethodSlot dropped = ItemMethodSlots.Find(ItemMethodSlots.EventKey(ItemEventKind.Dropped))!;
        var procedure = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("ruby_chisel_on_dropped"), "onDropped")
        {
            Kind = GraphKind.Procedure,
            ElementId = chisel.Id,
            Parameters = [.. dropped.Parameters.Select(p => VariableDefinition.Create(p.Name, p.Type))],
        };
        project.Assets.Add(procedure);
        var builder = new ProgramGraphBuilder(procedure, () => ProgramModel.From(project.Assets));
        GraphNode entry = builder.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode message = builder.Add(GraphNodeKind.SendMessage, 300, 0, values: ("message", "Dropped"));
        builder.Exec(entry, message).Data(entry, ProgramSample.Pin(procedure.Parameters[0]), message, "player").Commit();

        GraphAsset canCompost = EntitySample.Function(project, "ember_chip_compost_condition", DataType.Entity, DataType.Boolean, "true");
        canCompost.ElementId = ember.Id;

        chisel.Methods =
        [
            ElementMethod.Java(ItemMethodSlots.GlintCondition, "return entity != null && entity.isShiftKeyDown();"),
            ElementMethod.Nodal(ItemMethodSlots.EventKey(ItemEventKind.Dropped), procedure.Id),
            ElementMethod.Java(ItemMethodSlots.EventKey(ItemEventKind.PickedUp), "player.sendSystemMessage(net.minecraft.network.chat.Component.literal(\"Picked up \" + (int) amount));"),
        ];
        ember.Methods =
        [
            ElementMethod.Java(ItemMethodSlots.BurnTime, "return 45;"),
            ElementMethod.Nodal(ItemMethodSlots.CompostCondition, canCompost.Id),
        ];

        // Graphs made like the editor makes them: the burning condition of the ember, the shot of the bow.
        ItemAsset bow = Item("rune_bow");
        MethodSlot burns = ItemMethodSlots.Find(ItemMethodSlots.BurnCondition)!;
        GraphAsset burnsGraph = burns.CreateGraph(ResourceId.Parse("ember_chip_getburncondition"), ember.Id);
        MethodSlot shot = ItemMethodSlots.Find(ItemMethodSlots.EventKey(ItemEventKind.Shot))!;
        GraphAsset shotGraph = shot.CreateGraph(ResourceId.Parse("rune_bow_onshot"), bow.Id);
        project.Assets.Add(burnsGraph);
        project.Assets.Add(shotGraph);
        ember.Methods = [.. ember.Methods, ElementMethod.Nodal(burns.Key, burnsGraph.Id)];
        bow.Methods = [ElementMethod.Nodal(shot.Key, shotGraph.Id)];
    }

    /// <summary>Each new event sends a message to its player: the bow, the repeater, the juice and the chisel.</summary>
    private static NodeGraph EventsGraph(ModProject project)
    {
        string Ref(string id) => ContentReference.ToAsset(project.Assets.OfType<ItemAsset>().Single(i => i.ResourceId.Value == id).Id).ToStorageText();
        (string Item, ItemEventKind[] Events)[] links =
        [
            ("rune_bow", [ItemEventKind.RightClick, ItemEventKind.ChargeStarted, ItemEventKind.ChargingTick, ItemEventKind.Charged, ItemEventKind.Shot, ItemEventKind.Broken]),
            ("rune_repeater", [ItemEventKind.ChargingTick, ItemEventKind.Shot]),
            ("berry_juice", [ItemEventKind.EatingStarted, ItemEventKind.EatingTick, ItemEventKind.EatingStopped, ItemEventKind.Consumed]),
            ("ruby_chisel",
            [
                ItemEventKind.UseOnEntity, ItemEventKind.Broken, ItemEventKind.Dropped, ItemEventKind.PickedUp,
                ItemEventKind.ToolSwingTick, ItemEventKind.ToolSwung, ItemEventKind.HeldTick,
            ]),
        ];
        var builder = new GraphBuilder();
        int row = 0;
        foreach ((string item, ItemEventKind[] events) in links)
        {
            foreach (ItemEventKind kind in events)
            {
                GraphNode node = builder.Add(NodeCatalog.ForItemEvent(kind).Kind, 0, row * 200, ("item", Ref(item)));
                GraphNode message = builder.Add(GraphNodeKind.SendMessage, 300, row * 200, ("message", kind.ToString()));
                builder.Exec(node, message).Data(node, "player", message, "player");
                row++;
            }
        }

        return builder.Build();
    }

    private static NamespacedId Name(string id) => NamespacedId.TryParse(id, out NamespacedId? parsed) ? parsed! : throw new ArgumentException(id);
}
