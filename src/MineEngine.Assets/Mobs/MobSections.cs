using System.Collections.Immutable;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Mobs;

/// <summary>
/// A size of a mob (V0.7.9): a fixed factor, or the result of a function of the project that takes
/// the mob (its look or its collision box grows while it lives).
/// </summary>
public sealed record MobScale(MobValueMode Mode = MobValueMode.Fixed, double Value = 1, Guid? Function = null)
{
    public const double Min = 0.05;
    public const double Max = 16;

    public static MobScale Default { get; } = new();
}

/// <summary>
/// A layer drawn over the mob with the same model (V0.7.9): its texture, its light (it glows like
/// the eyes of a spider) and the red flash when it is hurt. Functions of the project can decide
/// when it shows, when it glows and when it flashes.
/// </summary>
public sealed record MobLayer(Guid TextureId)
{
    public bool Glow { get; init; }

    /// <summary>The layer does not turn red when the mob is hurt.</summary>
    public bool NoHurtOverlay { get; init; }

    /// <summary>Function (the mob → yes / no) that shows the layer; null: always shown.</summary>
    public Guid? VisibleFunction { get; init; }

    /// <summary>Function (the mob → yes / no) that makes the layer glow; null: <see cref="Glow"/>.</summary>
    public Guid? GlowFunction { get; init; }

    /// <summary>Function (the mob → yes / no) that keeps the red flash; null: <see cref="NoHurtOverlay"/>.</summary>
    public Guid? HurtFunction { get; init; }
}

/// <summary>An item that tames the mob with a chance, in percent, for each item given (bone: 33 %).</summary>
public sealed record MobTamingItem(ContentReference Item, double Chance)
{
    public const double DefaultChance = 33;
}

/// <summary>Taming (V0.7.9): an animal tamed by the items, which then follows its owner and sits when asked.</summary>
public sealed record MobTaming(bool Enabled, ImmutableList<MobTamingItem> Items)
{
    public static MobTaming Disabled { get; } = new(false, []);
}

/// <summary>Riding (V0.7.9): the player rides the mob and steers it, with a saddle if it needs one.</summary>
public sealed record MobRiding(bool Enabled)
{
    public static MobRiding Disabled { get; } = new(false);

    /// <summary>The player moves it forward and backward.</summary>
    public bool Forward { get; init; } = true;

    /// <summary>The player moves it sideways.</summary>
    public bool Strafe { get; init; } = true;

    /// <summary>A saddle must be put on it first (right-click with a saddle).</summary>
    public bool NeedsSaddle { get; init; }

    /// <summary>Index of the layer that shows the saddle (shown only once it is saddled); -1: none.</summary>
    public int SaddleLayer { get; init; } = -1;
}

/// <summary>Water entity (V0.7.9): breathing and push of the fluids, each fixed or decided by a function.</summary>
public sealed record MobWater(bool Enabled)
{
    public static MobWater Disabled { get; } = new(false);

    public bool Breathes { get; init; } = true;

    public Guid? BreathesFunction { get; init; }

    public bool PushedByFluid { get; init; } = true;

    public Guid? PushedFunction { get; init; }

    /// <summary>Fluid it lives in (minecraft:water by default).</summary>
    public ContentReference? Fluid { get; init; }
}

/// <summary>Boss (V0.7.9): its health shown in a bar at the top of the screen of the players nearby.</summary>
public sealed record MobBoss(bool Enabled)
{
    public static MobBoss Disabled { get; } = new(false);

    public bool ShowBar { get; init; } = true;

    public MobBossColor Color { get; init; } = MobBossColor.Purple;

    public MobBossOverlay Overlay { get; init; } = MobBossOverlay.Progress;
}

/// <summary>
/// Golem (V0.7.9): placing the activator block on top of a column of two identical blocks builds the
/// mob there, like a snow golem; a function of the project (the position of the activator → yes / no)
/// can decide instead.
/// </summary>
public sealed record MobGolem(bool Enabled)
{
    public static ContentReference DefaultActivator { get; } =
        ContentReference.ToId(NamespacedId.TryParse("minecraft:carved_pumpkin", out NamespacedId? id) ? id! : throw new InvalidOperationException());

    // Declared after the default activator: the static fields are set in their order.
    public static MobGolem Disabled { get; } = new(false);

    public ContentReference Activator { get; init; } = DefaultActivator;

    public Guid? Function { get; init; }
}

/// <summary>
/// Vehicle (V0.7.9, step 3): a boat or a minecart is placed by its item ("storm_wolf_boat"), with
/// its rarity and its texture; it does not spawn and has no egg.
/// </summary>
public sealed record MobVehicle(MobVehicleKind Kind)
{
    public static MobVehicle None { get; } = new(MobVehicleKind.None);

    public bool Enabled => Kind != MobVehicleKind.None;

    public ItemRarity Rarity { get; init; } = ItemRarity.Common;

    /// <summary>Texture of the item; null: the item of the game (oak boat, minecart).</summary>
    public Guid? ItemTextureId { get; init; }
}
