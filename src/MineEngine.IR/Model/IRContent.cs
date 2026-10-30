using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Contenu déclaratif du mod : ce qui existe dans le jeu.</summary>
public sealed class IRContent
{
    public IRContent(
        IEnumerable<IRItem> items,
        IEnumerable<IRBlock> blocks,
        IEnumerable<IRMob>? mobs = null,
        IEnumerable<IRRecipe>? recipes = null,
        IEnumerable<IRSound>? sounds = null,
        IEnumerable<IRBehavior>? behaviors = null)
    {
        Items = items.OrderBy(i => i.Id).ToList();
        Blocks = blocks.OrderBy(b => b.Id).ToList();
        Mobs = (mobs ?? []).OrderBy(m => m.Id).ToList();
        Recipes = (recipes ?? []).OrderBy(r => r.Id).ToList();
        Sounds = (sounds ?? []).OrderBy(s => s.Id).ToList();
        Behaviors = (behaviors ?? []).OrderBy(b => b.Id).ToList();
    }

    public IReadOnlyList<IRItem> Items { get; }

    public IReadOnlyList<IRBlock> Blocks { get; }

    public IReadOnlyList<IRMob> Mobs { get; }

    public IReadOnlyList<IRRecipe> Recipes { get; }

    public IReadOnlyList<IRSound> Sounds { get; }

    /// <summary>Comportements issus des graphes nodaux (seulement ceux qui réagissent à au moins un événement).</summary>
    public IReadOnlyList<IRBehavior> Behaviors { get; }

    /// <summary>Fonctions, procédures, classes, structures, interfaces et énumérations des graphes.</summary>
    public IReadOnlyList<IRDefinition> Definitions { get; init; } = [];

    /// <summary>Minecraft Java JSON models copied into the mod (models/custom).</summary>
    public IReadOnlyList<IRModel> Models { get; init; } = [];

    /// <summary>Arbres de comportement Puppeteer.</summary>
    public IReadOnlyList<IRBehaviorTree> BehaviorTrees { get; init; } = [];

    public IReadOnlyList<IRCommand> Commands { get; init; } = [];

    public IReadOnlyList<IRCreativeTab> CreativeTabs { get; init; } = [];

    public IReadOnlyList<IRKeybind> Keybinds { get; init; } = [];

    public IReadOnlyList<IREnchantment> Enchantments { get; init; } = [];

    public IReadOnlyList<IRFluid> Fluids { get; init; } = [];

    public IReadOnlyList<IRGameRule> GameRules { get; init; } = [];

    public IReadOnlyList<IREffect> Effects { get; init; } = [];

    /// <summary>Global variables (Global tab).</summary>
    public IReadOnlyList<IRGlobalVariable> Globals { get; init; } = [];

    /// <summary>Tags of the Tags tab.</summary>
    public IReadOnlyList<IRTag> Tags { get; init; } = [];

    /// <summary>Biomes of the mod.</summary>
    public IReadOnlyList<IRBiome> Biomes { get; init; } = [];

    /// <summary>Sparkle particle systems of the mod.</summary>
    public IReadOnlyList<IRSparkle> Sparkles { get; init; } = [];

    /// <summary>Mobs qui ont un œuf d'apparition.</summary>
    public IEnumerable<IRMob> MobsWithSpawnEgg => Mobs.Where(m => m.SpawnEgg is not null);

    /// <summary>Mobs that are a boat or a minecart, placed by their item (V0.7.9).</summary>
    public IEnumerable<IRMob> Vehicles => Mobs.Where(m => m.Vehicle is not null);

    public bool IsEmpty =>
        Items.Count == 0 && Blocks.Count == 0 && Mobs.Count == 0 && Recipes.Count == 0 && Sounds.Count == 0 && Behaviors.Count == 0
        && Definitions.Count == 0 && Commands.Count == 0 && CreativeTabs.Count == 0 && Keybinds.Count == 0 && Enchantments.Count == 0
        && Fluids.Count == 0 && GameRules.Count == 0 && Effects.Count == 0 && Globals.Count == 0 && Tags.Count == 0 && Biomes.Count == 0
        && Sparkles.Count == 0;

    /// <summary>Tous les éléments qui ont une forme item (items, puis blocs avec item).</summary>
    public IEnumerable<IRElement> ElementsWithItemForm =>
        Items.Cast<IRElement>().Concat(Blocks.Where(b => b.ItemForm is not null));
}

/// <summary>Élément de contenu qui apparaît en jeu avec un nom et une texture.</summary>
/// <summary>
/// Appearance of an item, a block or a mob: a texture, or a 3D model of the project (items and
/// blocks); with neither, the element keeps its normal look for this number.
/// </summary>
public sealed record IRAppearance(string Name, IRTexture? Texture, ResourceId? Model)
{
    /// <summary>Identifier of the files of appearance <paramref name="number"/> ("ruby_appearance_2").</summary>
    public static ResourceId FileId(ResourceId owner, int number) => ResourceId.Parse($"{owner.Value}_appearance_{number}");
}

public abstract class IRElement
{
    protected IRElement(Guid sourceAssetId, ResourceId id, string displayName, IRTexture texture)
    {
        SourceAssetId = sourceAssetId;
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Value : displayName;
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
    }

    /// <summary>Asset du projet dont provient cet élément (pour relier les erreurs de build à l'asset).</summary>
    public Guid SourceAssetId { get; }

    public ResourceId Id { get; }

    public string DisplayName { get; }

    /// <summary>Texture principale.</summary>
    public IRTexture Texture { get; }

    /// <summary>3D model of the project shown instead of the generated shape (see <see cref="IRModel"/>), or null.</summary>
    public ResourceId? CustomModel { get; init; }

    /// <summary>Other looks (number 1, 2...) switched by the appearance nodes of the graphs.</summary>
    public IReadOnlyList<IRAppearance> Appearances { get; init; } = [];

    /// <summary>Forme item (inventaire), ou null pour un bloc sans item.</summary>
    public abstract IRItemForm? ItemForm { get; }
}

/// <summary>Propriétés de l'élément dans l'inventaire.</summary>
public sealed class IRItemForm
{
    public IRItemForm(
        int maxStackSize,
        ItemRarity rarity,
        bool fireResistant,
        IReadOnlyList<string> tooltipLines,
        bool inModCreativeTab,
        IReadOnlySet<CreativeTab> creativeTabs)
    {
        MaxStackSize = maxStackSize;
        Rarity = rarity;
        FireResistant = fireResistant;
        TooltipLines = tooltipLines ?? [];
        InModCreativeTab = inModCreativeTab;
        CreativeTabs = creativeTabs ?? new HashSet<CreativeTab>();
    }

    public int MaxStackSize { get; }

    public ItemRarity Rarity { get; }

    public bool FireResistant { get; }

    public IReadOnlyList<string> TooltipLines { get; }

    public bool InModCreativeTab { get; }

    public IReadOnlySet<CreativeTab> CreativeTabs { get; }
}

/// <summary>Effet donné en mangeant : durée en ticks, niveau (0 = I), probabilité de 0 à 1.</summary>
public sealed record IRFoodEffect(FoodEffectKind Effect, int DurationTicks, int Amplifier, float Probability);

/// <summary>Valeurs nutritives d'un item comestible.</summary>
public sealed class IRFood
{
    public IRFood(int nutrition, float saturation, bool alwaysEdible, bool isMeat)
    {
        Nutrition = nutrition;
        Saturation = saturation;
        AlwaysEdible = alwaysEdible;
        IsMeat = isMeat;
    }

    /// <summary>Time to eat, in ticks (32 by default, 16 for berries).</summary>
    public int EatTicks { get; init; } = 32;

    /// <summary>Animation of the player while eating.</summary>
    public ItemUseAnimation Animation { get; init; }

    /// <summary>Item given back once eaten, or null.</summary>
    public NamespacedId? Result { get; init; }

    public IReadOnlyList<IRFoodEffect> Effects { get; init; } = [];

    public int Nutrition { get; }

    public float Saturation { get; }

    public bool AlwaysEdible { get; }

    public bool IsMeat { get; }
}

public sealed class IRItem : IRElement
{
    public IRItem(
        Guid sourceAssetId,
        ResourceId id,
        string displayName,
        IRTexture texture,
        IRItemForm itemForm,
        int durability,
        bool hasGlint,
        IRFood? food)
        : base(sourceAssetId, id, displayName, texture)
    {
        Form = itemForm ?? throw new ArgumentNullException(nameof(itemForm));
        Durability = durability;
        HasGlint = hasGlint;
        Food = food;
    }

    public IRItemForm Form { get; }

    public override IRItemForm? ItemForm => Form;

    /// <summary>Nombre d'utilisations ; 0 = pas de durabilité.</summary>
    public int Durability { get; }

    public bool HasGlint { get; }

    public IRFood? Food { get; }

    /// <summary>Classe de l'item : simple, outil, arme ou armure.</summary>
    public ItemKind Kind { get; init; } = ItemKind.Simple;

    public ItemTier Tier { get; init; } = ItemTier.Iron;

    public ArmorMaterialKind ArmorMaterial { get; init; } = ArmorMaterialKind.Iron;

    /// <summary>Dégâts ajoutés au matériau (outils et armes).</summary>
    public float AttackDamage { get; init; }

    /// <summary>Vitesse d'attaque (outils et armes).</summary>
    public float AttackSpeed { get; init; }

    /// <summary>Modèle "handheld" : tenu comme un outil.</summary>
    public bool Handheld { get; init; }

    /// <summary>Item rendu après une fabrication, ou null.</summary>
    public NamespacedId? CraftRemainder { get; init; }

    public bool NoRepair { get; init; }

    /// <summary>Durée de combustion en ticks ; 0 = pas un combustible.</summary>
    public int BurnTime { get; init; }

    /// <summary>Tags d'items auxquels l'item est ajouté.</summary>
    public IReadOnlyList<NamespacedId> Tags { get; init; } = [];

    /// <summary>What stays in the crafting grid (V0.7.12); <see cref="CraftRemainder"/> for another item.</summary>
    public ItemRemainderKind Remainder { get; init; }

    /// <summary>Flat texture shown in the inventories instead of the 3D model, or null.</summary>
    public IRTexture? GuiTexture { get; init; }

    /// <summary>Function (the holder → yes / no) that turns the glint on, or null.</summary>
    public IRFunctionBinding? GlintCondition { get; init; }

    /// <summary>Item or tag that repairs the item on an anvil, or null.</summary>
    public IRIngredient? RepairItem { get; init; }

    /// <summary>Every block mined with the item drops.</summary>
    public bool HarvestsAnyBlock { get; init; }

    /// <summary>Material of a tool chosen in the editor, or null (a material of the game).</summary>
    public IRToolTier? CustomTier { get; init; }

    /// <summary>Function (→ seconds) that gives the burn time, or null (<see cref="BurnTime"/>).</summary>
    public IRFunctionBinding? BurnTimeFunction { get; init; }

    /// <summary>Function (→ yes / no) that lets the item burn, or null.</summary>
    public IRFunctionBinding? BurnCondition { get; init; }

    /// <summary>Chance (0 to 1) to raise the level of a composter; 0 = not compostable.</summary>
    public float CompostChance { get; init; }

    /// <summary>Function (the player → yes / no) that lets the item go into a composter, or null.</summary>
    public IRFunctionBinding? CompostCondition { get; init; }

    /// <summary>Piglins barter with the item.</summary>
    public bool PiglinCurrency { get; init; }

    /// <summary>How the item shoots, or null.</summary>
    public IRRanged? Ranged { get; init; }

    /// <summary>Methods of the class of the item (V0.7.13): bindings and events, in Java or delegated to a graph of the item.</summary>
    public IReadOnlyList<IRElementMethod> Methods { get; init; } = [];

    /// <summary>The method of a place of the class, or null.</summary>
    public IRElementMethod? Method(string slot) => Methods.FirstOrDefault(m => m.Slot == slot);

    /// <summary>The fuel needs code to know its burn time.</summary>
    public bool HasFuelFunctions => BurnTime > 0
        && (BurnTimeFunction is not null || BurnCondition is not null || Method(ItemMethodSlots.BurnTime) is not null || Method(ItemMethodSlots.BurnCondition) is not null);

    /// <summary>A function or a method decides whether a player can compost the item.</summary>
    public bool HasCompostCondition => CompostChance > 0 && (CompostCondition is not null || Method(ItemMethodSlots.CompostCondition) is not null);
}

/// <summary>
/// A method of the class of an element: its Java name and what it gives back, and its body, the Java
/// code of the project or the call of a function of the program (the graph of the element).
/// </summary>
public sealed record IRElementMethod(string Slot, string JavaName, MethodReturn Return, string? JavaBody, Guid? FunctionId);

/// <summary>A ranged item: projectile, ammunition (used up unless <see cref="NeedsAmmo"/> is false), mode, rate, speed, damage.</summary>
public sealed record IRRanged(
    RangedItemKind Kind, RangedProjectile Projectile, NamespacedId Ammo, bool NeedsAmmo, RangedFireMode Mode, int RateTicks, float Speed, float Damage)
{
    /// <summary>A fishing rod is the one of the game; the others are written by the generator.</summary>
    public bool Shoots => Kind is RangedItemKind.Bow or RangedItemKind.Crossbow or RangedItemKind.Throw;
}

/// <summary>Material of a tool chosen in the editor: uses, speed, enchantability, mining level of the game.</summary>
public sealed record IRToolTier(int Uses, float Speed, int Enchantability, ItemTier MiningLevel);

/// <summary>Ce qu'un bloc laisse tomber.</summary>
public sealed class IRBlockDrop
{
    private IRBlockDrop(BlockDropKind kind, NamespacedId? item, int min, int max)
    {
        Kind = kind;
        Item = item;
        Min = Math.Min(min, max);
        Max = Math.Max(min, max);
    }

    public static IRBlockDrop Self { get; } = new(BlockDropKind.Self, null, 1, 1);

    public static IRBlockDrop Nothing { get; } = new(BlockDropKind.Nothing, null, 0, 0);

    public BlockDropKind Kind { get; }

    /// <summary>Item laissé quand <see cref="Kind"/> vaut OtherItem.</summary>
    public NamespacedId? Item { get; }

    public int Min { get; }

    public int Max { get; }

    public static IRBlockDrop OtherItem(NamespacedId item, int min, int max) =>
        new(BlockDropKind.OtherItem, item ?? throw new ArgumentNullException(nameof(item)), min, max);
}

public sealed class IRBlock : IRElement
{
    public IRBlock(
        Guid sourceAssetId,
        ResourceId id,
        string displayName,
        IRTexture texture,
        IRBlockSettings settings,
        IReadOnlyDictionary<BlockTextureSlot, IRTexture> extraTextures,
        IRBlockDrop drop,
        IRItemForm? itemForm)
        : base(sourceAssetId, id, displayName, texture)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ExtraTextures = extraTextures ?? new Dictionary<BlockTextureSlot, IRTexture>();
        Drop = drop ?? throw new ArgumentNullException(nameof(drop));
        Form = itemForm;
    }

    public IRBlockSettings Settings { get; }

    /// <summary>What makes the block a plant (V0.7.0), or null.</summary>
    public IRPlant? Plant { get; init; }

    /// <summary>The one tool that harvests it (V0.7.11), or null: any tool of the type and the level.</summary>
    public NamespacedId? RequiredTool { get; init; }

    /// <summary>Function (the position → yes / no) that must say yes for the block to drop (V0.7.11), or null.</summary>
    public IRFunctionBinding? DropCondition { get; init; }

    /// <summary>Advanced settings of the block (rotation, water, redstone, shape, properties, ore).</summary>
    public MineEngine.Assets.BlockOptions Options { get; init; } = MineEngine.Assets.BlockOptions.Default;

    /// <summary>Texture of the block in the inventory, or null (the block itself).</summary>
    public IRTexture? ItemTexture { get; init; }

    /// <summary>Texture of the particles of the block, or null (its main texture).</summary>
    public IRTexture? ParticleTexture { get; init; }

    /// <summary>Textures des emplacements secondaires ; un emplacement absent reprend la texture principale.</summary>
    public IReadOnlyDictionary<BlockTextureSlot, IRTexture> ExtraTextures { get; }

    public IRBlockDrop Drop { get; }

    public IRItemForm? Form { get; }

    public override IRItemForm? ItemForm => Form;

    public IRTexture GetTexture(BlockTextureSlot slot) =>
        slot != BlockTextureSlot.Main && ExtraTextures.TryGetValue(slot, out IRTexture? texture) ? texture : Texture;
}

/// <summary>Réglages physiques et visuels d'un bloc.</summary>
public sealed class IRBlockSettings
{
    public BlockModelKind Model { get; init; } = BlockModelKind.Cube;

    public BlockRenderType RenderType { get; init; } = BlockRenderType.Solid;

    public int LightLevel { get; init; }

    public float Hardness { get; init; } = 1.5f;

    public float Resistance { get; init; } = 6f;

    public bool Unbreakable { get; init; }

    public BlockSoundType Sound { get; init; } = BlockSoundType.Stone;

    public float Friction { get; init; } = 0.6f;

    public float SpeedFactor { get; init; } = 1f;

    public float JumpFactor { get; init; } = 1f;

    public HarvestTool HarvestTool { get; init; } = HarvestTool.None;

    public ToolTier ToolTier { get; init; } = ToolTier.Any;

    public bool RequiresCorrectTool { get; init; }

    public int ExperienceMin { get; init; }

    public int ExperienceMax { get; init; }

    public bool DropsExperience => ExperienceMax > 0;
}

/// <summary>Texture d'un élément : un fichier PNG existant, ou une texture de remplacement.</summary>
public sealed class IRTexture
{
    private IRTexture(string? sourceFile, byte[]? png = null)
    {
        SourceFile = sourceFile;
        Png = png;
    }

    public static IRTexture Placeholder { get; } = new(null);

    /// <summary>Image held in memory (texture embedded in a Blockbench model), or null.</summary>
    public byte[]? Png { get; }

    /// <summary>Chemin absolu du PNG source, ou null pour la texture de remplacement.</summary>
    public string? SourceFile { get; }

    public bool IsPlaceholder => SourceFile is null && Png is null;

    public static IRTexture FromPng(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        return new IRTexture(null, png);
    }

    public static IRTexture FromFile(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        return new IRTexture(absolutePath);
    }
}
