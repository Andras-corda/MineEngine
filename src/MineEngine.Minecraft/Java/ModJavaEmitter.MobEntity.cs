using MineEngine.Core.GameData;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java of the sections of a mob added in V0.7.9 (Entity.md): taming, riding, water entity,
/// immunities and weaknesses, boss bar, collisions, rider seat, dynamic collision box, type of
/// creature, raider, despawn and group functions, structures of the spawning and the golem.
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>Nom complet de la classe des événements de blocs du loader (le golem se construit quand son bloc est posé).</summary>
    protected abstract string BlockEventType { get; }

    /// <summary>Synchronized values of a mob: its texture (variant) and its saddle.</summary>
    private sealed record MobSynchedValue(string Accessor, string JavaType, string Serializer, string Default, string Getter, string Setter, string Tag, string TagGet, string TagPut);

    private static IReadOnlyList<MobSynchedValue> SynchedValues(IRMob mob, int textures)
    {
        var values = new List<MobSynchedValue>();
        if (textures > 1)
        {
            values.Add(new MobSynchedValue("VARIANT", "Integer", "INT", "0", "getVariant", "setVariant", "Variant", "getInt", "putInt"));
        }

        if (mob.Riding is { NeedsSaddle: true })
        {
            values.Add(new MobSynchedValue("SADDLED", "Boolean", "BOOLEAN", "false", "isSaddled", "setSaddled", "Saddled", "getBoolean", "putBoolean"));
        }

        return values;
    }

    /// <summary>Accessors, their definition, getters and setters, and the save in the NBT data of the mob.</summary>
    private static void WriteSynchedMembers(JavaSourceWriter w, JavaGameApi api, IReadOnlyList<MobSynchedValue> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        (string signature, IReadOnlyList<string> first) = api.DefineSynchedData(values[0].Accessor, values[0].Default);
        w.Line().Line("@Override").OpenBlock(signature).Line(first[0]);
        foreach (MobSynchedValue value in values)
        {
            w.Line(api.DefineSynchedData(value.Accessor, value.Default).Body[^1]);
        }

        w.CloseBlock();
        foreach (MobSynchedValue value in values)
        {
            string type = value.JavaType == "Integer" ? "int" : "boolean";
            w.Line()
                .OpenBlock($"public {type} {value.Getter}()")
                .Line($"return this.entityData.get({value.Accessor});")
                .CloseBlock()
                .Line()
                .OpenBlock($"public void {value.Setter}({type} value)")
                .Line($"this.entityData.set({value.Accessor}, value);")
                .CloseBlock();
        }

        w.Line()
            .Line("@Override")
            .OpenBlock("public void addAdditionalSaveData(CompoundTag tag)")
            .Line("super.addAdditionalSaveData(tag);");
        foreach (MobSynchedValue value in values)
        {
            w.Line($"tag.{value.TagPut}(\"{value.Tag}\", this.{value.Getter}());");
        }

        w.CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public void readAdditionalSaveData(CompoundTag tag)")
            .Line("super.readAdditionalSaveData(tag);");
        foreach (MobSynchedValue value in values)
        {
            w.OpenBlock($"if (tag.contains(\"{value.Tag}\"))")
                .Line($"this.{value.Setter}(tag.{value.TagGet}(\"{value.Tag}\"));")
                .CloseBlock();
        }

        w.CloseBlock();
    }

    /// <summary>
    /// Call of a function of the project bound to a mob: it takes the world, then the argument when it
    /// has one (the mob, or a position). Null when the function is not in the mod.
    /// </summary>
    internal static string? FunctionCall(GenerationContext context, JavaProgramNames names, IRFunctionBinding? function, string level, string argument)
    {
        if (function is null || context.Ir.Content.Definitions.OfType<IRFunction>().FirstOrDefault(f => f.Id == function.FunctionId) is not { } found)
        {
            return null;
        }

        return $"{names.Package}.{names.CallPrefix(found, null)}({level}{(function.TakesArgument ? ", " + argument : string.Empty)})";
    }

    /// <summary>Damage types of the game for each immunity (the same keys on every version).</summary>
    private static IEnumerable<string> DamageTypes(MobImmunity immunity) => immunity switch
    {
        MobImmunity.Fire => ["IN_FIRE", "ON_FIRE"],
        MobImmunity.Lava => ["LAVA"],
        MobImmunity.Lightning => ["LIGHTNING_BOLT"],
        MobImmunity.Arrows => ["ARROW"],
        MobImmunity.Fall => ["FALL"],
        MobImmunity.PlayerMelee => ["PLAYER_ATTACK"],
        MobImmunity.Drowning => ["DROWN"],
        MobImmunity.Trident => ["TRIDENT"],
        MobImmunity.Potions => ["MAGIC", "INDIRECT_MAGIC"],
        MobImmunity.Cactus => ["CACTUS"],
        MobImmunity.Explosion => ["EXPLOSION", "PLAYER_EXPLOSION"],
        MobImmunity.Anvil => ["FALLING_ANVIL"],
        MobImmunity.DragonBreath => ["DRAGON_BREATH"],
        _ => ["WITHER", "WITHER_SKULL"],
    };

    /// <summary>Imports the sections of V0.7.9 need in the class of the mob.</summary>
    private IEnumerable<string> EntitySectionImports(IRMob mob, JavaGameApi api, bool hasStructures)
    {
        if (mob.Water is not null)
        {
            yield return FluidTypeClass;
        }

        if (mob.Taming is not null || mob.Riding is not null)
        {
            yield return "net.minecraft.world.InteractionHand";
            yield return "net.minecraft.world.InteractionResult";
            yield return "net.minecraft.world.entity.player.Player";
            yield return "net.minecraft.world.item.ItemStack";
        }

        if (mob.Taming is not null)
        {
            yield return "net.minecraft.world.entity.ai.goal.FollowOwnerGoal";
            yield return "net.minecraft.world.entity.ai.goal.SitWhenOrderedToGoal";
        }

        if (mob.Riding is not null)
        {
            yield return "net.minecraft.world.entity.LivingEntity";
            yield return "net.minecraft.world.phys.Vec3";
            if (mob.Riding.NeedsSaddle)
            {
                yield return "net.minecraft.world.item.Items";
            }
        }

        if (mob.Immunities.Count > 0 || mob.Vulnerabilities.Contains(MobVulnerability.Potions))
        {
            yield return "net.minecraft.world.damagesource.DamageSource";
            yield return "net.minecraft.world.damagesource.DamageTypes";
        }

        if (mob.Immunities.Contains(MobImmunity.Potions))
        {
            yield return "net.minecraft.world.effect.MobEffectInstance";
        }

        if (mob.Boss is not null)
        {
            yield return "net.minecraft.server.level.ServerBossEvent";
            yield return "net.minecraft.server.level.ServerPlayer";
            yield return "net.minecraft.world.BossEvent";
        }

        if (!mob.HitboxScale.IsOne)
        {
            yield return "net.minecraft.world.entity.EntityDimensions";
            yield return "net.minecraft.world.entity.Pose";
        }

        foreach (string import in api.PassengerAttachment(0).Imports)
        {
            yield return import;
        }

        if (mob.CreatureType != MobCreatureType.Undefined && !api.CreatureTypeIsTag)
        {
            yield return "net.minecraft.world.entity.MobType";
        }

        if (mob.Kind == MobKind.Raider)
        {
            yield return "net.minecraft.sounds.SoundEvent";
            yield return "net.minecraft.sounds.SoundEvents";
            foreach (string import in api.RaidBuffs.Imports)
            {
                yield return import;
            }
        }

        if (hasStructures)
        {
            yield return "net.minecraft.core.BlockPos";
            yield return "net.minecraft.core.registries.Registries";
            yield return "net.minecraft.resources.ResourceKey";
            yield return "net.minecraft.resources.ResourceLocation";
            yield return "net.minecraft.util.RandomSource";
            yield return "net.minecraft.world.entity.MobSpawnType";
            yield return "net.minecraft.world.level.ServerLevelAccessor";
            yield return "net.minecraft.world.level.levelgen.structure.Structure";
            yield return MobJava.SpawnRuleImport(mob.Kind);
        }
    }

    /// <summary>The overrides of the sections of V0.7.9 in the class of the mob.</summary>
    private static void WriteEntitySections(JavaSourceWriter w, GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names, IRMob mob, string className)
    {
        string? Call(IRFunctionBinding? function) => FunctionCall(context, names, function, "this.level()", "this");
        MobEventCalls events = MobEvents(context, naming, mob);

        if (mob.Taming is not null || mob.Riding is not null || events.Has(MobEventKind.RightClicked))
        {
            WriteInteraction(w, mob, events);
        }

        if (mob.Riding is { } riding)
        {
            w.Line().Line("@Override")
                .OpenBlock("public LivingEntity getControllingPassenger()")
                .Line("return this.getFirstPassenger() instanceof Player player ? player : super.getControllingPassenger();")
                .CloseBlock()
                .Line().Line("@Override")
                .OpenBlock("protected void tickRidden(Player player, Vec3 input)")
                .Line("super.tickRidden(player, input);")
                .Line("this.setYRot(player.getYRot());")
                .Line("this.yRotO = this.getYRot();")
                .Line("this.setXRot(player.getXRot() * 0.5F);")
                .Line("this.setRot(this.getYRot(), this.getXRot());")
                .Line("this.yBodyRot = this.getYRot();")
                .Line("this.yHeadRot = this.yBodyRot;")
                .CloseBlock()
                .Line().Line("@Override")
                .OpenBlock("protected Vec3 getRiddenInput(Player player, Vec3 input)")
                .Line(riding.Strafe ? "float strafe = player.xxa * 0.5F;" : "float strafe = 0.0F;")
                .Line(riding.Forward ? "float forward = player.zza;" : "float forward = 0.0F;")
                .Line("return new Vec3(strafe, 0.0, forward <= 0.0F ? forward * 0.25F : forward);")
                .CloseBlock()
                .Line().Line("@Override")
                .OpenBlock("protected float getRiddenSpeed(Player player)")
                .Line("return (float) this.getAttributeValue(Attributes.MOVEMENT_SPEED);")
                .CloseBlock();
        }

        if (mob.Water is { } water)
        {
            // The loaders ask every fluid whether the mob drowns in it (canBreatheUnderwater is final since 1.20.5).
            string breathes = Call(water.BreathesFunction) ?? (water.Breathes ? "true" : "false");
            string pushed = Call(water.PushedFunction) ?? (water.Pushed ? "true" : "false");
            w.Line().Line("@Override")
                .OpenBlock("public boolean canDrownInFluidType(FluidType type)")
                .Line($"return !{breathes};")
                .CloseBlock()
                .Line().Line("@Override")
                .OpenBlock("public boolean isPushedByFluid()")
                .Line($"return {pushed};")
                .CloseBlock();
        }

        if (mob.Immunities.Count > 0 || mob.Vulnerabilities.Contains(MobVulnerability.Potions) || events.Has(MobEventKind.Hurt))
        {
            w.Line().Line("@Override").OpenBlock("public boolean hurt(DamageSource source, float amount)");
            List<string> immune = [.. mob.Immunities.Order().SelectMany(DamageTypes).Select(t => $"source.is(DamageTypes.{t})")];
            if (immune.Count > 0)
            {
                w.OpenBlock($"if ({string.Join(" || ", immune)})").Line("return false;").CloseBlock().Line();
            }

            if (mob.Vulnerabilities.Contains(MobVulnerability.Potions))
            {
                w.OpenBlock("if (source.is(DamageTypes.MAGIC) || source.is(DamageTypes.INDIRECT_MAGIC))")
                    .Line("amount *= 2.0F;")
                    .CloseBlock()
                    .Line();
            }

            if (events.Has(MobEventKind.Hurt))
            {
                w.Line("boolean hurt = super.hurt(source, amount);")
                    .OpenBlock("if (hurt && !this.level().isClientSide)");
                events.Write(w, MobEventKind.Hurt, PlayerOf("source.getEntity()"), "source.getEntity()", "amount");
                w.CloseBlock().Line().Line("return hurt;").CloseBlock();
            }
            else
            {
                w.Line("return super.hurt(source, amount);").CloseBlock();
            }
        }

        if (mob.Immunities.Contains(MobImmunity.Potions))
        {
            w.Line().Line("@Override")
                .OpenBlock("public boolean canBeAffected(MobEffectInstance effect)")
                .Line("return false;")
                .CloseBlock();
        }

        if (mob.Vulnerabilities.Contains(MobVulnerability.Water))
        {
            w.Line().Line("@Override")
                .OpenBlock("public boolean isSensitiveToWater()")
                .Line("return true;")
                .CloseBlock();
        }

        if (mob.NoCollision)
        {
            w.Line().Line("@Override")
                .OpenBlock("public boolean isPushable()")
                .Line("return false;")
                .CloseBlock()
                .Line().Line("@Override")
                .OpenBlock("protected void pushEntities()")
                .CloseBlock();
        }

        (string attachment, IReadOnlyList<string> attachmentBody, _) = api.PassengerAttachment(mob.PassengerOffset);
        w.Line().Line("@Override").OpenBlock(attachment);
        foreach (string line in attachmentBody)
        {
            w.Line(line);
        }

        w.CloseBlock();

        string? hitboxScale = null;
        if (!mob.HitboxScale.IsOne)
        {
            hitboxScale = Call(mob.HitboxScale.Function) is { } call ? $"(float) Math.max(0.05, {call})" : JavaNames.FloatLiteral((float)mob.HitboxScale.Value);
            (string signature, string superCall) = api.EntityDimensionsOverride;
            w.Line()
                .Line("private float hitboxScale = 1.0F;")
                .Line()
                .Line("@Override")
                .OpenBlock(signature)
                .Line($"return {superCall}.scale(this.hitboxScale);")
                .CloseBlock();
        }

        WriteTick(w, events, hitboxScale, mob.Vehicle?.Kind ?? MobVehicleKind.None);
        WriteMobEvents(w, events, mob.Vehicle is { } vehicle ? $"{naming.ItemsClass}.{naming.ConstantFor(vehicle.ItemId)}" : null);

        if (mob.CreatureType != MobCreatureType.Undefined && !api.CreatureTypeIsTag)
        {
            w.Line().Line("@Override")
                .OpenBlock("public MobType getMobType()")
                .Line($"return MobType.{mob.CreatureType.ToString().ToUpperInvariant()};")
                .CloseBlock();
        }

        if (mob.Kind == MobKind.Raider)
        {
            w.Line().Line("@Override")
                .OpenBlock(api.RaidBuffs.Signature)
                .CloseBlock()
                .Line().Line("@Override")
                .OpenBlock("public SoundEvent getCelebrateSound()")
                .Line("return SoundEvents.PILLAGER_CELEBRATE;")
                .CloseBlock();
        }

        if (mob.Spawning?.GroupFunction is { } group && Call(group) is { } groupCall)
        {
            w.Line().Line("@Override")
                .OpenBlock("public int getMaxSpawnClusterSize()")
                .Line($"return Math.max(1, (int) Math.round({groupCall}));")
                .CloseBlock();
        }

        if (mob.Boss is { } boss)
        {
            string overlay = boss.Overlay switch
            {
                MobBossOverlay.Notched6 => "NOTCHED_6",
                MobBossOverlay.Notched10 => "NOTCHED_10",
                MobBossOverlay.Notched12 => "NOTCHED_12",
                MobBossOverlay.Notched20 => "NOTCHED_20",
                _ => "PROGRESS",
            };
            w.Line()
                .Line($"private final ServerBossEvent bossEvent = new ServerBossEvent(this.getDisplayName(), BossEvent.BossBarColor.{boss.Color.ToString().ToUpperInvariant()}, BossEvent.BossBarOverlay.{overlay});")
                .Line()
                .Line("@Override")
                .OpenBlock("public void startSeenByPlayer(ServerPlayer player)")
                .Line("super.startSeenByPlayer(player);");
            if (boss.ShowBar)
            {
                w.Line("this.bossEvent.addPlayer(player);");
            }

            w.CloseBlock()
                .Line()
                .Line("@Override")
                .OpenBlock("public void stopSeenByPlayer(ServerPlayer player)")
                .Line("super.stopSeenByPlayer(player);")
                .Line("this.bossEvent.removePlayer(player);")
                .CloseBlock()
                .Line()
                .Line("@Override")
                .OpenBlock("protected void customServerAiStep()")
                .Line("super.customServerAiStep();")
                .Line("this.bossEvent.setProgress(this.getHealth() / this.getMaxHealth());")
                .CloseBlock();
        }

        if (mob.Spawning is { Structures.Count: > 0 } spawning)
        {
            // The rule of its family, and a piece of one of the structures at the place.
            w.Line()
                .Line("/** Natural spawning: the rule of the family, inside one of the chosen structures. */")
                .OpenBlock($"public static boolean checkSpawnRules(EntityType<{className}> type, ServerLevelAccessor level, MobSpawnType reason, BlockPos position, RandomSource random)")
                .OpenBlock($"if (!{MobJava.SpawnRule(mob.Kind).Replace("::", ".", StringComparison.Ordinal)}(type, level, reason, position, random))")
                .Line("return false;")
                .CloseBlock()
                .Line()
                .Line("String[] structures = {")
                .Indent();
            foreach (var structure in spawning.Structures)
            {
                w.Line(JavaNames.StringLiteral(structure.ToString()) + ",");
            }

            w.Unindent()
                .Line("};")
                .OpenBlock("for (String structure : structures)")
                .Line($"Structure found = level.registryAccess().registryOrThrow(Registries.STRUCTURE).get(ResourceKey.create(Registries.STRUCTURE, {api.ParseResourceLocation("structure")}));")
                .OpenBlock("if (found != null && level.getLevel().structureManager().getStructureWithPieceAt(position, found).isValid())")
                .Line("return true;")
                .CloseBlock()
                .CloseBlock()
                .Line()
                .Line("return false;")
                .CloseBlock();
        }
    }

    /// <summary>Right-click of a player: the graphs of the event, taming with the items, sitting, saddling, riding.</summary>
    private static void WriteInteraction(JavaSourceWriter w, IRMob mob, MobEventCalls events)
    {
        w.Line().Line("@Override")
            .OpenBlock("public InteractionResult mobInteract(Player player, InteractionHand hand)");
        if (events.Has(MobEventKind.RightClicked))
        {
            w.OpenBlock("if (!this.level().isClientSide)");
            events.Write(w, MobEventKind.RightClicked, "player", "player", "0.0");
            w.CloseBlock();
        }

        if (mob.Taming is not null || mob.Riding is not null)
        {
            w.Line("ItemStack stack = player.getItemInHand(hand);");
        }

        if (mob.Taming is { } taming)
        {
            w.OpenBlock("if (!this.isTame())")
                .Line("double chance = 0.0;");
            foreach (IRMobTamingItem item in taming)
            {
                w.OpenBlock($"if (stack.is(item({JavaNames.StringLiteral(item.Item.ToString())})))")
                    .Line($"chance = {MobJava.Double(item.Chance)};")
                    .CloseBlock();
            }

            w.OpenBlock("if (chance > 0.0)")
                .OpenBlock("if (!player.getAbilities().instabuild)")
                .Line("stack.shrink(1);")
                .CloseBlock()
                .OpenBlock("if (!this.level().isClientSide)")
                .OpenBlock("if (this.random.nextDouble() * 100.0 < chance)")
                .Line("this.tame(player);")
                .Line("this.level().broadcastEntityEvent(this, (byte) 7);");
            events.Write(w, MobEventKind.Tamed, "player", "player", "0.0");
            w.CloseBlock()
                .OpenBlock("else")
                .Line("this.level().broadcastEntityEvent(this, (byte) 6);")
                .CloseBlock()
                .CloseBlock()
                .Line("return InteractionResult.SUCCESS;")
                .CloseBlock()
                .CloseBlock();
        }

        if (mob.Riding is { } riding)
        {
            string owner = mob.Taming is not null ? " && this.isTame() && this.isOwnedBy(player)" : string.Empty;
            if (riding.NeedsSaddle)
            {
                w.OpenBlock($"if (!this.isSaddled() && stack.is(Items.SADDLE){owner})")
                    .Line("this.setSaddled(true);")
                    .OpenBlock("if (!player.getAbilities().instabuild)")
                    .Line("stack.shrink(1);")
                    .CloseBlock()
                    .Line("return InteractionResult.SUCCESS;")
                    .CloseBlock();
            }

            string saddle = riding.NeedsSaddle ? " && this.isSaddled()" : string.Empty;
            w.OpenBlock($"if (!this.isVehicle() && !player.isSecondaryUseActive(){saddle}{owner})")
                .OpenBlock("if (!this.level().isClientSide)")
                .Line("player.startRiding(this);")
                .CloseBlock()
                .Line("return InteractionResult.SUCCESS;")
                .CloseBlock();
        }

        if (mob.Taming is not null)
        {
            // A tamed animal sits or stands when its owner (sneaking, if it can be ridden) clicks it.
            string sneak = mob.Riding is not null ? " && player.isSecondaryUseActive()" : string.Empty;
            w.OpenBlock($"if (this.isTame() && this.isOwnedBy(player){sneak})")
                .Line("this.setOrderedToSit(!this.isOrderedToSit());")
                .Line("return InteractionResult.SUCCESS;")
                .CloseBlock();
        }

        w.Line("return super.mobInteract(player, hand);").CloseBlock();
    }

    // ----- Events and triggers (V0.7.9, step 2) -----

    /// <summary>
    /// Calls of the graphs that react to the events of a mob ("StormWolfGraphBehavior.onMobHurt1"), by
    /// event. Every method takes the mob, the level, its position, the player, the other entity and an amount.
    /// </summary>
    private sealed class MobEventCalls(IReadOnlyDictionary<MobEventKind, IReadOnlyList<string>> calls, IReadOnlyList<string> imports)
    {
        public IReadOnlyList<string> Imports => imports;

        public bool Has(MobEventKind kind) => calls.ContainsKey(kind);

        /// <summary>One line per graph of the event; nothing when no graph reacts to it.</summary>
        public void Write(JavaSourceWriter w, MobEventKind kind, string player, string other, string amount)
        {
            foreach (string call in calls.TryGetValue(kind, out IReadOnlyList<string>? list) ? list : [])
            {
                w.Line($"{call}(this, this.level(), this.blockPosition(), {player}, {other}, {amount});");
            }
        }
    }

    private static MobEventCalls MobEvents(GenerationContext context, JavaModNaming naming, IRMob mob)
    {
        var calls = new Dictionary<MobEventKind, List<string>>();
        var imports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (IRBehavior behavior in context.Ir.Content.Behaviors)
        {
            foreach ((IREventHandler handler, string method) in HandlerMethods(behavior))
            {
                if (handler.Mob != mob.Id.Value || Assets.Graphs.NodeCatalog.Get(handler.Event).MobEvent is not { } kind)
                {
                    continue;
                }

                string behaviorClass = naming.BehaviorClassFor(behavior.Id);
                imports.Add($"{naming.BehaviorPackage}.{behaviorClass}");
                calls.TryAdd(kind, []);
                calls[kind].Add($"{behaviorClass}.{method}");
            }
        }

        if (calls.Count > 0)
        {
            imports.UnionWith(["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.player.Player"]);
        }

        foreach (MobEventKind kind in calls.Keys)
        {
            imports.UnionWith(kind switch
            {
                MobEventKind.StruckByLightning => ["net.minecraft.server.level.ServerLevel", "net.minecraft.world.entity.LightningBolt"],
                MobEventKind.Falls or MobEventKind.Dies or MobEventKind.Hurt => ["net.minecraft.world.damagesource.DamageSource"],
                MobEventKind.KillsEntity => ["net.minecraft.server.level.ServerLevel", "net.minecraft.world.entity.LivingEntity"],
                MobEventKind.RightClicked => ["net.minecraft.world.InteractionHand", "net.minecraft.world.InteractionResult"],
                _ => [],
            });
        }

        return new MobEventCalls(calls.ToDictionary(c => c.Key, c => (IReadOnlyList<string>)c.Value), [.. imports]);
    }

    /// <summary>The player behind an entity of the game, or null.</summary>
    private static string PlayerOf(string entity) => $"({entity} instanceof Player ? (Player) {entity} : null)";

    /// <summary>
    /// The single tick of the mob: its dynamic collision box, then on the server its first appearance
    /// (once, kept in its persistent data), every tick and every tick while it is ridden.
    /// </summary>
    private static void WriteTick(JavaSourceWriter w, MobEventCalls events, string? hitboxScale, MobVehicleKind vehicle)
    {
        bool server = events.Has(MobEventKind.Spawns) || events.Has(MobEventKind.Tick) || events.Has(MobEventKind.RiddenTick);
        if (hitboxScale is null && !server && vehicle == MobVehicleKind.None)
        {
            return;
        }

        w.Line().Line("@Override").OpenBlock("public void tick()").Line("super.tick();");
        WriteVehicleTick(w, vehicle);
        if (hitboxScale is not null)
        {
            w.Line($"float scale = {hitboxScale};")
                .OpenBlock("if (scale != this.hitboxScale)")
                .Line("this.hitboxScale = scale;")
                .Line("this.refreshDimensions();")
                .CloseBlock();
        }

        if (server)
        {
            w.OpenBlock("if (!this.level().isClientSide && this.isAlive())");
            if (events.Has(MobEventKind.Spawns))
            {
                w.OpenBlock("if (!this.getPersistentData().getBoolean(\"mineengine_spawned\"))")
                    .Line("this.getPersistentData().putBoolean(\"mineengine_spawned\", true);");
                events.Write(w, MobEventKind.Spawns, "null", "null", "0.0");
                w.CloseBlock();
            }

            events.Write(w, MobEventKind.Tick, "null", "null", "0.0");
            if (events.Has(MobEventKind.RiddenTick))
            {
                w.OpenBlock("if (this.isVehicle())")
                    .Line("Entity rider = this.getFirstPassenger();");
                events.Write(w, MobEventKind.RiddenTick, PlayerOf("rider"), "rider", "0.0");
                w.CloseBlock();
            }

            w.CloseBlock();
        }

        w.CloseBlock();
    }

    /// <summary>The other events, each in the method of the game that sees it happen, on the server only.</summary>
    private static void WriteMobEvents(JavaSourceWriter w, MobEventCalls events, string? vehicleItem)
    {
        void Override(MobEventKind kind, string signature, string before, string? guard, string player, string other, string amount, string after)
        {
            if (!events.Has(kind))
            {
                return;
            }

            w.Line().Line("@Override").OpenBlock(signature);
            foreach (string line in before.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                w.Line(line + ";");
            }

            w.OpenBlock(guard is null ? "if (!this.level().isClientSide)" : $"if (!this.level().isClientSide && {guard})");
            events.Write(w, kind, player, other, amount);
            w.CloseBlock();
            if (after.Length > 0)
            {
                w.Line(after);
            }

            w.CloseBlock();
        }

        Override(MobEventKind.StruckByLightning, "public void thunderHit(ServerLevel level, LightningBolt bolt)",
            "super.thunderHit(level, bolt);", null, "null", "bolt", "0.0", string.Empty);
        Override(MobEventKind.Falls, "public boolean causeFallDamage(float distance, float multiplier, DamageSource source)",
            "boolean hurt = super.causeFallDamage(distance, multiplier, source);", "distance > 1.0F", "null", "null", "distance", "return hurt;");

        // The game can call die twice (the totem aside): the "dead" flag keeps the event once. A
        // broken vehicle gives back its item.
        if (events.Has(MobEventKind.Dies) || vehicleItem is not null)
        {
            w.Line().Line("@Override")
                .OpenBlock("public void die(DamageSource source)")
                .Line("boolean wasDead = this.dead;")
                .Line("super.die(source);")
                .OpenBlock("if (!this.level().isClientSide && !wasDead)");
            if (vehicleItem is not null)
            {
                w.Line($"this.spawnAtLocation({vehicleItem}.get());");
            }

            events.Write(w, MobEventKind.Dies, PlayerOf("source.getEntity()"), "source.getEntity()", "0.0");
            w.CloseBlock().CloseBlock();
        }

        Override(MobEventKind.PlayerCollides, "public void playerTouch(Player player)",
            "super.playerTouch(player);", null, "player", "player", "0.0", string.Empty);
        Override(MobEventKind.KillsEntity, "public boolean killedEntity(ServerLevel level, LivingEntity entity)",
            "boolean killed = super.killedEntity(level, entity);", null, PlayerOf("entity"), "entity", "0.0", "return killed;");
        Override(MobEventKind.RideStarts, "protected void addPassenger(Entity passenger)",
            "super.addPassenger(passenger);", null, PlayerOf("passenger"), "passenger", "0.0", string.Empty);
        Override(MobEventKind.RideStops, "protected void removePassenger(Entity passenger)",
            "super.removePassenger(passenger);", null, PlayerOf("passenger"), "passenger", "0.0", string.Empty);
        Override(MobEventKind.EntityCollides, "protected void doPush(Entity entity)",
            "super.doPush(entity);", null, PlayerOf("entity"), "entity", "0.0", string.Empty);
    }

    // ----- Vehicles (V0.7.9, step 3) -----

    /// <summary>Imports of the item class for the items that place the boats and minecarts.</summary>
    private static readonly string[] VehicleItemImports =
    [
        "net.minecraft.core.BlockPos",
        "net.minecraft.tags.BlockTags",
        "net.minecraft.world.InteractionHand",
        "net.minecraft.world.InteractionResult",
        "net.minecraft.world.InteractionResultHolder",
        "net.minecraft.world.entity.Entity",
        "net.minecraft.world.entity.player.Player",
        "net.minecraft.world.item.Item",
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.Rarity",
        "net.minecraft.world.item.context.UseOnContext",
        "net.minecraft.world.level.ClipContext",
        "net.minecraft.world.level.Level",
        "net.minecraft.world.phys.BlockHitResult",
        "net.minecraft.world.phys.HitResult",
        "net.minecraft.world.phys.Vec3",
    ];

    /// <summary>Imports of the class of a vehicle: the water of the boat, the rails of the minecart, the item given back.</summary>
    private static IEnumerable<string> VehicleImports(IRMob mob, JavaModNaming naming) => mob.Vehicle?.Kind switch
    {
        MobVehicleKind.Boat => [$"{naming.RegistryPackage}.{naming.ItemsClass}", "net.minecraft.world.damagesource.DamageSource", "net.minecraft.tags.FluidTags", "net.minecraft.world.phys.Vec3"],
        MobVehicleKind.Minecart =>
        [
            $"{naming.RegistryPackage}.{naming.ItemsClass}", "net.minecraft.world.damagesource.DamageSource", "net.minecraft.core.BlockPos", "net.minecraft.world.phys.Vec3",
            "net.minecraft.world.level.block.BaseRailBlock", "net.minecraft.world.level.block.state.BlockState", "net.minecraft.world.level.block.state.properties.RailShape",
        ],
        _ => [],
    };

    /// <summary>
    /// The item places the vehicle like the items of the game: a boat where the player looks (water
    /// or ground), a minecart on the rail clicked. It is used up, except in creative mode.
    /// </summary>
    private static List<string> VehicleItemOverrides(MobVehicleKind kind, string entityType)
    {
        if (kind == MobVehicleKind.Boat)
        {
            return
            [
                "@Override",
                "public InteractionResultHolder<ItemStack> use(Level level, Player player, InteractionHand hand) {",
                "    ItemStack stack = player.getItemInHand(hand);",
                "    BlockHitResult hit = getPlayerPOVHitResult(level, player, ClipContext.Fluid.ANY);",
                "    if (hit.getType() != HitResult.Type.BLOCK) {",
                "        return InteractionResultHolder.pass(stack);",
                "    }",
                "    if (!level.isClientSide) {",
                $"        Entity vehicle = {entityType}.get().create(level);",
                "        if (vehicle != null) {",
                "            Vec3 at = hit.getLocation();",
                "            vehicle.moveTo(at.x, at.y, at.z, player.getYRot(), 0.0F);",
                "            level.addFreshEntity(vehicle);",
                "            if (!player.getAbilities().instabuild) {",
                "                stack.shrink(1);",
                "            }",
                "        }",
                "    }",
                "    return InteractionResultHolder.sidedSuccess(stack, level.isClientSide());",
                "}",
            ];
        }

        return
        [
            "@Override",
            "public InteractionResult useOn(UseOnContext context) {",
            "    Level level = context.getLevel();",
            "    BlockPos position = context.getClickedPos();",
            "    if (!level.getBlockState(position).is(BlockTags.RAILS)) {",
            "        return InteractionResult.FAIL;",
            "    }",
            "    if (!level.isClientSide) {",
            $"        Entity vehicle = {entityType}.get().create(level);",
            "        if (vehicle != null) {",
            "            Player player = context.getPlayer();",
            "            vehicle.moveTo(position.getX() + 0.5, position.getY() + 0.0625, position.getZ() + 0.5, player != null ? player.getYRot() : 0.0F, 0.0F);",
            "            level.addFreshEntity(vehicle);",
            "            if (player == null || !player.getAbilities().instabuild) {",
            "                context.getItemInHand().shrink(1);",
            "            }",
            "        }",
            "    }",
            "    return InteractionResult.sidedSuccess(level.isClientSide);",
            "}",
        ];
    }

    /// <summary>
    /// A boat floats up to the surface of the water. A minecart on a straight rail stays on its
    /// middle and only moves along it; on a curve it is free to turn.
    /// </summary>
    private static void WriteVehicleTick(JavaSourceWriter w, MobVehicleKind vehicle)
    {
        if (vehicle == MobVehicleKind.Boat)
        {
            w.OpenBlock("if (this.getFluidHeight(FluidTags.WATER) > 0.3)")
                .Line("Vec3 motion = this.getDeltaMovement();")
                .Line("this.setDeltaMovement(motion.x, Math.min(motion.y + 0.04, 0.1), motion.z);")
                .CloseBlock();
        }
        else if (vehicle == MobVehicleKind.Minecart)
        {
            w.Line("BlockPos railPosition = this.blockPosition();")
                .Line("BlockState rail = this.level().getBlockState(railPosition);")
                .OpenBlock("if (!(rail.getBlock() instanceof BaseRailBlock))")
                .Line("railPosition = railPosition.below();")
                .Line("rail = this.level().getBlockState(railPosition);")
                .CloseBlock()
                .OpenBlock("if (rail.getBlock() instanceof BaseRailBlock block)")
                .Line("RailShape shape = rail.getValue(block.getShapeProperty());")
                .Line("Vec3 motion = this.getDeltaMovement();")
                .OpenBlock("if (shape == RailShape.NORTH_SOUTH || shape == RailShape.ASCENDING_NORTH || shape == RailShape.ASCENDING_SOUTH)")
                .Line("this.setDeltaMovement(0.0, motion.y, motion.z);")
                .Line("this.setPos(railPosition.getX() + 0.5, this.getY(), this.getZ());")
                .CloseBlock()
                .OpenBlock("else if (shape == RailShape.EAST_WEST || shape == RailShape.ASCENDING_EAST || shape == RailShape.ASCENDING_WEST)")
                .Line("this.setDeltaMovement(motion.x, motion.y, 0.0);")
                .Line("this.setPos(this.getX(), this.getY(), railPosition.getZ() + 0.5);")
                .CloseBlock()
                .CloseBlock();
        }
    }

    // ----- Golem -----

    /// <summary>Imports of the registry of the entities for the golems.</summary>
    private IEnumerable<string> GolemImports(IReadOnlyList<IRMob> mobs) => mobs.Any(m => m.Golem is not null)
        ? [BlockEventType, "net.minecraft.core.BlockPos", "net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.server.level.ServerLevel",
            "net.minecraft.world.entity.Mob", "net.minecraft.world.level.block.Block", "net.minecraft.world.level.block.Blocks"]
        : [];

    /// <summary>
    /// A golem is built when its activator block is placed: on a column of two identical blocks (they
    /// disappear and the mob stands there), or where its function (the position → yes / no) says.
    /// </summary>
    private void WriteGolems(JavaSourceWriter w, GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names, IReadOnlyList<IRMob> mobs)
    {
        List<IRMob> golems = [.. mobs.Where(m => m.Golem is not null)];
        if (golems.Count == 0)
        {
            return;
        }

        string eventClass = SimpleName(BlockEventType);
        w.Line()
            .Line("/** Golems: placing their activator block builds them (registered on the game bus). */")
            .OpenBlock($"public static void onBlockPlaced({eventClass}.EntityPlaceEvent event)")
            .OpenBlock("if (!(event.getLevel() instanceof ServerLevel level))")
            .Line("return;")
            .CloseBlock()
            .Line()
            .Line("BlockPos top = event.getPos();");
        foreach (IRMob mob in golems)
        {
            IRMobGolem golem = mob.Golem!;
            string activator = $"BuiltInRegistries.BLOCK.get({api.ParseResourceLocation(JavaNames.StringLiteral(golem.Activator.ToString()))})";
            string? custom = FunctionCall(context, names, golem.Function, "level", "top");
            w.OpenBlock($"if (event.getPlacedBlock().is({activator}))");
            if (custom is not null)
            {
                w.OpenBlock($"if ({custom})")
                    .Line($"spawnGolem(level, {naming.ConstantFor(mob.Id)}.get().create(level), top);")
                    .Line("return;")
                    .CloseBlock();
            }
            else
            {
                w.Line("Block body = level.getBlockState(top.below()).getBlock();")
                    .OpenBlock("if (body != Blocks.AIR && level.getBlockState(top.below(2)).getBlock() == body)")
                    .OpenBlock("for (int i = 0; i < 3; i++)")
                    .Line("level.setBlock(top.below(i), Blocks.AIR.defaultBlockState(), 2);")
                    .CloseBlock()
                    .Line($"spawnGolem(level, {naming.ConstantFor(mob.Id)}.get().create(level), top.below(2));")
                    .Line("return;")
                    .CloseBlock();
            }

            w.CloseBlock();
        }

        w.CloseBlock()
            .Line()
            .OpenBlock("private static void spawnGolem(ServerLevel level, Mob mob, BlockPos position)")
            .OpenBlock("if (mob != null)")
            .Line("mob.moveTo(position.getX() + 0.5, position.getY(), position.getZ() + 0.5, 0.0F, 0.0F);")
            .Line("level.addFreshEntity(mob);")
            .CloseBlock()
            .CloseBlock();
    }

    // ----- Renderer of a mob -----

    /// <summary>
    /// Renderer of a mob (V0.7.9: one class per mob): its model (of the game or Blockbench), its
    /// textures (variants of the model, variations, appearances), its shadow, its scale (fixed or
    /// given by a function), a see-through texture, and its layers (of the model, then of the mob)
    /// drawn over it, each one shown, lit and flashed as its functions say.
    /// </summary>
    private static void WriteMobRenderer(GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names, IRMob mob)
    {
        ResourcePaths paths = context.Paths;
        string className = naming.RendererClassFor(mob.Id);
        string entityClass = naming.EntityClassFor(mob.Id);
        string Location(string path) => api.ParseResourceLocation(JavaNames.StringLiteral(path.Contains(':', StringComparison.Ordinal) ? path : paths.Reference(path)));
        IRModel? model = CustomModelMobs(context).Contains(mob) ? FindModel(context, mob) : null;

        string modelType;
        string modelCreation;
        string baseTexture;
        var imports = new List<string>
        {
            "com.mojang.blaze3d.vertex.PoseStack",
            "com.mojang.blaze3d.vertex.VertexConsumer",
            "net.minecraft.client.renderer.MultiBufferSource",
            "net.minecraft.client.renderer.RenderType",
            "net.minecraft.client.renderer.entity.EntityRendererProvider",
            "net.minecraft.client.renderer.entity.LivingEntityRenderer",
            "net.minecraft.client.renderer.entity.MobRenderer",
            "net.minecraft.client.renderer.entity.RenderLayerParent",
            "net.minecraft.client.renderer.entity.layers.RenderLayer",
            "net.minecraft.client.renderer.texture.OverlayTexture",
            "net.minecraft.resources.ResourceLocation",
            $"{naming.EntityPackage}.{entityClass}",
        };
        if (model is not null)
        {
            string modelClass = naming.EntityModelClassFor(model.Id);
            modelType = $"{modelClass}<{entityClass}>";
            modelCreation = $"new {modelClass}<>(context.bakeLayer({modelClass}.LAYER_LOCATION))";
            imports.Add($"{naming.EntityModelPackage}.{modelClass}");
            baseTexture = mob.Texture is not null || model.Textures.Count == 0
                ? $"textures/entity/{mob.Id}.png"
                : $"textures/block/model/{model.Id}_{model.Textures[0].Key}.png";
        }
        else
        {
            MobModelJava vanilla = MobJava.Model(mob.Model);
            modelType = $"{vanilla.ModelClass}<{entityClass}>";
            modelCreation = $"new {vanilla.ModelClass}<>(context.bakeLayer(ModelLayers.{vanilla.Layer}))";
            imports.Add("net.minecraft.client.model." + vanilla.ModelClass);
            imports.Add("net.minecraft.client.model.geom.ModelLayers");
            baseTexture = mob.Texture is null ? "minecraft:" + vanilla.VanillaTexture : $"textures/entity/{mob.Id}.png";
        }

        List<string> textures =
        [
            Location(baseTexture),
            .. Enumerable.Range(1, model?.Variants.Count ?? 0).Select(i => Location($"textures/entity/model/{model!.Id}_variant_{i}.png")),
            .. Enumerable.Range(1, mob.Variations.Count).Select(i => Location($"textures/entity/{mob.Id}_variation_{i}.png")),
            .. AppearanceTextures(mob, baseTexture).Select(Location),
        ];

        // Layers: those of the Blockbench model (emissive or not), then those of the mob.
        var layers = new List<(string Texture, bool Glow, bool NoHurt, IRMobLayer? Mob, int MobIndex)>();
        for (int i = 0; i < (model?.Layers.Count ?? 0); i++)
        {
            layers.Add((Location($"textures/entity/model/{model!.Id}_layer_{i + 1}.png"), model.Layers[i].Emissive, false, null, -1));
        }

        for (int i = 0; i < mob.Layers.Count; i++)
        {
            layers.Add((Location($"textures/entity/{mob.Id}_layer_{i + 1}.png"), mob.Layers[i].Glow, mob.Layers[i].NoHurtOverlay, mob.Layers[i], i));
        }

        string? Call(IRFunctionBinding? function) => FunctionCall(context, names, function, "entity.level()", "entity");

        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, $"Affichage du mob « {mob.DisplayName} ».")
            .OpenBlock($"public class {className} extends MobRenderer<{entityClass}, {modelType}>")
            .Line("private static final ResourceLocation[] TEXTURES = {")
            .Indent();
        foreach (string texture in textures)
        {
            w.Line(texture + ",");
        }

        w.Unindent().Line("};");
        for (int i = 0; i < layers.Count; i++)
        {
            w.Line($"private static final ResourceLocation LAYER_{i + 1} = {layers[i].Texture};");
        }

        w.Line()
            .OpenBlock($"public {className}(EntityRendererProvider.Context context)")
            .Line($"super(context, {modelCreation}, {JavaNames.FloatLiteral(mob.ShadowRadius)});");
        for (int i = 0; i < layers.Count; i++)
        {
            w.Line($"this.addLayer(new TextureLayer(this, LAYER_{i + 1}, {i}));");
        }

        w.CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock($"public ResourceLocation getTextureLocation({entityClass} entity)")
            .Line(textures.Count > 1 ? "return TEXTURES[Math.floorMod(entity.getVariant(), TEXTURES.length)];" : "return TEXTURES[0];")
            .CloseBlock();

        if (!mob.VisualScale.IsOne)
        {
            string scale = Call(mob.VisualScale.Function) is { } call ? $"(float) Math.max(0.05, {call})" : JavaNames.FloatLiteral((float)mob.VisualScale.Value);
            w.Line()
                .Line("@Override")
                .OpenBlock($"protected void scale({entityClass} entity, PoseStack poseStack, float partialTick)")
                .Line($"float scale = {scale};")
                .Line("poseStack.scale(scale, scale, scale);")
                .CloseBlock();
        }

        if (mob.TextureTransparent)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock($"protected RenderType getRenderType({entityClass} entity, boolean bodyVisible, boolean translucent, boolean glowing)")
                .Line("return bodyVisible ? RenderType.entityTranslucent(this.getTextureLocation(entity)) : super.getRenderType(entity, bodyVisible, translucent, glowing);")
                .CloseBlock();
        }

        if (layers.Count > 0)
        {
            WriteLayerRules(w, entityClass, mob, layers.Select(l => (l.Glow, l.NoHurt, l.Mob, l.MobIndex)).ToList(), Call);

            // Same model, other texture; a glowing layer ignores the light, like the eyes of a spider.
            string color = api.PackedRenderColor ? "-1" : "1.0f, 1.0f, 1.0f, 1.0f";
            w.Line()
                .OpenBlock($"private static final class TextureLayer extends RenderLayer<{entityClass}, {modelType}>")
                .Line("private final ResourceLocation texture;")
                .Line("private final int index;")
                .Line()
                .OpenBlock($"TextureLayer(RenderLayerParent<{entityClass}, {modelType}> parent, ResourceLocation texture, int index)")
                .Line("super(parent);")
                .Line("this.texture = texture;")
                .Line("this.index = index;")
                .CloseBlock()
                .Line()
                .Line("@Override")
                .OpenBlock($"public void render(PoseStack poseStack, MultiBufferSource buffers, int packedLight, {entityClass} entity, float limbSwing, float limbSwingAmount, float partialTick, float ageInTicks, float netHeadYaw, float headPitch)")
                .OpenBlock("if (!isVisible(entity, this.index))")
                .Line("return;")
                .CloseBlock()
                .Line()
                .Line("boolean glow = glows(entity, this.index);")
                .Line("VertexConsumer consumer = buffers.getBuffer(glow ? RenderType.eyes(this.texture) : RenderType.entityTranslucent(this.texture));")
                .Line("int overlay = flashes(entity, this.index) ? LivingEntityRenderer.getOverlayCoords(entity, 0.0f) : OverlayTexture.NO_OVERLAY;")
                .Line($"this.getParentModel().renderToBuffer(poseStack, consumer, glow ? 15728880 : packedLight, overlay, {color});")
                .CloseBlock()
                .CloseBlock();
        }

        w.CloseBlock();
        string file = naming.SourcePath(naming.ClientPackage, className);
        context.Files.WriteText(file, w.ToString());
        context.SourceMap.Add(file, 1, w.NextLineNumber - 1, mob.SourceAssetId, $"Mob '{mob.Id}'");
    }

    /// <summary>
    /// When each layer shows, glows and flashes red: its box, or its function (the mob → yes / no);
    /// the layer of the saddle shows only once the mob is saddled.
    /// </summary>
    private static void WriteLayerRules(JavaSourceWriter w, string entityClass, IRMob mob, IReadOnlyList<(bool Glow, bool NoHurt, IRMobLayer? Mob, int MobIndex)> layers, Func<IRFunctionBinding?, string?> call)
    {
        void Rule(string name, Func<(bool Glow, bool NoHurt, IRMobLayer? Mob, int MobIndex), string> rule)
        {
            w.Line()
                .OpenBlock($"private static boolean {name}({entityClass} entity, int layer)")
                .OpenBlock("return switch (layer)");
            for (int i = 0; i < layers.Count; i++)
            {
                w.Line($"case {i} -> {rule(layers[i])};");
            }

            w.Line("default -> false;").CloseBlock(";").CloseBlock();
        }

        int saddle = mob.Riding is { NeedsSaddle: true } riding ? riding.SaddleLayer : -1;
        Rule("isVisible", l =>
        {
            string visible = call(l.Mob?.Visible) ?? "true";
            return l.MobIndex >= 0 && l.MobIndex == saddle ? $"entity.isSaddled() && {visible}" : visible;
        });
        Rule("glows", l => call(l.Mob?.GlowFunction) ?? (l.Glow ? "true" : "false"));
        Rule("flashes", l => call(l.Mob?.HurtFunction) ?? (l.NoHurt ? "false" : "true"));
    }
}
