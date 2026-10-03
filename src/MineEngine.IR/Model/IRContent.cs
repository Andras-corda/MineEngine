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
        IEnumerable<IRSound>? sounds = null)
    {
        Items = items.OrderBy(i => i.Id).ToList();
        Blocks = blocks.OrderBy(b => b.Id).ToList();
        Mobs = (mobs ?? []).OrderBy(m => m.Id).ToList();
        Recipes = (recipes ?? []).OrderBy(r => r.Id).ToList();
        Sounds = (sounds ?? []).OrderBy(s => s.Id).ToList();
    }

    public IReadOnlyList<IRItem> Items { get; }

    public IReadOnlyList<IRBlock> Blocks { get; }

    public IReadOnlyList<IRMob> Mobs { get; }

    public IReadOnlyList<IRRecipe> Recipes { get; }

    public IReadOnlyList<IRSound> Sounds { get; }

    /// <summary>Mobs qui ont un œuf d'apparition.</summary>
    public IEnumerable<IRMob> MobsWithSpawnEgg => Mobs.Where(m => m.SpawnEgg is not null);

    public bool IsEmpty => Items.Count == 0 && Blocks.Count == 0 && Mobs.Count == 0 && Recipes.Count == 0 && Sounds.Count == 0;

    /// <summary>Tous les éléments qui ont une forme item (items, puis blocs avec item).</summary>
    public IEnumerable<IRElement> ElementsWithItemForm =>
        Items.Cast<IRElement>().Concat(Blocks.Where(b => b.ItemForm is not null));
}

/// <summary>Élément de contenu qui apparaît en jeu avec un nom et une texture.</summary>
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
}

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
    private IRTexture(string? sourceFile)
    {
        SourceFile = sourceFile;
    }

    public static IRTexture Placeholder { get; } = new(null);

    /// <summary>Chemin absolu du PNG source, ou null pour la texture de remplacement.</summary>
    public string? SourceFile { get; }

    public bool IsPlaceholder => SourceFile is null;

    public static IRTexture FromFile(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        return new IRTexture(absolutePath);
    }
}
