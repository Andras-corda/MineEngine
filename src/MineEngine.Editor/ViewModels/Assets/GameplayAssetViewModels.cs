using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Properties of an enchantment (V0.6.3).</summary>
public sealed class EnchantmentAssetViewModel : AssetViewModel
{
    private readonly EnchantmentAsset _enchantment;

    public EnchantmentAssetViewModel(EnchantmentAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _enchantment = model;
    }

    public IReadOnlyList<Choice<EnchantmentTarget>> TargetChoices => GameDataChoices.EnchantmentTargets;

    public IReadOnlyList<Choice<EnchantmentRarity>> RarityChoices => GameDataChoices.EnchantmentRarities;

    public int MaxLevelLimit => EnchantmentAsset.MaxLevelLimit;

    public EnchantmentTarget Target
    {
        get => _enchantment.Target;
        set => ChangeIfDifferent(Tr.T("Change the enchanted items"), nameof(EnchantmentAsset.Target), () => _enchantment.Target, v => _enchantment.Target = v, value);
    }

    public EnchantmentRarity Rarity
    {
        get => _enchantment.Rarity;
        set => ChangeIfDifferent(Tr.T("Change rarity"), nameof(EnchantmentAsset.Rarity), () => _enchantment.Rarity, v => _enchantment.Rarity = v, value);
    }

    public double MaxLevel
    {
        get => _enchantment.MaxLevel;
        set => ChangeNumber(Tr.T("Change the maximum level"), nameof(EnchantmentAsset.MaxLevel), value, () => _enchantment.MaxLevel, v => _enchantment.MaxLevel = v,
            v => Math.Clamp((int)Math.Round(v), 1, EnchantmentAsset.MaxLevelLimit));
    }

    public bool Treasure
    {
        get => _enchantment.Treasure;
        set => ChangeIfDifferent(Tr.T("Change treasure"), nameof(EnchantmentAsset.Treasure), () => _enchantment.Treasure, v => _enchantment.Treasure = v, value);
    }

    public bool Curse
    {
        get => _enchantment.Curse;
        set => ChangeIfDifferent(Tr.T("Change curse"), nameof(EnchantmentAsset.Curse), () => _enchantment.Curse, v => _enchantment.Curse = v, value);
    }

    public double DamageBonus
    {
        get => _enchantment.DamageBonus;
        set => ChangeNumber(Tr.T("Change the damage bonus"), nameof(EnchantmentAsset.DamageBonus), value, () => _enchantment.DamageBonus, v => _enchantment.DamageBonus = v,
            v => (float)Math.Clamp(v, 0, 100));
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
    }
}

/// <summary>Properties of a fluid (V0.6.3).</summary>
public sealed class FluidAssetViewModel : AssetViewModel
{
    private readonly FluidAsset _fluid;
    private string _colorText;

    public FluidAssetViewModel(FluidAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _fluid = model;
        _colorText = model.Color;
        StillTexture = new TextureSlotViewModel(model, context, nameof(FluidAsset.StillTextureId), Tr.T("Still fluid"),
            Tr.T("None (water, tinted by the color)"), () => _fluid.StillTextureId, v => _fluid.StillTextureId = v);
        FlowingTexture = new TextureSlotViewModel(model, context, nameof(FluidAsset.FlowingTextureId), Tr.T("Flowing fluid"),
            Tr.T("None (water, tinted by the color)"), () => _fluid.FlowingTextureId, v => _fluid.FlowingTextureId = v);
        BucketTexture = new TextureSlotViewModel(model, context, nameof(FluidAsset.BucketTextureId), Tr.T("Bucket"),
            Tr.T("None (water bucket)"), () => _fluid.BucketTextureId, v => _fluid.BucketTextureId = v);
    }

    public TextureSlotViewModel StillTexture { get; }

    public TextureSlotViewModel FlowingTexture { get; }

    public TextureSlotViewModel BucketTexture { get; }

    public override string? IconPath => StillTexture.FullPath;

    /// <summary>Color typed as "#RRGGBB"; applied when it is a valid color.</summary>
    public string ColorText
    {
        get => _colorText;
        set
        {
            _colorText = value ?? string.Empty;
            if (FluidAsset.NormalizeColor(_colorText) is { } color && color != _fluid.Color)
            {
                ChangeIfDifferent(Tr.T("Change the color"), nameof(FluidAsset.Color), () => _fluid.Color, v => _fluid.Color = v, color);
            }
        }
    }

    public string BucketId => _fluid.BucketId;

    public double LightLevel
    {
        get => _fluid.LightLevel;
        set => ChangeNumber(Tr.T("Change light level"), nameof(FluidAsset.LightLevel), value, () => _fluid.LightLevel, v => _fluid.LightLevel = v, v => Math.Clamp((int)Math.Round(v), 0, 15));
    }

    public double FlowSpeed
    {
        get => _fluid.FlowSpeed;
        set => ChangeNumber(Tr.T("Change the flow speed"), nameof(FluidAsset.FlowSpeed), value, () => _fluid.FlowSpeed, v => _fluid.FlowSpeed = v, v => Math.Clamp((int)Math.Round(v), 1, 100));
    }

    public double SpreadDistance
    {
        get => _fluid.SpreadDistance;
        set => ChangeNumber(Tr.T("Change the spread distance"), nameof(FluidAsset.SpreadDistance), value, () => _fluid.SpreadDistance, v => _fluid.SpreadDistance = v, v => Math.Clamp((int)Math.Round(v), 1, 8));
    }

    public double LevelDecrease
    {
        get => _fluid.LevelDecrease;
        set => ChangeNumber(Tr.T("Change the level lost per block"), nameof(FluidAsset.LevelDecrease), value, () => _fluid.LevelDecrease, v => _fluid.LevelDecrease = v, v => Math.Clamp((int)Math.Round(v), 1, 7));
    }

    public double Density
    {
        get => _fluid.Density;
        set => ChangeNumber(Tr.T("Change the density"), nameof(FluidAsset.Density), value, () => _fluid.Density, v => _fluid.Density = v, v => Math.Clamp((int)Math.Round(v), -10000, 10000));
    }

    public double Viscosity
    {
        get => _fluid.Viscosity;
        set => ChangeNumber(Tr.T("Change the viscosity"), nameof(FluidAsset.Viscosity), value, () => _fluid.Viscosity, v => _fluid.Viscosity = v, v => Math.Clamp((int)Math.Round(v), 0, 100000));
    }

    public bool Infinite
    {
        get => _fluid.Infinite;
        set => ChangeIfDifferent(Tr.T("Change infinite sources"), nameof(FluidAsset.Infinite), () => _fluid.Infinite, v => _fluid.Infinite = v, value);
    }

    public bool HasBucket
    {
        get => _fluid.HasBucket;
        set => ChangeIfDifferent(Tr.T("Change the bucket"), nameof(FluidAsset.HasBucket), () => _fluid.HasBucket, v => _fluid.HasBucket = v, value);
    }

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        StillTexture.Refresh();
        FlowingTexture.Refresh();
        BucketTexture.Refresh();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(FluidAsset.Color):
                _colorText = _fluid.Color;
                OnPropertyChanged(nameof(ColorText));
                break;
            case nameof(FluidAsset.StillTextureId):
                StillTexture.Refresh();
                OnPropertyChanged(nameof(IconPath));
                OnPropertyChanged(nameof(HasIcon));
                break;
            case nameof(FluidAsset.FlowingTextureId):
                FlowingTexture.Refresh();
                break;
            case nameof(FluidAsset.BucketTextureId):
                BucketTexture.Refresh();
                break;
            case nameof(Core.Assets.Asset.ResourceId):
                OnPropertyChanged(nameof(BucketId));
                break;
        }
    }
}

/// <summary>Properties of a game rule (V0.6.3).</summary>
/// <summary>
/// Damage type (V0.7.10): its two death messages with a preview, its exhaustion, scaling, sound and kind
/// of message, and its properties (tags of the game), each a box.
/// </summary>
public sealed class DamageTypeAssetViewModel : AssetViewModel
{
    private readonly DamageTypeAsset _damage;

    public DamageTypeAssetViewModel(DamageTypeAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _damage = model;
        FlagOptions = [.. GameDataChoices.DamageTypeFlags.Select(c => new ToggleOptionViewModel(c.Label,
            () => _damage.Flags.Contains(c.Value),
            on => ChangeIfDifferent(Tr.T("Change the properties of the damage"), nameof(DamageTypeAsset.Flags), () => _damage.Flags, v => _damage.Flags = v,
                on ? new HashSet<DamageTypeFlag>(_damage.Flags) { c.Value } : new HashSet<DamageTypeFlag>(_damage.Flags.Where(f => f != c.Value)))))];
    }

    public IReadOnlyList<Choice<DamageScaling>> ScalingChoices => GameDataChoices.DamageScalings;

    public IReadOnlyList<Choice<DamageEffects>> EffectsChoices => GameDataChoices.DamageEffectsChoices;

    public IReadOnlyList<Choice<DeathMessageType>> MessageTypeChoices => GameDataChoices.DeathMessageTypes;

    public IReadOnlyList<ToggleOptionViewModel> FlagOptions { get; }

    public string DeathMessage
    {
        get => _damage.DeathMessage;
        set => ChangeIfDifferent(Tr.T("Change the death message"), nameof(DamageTypeAsset.DeathMessage), () => _damage.DeathMessage, v => _damage.DeathMessage = v, (value ?? string.Empty).Trim());
    }

    public string KillerMessage
    {
        get => _damage.KillerMessage;
        set => ChangeIfDifferent(Tr.T("Change the death message"), nameof(DamageTypeAsset.KillerMessage), () => _damage.KillerMessage, v => _damage.KillerMessage = v, (value ?? string.Empty).Trim());
    }

    /// <summary>The two messages as the chat shows them, with example names.</summary>
    public string MessagePreview =>
        Example(_damage.DeathMessage) + (_damage.KillerMessage.Length > 0 ? Environment.NewLine + Example(_damage.KillerMessage) : string.Empty);

    public double Exhaustion
    {
        get => Math.Round(_damage.Exhaustion, 3);
        set => ChangeNumber(Tr.T("Change the exhaustion"), nameof(DamageTypeAsset.Exhaustion), value, () => _damage.Exhaustion, v => _damage.Exhaustion = v, v => (float)v);
    }

    public double MaxExhaustion => DamageTypeAsset.MaxExhaustion;

    public DamageScaling Scaling
    {
        get => _damage.Scaling;
        set => ChangeIfDifferent(Tr.T("Change the scaling"), nameof(DamageTypeAsset.Scaling), () => _damage.Scaling, v => _damage.Scaling = v, value);
    }

    public DamageEffects Effects
    {
        get => _damage.Effects;
        set => ChangeIfDifferent(Tr.T("Change the sound of the damage"), nameof(DamageTypeAsset.Effects), () => _damage.Effects, v => _damage.Effects = v, value);
    }

    public DeathMessageType MessageType
    {
        get => _damage.MessageType;
        set => ChangeIfDifferent(Tr.T("Change the kind of death message"), nameof(DamageTypeAsset.MessageType), () => _damage.MessageType, v => _damage.MessageType = v, value);
    }

    /// <summary>How graphs use it: the node and the identifier.</summary>
    public string Usage => Tr.F("Node \"Damage of a type\" with \"{0}\", then \"Damage with a source\".", _damage.ResourceId.Value);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(DamageTypeAsset.DeathMessage):
            case nameof(DamageTypeAsset.KillerMessage):
                OnPropertyChanged(nameof(MessagePreview));
                break;
            case nameof(DamageTypeAsset.Flags):
                foreach (ToggleOptionViewModel option in FlagOptions)
                {
                    option.Refresh();
                }

                break;
            case nameof(Asset.ResourceId):
                OnPropertyChanged(nameof(Usage));
                break;
        }
    }

    private static string Example(string message) =>
        message.Replace("{victim}", "Steve", StringComparison.Ordinal).Replace("{killer}", "Zombie", StringComparison.Ordinal);
}

public sealed class GameRuleAssetViewModel : AssetViewModel
{
    private readonly GameRuleAsset _rule;

    public GameRuleAssetViewModel(GameRuleAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _rule = model;
    }

    public IReadOnlyList<Choice<GameRuleKind>> KindChoices => GameDataChoices.GameRuleKinds;

    public IReadOnlyList<Choice<GameRuleCategory>> CategoryChoices => GameDataChoices.GameRuleCategories;

    public string Usage => _rule.Kind == GameRuleKind.Boolean ? $"/gamerule {_rule.RuleName} true" : $"/gamerule {_rule.RuleName} 10";

    public GameRuleKind Kind
    {
        get => _rule.Kind;
        set => ChangeIfDifferent(Tr.T("Change the type of the rule"), nameof(GameRuleAsset.Kind), () => _rule.Kind, v => _rule.Kind = v, value);
    }

    public bool IsBoolean => _rule.Kind == GameRuleKind.Boolean;

    public GameRuleCategory Category
    {
        get => _rule.Category;
        set => ChangeIfDifferent(Tr.T("Change the category"), nameof(GameRuleAsset.Category), () => _rule.Category, v => _rule.Category = v, value);
    }

    public bool DefaultBoolean
    {
        get => _rule.DefaultBoolean;
        set => ChangeIfDifferent(Tr.T("Change the default value"), nameof(GameRuleAsset.DefaultBoolean), () => _rule.DefaultBoolean, v => _rule.DefaultBoolean = v, value);
    }

    public double DefaultNumber
    {
        get => _rule.DefaultNumber;
        set => ChangeNumber(Tr.T("Change the default value"), nameof(GameRuleAsset.DefaultNumber), value, () => _rule.DefaultNumber, v => _rule.DefaultNumber = v, v => (int)Math.Round(v));
    }

    public string Description
    {
        get => _rule.Description;
        set => ChangeIfDifferent(Tr.T("Change the description"), nameof(GameRuleAsset.Description), () => _rule.Description, v => _rule.Description = v, (value ?? string.Empty).Trim());
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(IsBoolean));
        OnPropertyChanged(nameof(Usage));
    }
}

/// <summary>Properties of a status effect (V0.6.3).</summary>
public sealed class EffectAssetViewModel : AssetViewModel
{
    private readonly EffectAsset _effect;
    private string _colorText;

    public EffectAssetViewModel(EffectAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _effect = model;
        _colorText = model.Color;
        IconTexture = new TextureSlotViewModel(model, context, nameof(EffectAsset.IconTextureId), Tr.T("Icon"),
            Tr.T("None (placeholder)"), () => _effect.IconTextureId, v => _effect.IconTextureId = v);
    }

    public TextureSlotViewModel IconTexture { get; }

    public override string? IconPath => IconTexture.FullPath;

    public IReadOnlyList<Choice<EffectCategory>> CategoryChoices => GameDataChoices.EffectCategories;

    public EffectCategory Category
    {
        get => _effect.Category;
        set => ChangeIfDifferent(Tr.T("Change the kind of effect"), nameof(EffectAsset.Category), () => _effect.Category, v => _effect.Category = v, value);
    }

    public string ColorText
    {
        get => _colorText;
        set
        {
            _colorText = value ?? string.Empty;
            if (FluidAsset.NormalizeColor(_colorText) is { } color && color != _effect.Color)
            {
                ChangeIfDifferent(Tr.T("Change the color"), nameof(EffectAsset.Color), () => _effect.Color, v => _effect.Color = v, color);
            }
        }
    }

    public double SpeedPercent
    {
        get => _effect.SpeedPercent;
        set => ChangeNumber(Tr.T("Change the speed"), nameof(EffectAsset.SpeedPercent), value, () => _effect.SpeedPercent, v => _effect.SpeedPercent = v, v => Math.Clamp((int)Math.Round(v), -100, 500));
    }

    public double AttackDamage
    {
        get => _effect.AttackDamage;
        set => ChangeNumber(Tr.T("Change the attack damage"), nameof(EffectAsset.AttackDamage), value, () => _effect.AttackDamage, v => _effect.AttackDamage = v, v => (float)Math.Clamp(v, -100, 100));
    }

    public double Armor
    {
        get => _effect.Armor;
        set => ChangeNumber(Tr.T("Change the armor"), nameof(EffectAsset.Armor), value, () => _effect.Armor, v => _effect.Armor = v, v => (float)Math.Clamp(v, -30, 30));
    }

    public double MaxHealth
    {
        get => _effect.MaxHealth;
        set => ChangeNumber(Tr.T("Change the maximum health"), nameof(EffectAsset.MaxHealth), value, () => _effect.MaxHealth, v => _effect.MaxHealth = v, v => (float)Math.Clamp(v, -100, 100));
    }

    public double HealAmount
    {
        get => _effect.HealAmount;
        set => ChangeNumber(Tr.T("Change the healing"), nameof(EffectAsset.HealAmount), value, () => _effect.HealAmount, v => _effect.HealAmount = v, v => (float)Math.Clamp(v, 0, 100));
    }

    public double DamageAmount
    {
        get => _effect.DamageAmount;
        set => ChangeNumber(Tr.T("Change the damage"), nameof(EffectAsset.DamageAmount), value, () => _effect.DamageAmount, v => _effect.DamageAmount = v, v => (float)Math.Clamp(v, 0, 100));
    }

    public double Interval
    {
        get => _effect.Interval;
        set => ChangeNumber(Tr.T("Change the interval"), nameof(EffectAsset.Interval), value, () => _effect.Interval, v => _effect.Interval = v, v => Math.Clamp((int)Math.Round(v), 1, 1200));
    }

    public bool HasPotion
    {
        get => _effect.HasPotion;
        set => ChangeIfDifferent(Tr.T("Change the potion"), nameof(EffectAsset.HasPotion), () => _effect.HasPotion, v => _effect.HasPotion = v, value);
    }

    public double PotionSeconds
    {
        get => _effect.PotionSeconds;
        set => ChangeNumber(Tr.T("Change the duration of the potion"), nameof(EffectAsset.PotionSeconds), value, () => _effect.PotionSeconds, v => _effect.PotionSeconds = v, v => Math.Clamp((int)Math.Round(v), 1, 3600));
    }

    /// <summary>Identifier to type in the effect nodes of the graphs ("magicmod:frenzy").</summary>
    public string FullId => $"{Context.Project.Settings.ModId}:{Model.ResourceId}";

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        IconTexture.Refresh();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(EffectAsset.Color))
        {
            _colorText = _effect.Color;
            OnPropertyChanged(nameof(ColorText));
        }
        else if (propertyName == nameof(EffectAsset.IconTextureId))
        {
            IconTexture.Refresh();
            OnPropertyChanged(nameof(IconPath));
            OnPropertyChanged(nameof(HasIcon));
        }
        else if (propertyName == nameof(Core.Assets.Asset.ResourceId))
        {
            OnPropertyChanged(nameof(FullId));
        }
    }
}

/// <summary>A global variable (V0.6.7): it is edited in the Global tab, not in the Dashboard.</summary>
public sealed class GlobalVariableAssetViewModel : AssetViewModel
{
    public GlobalVariableAssetViewModel(GlobalVariableAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    /// <summary>The variable does not exist in game under its identifier: it gives the name of its Java accessors.</summary>
    public override bool HasGameId => false;
}

/// <summary>A tag (V0.7.1): it is edited in the Tags tab, not in the Dashboard.</summary>
public sealed class TagAssetViewModel : AssetViewModel
{
    public TagAssetViewModel(TagAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    public override bool HasGameId => false;
}
