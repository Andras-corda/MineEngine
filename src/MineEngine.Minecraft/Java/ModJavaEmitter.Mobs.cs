using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Partie "sons et mobs" du code Java : registre des sons, registre des entités
/// (attributs, règles d'apparition), une classe par mob (IA, sons, reproduction) et
/// les moteurs de rendu côté client.
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>Imports du registre des entités.</summary>
    protected abstract IReadOnlyList<string> EntitiesRegistryImports { get; }

    /// <summary>Imports du registre des sons.</summary>
    protected abstract IReadOnlyList<string> SoundsRegistryImports { get; }

    /// <summary>Type Java du champ qui porte le type d'entité d'un mob.</summary>
    protected abstract string EntityTypeHolderType(string entityClass);

    /// <summary>Type Java des champs qui portent un son.</summary>
    protected abstract string SoundHolderType { get; }

    /// <summary>Nom complet de la classe des œufs d'apparition du loader.</summary>
    protected abstract string SpawnEggItemType { get; }

    /// <summary>Nom complet de l'événement de création des attributs.</summary>
    protected abstract string EntityAttributeEventType { get; }

    /// <summary>Nom complet de l'événement d'enregistrement des règles d'apparition.</summary>
    protected abstract string SpawnPlacementEventType { get; }

    /// <summary>Nom complet de l'événement de rendu des entités (client).</summary>
    protected abstract string EntityRenderersEventType { get; }

    /// <summary>Imports qui permettent de savoir si le code tourne sur le client.</summary>
    protected abstract IReadOnlyList<string> ClientEnvironmentImports { get; }

    /// <summary>Condition Java vraie seulement sur le client.</summary>
    protected virtual string ClientEnvironmentCheck => "FMLEnvironment.dist == Dist.CLIENT";

    protected abstract string EntityTypesRegisterDeclaration(JavaModNaming naming);

    protected abstract string SoundEventsRegisterDeclaration(JavaModNaming naming);

    // ----- Sons -----

    private JavaSourceWriter WriteSoundsClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        string file = naming.SourcePath(naming.RegistryPackage, naming.SoundsClass);
        var imports = new List<string>(SoundsRegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "net.minecraft.resources.ResourceLocation",
            "net.minecraft.sounds.SoundEvent",
        };

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Sons du mod.")
            .OpenBlock($"public final class {naming.SoundsClass}")
            .Line(SoundEventsRegisterDeclaration(naming));

        foreach (IRSound sound in context.Ir.Content.Sounds)
        {
            w.Line();
            int firstLine = w.NextLineNumber;
            string location = api.ParseResourceLocation(JavaNames.StringLiteral(context.Paths.Reference(sound.Id.Value)));
            w.Line($"public static final {SoundHolderType} {naming.ConstantFor(sound.Id)} = SOUND_EVENTS.register(")
                .Indent().Indent()
                .Line($"{JavaNames.StringLiteral(sound.Id.Value)}, () -> SoundEvent.createVariableRangeEvent({location}));")
                .Unindent().Unindent();
            context.SourceMap.Add(file, firstLine, w.NextLineNumber - 1, sound.SourceAssetId, $"Son '{sound.Id}'");
        }

        return WritePrivateConstructor(w, naming.SoundsClass);
    }

    // ----- Registre des entités -----

    private JavaSourceWriter WriteEntitiesClass(GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names)
    {
        IReadOnlyList<IRMob> mobs = context.Ir.Content.Mobs;
        List<IRMob> spawning = mobs.Where(m => m.Spawning is not null).ToList();
        string file = naming.SourcePath(naming.RegistryPackage, naming.EntitiesClass);

        var imports = new List<string>(EntitiesRegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            EntityAttributeEventType,
            "net.minecraft.world.entity.EntityType",
            "net.minecraft.world.entity.MobCategory",
        };
        imports.AddRange(mobs.Select(m => $"{naming.EntityPackage}.{naming.EntityClassFor(m.Id)}"));
        imports.AddRange(GolemImports(mobs));
        if (spawning.Count > 0)
        {
            imports.Add(SpawnPlacementEventType);
            imports.Add(api.OnGroundSpawnPlacement.Import);
            imports.Add("net.minecraft.world.level.levelgen.Heightmap");
            imports.AddRange(spawning.Select(m => MobJava.SpawnRuleImport(m.Kind)));
        }

        JavaSourceWriter w = WriteHeader(naming.RegistryPackage, imports, "Mobs du mod : types d'entités, attributs et règles d'apparition.")
            .OpenBlock($"public final class {naming.EntitiesClass}")
            .Line(EntityTypesRegisterDeclaration(naming));

        foreach (IRMob mob in mobs)
        {
            string entityClass = naming.EntityClassFor(mob.Id);
            string id = JavaNames.StringLiteral(mob.Id.Value);
            w.Line();
            int firstLine = w.NextLineNumber;
            w.Line($"public static final {EntityTypeHolderType(entityClass)} {naming.ConstantFor(mob.Id)} = ENTITY_TYPES.register(")
                .Indent().Indent()
                .Line($"{id}, () -> EntityType.Builder.of({entityClass}::new, {MobJava.Category(mob.Kind)})")
                .Indent().Indent()
                .Line($".sized({JavaNames.FloatLiteral(mob.Width)}, {JavaNames.FloatLiteral(mob.Height)})")
                .Line($".clientTrackingRange({mob.TrackingRange})");
            if (mob.FireImmune)
            {
                w.Line(".fireImmune()");
            }

            w.Line($".build({id}));")
                .Unindent().Unindent().Unindent().Unindent();
            context.SourceMap.Add(file, firstLine, w.NextLineNumber - 1, mob.SourceAssetId, $"Mob '{mob.Id}'");
        }

        w.Line().OpenBlock($"public static void registerAttributes({SimpleName(EntityAttributeEventType)} event)");
        foreach (IRMob mob in mobs)
        {
            w.Line($"event.put({naming.ConstantFor(mob.Id)}.get(), {naming.EntityClassFor(mob.Id)}.createAttributes().build());");
        }

        w.CloseBlock();

        WriteGolems(w, context, naming, api, names, mobs);

        if (spawning.Count > 0)
        {
            string eventClass = SimpleName(SpawnPlacementEventType);
            w.Line().OpenBlock($"public static void registerSpawnPlacements({eventClass} event)");
            foreach (IRMob mob in spawning)
            {
                // A mob limited to structures has its own rule (the one of its family, inside a structure).
                string rule = mob.Spawning!.Structures.Count > 0 ? $"{naming.EntityClassFor(mob.Id)}::checkSpawnRules" : MobJava.SpawnRule(mob.Kind);
                w.Line($"event.register({naming.ConstantFor(mob.Id)}.get(), {api.OnGroundSpawnPlacement.Expression},")
                    .Indent().Indent()
                    .Line($"Heightmap.Types.MOTION_BLOCKING_NO_LEAVES, {rule}, {eventClass}.Operation.REPLACE);")
                    .Unindent().Unindent();
            }

            w.CloseBlock();
        }

        return WritePrivateConstructor(w, naming.EntitiesClass);
    }

    // ----- Classe d'un mob -----

    private void WriteEntityClass(GenerationContext context, JavaModNaming naming, JavaGameApi api, JavaProgramNames names, IRMob mob)
    {
        string className = naming.EntityClassFor(mob.Id);
        bool tamable = mob.Taming is not null;
        string baseClass = MobJava.BaseClass(mob.Kind, tamable);
        bool isAnimal = mob.Kind == MobKind.Animal;
        bool hasStructures = mob.Spawning is { Structures.Count: > 0 };
        List<(IRMobGoal Goal, string Expression)> goals = [];
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            MobJava.BaseClassImport(mob.Kind, tamable),
            "net.minecraft.world.entity.EntityType",
            "net.minecraft.world.entity.ai.attributes.AttributeSupplier",
            "net.minecraft.world.entity.ai.attributes.Attributes",
            "net.minecraft.world.level.Level",
        };

        if (mob.Kind != MobKind.Monster)
        {
            imports.Add("net.minecraft.world.entity.Mob");
        }

        if (mob.Kind == MobKind.Raider)
        {
            imports.Add("net.minecraft.world.entity.monster.Monster");
        }

        imports.UnionWith(EntitySectionImports(mob, api, hasStructures));
        imports.UnionWith(MobEvents(context, naming, mob).Imports);
        imports.UnionWith(VehicleImports(mob, naming));

        string? treeClass = mob.BehaviorTree is { } tree ? naming.BehaviorTreeClassFor(tree) : null;
        if (treeClass is not null)
        {
            imports.Add("net.minecraft.world.entity.ai.goal.FloatGoal");
            imports.Add($"{naming.AiPackage}.{PuppeteerRuntimeJava.ClassName}");
            imports.Add($"{naming.AiPackage}.{treeClass}");
        }

        foreach (IRMobGoal goal in mob.Goals)
        {
            (string expression, IReadOnlyList<string> goalImports) = MobJava.Goal(goal);
            goals.Add((goal, expression));
            imports.UnionWith(goalImports);
        }

        // Textures: the random variants of the model and the variations of the mob (chosen at spawn),
        // then the appearances (set by the graphs).
        int variants = VariantCount(context, mob) + mob.Variations.Count;
        int appearances = mob.Appearances.Count;
        int textures = variants + appearances;
        IReadOnlyList<MobSynchedValue> synched = SynchedValues(mob, textures);
        if (synched.Count > 0)
        {
            imports.Add("net.minecraft.nbt.CompoundTag");
            imports.Add("net.minecraft.network.syncher.EntityDataAccessor");
            imports.Add("net.minecraft.network.syncher.EntityDataSerializers");
            imports.Add("net.minecraft.network.syncher.SynchedEntityData");
        }

        bool usesItems = mob.Goals.Any(g => g.Item is not null) || (isAnimal && mob.BreedingItem is not null) || mob.Taming is { Count: > 0 };
        bool usesSounds = mob.AmbientSound is not null || mob.HurtSound is not null || mob.DeathSound is not null;
        if (usesItems || usesSounds)
        {
            imports.Add("net.minecraft.core.registries.BuiltInRegistries");
            imports.Add("net.minecraft.resources.ResourceLocation");
        }

        if (usesItems)
        {
            imports.Add("net.minecraft.world.item.Item");
        }

        if (usesSounds)
        {
            imports.Add("net.minecraft.sounds.SoundEvent");
        }

        if (mob.HurtSound is not null)
        {
            imports.Add("net.minecraft.world.damagesource.DamageSource");
        }

        if (isAnimal)
        {
            imports.Add($"{naming.RegistryPackage}.{naming.EntitiesClass}");
            imports.Add("net.minecraft.server.level.ServerLevel");
            imports.Add("net.minecraft.world.entity.AgeableMob");
            imports.Add("net.minecraft.world.item.ItemStack");
        }

        JavaSourceWriter w = WriteHeader(naming.EntityPackage, imports, $"Mob « {mob.DisplayName} » : attributs, IA et sons.")
            .OpenBlock($"public class {className} extends {baseClass}" + (appearances > 0 ? $" implements {AppearanceInterface}" : string.Empty));
        foreach (MobSynchedValue value in synched)
        {
            w.Line($"private static final EntityDataAccessor<{value.JavaType}> {value.Accessor} = SynchedEntityData.defineId({className}.class, EntityDataSerializers.{value.Serializer});");
        }

        if (synched.Count > 0)
        {
            w.Line();
        }

        w.OpenBlock($"public {className}(EntityType<? extends {baseClass}> type, Level level)")
            .Line("super(type, level);")
            .Line($"this.xpReward = {mob.Experience};");
        if (!api.StepHeightIsAttribute && Math.Abs(mob.StepHeight - 0.6) > 1e-9)
        {
            w.Line($"this.setMaxUpStep({JavaNames.FloatLiteral((float)mob.StepHeight)});");
        }
        if (variants > 1)
        {
            // Each new mob takes a texture of its model at random; a saved mob keeps its own (read after the constructor).
            w.OpenBlock("if (!level.isClientSide)")
                .Line($"this.setVariant(this.random.nextInt({variants}));")
                .CloseBlock();
        }

        w.CloseBlock();
        WriteSynchedMembers(w, api, synched);

        if (appearances > 0)
        {
            w.Line()
                .Line("@Override")
                .OpenBlock("public int getAppearance()")
                .Line($"return this.getVariant() < {variants} ? 0 : this.getVariant() - {variants} + 1;")
                .CloseBlock()
                .Line()
                .Line("@Override")
                .OpenBlock("public void setAppearance(int appearance)")
                .Line($"this.setVariant(appearance <= 0 || appearance > {appearances} ? 0 : {variants} + appearance - 1);")
                .CloseBlock();
        }

        // Attributs
        w.Line()
            .OpenBlock("public static AttributeSupplier.Builder createAttributes()")
            .Line($"return {MobJava.AttributesStart(mob.Kind)}")
            .Indent().Indent()
            .Line($".add(Attributes.MAX_HEALTH, {MobJava.Double(mob.Health)})")
            .Line($".add(Attributes.ARMOR, {MobJava.Double(mob.Armor)})")
            .Line($".add(Attributes.MOVEMENT_SPEED, {MobJava.Double(mob.MovementSpeed)})")
            .Line($".add(Attributes.ATTACK_DAMAGE, {MobJava.Double(mob.AttackDamage)})")
            .Line($".add(Attributes.FOLLOW_RANGE, {MobJava.Double(mob.FollowRange)})")
            .Line($".add(Attributes.KNOCKBACK_RESISTANCE, {MobJava.Double(mob.KnockbackResistance)})");
        if (mob.Knockback > 0)
        {
            w.Line($".add(Attributes.ATTACK_KNOCKBACK, {MobJava.Double(mob.Knockback)})");
        }

        if (api.StepHeightIsAttribute)
        {
            w.Line($".add(Attributes.STEP_HEIGHT, {MobJava.Double(mob.StepHeight)})");
        }

        w.Line(";")
            .Unindent().Unindent()
            .CloseBlock();

        // IA : un arbre Puppeteer (le mob nage toujours), ou la liste de comportements, dont
        // l'ordre donne la priorité, séparément pour les actions et les cibles.
        w.Line().Line("@Override").OpenBlock("protected void registerGoals()");
        if (treeClass is not null)
        {
            w.Line("this.goalSelector.addGoal(0, new FloatGoal(this));")
                .Line($"this.goalSelector.addGoal(1, new {PuppeteerRuntimeJava.ClassName}.TreeGoal(this, {treeClass}.build()));");
        }

        int goalPriority = 0;
        int targetPriority = 0;
        if (tamable)
        {
            w.Line($"this.goalSelector.addGoal({++goalPriority + (treeClass is null ? 0 : 1)}, new SitWhenOrderedToGoal(this));");
        }

        foreach ((IRMobGoal goal, string expression) in goals)
        {
            bool isTarget = goal.Kind is MobGoalKind.HurtByTarget or MobGoalKind.AttackNearest;
            string selector = isTarget ? "targetSelector" : "goalSelector";
            int priority = isTarget ? ++targetPriority : ++goalPriority;
            w.Line($"this.{selector}.addGoal({priority}, {expression});");
        }

        if (tamable)
        {
            w.Line($"this.goalSelector.addGoal({++goalPriority + (treeClass is null ? 0 : 1)}, {api.FollowOwnerGoal});");
        }

        w.CloseBlock();

        WriteSoundOverride(w, "protected SoundEvent getAmbientSound()", mob.AmbientSound);
        WriteSoundOverride(w, "protected SoundEvent getHurtSound(DamageSource source)", mob.HurtSound);
        WriteSoundOverride(w, "protected SoundEvent getDeathSound()", mob.DeathSound);

        string? despawn = FunctionCall(context, names, mob.DespawnFunction, "this.level()", "this");
        if (mob.Persistent || despawn is not null)
        {
            w.Line().Line("@Override")
                .OpenBlock("public boolean removeWhenFarAway(double distance)")
                .Line(mob.Persistent ? "return false;" : $"return {despawn};")
                .CloseBlock();
        }

        bool burnsAtNight = mob.Vulnerabilities.Contains(MobVulnerability.Moon);
        if (mob.BurnsInDaylight || burnsAtNight)
        {
            w.Line().Line("@Override").OpenBlock("public void aiStep()");
            if (mob.BurnsInDaylight)
            {
                w.OpenBlock("if (this.isAlive() && this.isSunBurnTick())")
                    .Line(api.IgniteSelf(8))
                    .CloseBlock();
            }

            if (burnsAtNight)
            {
                // Under the sky at night, the moon hurts it every second.
                w.OpenBlock("if (this.isAlive() && !this.level().isClientSide && this.level().isNight() && this.level().canSeeSky(this.blockPosition()) && this.tickCount % 20 == 0)")
                    .Line("this.hurt(this.damageSources().magic(), 1.0F);")
                    .CloseBlock();
            }

            w.Line("super.aiStep();").CloseBlock();
        }

        WriteEntitySections(w, context, naming, api, names, mob, className);

        if (isAnimal)
        {
            string food = mob.BreedingItem is { } breeding
                ? $"return stack.is(item({JavaNames.StringLiteral(breeding.ToString())}));"
                : "return false;";
            w.Line().Line("@Override")
                .OpenBlock("public boolean isFood(ItemStack stack)")
                .Line(food)
                .CloseBlock()
                .Line()
                .Line("@Override")
                .OpenBlock("public AgeableMob getBreedOffspring(ServerLevel level, AgeableMob partner)")
                .Line($"return {naming.EntitiesClass}.{naming.ConstantFor(mob.Id)}.get().create(level);")
                .CloseBlock();
        }

        // Items et sons sont retrouvés par identifiant : même code pour le jeu et pour le mod.
        if (usesItems)
        {
            w.Line()
                .OpenBlock("private static Item item(String id)")
                .Line($"return BuiltInRegistries.ITEM.get({api.ParseResourceLocation("id")});")
                .CloseBlock();
        }

        if (usesSounds)
        {
            w.Line()
                .OpenBlock("private static SoundEvent sound(String id)")
                .Line($"return BuiltInRegistries.SOUND_EVENT.get({api.ParseResourceLocation("id")});")
                .CloseBlock();
        }

        w.CloseBlock();

        string path = naming.SourcePath(naming.EntityPackage, className);
        context.Files.WriteText(path, w.ToString());
        context.SourceMap.Add(path, 1, w.NextLineNumber - 1, mob.SourceAssetId, $"Mob '{mob.Id}'");
    }

    private static void WriteSoundOverride(JavaSourceWriter w, string signature, NamespacedId? sound)
    {
        if (sound is null)
        {
            return;
        }

        w.Line().Line("@Override")
            .OpenBlock(signature)
            .Line($"return sound({JavaNames.StringLiteral(sound.ToString())});")
            .CloseBlock();
    }

    // ----- Rendu (client) -----

    /// <summary>Registration of the renderer of each mob (one class each) and of the Blockbench models.</summary>
    private JavaSourceWriter WriteRenderersClass(GenerationContext context, JavaModNaming naming)
    {
        IReadOnlyList<IRMob> mobs = context.Ir.Content.Mobs;
        List<IRMob> custom = [.. CustomModelMobs(context)];
        var imports = new List<string> { EntityRenderersEventType, $"{naming.RegistryPackage}.{naming.EntitiesClass}" };
        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, "Affichage des mobs (chargé seulement par le client).")
            .OpenBlock($"public final class {naming.EntityRenderersClass}")
            .OpenBlock($"public static void registerRenderers({SimpleName(EntityRenderersEventType)}.RegisterRenderers event)");
        foreach (IRMob mob in mobs)
        {
            w.Line($"event.registerEntityRenderer({naming.EntitiesClass}.{naming.ConstantFor(mob.Id)}.get(), {naming.RendererClassFor(mob.Id)}::new);");
        }

        w.CloseBlock();

        // The Java entity models made from Blockbench models are declared once each.
        if (custom.Count > 0)
        {
            w.Line().OpenBlock($"public static void registerLayerDefinitions({SimpleName(EntityRenderersEventType)}.RegisterLayerDefinitions event)");
            foreach (ResourceId model in custom.Select(m => m.CustomModel!).Distinct())
            {
                string modelClass = $"{naming.EntityModelPackage}.{naming.EntityModelClassFor(model)}";
                w.Line($"event.registerLayerDefinition({modelClass}.LAYER_LOCATION, {modelClass}::createBodyLayer);");
            }

            w.CloseBlock();
        }

        return WritePrivateConstructor(w, naming.EntityRenderersClass);
    }

    // ----- Mobs with a 3D model (Blockbench) -----

    private static IEnumerable<IRMob> CustomModelMobs(GenerationContext context) =>
        context.Ir.Content.Mobs.Where(m => m.CustomModel is not null && FindModel(context, m)?.Blockbench is not null);

    private static IRModel? FindModel(GenerationContext context, IRMob mob) =>
        mob.CustomModel is { } id ? context.Ir.Content.Models.FirstOrDefault(m => m.Id == id) : null;

    /// <summary>Number of textures a mob can take: its model texture and the variants of the model.</summary>
    private static int VariantCount(GenerationContext context, IRMob mob) =>
        FindModel(context, mob) is { Blockbench: not null } model ? 1 + model.Variants.Count : 1;

    /// <summary>One Java entity model per Blockbench model used by a mob, and one renderer per such mob.</summary>
    private static void WriteCustomMobModels(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        List<IRMob> mobs = [.. CustomModelMobs(context)];
        foreach (IRModel model in mobs.Select(m => FindModel(context, m)!).DistinctBy(m => m.Id))
        {
            string className = naming.EntityModelClassFor(model.Id);
            string layer = api.ParseResourceLocation(JavaNames.StringLiteral(context.Paths.Reference(model.Id.Value)));
            JavaSourceWriter w = WriteHeader(naming.EntityModelPackage, EntityModelJava.Imports, $"Modèle 3D « {model.Id} » (Blockbench), pour les mobs.");
            new EntityModelJava(model.Blockbench!).WriteClass(w, className, layer, api.PackedRenderColor);
            string path = naming.SourcePath(naming.EntityModelPackage, className);
            context.Files.WriteText(path, w.ToString());
            context.SourceMap.Add(path, 1, w.NextLineNumber - 1, model.SourceAssetId, $"Modèle '{model.Id}'");
        }
    }

    /// <summary>Texture paths of the appearances of a mob; one without texture takes <paramref name="baseTexture"/>.</summary>
    private static IEnumerable<string> AppearanceTextures(IRMob mob, string baseTexture) =>
        mob.Appearances.Select((a, i) => a.Texture is null ? baseTexture : $"textures/entity/{IRAppearance.FileId(mob.Id, i + 1)}.png");
}
