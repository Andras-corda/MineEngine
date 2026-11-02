using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>Command of the mod (V0.6.2); its handlers are the "On command" events naming it.</summary>
public sealed record IRCommand(
    Guid SourceAssetId, ResourceId Id, int PermissionLevel, CommandArgumentKind Argument, string ArgumentName, int Minimum, int Maximum, string Feedback);

/// <summary>Creative tab (V0.6.2): title, icon and items in order.</summary>
public sealed record IRCreativeTab(Guid SourceAssetId, ResourceId Id, string Title, NamespacedId? Icon, IReadOnlyList<NamespacedId> Items);

/// <summary>Keybind (V0.6.2): name in the Controls screen and default key (GLFW code).</summary>
public sealed record IRKeybind(Guid SourceAssetId, ResourceId Id, string Name, int KeyCode);

/// <summary>Enchantment (V0.6.3).</summary>
public sealed record IREnchantment(
    Guid SourceAssetId, ResourceId Id, string Name, EnchantmentTarget Target, EnchantmentRarity Rarity, int MaxLevel, bool Treasure, bool Curse, float DamageBonus)
{
    public int Weight => Rarity switch
    {
        EnchantmentRarity.Common => 10,
        EnchantmentRarity.Uncommon => 5,
        EnchantmentRarity.Rare => 2,
        _ => 1,
    };
}

/// <summary>Fluid (V0.6.3): its block has the identifier of the fluid; its bucket "&lt;id&gt;_bucket".</summary>
public sealed record IRFluid(Guid SourceAssetId, ResourceId Id, string Name)
{
    public int Color { get; init; } = 0x3F76E4;

    public int LightLevel { get; init; }

    public int FlowSpeed { get; init; } = 5;

    public int SpreadDistance { get; init; } = 4;

    public int LevelDecrease { get; init; } = 1;

    public bool Infinite { get; init; } = true;

    public int Density { get; init; } = 1000;

    public int Viscosity { get; init; } = 1000;

    /// <summary>Texture of the still fluid, or null for the water texture tinted by the color.</summary>
    public IRTexture? StillTexture { get; init; }

    public IRTexture? FlowingTexture { get; init; }

    /// <summary>Bucket of the fluid, or null when it has none.</summary>
    public string? BucketId { get; init; }

    /// <summary>Texture of the bucket, or null for the water bucket.</summary>
    public IRTexture? BucketTexture { get; init; }
}

/// <summary>Game rule (V0.6.3).</summary>
/// <summary>Global variable of the mod (V0.6.7): stored by the class MineEngineGlobals according to its scope.</summary>
public sealed record IRGlobalVariable(Guid Id, string Name, DataType Type, GlobalScope Scope, string DefaultValue)
{
    public bool IsPerPlayer => Scope is GlobalScope.PlayerLifetime or GlobalScope.PlayerPersistent;
}

public sealed record IRGameRule(
    Guid SourceAssetId, ResourceId Id, string Name, GameRuleKind Kind, GameRuleCategory Category, bool DefaultBoolean, int DefaultNumber, string Description);

/// <summary>
/// Damage type of the mod (V0.7.10): its messages ({victim} and {killer} already turned into the
/// arguments of the game), its exhaustion, scaling, sound, kind of death message and tags.
/// </summary>
public sealed record IRDamageType(
    Guid SourceAssetId, ResourceId Id, string Name, string DeathMessage, string KillerMessage, float Exhaustion,
    DamageScaling Scaling, DamageEffects Effects, DeathMessageType MessageType, IReadOnlySet<DamageTypeFlag> Flags);

/// <summary>Drop table of the mod (V0.7.10): its kind and its pools.</summary>
public sealed record IRLootTable(Guid SourceAssetId, ResourceId Id, LootTableKind Kind, IReadOnlyList<IRLootPool> Pools);

public sealed record IRLootPool(int RollsMin, int RollsMax, float BonusRolls, IReadOnlyList<IRLootEntry> Entries);

/// <summary>An entry: its item or tag (null: nothing), weight, count, conditions and functions (see <see cref="LootEntry"/>).</summary>
public sealed record IRLootEntry(IRIngredient? Item, int Weight, int CountMin, int CountMax)
{
    public int Chance { get; init; } = 100;

    public bool KilledByPlayer { get; init; }

    public LootSilkTouch SilkTouch { get; init; }

    public bool SurvivesExplosion { get; init; }

    public int LootingBonus { get; init; }

    public bool SmeltIfOnFire { get; init; }

    public bool EnchantRandomly { get; init; }
}

/// <summary>Status effect (V0.6.3), with its potion when it has one.</summary>
public sealed record IREffect(Guid SourceAssetId, ResourceId Id, string Name, EffectCategory Category, int Color, IRTexture Icon)
{
    public int SpeedPercent { get; init; }

    public float AttackDamage { get; init; }

    public float Armor { get; init; }

    public float MaxHealth { get; init; }

    public float HealAmount { get; init; }

    public float DamageAmount { get; init; }

    public int Interval { get; init; } = 40;

    public bool HasPeriodicAction => HealAmount > 0 || DamageAmount > 0;

    /// <summary>Duration of the potion in ticks, or 0 when the effect has no potion.</summary>
    public int PotionTicks { get; init; }
}

/// <summary>
/// A component of a GUI (V0.8.0), in pixels of the GUI. <see cref="Color"/> is 0xRRGGBB; an image has
/// its texture and the size of its file (the game stretches it to the size of the component).
/// </summary>
public sealed record IRGuiComponent(GuiComponentKind Kind, string Name, int X, int Y, int Width, int Height)
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public int Color { get; init; } = 0x404040;

    public string Tooltip { get; init; } = string.Empty;

    public GuiSlotMode SlotMode { get; init; }

    public bool Checked { get; init; }

    public int MaxLength { get; init; } = 32;

    public IRTexture? Texture { get; init; }

    /// <summary>How it looks (V0.8.1).</summary>
    public GuiStyle Style { get; init; } = GuiStyle.Default;

    public int TextureWidth { get; init; } = 16;

    public int TextureHeight { get; init; } = 16;
}

/// <summary>
/// GUI of the mod (V0.8.0): its title (the name of the element), its size, the place of the player's
/// inventory (top left corner of its first slot frame) when it has one, and its components.
/// </summary>
public sealed record IRGui(
    Guid SourceAssetId, ResourceId Id, string Title, int Width, int Height, bool HasInventory, int InventoryX, int InventoryY,
    bool PauseGame, bool RenderBackground, IReadOnlyList<IRGuiComponent> Components)
{
    public IEnumerable<IRGuiComponent> Slots => Components.Where(c => c.Kind == GuiComponentKind.Slot);

    /// <summary>The components drawn with a texture: the images, and the buttons that are images (V0.8.1).</summary>
    public IEnumerable<IRGuiComponent> Images => Components.Where(c => c.Texture is not null);

    /// <summary>Events of the GUI and of its components (V0.8.0), in Java or as graphs.</summary>
    public IReadOnlyList<IRElementMethod> Methods { get; init; } = [];

    public IRElementMethod? Method(string slot) => Methods.FirstOrDefault(m => m.Slot == slot);
}
