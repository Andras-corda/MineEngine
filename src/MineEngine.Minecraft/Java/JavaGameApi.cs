using System.Globalization;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Différences de l'API Java de Minecraft selon la version (indépendantes du loader) :
/// builder de nourriture, signature de l'info-bulle, construction des blocs à expérience,
/// création des ResourceLocation, mise à feu et placement des apparitions.
/// </summary>
public abstract class JavaGameApi
{
    public static JavaGameApi For(MinecraftVersion version) =>
        version.IsAtLeast("1.20.5") ? new ModernJavaGameApi() : new LegacyJavaGameApi();

    /// <summary>Vrai si la viande est déclarée par le tag minecraft:meat (et non par le builder).</summary>
    public abstract bool MeatUsesItemTag { get; }

    /// <summary>True from 1.20.5: item data lives in data components and enchantments in a data-driven registry.</summary>
    public abstract bool UsesDataComponents { get; }

    /// <summary>Imports nécessaires à la redéfinition de l'info-bulle.</summary>
    public abstract IReadOnlyList<string> TooltipImports { get; }

    /// <summary>Expression Java "new FoodProperties.Builder()...build()".</summary>
    public abstract string FoodProperties(IRFood food);

    /// <summary>Signature de la méthode d'info-bulle d'un item.</summary>
    public abstract string TooltipMethodSignature { get; }

    /// <summary>Appel à la méthode parente de l'info-bulle.</summary>
    public abstract string TooltipSuperCall { get; }

    /// <summary>Début et fin de l'appel au constructeur d'un bloc à expérience, autour de ses propriétés.</summary>
    public abstract (string Head, string Tail) ExperienceBlockConstruction(int min, int max);

    /// <summary>Expression qui crée un ResourceLocation à partir d'un texte "espace:chemin".</summary>
    public abstract string ParseResourceLocation(string textExpression);

    /// <summary>Instruction qui enflamme l'entité courante pour un nombre de secondes.</summary>
    public abstract string IgniteSelf(int seconds);

    /// <summary>Copy of the properties of a vanilla block ("Blocks.WATER"): copy (1.20.1) or ofFullCopy (1.20.5).</summary>
    public abstract string CopyBlockProperties(string block);

    /// <summary>Fluid given to a liquid block or a bucket: the supplier (Forge 1.20.1) or the fluid itself (1.20.5).</summary>
    public abstract string FluidArgument(string holder);

    /// <summary>True from 1.21: enchantments are data (JSON) instead of Java classes.</summary>
    public abstract bool EnchantmentsAreData { get; }

    /// <summary>Line that adds an attribute modifier to a status effect (in its constructor).</summary>
    public abstract string EffectAttributeModifier(string attribute, string id, double amount, bool multiply);

    /// <summary>Overrides of a status effect that heals or hurts every <paramref name="interval"/> ticks.</summary>
    public abstract IReadOnlyList<string> EffectTick(int interval, IReadOnlyList<string> actions);

    /// <summary>True when EntityModel.renderToBuffer takes one packed color (1.21) instead of four floats.</summary>
    public abstract bool PackedRenderColor { get; }

    /// <summary>Override of defineSynchedData that gives an entity data its default value.</summary>
    public abstract (string Signature, IReadOnlyList<string> Body) DefineSynchedData(string accessor, string defaultValue);

    /// <summary>
    /// Expressions Java qui créent une flèche et une petite boule de feu tirées par un mob
    /// (Puppeteer) ; les variables level, mob, dx, dy et dz sont déjà définies.
    /// </summary>
    public abstract (string Arrow, string SmallFireball) ProjectileCreation { get; }

    /// <summary>Type de placement "au sol" des règles d'apparition, et son import.</summary>
    public abstract (string Expression, string Import) OnGroundSpawnPlacement { get; }

    // ----- Mobs (V0.7.9) -----

    /// <summary>Height where a rider sits: the method of the mob that gives it, its body and its imports.</summary>
    public abstract (string Signature, IReadOnlyList<string> Body, IReadOnlyList<string> Imports) PassengerAttachment(double height);

    /// <summary>Method of the mob that gives its collision box, and the call of the one of its parent class.</summary>
    public abstract (string Signature, string SuperCall) EntityDimensionsOverride { get; }

    /// <summary>The type of creature (undead, arthropod...) is a tag of entity types, not a method of the mob.</summary>
    public abstract bool CreatureTypeIsTag { get; }

    /// <summary>The step height is an attribute of the mob, not a field set in its constructor.</summary>
    public abstract bool StepHeightIsAttribute { get; }

    /// <summary>Method a raider must have (the bonus of the raid waves) and its imports.</summary>
    public abstract (string Signature, IReadOnlyList<string> Imports) RaidBuffs { get; }

    /// <summary>Goal of a tamed animal that follows its owner.</summary>
    public abstract string FollowOwnerGoal { get; }

    /// <summary>
    /// Construction d'un outil, d'une arme ou d'une armure : début de l'appel au constructeur
    /// (avant les propriétés), appels ajoutés aux propriétés, fin de l'appel. 1.20 passe les
    /// dégâts au constructeur ; 1.20.5 et plus les passent par les propriétés (attributes).
    /// </summary>
    /// <param name="customTier">Field of the material chosen in the editor (V0.7.12), or null for a material of the game.</param>
    public abstract (string Head, IReadOnlyList<string> PropertyCalls, string Tail) EquipmentConstruction(IRItem item, string? customTier);

    /// <summary>Method of a custom material that says which blocks it harvests, and its imports (V0.7.12).</summary>
    public abstract (string Method, IReadOnlyList<string> Imports) TierMiningLevel(ItemTier level);

    protected static string Float(float value) => JavaNames.FloatLiteral(value);

    protected static string Int(float value) => ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

    protected static string TierConstant(ItemTier tier) => "Tiers." + tier.ToString().ToUpperInvariant();

    protected static string ArmorMaterialConstant(ArmorMaterialKind material) => "ArmorMaterials." + material switch
    {
        ArmorMaterialKind.Chainmail => "CHAIN",
        _ => material.ToString().ToUpperInvariant(),
    };

    protected static string ArmorTypeConstant(ItemKind kind) => "ArmorItem.Type." + kind.ToString().ToUpperInvariant();

    /// <summary>Classe Java d'un outil ou d'une arme ("SwordItem").</summary>
    protected static string ToolClass(ItemKind kind) => kind + "Item";

    /// <summary>Rapidité et effets de la nourriture : appels identiques dans les deux API.</summary>
    protected static string FoodExtras(IRFood food) =>
        string.Concat(food.Effects.Select(e =>
            $".effect(new MobEffectInstance({GameDataJava.Effect(e.Effect)}, {e.DurationTicks}, {e.Amplifier}), {Float(e.Probability)})"));

    protected static string Experience(int min, int max) =>
        string.Create(CultureInfo.InvariantCulture, $"UniformInt.of({min}, {max})");
}

/// <summary>Minecraft 1.20 à 1.20.4.</summary>
public sealed class LegacyJavaGameApi : JavaGameApi
{
    public override bool MeatUsesItemTag => false;

    public override bool UsesDataComponents => false;

    public override IReadOnlyList<string> TooltipImports { get; } =
    [
        "java.util.List",
        "net.minecraft.network.chat.Component",
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.TooltipFlag",
        "net.minecraft.world.level.Level",
    ];

    public override string TooltipMethodSignature =>
        "public void appendHoverText(ItemStack stack, Level level, List<Component> tooltip, TooltipFlag flag)";

    public override string TooltipSuperCall => "super.appendHoverText(stack, level, tooltip, flag);";

    public override string FoodProperties(IRFood food) =>
        $"new FoodProperties.Builder().nutrition({food.Nutrition}).saturationMod({Float(food.Saturation)})"
        + (food.IsMeat ? ".meat()" : string.Empty)
        + (food.AlwaysEdible ? ".alwaysEat()" : string.Empty)
        + FoodExtras(food)
        + ".build()";

    public override (string Head, string Tail) ExperienceBlockConstruction(int min, int max) =>
        ("new DropExperienceBlock(", $", {Experience(min, max)})");

    public override string ParseResourceLocation(string textExpression) => $"new ResourceLocation({textExpression})";

    public override string IgniteSelf(int seconds) => $"this.setSecondsOnFire({seconds});";

    public override bool PackedRenderColor => false;

    public override string CopyBlockProperties(string block) => $"BlockBehaviour.Properties.copy({block})";

    public override string FluidArgument(string holder) => holder;

    public override bool EnchantmentsAreData => false;

    public override string EffectAttributeModifier(string attribute, string id, double amount, bool multiply) =>
        $"this.addAttributeModifier(Attributes.{attribute}, {JavaNames.StringLiteral(DeterministicUuid(id))}, {JavaNames.DoubleLiteral(amount)}, "
        + $"AttributeModifier.Operation.{(multiply ? "MULTIPLY_TOTAL" : "ADDITION")});";

    public override IReadOnlyList<string> EffectTick(int interval, IReadOnlyList<string> actions) =>
    [
        "@Override",
        "public boolean isDurationEffectTick(int duration, int amplifier) {",
        $"    return duration % {interval} == 0;",
        "}",
        string.Empty,
        "@Override",
        "public void applyEffectTick(LivingEntity entity, int amplifier) {",
        .. actions.Select(a => "    " + a),
        "}",
    ];

    /// <summary>The same identifier always gives the same UUID: the modifier keeps its UUID from one build to the next.</summary>
    private static string DeterministicUuid(string id)
    {
        byte[] hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(id));
        return new Guid(hash).ToString("D");
    }

    public override (string Signature, IReadOnlyList<string> Body) DefineSynchedData(string accessor, string defaultValue) =>
        ("protected void defineSynchedData()", ["super.defineSynchedData();", $"this.entityData.define({accessor}, {defaultValue});"]);

    public override (string Arrow, string SmallFireball) ProjectileCreation =>
        ("new Arrow(level, mob)", "new SmallFireball(level, mob, dx, dy, dz)");

    public override (string Signature, IReadOnlyList<string> Body, IReadOnlyList<string> Imports) PassengerAttachment(double height) =>
        ("public double getPassengersRidingOffset()", [$"return {JavaNames.DoubleLiteral(height)};"], []);

    public override (string Signature, string SuperCall) EntityDimensionsOverride =>
        ("public EntityDimensions getDimensions(Pose pose)", "super.getDimensions(pose)");

    public override bool CreatureTypeIsTag => false;

    public override bool StepHeightIsAttribute => false;

    public override (string Signature, IReadOnlyList<string> Imports) RaidBuffs => ("public void applyRaidBuffs(int wave, boolean unused)", []);

    public override string FollowOwnerGoal => "new FollowOwnerGoal(this, 1.0, 10.0F, 2.0F, false)";

    public override (string Expression, string Import) OnGroundSpawnPlacement =>
        ("SpawnPlacements.Type.ON_GROUND", "net.minecraft.world.entity.SpawnPlacements");

    public override (string Head, IReadOnlyList<string> PropertyCalls, string Tail) EquipmentConstruction(IRItem item, string? customTier)
    {
        if (item.Kind.IsArmor())
        {
            return ($"new ArmorItem({ArmorMaterialConstant(item.ArmorMaterial)}, {ArmorTypeConstant(item.Kind)}, ", [], ")");
        }

        // SwordItem, PickaxeItem et HoeItem prennent des dégâts entiers ; AxeItem et ShovelItem des décimaux.
        string damage = item.Kind is ItemKind.Axe or ItemKind.Shovel ? Float(item.AttackDamage) : Int(item.AttackDamage);
        return ($"new {ToolClass(item.Kind)}({customTier ?? TierConstant(item.Tier)}, {damage}, {Float(item.AttackSpeed)}, ", [], ")");
    }

    /// <summary>Levels of the game: wood and gold 0, stone 1, iron 2, diamond 3, netherite 4.</summary>
    public override (string Method, IReadOnlyList<string> Imports) TierMiningLevel(ItemTier level) =>
        ($"public int getLevel() {{ return {level switch { ItemTier.Stone => 1, ItemTier.Iron => 2, ItemTier.Diamond => 3, ItemTier.Netherite => 4, _ => 0 }}; }}", []);
}

/// <summary>Minecraft 1.20.5 et plus (dont 1.21.1).</summary>
public sealed class ModernJavaGameApi : JavaGameApi
{
    public override bool MeatUsesItemTag => true;

    public override bool UsesDataComponents => true;

    public override IReadOnlyList<string> TooltipImports { get; } =
    [
        "java.util.List",
        "net.minecraft.network.chat.Component",
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.TooltipFlag",
    ];

    public override string TooltipMethodSignature =>
        "public void appendHoverText(ItemStack stack, Item.TooltipContext context, List<Component> tooltip, TooltipFlag flag)";

    public override string TooltipSuperCall => "super.appendHoverText(stack, context, tooltip, flag);";

    public override string FoodProperties(IRFood food) =>
        $"new FoodProperties.Builder().nutrition({food.Nutrition}).saturationModifier({Float(food.Saturation)})"
        + (food.AlwaysEdible ? ".alwaysEdible()" : string.Empty)
        + FoodExtras(food)
        + ".build()";

    public override (string Head, string Tail) ExperienceBlockConstruction(int min, int max) =>
        ($"new DropExperienceBlock({Experience(min, max)}, ", ")");

    public override string ParseResourceLocation(string textExpression) => $"ResourceLocation.parse({textExpression})";

    public override string IgniteSelf(int seconds) => $"this.igniteForSeconds({seconds});";

    // Targets are 1.20.1 and 1.21.1: the packed color came with 1.21, after the data components of 1.20.5.
    public override bool PackedRenderColor => true;

    public override string CopyBlockProperties(string block) => $"BlockBehaviour.Properties.ofFullCopy({block})";

    public override string FluidArgument(string holder) => holder + ".get()";

    public override bool EnchantmentsAreData => true;

    public override string EffectAttributeModifier(string attribute, string id, double amount, bool multiply) =>
        $"this.addAttributeModifier(Attributes.{attribute}, {ParseResourceLocation(JavaNames.StringLiteral(id))}, {JavaNames.DoubleLiteral(amount)}, "
        + $"AttributeModifier.Operation.{(multiply ? "ADD_MULTIPLIED_TOTAL" : "ADD_VALUE")});";

    public override IReadOnlyList<string> EffectTick(int interval, IReadOnlyList<string> actions) =>
    [
        "@Override",
        "public boolean shouldApplyEffectTickThisTick(int duration, int amplifier) {",
        $"    return duration % {interval} == 0;",
        "}",
        string.Empty,
        "@Override",
        "public boolean applyEffectTick(LivingEntity entity, int amplifier) {",
        .. actions.Select(a => "    " + a),
        "    return true;",
        "}",
    ];

    public override (string Signature, IReadOnlyList<string> Body) DefineSynchedData(string accessor, string defaultValue) =>
        ("protected void defineSynchedData(SynchedEntityData.Builder builder)", ["super.defineSynchedData(builder);", $"builder.define({accessor}, {defaultValue});"]);

    public override (string Arrow, string SmallFireball) ProjectileCreation =>
        ("new Arrow(level, mob, new ItemStack(Items.ARROW), null)", "new SmallFireball(level, mob, new Vec3(dx, dy, dz).normalize())");

    public override (string Signature, IReadOnlyList<string> Body, IReadOnlyList<string> Imports) PassengerAttachment(double height) =>
        ("protected Vec3 getPassengerAttachmentPoint(Entity entity, EntityDimensions dimensions, float scale)",
            [$"return new Vec3(0.0, {JavaNames.DoubleLiteral(height)} * scale, 0.0);"],
            ["net.minecraft.world.entity.Entity", "net.minecraft.world.entity.EntityDimensions", "net.minecraft.world.phys.Vec3"]);

    public override (string Signature, string SuperCall) EntityDimensionsOverride =>
        ("public EntityDimensions getDefaultDimensions(Pose pose)", "super.getDefaultDimensions(pose)");

    public override bool CreatureTypeIsTag => true;

    public override bool StepHeightIsAttribute => true;

    public override (string Signature, IReadOnlyList<string> Imports) RaidBuffs =>
        ("public void applyRaidBuffs(ServerLevel level, int wave, boolean unused)", ["net.minecraft.server.level.ServerLevel"]);

    public override string FollowOwnerGoal => "new FollowOwnerGoal(this, 1.0, 10.0F, 2.0F)";

    public override (string Expression, string Import) OnGroundSpawnPlacement =>
        ("SpawnPlacementTypes.ON_GROUND", "net.minecraft.world.entity.SpawnPlacementTypes");

    public override (string Head, IReadOnlyList<string> PropertyCalls, string Tail) EquipmentConstruction(IRItem item, string? customTier)
    {
        if (item.Kind.IsArmor())
        {
            string type = ArmorTypeConstant(item.Kind);
            return ($"new ArmorItem({ArmorMaterialConstant(item.ArmorMaterial)}, {type}, ",
                [$".durability({type}.getDurability({ArmorDurabilityMultiplier(item.ArmorMaterial)}))"], ")");
        }

        string tier = customTier ?? TierConstant(item.Tier);
        string toolClass = ToolClass(item.Kind);
        string damage = item.Kind == ItemKind.Sword ? Int(item.AttackDamage) : Float(item.AttackDamage);
        return ($"new {toolClass}({tier}, ", [$".attributes({toolClass}.createAttributes({tier}, {damage}, {Float(item.AttackSpeed)}))"], ")");
    }

    /// <summary>The blocks a material cannot harvest are a tag of the game since 1.20.5.</summary>
    public override (string Method, IReadOnlyList<string> Imports) TierMiningLevel(ItemTier level) =>
        ($"public TagKey<Block> getIncorrectBlocksForDrops() {{ return BlockTags.INCORRECT_FOR_{level switch { ItemTier.Wood => "WOODEN", _ => level.ToString().ToUpperInvariant() }}_TOOL; }}",
            ["net.minecraft.tags.TagKey", "net.minecraft.tags.BlockTags", "net.minecraft.world.level.block.Block"]);

    /// <summary>Multiplicateur de durabilité des matériaux d'armure de Minecraft.</summary>
    private static int ArmorDurabilityMultiplier(ArmorMaterialKind material) => material switch
    {
        ArmorMaterialKind.Leather => 5,
        ArmorMaterialKind.Gold => 7,
        ArmorMaterialKind.Turtle => 25,
        ArmorMaterialKind.Diamond => 33,
        ArmorMaterialKind.Netherite => 37,
        _ => 15,
    };
}
