using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Mobs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Mob rework (V0.7.9, Entity.md): the new sections, their saving, their checks and their Java.</summary>
public sealed class MobEntityTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, EntitySample Sample) CreateSample(TemporaryDirectory temp, ModLoaderKind loader = ModLoaderKind.NeoForge, string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        TextureAsset texture = SampleContent.AddTexture(project, "wolf_skin");
        return (project, EntitySample.AddTo(project, texture));
    }

    [Fact]
    public void The_new_sections_are_saved_and_read_back()
    {
        using var temp = new TemporaryDirectory();
        (_, EntitySample sample) = CreateSample(temp);

        foreach (MobAsset mob in new[] { sample.Wolf, sample.Guardian, sample.Raft, sample.Trolley })
        {
            string json = Serializer.Serialize(mob);
            var read = (MobAsset)Serializer.Deserialize(json, mob.ResourceId.Value);
            Assert.Equal(json, Serializer.Serialize(read));
        }

        var wolf = (MobAsset)Serializer.Deserialize(Serializer.Serialize(sample.Wolf), "storm_wolf");
        Assert.Equal(new MobScale(MobValueMode.Dynamic, 1, sample.Size.Id), wolf.VisualScale);
        Assert.Equal([sample.Wolf.Variations[0]], wolf.Variations);
        Assert.Equal(sample.Glows.Id, wolf.Layers[0].GlowFunction);
        Assert.True(wolf.Taming.Enabled);
        Assert.Equal([33.0, 50.0], wolf.Taming.Items.Select(i => i.Chance));
        Assert.Equal(1, wolf.Riding.SaddleLayer);
        Assert.Equal(new HashSet<MobDimension> { MobDimension.Overworld, MobDimension.Nether }, wolf.Dimensions);
        Assert.Equal(sample.Pack.Id, wolf.GroupFunction);
    }

    [Fact]
    public void A_mob_saved_before_the_rework_keeps_its_fire_sun_and_dimensions()
    {
        const string Old = """
            {
              "formatVersion": 2, "guid": "0b6d8d0e-6c7d-4a5c-8d39-5b1a0fa0d001", "type": "Mob", "resourceId": "old_mob", "displayName": "Old mob",
              "properties": {
                "fireImmune": true, "burnsInDaylight": true,
                "spawning": { "enabled": true, "biomes": [ "Overworld", "Forest", "Nether" ], "weight": 10, "minGroup": 1, "maxGroup": 3 }
              }
            }
            """;

        var mob = (MobAsset)Serializer.Deserialize(Old, "old_mob");

        Assert.True(mob.FireImmune);
        Assert.Contains(MobImmunity.Lava, mob.Immunities);
        Assert.True(mob.BurnsInDaylight);
        Assert.Equal(new HashSet<MobDimension> { MobDimension.Overworld, MobDimension.Nether }, mob.Dimensions);
        Assert.Equal(new HashSet<SpawnBiome> { SpawnBiome.Forest }, mob.SpawnBiomes);
    }

    [Fact]
    public void The_spawning_follows_the_dimensions_the_biomes_and_the_group_mode()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, EntitySample sample) = CreateSample(temp);
        var diagnostics = new DiagnosticBag();

        IRContent content = ModIRBuilder.CreateDefault().Build(project, diagnostics)!.Content;
        IRMob wolf = content.Mobs.Single(m => m.Id.Value == "storm_wolf");
        IRMob guardian = content.Mobs.Single(m => m.Id.Value == "stone_guardian");

        Assert.Equal(["#minecraft:is_forest", "#minecraft:is_nether"], wolf.Spawning!.Biomes);
        Assert.Equal(new IRFunctionBinding(sample.Pack.Id, true), wolf.Spawning.GroupFunction);
        Assert.Equal((2, 2), (guardian.Spawning!.MinGroup, guardian.Spawning.MaxGroup));
        Assert.Equal(["minecraft:pillager_outpost"], guardian.Spawning.Structures.Select(s => s.ToString()));
        // A fixed factor of the collision box is in its size; the guardian's is given by a function.
        Assert.Equal(1.2f * 0.9f, wolf.Width, 3);
        Assert.Equal(sample.Size.Id, guardian.HitboxScale.Function!.FunctionId);
        Assert.Null(wolf.Taming is null ? "no taming" : null);
    }

    [Fact]
    public void A_function_of_the_wrong_kind_is_reported()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, EntitySample sample) = CreateSample(temp);
        // The golem function takes a position: it cannot give a size, which takes the mob.
        sample.Wolf.VisualScale = new MobScale(MobValueMode.Dynamic, 1, sample.GolemHere.Id);
        var diagnostics = new DiagnosticBag();

        // An error stops the build of the mod: the IR is not given.
        Assert.Null(ModIRBuilder.CreateDefault().Build(project, diagnostics));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("golem_here", StringComparison.Ordinal));
    }

    [Fact]
    public void The_checks_report_the_mistakes_of_the_sections()
    {
        var mob = new MobAsset(Guid.NewGuid(), ResourceId.Parse("bad"), "Bad")
        {
            Kind = MobKind.Monster,
            Taming = new MobTaming(true, []),
            Riding = new MobRiding(true) { Forward = false, Strafe = false, SaddleLayer = 3 },
            SpawnsNaturally = true,
            InStructures = true,
            GroupMode = MobGroupMode.Dynamic,
        };
        var diagnostics = new DiagnosticBag();

        mob.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message == Tr.T("Only a mob of the Animal family can be tamed."));
        Assert.Contains(diagnostics, d => d.Message == Tr.T("The mob can be tamed but no item tames it."));
        Assert.Contains(diagnostics, d => d.Message == Tr.T("The layer of the saddle no longer exists."));
        Assert.Contains(diagnostics, d => d.Message == Tr.T("The mob spawns only in structures but none is chosen."));
        Assert.Contains(diagnostics, d => d.Message == Tr.T("The group size is dynamic but no function gives it."));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_sections_become_the_java_of_the_mob(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateSample(temp, loader, version);
        bool modern = version != "1.20.1";

        (string java, string resources) = await GenerateAsync(project, loader, version);

        string wolf = File.ReadAllText(Path.Combine(java, "entity/StormWolfEntity.java"));
        Assert.Contains("public class StormWolfEntity extends TamableAnimal", wolf);
        Assert.Contains("if (stack.is(item(\"minecraft:bone\")))", wolf);
        Assert.Contains("this.tame(player);", wolf);
        Assert.Contains("new SitWhenOrderedToGoal(this)", wolf);
        Assert.Contains(modern ? "new FollowOwnerGoal(this, 1.0, 10.0F, 2.0F))" : "new FollowOwnerGoal(this, 1.0, 10.0F, 2.0F, false))", wolf);
        Assert.Contains("if (!this.isSaddled() && stack.is(Items.SADDLE)", wolf);
        Assert.Contains("player.startRiding(this);", wolf);
        Assert.Contains("protected Vec3 getRiddenInput(Player player, Vec3 input)", wolf);
        Assert.Contains("public boolean canDrownInFluidType(FluidType type)", wolf);
        Assert.Contains("return !com.mineengine.mods.testmod.code.BeastGlows.beastGlows(this.level(), this);", wolf);
        Assert.Contains("source.is(DamageTypes.FALL)", wolf);
        Assert.Contains("public boolean canBeAffected(MobEffectInstance effect)", wolf);
        Assert.Contains("this.level().isNight()", wolf);
        Assert.Contains("public boolean isSensitiveToWater()", wolf);
        Assert.Contains(modern ? "protected Vec3 getPassengerAttachmentPoint(" : "public double getPassengersRidingOffset()", wolf);
        Assert.Contains(modern ? ".add(Attributes.STEP_HEIGHT, 1.0)" : "this.setMaxUpStep(1.0f);", wolf);
        Assert.Contains(".add(Attributes.ATTACK_KNOCKBACK, 0.5)", wolf);
        Assert.Contains("return Math.max(1, (int) Math.round(com.mineengine.mods.testmod.code.PackSize.packSize(this.level(), this)));", wolf);
        Assert.Contains("this.setVariant(this.random.nextInt(2));", wolf);

        string guardian = File.ReadAllText(Path.Combine(java, "entity/StoneGuardianEntity.java"));
        Assert.Contains("public class StoneGuardianEntity extends Raider", guardian);
        Assert.Contains(modern ? "public void applyRaidBuffs(ServerLevel level, int wave, boolean unused)" : "public void applyRaidBuffs(int wave, boolean unused)", guardian);
        Assert.Contains("BossEvent.BossBarColor.RED, BossEvent.BossBarOverlay.NOTCHED_10", guardian);
        Assert.Contains("this.bossEvent.setProgress(this.getHealth() / this.getMaxHealth());", guardian);
        Assert.Contains("public boolean isPushable()", guardian);
        Assert.Contains(modern ? "public EntityDimensions getDefaultDimensions(Pose pose)" : "public EntityDimensions getDimensions(Pose pose)", guardian);
        Assert.Contains("public static boolean checkSpawnRules(EntityType<StoneGuardianEntity> type", guardian);
        Assert.Contains("\"minecraft:pillager_outpost\",", guardian);
        Assert.Contains("amount *= 2.0F;", guardian);
        if (modern)
        {
            Assert.DoesNotContain("getMobType", guardian);
            JsonNode tag = JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "../../data/minecraft/tags/entity_type/illager.json")))!;
            Assert.Equal("testmod:stone_guardian", tag["values"]![0]!.GetValue<string>());
        }
        else
        {
            Assert.Contains("return MobType.ILLAGER;", guardian);
        }

        string entities = File.ReadAllText(Path.Combine(java, "registry/ModEntities.java"));
        Assert.Contains("public static void onBlockPlaced(BlockEvent.EntityPlaceEvent event)", entities);
        Assert.Contains("if (com.mineengine.mods.testmod.code.GolemHere.golemHere(level, top))", entities);
        Assert.Contains("StoneGuardianEntity::checkSpawnRules", entities);
        Assert.Contains(".clientTrackingRange(10)", entities);

        string renderer = File.ReadAllText(Path.Combine(java, "client/StormWolfRenderer.java"));
        Assert.Contains("float scale = (float) Math.max(0.05, com.mineengine.mods.testmod.code.BeastSize.beastSize(entity.level(), entity));", renderer);
        Assert.Contains("RenderType.entityTranslucent(this.getTextureLocation(entity))", renderer);
        Assert.Contains("case 0 -> com.mineengine.mods.testmod.code.BeastGlows.beastGlows(entity.level(), entity);", renderer);
        Assert.Contains("case 1 -> entity.isSaddled() && true;", renderer);
        // The shadow grows with the fixed factor of the collision box (0.8 x 1.2).
        Assert.Contains("super(context, new CowModel<>(context.bakeLayer(ModelLayers.COW)), 0.96f);", renderer);

        string main = File.ReadAllText(Directory.GetFiles(java, "*.java").Single(f => File.ReadAllText(f).Contains("@Mod(", StringComparison.Ordinal)));
        Assert.Contains("EVENT_BUS.addListener(ModEntities::onBlockPlaced);", main);
        Assert.True(File.Exists(Path.Combine(resources, "textures/entity/storm_wolf_layer_2.png")));
        Assert.Contains("item/storm_wolf_spawn_egg", File.ReadAllText(Path.Combine(resources, "models/item/storm_wolf_spawn_egg.json")));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_events_of_the_mob_call_its_graphs(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateSample(temp, loader, version);

        (string java, _) = await GenerateAsync(project, loader, version);

        string behavior = File.ReadAllText(Path.Combine(java, "behavior/BeastEventsBehavior.java"));
        Assert.Contains("public static void onMobHurt1(LivingEntity mob, Level level, BlockPos position, Player player, Entity other, double amount)", behavior);
        Assert.DoesNotContain("@SubscribeEvent", behavior);

        string wolf = File.ReadAllText(Path.Combine(java, "entity/StormWolfEntity.java"));
        string Call(string method, string player, string other, string amount) =>
            $"BeastEventsBehavior.{method}(this, this.level(), this.blockPosition(), {player}, {other}, {amount});";
        Assert.Contains(Call("onMobStruckByLightning1", "null", "bolt", "0.0"), wolf);
        Assert.Contains("public void thunderHit(ServerLevel level, LightningBolt bolt)", wolf);
        Assert.Contains(Call("onMobFalls1", "null", "null", "distance"), wolf);
        Assert.Contains("boolean wasDead = this.dead;", wolf);
        Assert.Contains(Call("onMobDies1", "(source.getEntity() instanceof Player ? (Player) source.getEntity() : null)", "source.getEntity()", "0.0"), wolf);
        Assert.Contains(Call("onMobPlayerCollides1", "player", "player", "0.0"), wolf);
        Assert.Contains(Call("onMobKillsEntity1", "(entity instanceof Player ? (Player) entity : null)", "entity", "0.0"), wolf);
        Assert.Contains("protected void addPassenger(Entity passenger)", wolf);
        Assert.Contains("protected void removePassenger(Entity passenger)", wolf);
        Assert.Contains(Call("onMobEntityCollides1", "(entity instanceof Player ? (Player) entity : null)", "entity", "0.0"), wolf);

        // The wolf already had a hurt (immunities) and a right-click (taming, riding): the events join them,
        // so each method is written once.
        Assert.Single(Occurrences(wolf, "public boolean hurt(DamageSource source, float amount)"));
        Assert.Single(Occurrences(wolf, "public InteractionResult mobInteract(Player player, InteractionHand hand)"));
        Assert.Single(Occurrences(wolf, "public void tick()"));
        Assert.Contains(Call("onMobHurt1", "(source.getEntity() instanceof Player ? (Player) source.getEntity() : null)", "source.getEntity()", "amount"), wolf);
        Assert.Contains(Call("onMobRightClicked1", "player", "player", "0.0"), wolf);
        Assert.Contains(Call("onMobTamed1", "player", "player", "0.0"), wolf);
        Assert.Contains("this.getPersistentData().putBoolean(\"mineengine_spawned\", true);", wolf);
        Assert.Contains(Call("onMobSpawns1", "null", "null", "0.0"), wolf);
        Assert.Contains(Call("onMobTick1", "null", "null", "0.0"), wolf);
        Assert.Contains(Call("onMobRiddenTick1", "(rider instanceof Player ? (Player) rider : null)", "rider", "0.0"), wolf);

        // The guardian is named with the namespace of the mod; it only has its hurt event, and its tick
        // keeps the dynamic collision box.
        string guardian = File.ReadAllText(Path.Combine(java, "entity/StoneGuardianEntity.java"));
        Assert.Contains(Call("onMobHurt2", "(source.getEntity() instanceof Player ? (Player) source.getEntity() : null)", "source.getEntity()", "amount"), guardian);
        Assert.DoesNotContain("onMobTick", guardian);
        Assert.Single(Occurrences(guardian, "public void tick()"));
        Assert.Contains("this.refreshDimensions();", guardian);
    }

    [Fact]
    public void An_event_of_a_mob_that_does_not_exist_is_reported()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, EntitySample sample) = CreateSample(temp);
        var builder = new GraphBuilder();
        builder.Add(GraphNodeKind.OnMobTick, 0, 0, ("mob", "ghost"));
        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("ghost_events"), "Ghost") { Graph = builder.Build() });
        var diagnostics = new DiagnosticBag();

        Assert.Null(ModIRBuilder.CreateDefault().Build(project, diagnostics));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("\"ghost\"", StringComparison.Ordinal));
        Assert.NotNull(sample.Events);
    }

    private static IEnumerable<int> Occurrences(string text, string value)
    {
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            yield return index;
        }
    }

    [Fact]
    public void A_vehicle_is_ridden_and_neither_spawns_nor_has_an_egg_or_ai()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, EntitySample sample) = CreateSample(temp);
        var raft = (MobAsset)Serializer.Deserialize(Serializer.Serialize(sample.Raft), "log_raft");
        Assert.Equal(MobVehicleKind.Boat, raft.Vehicle.Kind);
        Assert.Equal(ItemRarity.Rare, raft.Vehicle.Rarity);
        Assert.Equal("log_raft_boat", raft.VehicleItemId);
        var diagnostics = new DiagnosticBag();

        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;

        IRMob boat = ir.Content.Mobs.Single(m => m.Id.Value == "log_raft");
        IRMob cart = ir.Content.Mobs.Single(m => m.Id.Value == "mine_trolley");
        Assert.Equal(new IRMobRiding(true, false, false, -1), boat.Riding);
        Assert.Contains(MobImmunity.Drowning, boat.Immunities);
        Assert.NotNull(boat.Vehicle!.Texture);
        Assert.Null(cart.SpawnEgg);
        Assert.Null(cart.Spawning);
        Assert.Empty(cart.Goals);
        Assert.True(cart.Persistent);
        Assert.Equal(1.0, cart.StepHeight);
        Assert.Equal(["log_raft_boat", "mine_trolley_minecart"], ir.Content.Vehicles.Select(m => m.Vehicle!.ItemId));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task The_vehicles_are_placed_by_their_item(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateSample(temp, loader, version);

        (string java, string resources) = await GenerateAsync(project, loader, version);

        string items = File.ReadAllText(Path.Combine(java, "registry/ModItems.java"));
        Assert.Contains("\"log_raft_boat\", () -> new Item(new Item.Properties().stacksTo(1).rarity(Rarity.RARE)) {", items);
        Assert.Contains("BlockHitResult hit = getPlayerPOVHitResult(level, player, ClipContext.Fluid.ANY);", items);
        Assert.Contains("Entity vehicle = ModEntities.LOG_RAFT.get().create(level);", items);
        Assert.Contains("if (!level.getBlockState(position).is(BlockTags.RAILS)) {", items);
        Assert.Contains("Entity vehicle = ModEntities.MINE_TROLLEY.get().create(level);", items);
        Assert.DoesNotContain("MINE_TROLLEY_SPAWN_EGG", items);

        string tabs = File.ReadAllText(Path.Combine(java, "registry/ModCreativeTabs.java"));
        Assert.Contains("output.accept(ModItems.LOG_RAFT_BOAT.get());", tabs);
        Assert.Contains("event.accept(ModItems.MINE_TROLLEY_MINECART", tabs);

        string raft = File.ReadAllText(Path.Combine(java, "entity/LogRaftEntity.java"));
        Assert.Contains("if (this.getFluidHeight(FluidTags.WATER) > 0.3)", raft);
        Assert.Contains("this.spawnAtLocation(ModItems.LOG_RAFT_BOAT.get());", raft);
        Assert.Contains("player.startRiding(this);", raft);
        Assert.Contains("source.is(DamageTypes.DROWN)", raft);

        string trolley = File.ReadAllText(Path.Combine(java, "entity/MineTrolleyEntity.java"));
        Assert.Contains("if (rail.getBlock() instanceof BaseRailBlock block)", trolley);
        Assert.Contains("this.setPos(railPosition.getX() + 0.5, this.getY(), this.getZ());", trolley);
        Assert.Contains("public boolean removeWhenFarAway(double distance)", trolley);

        Assert.Contains("testmod:item/log_raft_boat", File.ReadAllText(Path.Combine(resources, "models/item/log_raft_boat.json")));
        Assert.True(File.Exists(Path.Combine(resources, "textures/item/log_raft_boat.png")));
        Assert.Contains("minecraft:item/minecart", File.ReadAllText(Path.Combine(resources, "models/item/mine_trolley_minecart.json")));
        JsonNode lang = JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "lang/en_us.json")))!;
        Assert.Equal("Radeau", lang["item.testmod.log_raft_boat"]!.GetValue<string>());
        Assert.Null(lang["item.testmod.mine_trolley_spawn_egg"]);
    }

    /// <summary>Generates the project and gives the folder of its Java package and of its assets.</summary>
    private static async Task<(string Java, string Resources)> GenerateAsync(ModProject project, ModLoaderKind loader, string version)
    {
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return (Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod"),
            Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources/assets/testmod"));
    }
}
