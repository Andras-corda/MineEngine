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

    private JavaSourceWriter WriteEntitiesClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
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
                .Line($".sized({JavaNames.FloatLiteral(mob.Width)}, {JavaNames.FloatLiteral(mob.Height)})");
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

        if (spawning.Count > 0)
        {
            string eventClass = SimpleName(SpawnPlacementEventType);
            w.Line().OpenBlock($"public static void registerSpawnPlacements({eventClass} event)");
            foreach (IRMob mob in spawning)
            {
                w.Line($"event.register({naming.ConstantFor(mob.Id)}.get(), {api.OnGroundSpawnPlacement.Expression},")
                    .Indent().Indent()
                    .Line($"Heightmap.Types.MOTION_BLOCKING_NO_LEAVES, {MobJava.SpawnRule(mob.Kind)}, {eventClass}.Operation.REPLACE);")
                    .Unindent().Unindent();
            }

            w.CloseBlock();
        }

        return WritePrivateConstructor(w, naming.EntitiesClass);
    }

    // ----- Classe d'un mob -----

    private static void WriteEntityClass(GenerationContext context, JavaModNaming naming, JavaGameApi api, IRMob mob)
    {
        string className = naming.EntityClassFor(mob.Id);
        string baseClass = MobJava.BaseClass(mob.Kind);
        bool isAnimal = mob.Kind == MobKind.Animal;
        List<(IRMobGoal Goal, string Expression)> goals = [];
        var imports = new SortedSet<string>(StringComparer.Ordinal)
        {
            MobJava.BaseClassImport(mob.Kind),
            "net.minecraft.world.entity.EntityType",
            "net.minecraft.world.entity.ai.attributes.AttributeSupplier",
            "net.minecraft.world.entity.ai.attributes.Attributes",
            "net.minecraft.world.level.Level",
        };

        if (mob.Kind != MobKind.Monster)
        {
            imports.Add("net.minecraft.world.entity.Mob");
        }

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

        int variants = VariantCount(context, mob);
        if (variants > 1)
        {
            imports.Add("net.minecraft.nbt.CompoundTag");
            imports.Add("net.minecraft.network.syncher.EntityDataAccessor");
            imports.Add("net.minecraft.network.syncher.EntityDataSerializers");
            imports.Add("net.minecraft.network.syncher.SynchedEntityData");
        }

        bool usesItems = mob.Goals.Any(g => g.Item is not null) || (isAnimal && mob.BreedingItem is not null);
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
            .OpenBlock($"public class {className} extends {baseClass}");
        if (variants > 1)
        {
            w.Line($"private static final EntityDataAccessor<Integer> VARIANT = SynchedEntityData.defineId({className}.class, EntityDataSerializers.INT);")
                .Line();
        }

        w.OpenBlock($"public {className}(EntityType<? extends {baseClass}> type, Level level)")
            .Line("super(type, level);")
            .Line($"this.xpReward = {mob.Experience};");
        if (variants > 1)
        {
            // Each new mob takes a texture of its model at random; a saved mob keeps its own (read after the constructor).
            w.OpenBlock("if (!level.isClientSide)")
                .Line($"this.setVariant(this.random.nextInt({variants}));")
                .CloseBlock();
        }

        w.CloseBlock();
        if (variants > 1)
        {
            WriteVariantMembers(w, api);
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
            .Line($".add(Attributes.KNOCKBACK_RESISTANCE, {MobJava.Double(mob.KnockbackResistance)});")
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
        foreach ((IRMobGoal goal, string expression) in goals)
        {
            bool isTarget = goal.Kind is MobGoalKind.HurtByTarget or MobGoalKind.AttackNearest;
            string selector = isTarget ? "targetSelector" : "goalSelector";
            int priority = isTarget ? ++targetPriority : ++goalPriority;
            w.Line($"this.{selector}.addGoal({priority}, {expression});");
        }

        w.CloseBlock();

        WriteSoundOverride(w, "protected SoundEvent getAmbientSound()", mob.AmbientSound);
        WriteSoundOverride(w, "protected SoundEvent getHurtSound(DamageSource source)", mob.HurtSound);
        WriteSoundOverride(w, "protected SoundEvent getDeathSound()", mob.DeathSound);

        if (mob.Persistent)
        {
            w.Line().Line("@Override")
                .OpenBlock("public boolean removeWhenFarAway(double distance)")
                .Line("return false;")
                .CloseBlock();
        }

        if (mob.BurnsInDaylight)
        {
            w.Line().Line("@Override")
                .OpenBlock("public void aiStep()")
                .OpenBlock("if (this.isAlive() && this.isSunBurnTick())")
                .Line(api.IgniteSelf(8))
                .CloseBlock()
                .Line("super.aiStep();")
                .CloseBlock();
        }

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

    private JavaSourceWriter WriteRenderersClass(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        IReadOnlyList<IRMob> mobs = context.Ir.Content.Mobs;
        var imports = new List<string>
        {
            EntityRenderersEventType,
            $"{naming.RegistryPackage}.{naming.EntitiesClass}",
            "net.minecraft.client.model.geom.ModelLayers",
            "net.minecraft.client.renderer.entity.MobRenderer",
            "net.minecraft.resources.ResourceLocation",
        };
        foreach (IRMob mob in mobs.Where(m => m.CustomModel is null))
        {
            imports.Add($"{naming.EntityPackage}.{naming.EntityClassFor(mob.Id)}");
            imports.Add("net.minecraft.client.model." + MobJava.Model(mob.Model).ModelClass);
        }

        List<IRMob> custom = [.. CustomModelMobs(context)];

        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, "Affichage des mobs (chargé seulement par le client).")
            .OpenBlock($"public final class {naming.EntityRenderersClass}");

        foreach (IRMob mob in mobs.Where(m => m.CustomModel is null))
        {
            string texture = mob.Texture is null
                ? "minecraft:" + MobJava.Model(mob.Model).VanillaTexture
                : context.Paths.Reference($"textures/entity/{mob.Id}.png");
            w.Line($"private static final ResourceLocation {TextureConstant(naming, mob.Id)} =")
                .Indent().Indent()
                .Line(api.ParseResourceLocation(JavaNames.StringLiteral(texture)) + ";")
                .Unindent().Unindent();
        }

        w.Line().OpenBlock($"public static void registerRenderers({SimpleName(EntityRenderersEventType)}.RegisterRenderers event)");
        foreach (IRMob mob in custom)
        {
            w.Line($"event.registerEntityRenderer({naming.EntitiesClass}.{naming.ConstantFor(mob.Id)}.get(), {naming.ClientPackage}.{naming.RendererClassFor(mob.Id)}::new);");
        }

        List<IRMob> vanilla = [.. mobs.Where(m => m.CustomModel is null)];
        for (int i = 0; i < vanilla.Count; i++)
        {
            IRMob mob = vanilla[i];
            MobModelJava model = MobJava.Model(mob.Model);
            string entityClass = naming.EntityClassFor(mob.Id);
            if (i > 0 || custom.Count > 0)
            {
                w.Line();
            }

            w.Line($"event.registerEntityRenderer({naming.EntitiesClass}.{naming.ConstantFor(mob.Id)}.get(), context -> new MobRenderer<{entityClass}, {model.ModelClass}<{entityClass}>>(")
                .Indent().Indent()
                .Line($"context, new {model.ModelClass}<>(context.bakeLayer(ModelLayers.{model.Layer})), {JavaNames.FloatLiteral(model.ShadowRadius)}) {{")
                .Unindent()
                .Line("@Override")
                .OpenBlock($"public ResourceLocation getTextureLocation({entityClass} entity)")
                .Line($"return {TextureConstant(naming, mob.Id)};")
                .CloseBlock()
                .Unindent()
                .Line("});");
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

    /// <summary>The variant is synchronized with the clients (for the texture) and saved with the mob.</summary>
    private static void WriteVariantMembers(JavaSourceWriter w, JavaGameApi api)
    {
        (string signature, IReadOnlyList<string> body) = api.DefineSynchedData("VARIANT", "0");
        w.Line().Line("@Override").OpenBlock(signature);
        foreach (string line in body)
        {
            w.Line(line);
        }

        w.CloseBlock()
            .Line()
            .OpenBlock("public int getVariant()")
            .Line("return this.entityData.get(VARIANT);")
            .CloseBlock()
            .Line()
            .OpenBlock("public void setVariant(int variant)")
            .Line("this.entityData.set(VARIANT, variant);")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public void addAdditionalSaveData(CompoundTag tag)")
            .Line("super.addAdditionalSaveData(tag);")
            .Line("tag.putInt(\"Variant\", this.getVariant());")
            .CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock("public void readAdditionalSaveData(CompoundTag tag)")
            .Line("super.readAdditionalSaveData(tag);")
            .OpenBlock("if (tag.contains(\"Variant\"))")
            .Line("this.setVariant(tag.getInt(\"Variant\"));")
            .CloseBlock()
            .CloseBlock();
    }

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

        foreach (IRMob mob in mobs)
        {
            WriteCustomRenderer(context, naming, api, mob, FindModel(context, mob)!);
        }
    }

    /// <summary>
    /// Renderer of a mob shown with its Blockbench model: the texture of the model (or the texture of the
    /// mob), its variants chosen by the mob, and its layers drawn over it (an emissive layer glows).
    /// </summary>
    private static void WriteCustomRenderer(GenerationContext context, JavaModNaming naming, JavaGameApi api, IRMob mob, IRModel model)
    {
        ResourcePaths paths = context.Paths;
        string className = naming.RendererClassFor(mob.Id);
        string entityClass = naming.EntityClassFor(mob.Id);
        string modelClass = naming.EntityModelClassFor(model.Id);
        string modelType = $"{modelClass}<{entityClass}>";
        string Location(string path) => api.ParseResourceLocation(JavaNames.StringLiteral(paths.Reference(path)));

        string baseTexture = mob.Texture is not null || model.Textures.Count == 0
            ? $"textures/entity/{mob.Id}.png"
            : $"textures/block/model/{model.Id}_{model.Textures[0].Key}.png";
        List<string> textures = [Location(baseTexture), .. Enumerable.Range(1, model.Variants.Count).Select(i => Location($"textures/entity/model/{model.Id}_variant_{i}.png"))];

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
            "net.minecraft.resources.ResourceLocation",
            $"{naming.EntityModelPackage}.{modelClass}",
            $"{naming.EntityPackage}.{entityClass}",
        };

        float shadow = Math.Clamp(mob.Width * 0.5f, 0.2f, 2f);
        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports, $"Affichage du mob « {mob.DisplayName} » avec son modèle 3D.")
            .OpenBlock($"public class {className} extends MobRenderer<{entityClass}, {modelType}>")
            .Line("private static final ResourceLocation[] TEXTURES = {")
            .Indent();
        foreach (string texture in textures)
        {
            w.Line(texture + ",");
        }

        w.Unindent().Line("};");
        for (int i = 0; i < model.Layers.Count; i++)
        {
            w.Line($"private static final ResourceLocation LAYER_{i + 1} = {Location($"textures/entity/model/{model.Id}_layer_{i + 1}.png")};");
        }

        w.Line()
            .OpenBlock($"public {className}(EntityRendererProvider.Context context)")
            .Line($"super(context, new {modelClass}<>(context.bakeLayer({modelClass}.LAYER_LOCATION)), {JavaNames.FloatLiteral(shadow)});");
        for (int i = 0; i < model.Layers.Count; i++)
        {
            w.Line($"this.addLayer(new TextureLayer(this, LAYER_{i + 1}, {(model.Layers[i].Emissive ? "true" : "false")}));");
        }

        w.CloseBlock()
            .Line()
            .Line("@Override")
            .OpenBlock($"public ResourceLocation getTextureLocation({entityClass} entity)")
            .Line(textures.Count > 1 ? "return TEXTURES[Math.floorMod(entity.getVariant(), TEXTURES.length)];" : "return TEXTURES[0];")
            .CloseBlock();

        if (model.Layers.Count > 0)
        {
            // Same model, other texture; an emissive layer ignores the light, like the eyes of a spider.
            string color = api.PackedRenderColor ? "-1" : "1.0f, 1.0f, 1.0f, 1.0f";
            w.Line()
                .OpenBlock($"private static final class TextureLayer extends RenderLayer<{entityClass}, {modelType}>")
                .Line("private final ResourceLocation texture;")
                .Line("private final boolean emissive;")
                .Line()
                .OpenBlock($"TextureLayer(RenderLayerParent<{entityClass}, {modelType}> parent, ResourceLocation texture, boolean emissive)")
                .Line("super(parent);")
                .Line("this.texture = texture;")
                .Line("this.emissive = emissive;")
                .CloseBlock()
                .Line()
                .Line("@Override")
                .OpenBlock($"public void render(PoseStack poseStack, MultiBufferSource buffers, int packedLight, {entityClass} entity, float limbSwing, float limbSwingAmount, float partialTick, float ageInTicks, float netHeadYaw, float headPitch)")
                .Line("VertexConsumer consumer = buffers.getBuffer(this.emissive ? RenderType.eyes(this.texture) : RenderType.entityCutoutNoCull(this.texture));")
                .Line($"this.getParentModel().renderToBuffer(poseStack, consumer, this.emissive ? 15728880 : packedLight, LivingEntityRenderer.getOverlayCoords(entity, 0.0f), {color});")
                .CloseBlock()
                .CloseBlock();
        }

        w.CloseBlock();
        string file = naming.SourcePath(naming.ClientPackage, className);
        context.Files.WriteText(file, w.ToString());
        context.SourceMap.Add(file, 1, w.NextLineNumber - 1, mob.SourceAssetId, $"Mob '{mob.Id}'");
    }

    private static string TextureConstant(JavaModNaming naming, ResourceId id) => naming.ConstantFor(id) + "_TEXTURE";
}
