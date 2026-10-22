using MineEngine.Assets;
using MineEngine.Core.GameData;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Inspector d'un bloc : visuel, propriétés, récolte et forme item, comme dans MCreator.</summary>
public sealed class BlockAssetViewModel : InventoryAssetViewModel
{
    private readonly BlockAsset _block;
    private readonly IReadOnlyList<TextureSlotViewModel> _extraSlots;

    public BlockAssetViewModel(BlockAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _block = model;
        _extraSlots =
        [
            .. Enum.GetValues<BlockTextureSlot>()
                .Where(s => s != BlockTextureSlot.Main)
                .Select(slot => new TextureSlotViewModel(
                    model, context, BlockAsset.TexturePropertyName(slot), GameDataChoices.SlotLabel(slot),
                    Tr.T("Uses the main texture"),
                    () => model.GetTexture(slot), v => model.SetTexture(slot, v))),
        ];
        DropItemPicker = new ReferencePickerViewModel(
            context, ReferenceDomain.Item, () => _block.DropItem,
            v => Change(Tr.T("Change dropped item"), nameof(BlockAsset.DropItem), () => _block.DropItem, r => _block.DropItem = r, v));
        Options = new BlockOptionsViewModel(model, ChangeOptions);
        ItemTextureSlot = new TextureSlotViewModel(model, context, nameof(BlockAsset.Options), Tr.T("Item texture"),
            Tr.T("The block itself"), () => model.Options.ItemTextureId, v => model.Options = model.Options with { ItemTextureId = v });
        ParticleTextureSlot = new TextureSlotViewModel(model, context, nameof(BlockAsset.Options), Tr.T("Particle texture"),
            Tr.T("Uses the main texture"), () => model.Options.ParticleTextureId, v => model.Options = model.Options with { ParticleTextureId = v });
        UpdateMainTextureLabel();
    }

    /// <summary>Advanced settings (rotation, water, rendering, redstone, fire, shape, states, ore).</summary>
    public BlockOptionsViewModel Options { get; }

    /// <summary>Texture of the block in the inventory and in hand.</summary>
    public TextureSlotViewModel ItemTextureSlot { get; }

    /// <summary>Texture of the particles when the block breaks.</summary>
    public TextureSlotViewModel ParticleTextureSlot { get; }

    private void ChangeOptions(string description, BlockOptions value) =>
        ChangeIfDifferent(description, nameof(BlockAsset.Options), () => _block.Options, v => _block.Options = v, value);

    // ----- Listes de choix -----

    public IReadOnlyList<Choice<BlockModelKind>> ModelChoices => GameDataChoices.BlockModels;

    public override bool ShowsModelPicker => _block.Model == BlockModelKind.Custom;

    public IReadOnlyList<Choice<BlockRenderType>> RenderTypeChoices => GameDataChoices.RenderTypes;

    public IReadOnlyList<Choice<BlockSoundType>> SoundChoices => GameDataChoices.Sounds;

    public IReadOnlyList<Choice<HarvestTool>> HarvestToolChoices => GameDataChoices.HarvestTools;

    public IReadOnlyList<Choice<ToolTier>> ToolTierChoices => GameDataChoices.ToolTiers;

    public IReadOnlyList<Choice<BlockDropKind>> DropKindChoices => GameDataChoices.DropKinds;

    public double MaxStrength => BlockAsset.MaxStrength;

    public int MaxLightLevel => BlockAsset.MaxLightLevel;

    public int MaxDropCount => BlockAsset.MaxDropCount;

    public int MaxExperience => BlockAsset.MaxExperience;

    // ----- Visuel -----

    public BlockModelKind BlockModel
    {
        get => _block.Model;
        set => ChangeIfDifferent(Tr.T("Change block shape"), nameof(BlockAsset.Model), () => _block.Model, v => _block.Model = v, value);
    }

    public BlockRenderType RenderType
    {
        get => _block.RenderType;
        set => ChangeIfDifferent(Tr.T("Change transparency"), nameof(BlockAsset.RenderType), () => _block.RenderType, v => _block.RenderType = v, value);
    }

    public double LightLevel
    {
        get => _block.LightLevel;
        set => ChangeNumber(Tr.T("Change light level"), nameof(BlockAsset.LightLevel), value, () => _block.LightLevel, v => _block.LightLevel = v,
            v => Math.Clamp((int)Math.Round(v), 0, BlockAsset.MaxLightLevel));
    }

    /// <summary>Emplacements de texture secondaires utiles pour la forme choisie.</summary>
    public IReadOnlyList<TextureSlotViewModel> ExtraTextures => _block.Model switch
    {
        BlockModelKind.CubeFaces => _extraSlots,
        BlockModelKind.Column => [_extraSlots[0]],
        _ => [],
    };

    // ----- Propriétés -----

    public double Hardness
    {
        get => _block.Hardness;
        set => ChangeNumber(Tr.T("Change hardness"), nameof(BlockAsset.Hardness), value, () => _block.Hardness, v => _block.Hardness = v,
            v => (float)Math.Clamp(v, 0d, BlockAsset.MaxStrength));
    }

    public double Resistance
    {
        get => _block.Resistance;
        set => ChangeNumber(Tr.T("Change resistance"), nameof(BlockAsset.Resistance), value, () => _block.Resistance, v => _block.Resistance = v,
            v => (float)Math.Clamp(v, 0d, BlockAsset.MaxStrength));
    }

    public bool Unbreakable
    {
        get => _block.Unbreakable;
        set => ChangeIfDifferent(Tr.T("Change \"unbreakable\""), nameof(BlockAsset.Unbreakable), () => _block.Unbreakable, v => _block.Unbreakable = v, value);
    }

    public BlockSoundType Sound
    {
        get => _block.Sound;
        set => ChangeIfDifferent(Tr.T("Change sound"), nameof(BlockAsset.Sound), () => _block.Sound, v => _block.Sound = v, value);
    }

    public double Friction
    {
        get => _block.Friction;
        set => ChangeNumber(Tr.T("Change slipperiness"), nameof(BlockAsset.Friction), value, () => _block.Friction, v => _block.Friction = v, v => (float)v);
    }

    public double SpeedFactor
    {
        get => _block.SpeedFactor;
        set => ChangeNumber(Tr.T("Change walk speed"), nameof(BlockAsset.SpeedFactor), value, () => _block.SpeedFactor, v => _block.SpeedFactor = v, v => (float)v);
    }

    public double JumpFactor
    {
        get => _block.JumpFactor;
        set => ChangeNumber(Tr.T("Change jump height"), nameof(BlockAsset.JumpFactor), value, () => _block.JumpFactor, v => _block.JumpFactor = v, v => (float)v);
    }

    // ----- Récolte -----

    public HarvestTool HarvestTool
    {
        get => _block.HarvestTool;
        set => ChangeIfDifferent(Tr.T("Change tool"), nameof(BlockAsset.HarvestTool), () => _block.HarvestTool, v => _block.HarvestTool = v, value);
    }

    public ToolTier ToolTier
    {
        get => _block.ToolTier;
        set => ChangeIfDifferent(Tr.T("Change tool level"), nameof(BlockAsset.ToolTier), () => _block.ToolTier, v => _block.ToolTier = v, value);
    }

    public bool RequiresCorrectTool
    {
        get => _block.RequiresCorrectTool;
        set => ChangeIfDifferent(Tr.T("Change \"requires tool\""), nameof(BlockAsset.RequiresCorrectTool), () => _block.RequiresCorrectTool, v => _block.RequiresCorrectTool = v, value);
    }

    public BlockDropKind DropKind
    {
        get => _block.DropKind;
        set => ChangeIfDifferent(Tr.T("Change block drops"), nameof(BlockAsset.DropKind), () => _block.DropKind, v => _block.DropKind = v, value);
    }

    public bool IsDropOtherItem => _block.DropKind == BlockDropKind.OtherItem;

    /// <summary>Item laissé : item du projet ou du jeu.</summary>
    public ReferencePickerViewModel DropItemPicker { get; }

    public double DropMin
    {
        get => _block.DropMin;
        set => ChangeNumber(Tr.T("Change dropped quantity"), nameof(BlockAsset.DropMin), value, () => _block.DropMin, v => _block.DropMin = v,
            v => Math.Clamp((int)Math.Round(v), 1, BlockAsset.MaxDropCount));
    }

    public double DropMax
    {
        get => _block.DropMax;
        set => ChangeNumber(Tr.T("Change dropped quantity"), nameof(BlockAsset.DropMax), value, () => _block.DropMax, v => _block.DropMax = v,
            v => Math.Clamp((int)Math.Round(v), 1, BlockAsset.MaxDropCount));
    }

    public double ExperienceMin
    {
        get => _block.ExperienceMin;
        set => ChangeNumber(Tr.T("Change experience"), nameof(BlockAsset.ExperienceMin), value, () => _block.ExperienceMin, v => _block.ExperienceMin = v,
            v => Math.Clamp((int)Math.Round(v), 0, BlockAsset.MaxExperience));
    }

    public double ExperienceMax
    {
        get => _block.ExperienceMax;
        set => ChangeNumber(Tr.T("Change experience"), nameof(BlockAsset.ExperienceMax), value, () => _block.ExperienceMax, v => _block.ExperienceMax = v,
            v => Math.Clamp((int)Math.Round(v), 0, BlockAsset.MaxExperience));
    }

    // ----- Forme item -----

    public bool HasItemForm
    {
        get => _block.HasItemFormEnabled;
        set => ChangeIfDifferent(Tr.T("Change item form"), nameof(BlockAsset.HasItemFormEnabled), () => _block.HasItemFormEnabled, v => _block.HasItemFormEnabled = v, value);
    }

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        DropItemPicker.Refresh();
        foreach (TextureSlotViewModel slot in _extraSlots)
        {
            slot.Refresh();
        }

        ItemTextureSlot.Refresh();
        ParticleTextureSlot.Refresh();
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(BlockAsset.Model):
                OnPropertyChanged(nameof(BlockModel));
                OnPropertyChanged(nameof(ExtraTextures));
                UpdateMainTextureLabel();
                break;

            case nameof(BlockAsset.DropKind):
                OnPropertyChanged(nameof(IsDropOtherItem));
                break;

            case nameof(BlockAsset.HasItemFormEnabled):
                OnPropertyChanged(nameof(HasItemForm));
                break;

            case nameof(BlockAsset.DropItem):
                DropItemPicker.Refresh();
                break;

            case nameof(BlockAsset.Options):
                Options.Refresh();
                ItemTextureSlot.Refresh();
                ParticleTextureSlot.Refresh();
                break;

            default:
                _extraSlots.FirstOrDefault(s => s.PropertyName == propertyName)?.Refresh();
                break;
        }
    }

    private void UpdateMainTextureLabel() => MainTexture.Label = _block.Model switch
    {
        BlockModelKind.CubeFaces => Tr.T("Default"),
        BlockModelKind.Column => Tr.T("Sides"),
        _ => Tr.T("Texture"),
    };
}
