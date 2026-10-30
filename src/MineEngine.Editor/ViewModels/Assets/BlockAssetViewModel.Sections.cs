using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.GameData;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Block tab of V0.7.11 (block.md): the boxes that turn a group of fields on (transparency, glow,
/// slipperiness, jump, speed, experience), the experience fixed or in a range, and the conditions of
/// the drops (the one tool, the function). A box that is checked without changing anything stays
/// checked in the editor; unchecking it puts the value of the game back.
/// </summary>
public sealed partial class BlockAssetViewModel
{
    private bool _showGlow;
    private bool _showFriction;
    private bool _showSpeed;
    private bool _showJump;
    private bool _showExperience;
    private bool _experienceRange;

    /// <summary>Transparent kinds of rendering (the solid one is the unchecked box).</summary>
    public IReadOnlyList<Choice<BlockRenderType>> TransparencyChoices => [.. GameDataChoices.RenderTypes.Where(c => c.Value != BlockRenderType.Solid)];

    public bool HasTransparency
    {
        get => _block.RenderType != BlockRenderType.Solid;
        set => RenderType = value ? (_block.RenderType == BlockRenderType.Solid ? BlockRenderType.Cutout : _block.RenderType) : BlockRenderType.Solid;
    }

    /// <summary>The block gives light: its level (luminescence) is then chosen.</summary>
    public bool IsGlowing
    {
        get => _showGlow || _block.LightLevel > 0;
        set => Toggle(ref _showGlow, value, nameof(IsGlowing), () => LightLevel = 0);
    }

    public bool HasSlipperiness
    {
        get => _showFriction || _block.Friction != BlockAsset.DefaultFriction;
        set => Toggle(ref _showFriction, value, nameof(HasSlipperiness), () => Friction = BlockAsset.DefaultFriction);
    }

    public bool HasSpeedFactor
    {
        get => _showSpeed || _block.SpeedFactor != 1f;
        set => Toggle(ref _showSpeed, value, nameof(HasSpeedFactor), () => SpeedFactor = 1);
    }

    public bool HasJumpFactor
    {
        get => _showJump || _block.JumpFactor != 1f;
        set => Toggle(ref _showJump, value, nameof(HasJumpFactor), () => JumpFactor = 1);
    }

    // ----- Experience -----

    public bool DropsExperience
    {
        get => _showExperience || _block.ExperienceMax > 0;
        set => Toggle(ref _showExperience, value, nameof(DropsExperience), () =>
        {
            ExperienceMin = 0;
            ExperienceMax = 0;
        });
    }

    /// <summary>The experience is a range (min and max) instead of a fixed amount.</summary>
    public bool IsExperienceRange
    {
        get => _experienceRange || _block.ExperienceMin != _block.ExperienceMax;
        set
        {
            _experienceRange = value;
            if (!value)
            {
                ExperienceMin = _block.ExperienceMax;
            }

            OnPropertyChanged(nameof(IsExperienceRange));
            OnPropertyChanged(nameof(IsExperienceFixed));
        }
    }

    public bool IsExperienceFixed
    {
        get => !IsExperienceRange;
        set => IsExperienceRange = !value;
    }

    /// <summary>The fixed amount of experience: the minimum and the maximum together.</summary>
    public double ExperienceAmount
    {
        get => _block.ExperienceMax;
        set
        {
            ExperienceMax = value;
            ExperienceMin = value;
        }
    }

    // ----- Conditions of the drops -----

    /// <summary>The one tool that harvests the block (item of the project or of the game); empty: any tool of the type.</summary>
    public ReferencePickerViewModel RequiredToolPicker { get; }

    public Guid? DropCondition
    {
        get => _block.DropCondition;
        set => ChangeIfDifferent(Tr.T("Change the drop condition"), nameof(BlockAsset.DropCondition), () => _block.DropCondition, v => _block.DropCondition = v, value);
    }

    /// <summary>Functions of the project (the position of the block → yes / no).</summary>
    public IReadOnlyList<Choice<Guid?>> FunctionChoices =>
    [
        new(null, Tr.T("None")),
        .. Context.Project.Assets.OfType<GraphAsset>().Where(g => g.Kind == GraphKind.Function && g.ElementId is null)
            .OrderBy(g => g.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new Choice<Guid?>(g.Id, DisplayLabel(g))),
    ];

    /// <summary>A box shown checked; unchecked, the value goes back to the one of the game.</summary>
    private void Toggle(ref bool shown, bool value, string propertyName, Action reset)
    {
        shown = value;
        if (!value)
        {
            reset();
        }

        OnPropertyChanged(propertyName);
    }

    private void OnSectionPropertyChanged(string propertyName)
    {
        string[] derived = propertyName switch
        {
            nameof(BlockAsset.RenderType) => [nameof(HasTransparency)],
            nameof(BlockAsset.LightLevel) => [nameof(IsGlowing)],
            nameof(BlockAsset.Friction) => [nameof(HasSlipperiness)],
            nameof(BlockAsset.SpeedFactor) => [nameof(HasSpeedFactor)],
            nameof(BlockAsset.JumpFactor) => [nameof(HasJumpFactor)],
            nameof(BlockAsset.ExperienceMin) or nameof(BlockAsset.ExperienceMax) =>
                [nameof(DropsExperience), nameof(IsExperienceRange), nameof(IsExperienceFixed), nameof(ExperienceAmount)],
            nameof(BlockAsset.DropCondition) => [nameof(DropCondition)],
            _ => [],
        };
        foreach (string name in derived)
        {
            OnPropertyChanged(name);
        }

        if (propertyName == nameof(BlockAsset.RequiredTool))
        {
            RequiredToolPicker.Refresh();
        }
    }
}
