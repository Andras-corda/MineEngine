using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Game rule of the mod (V0.6.3): true or false, or a number, changed with "/gamerule" or in the
/// "Edit game rules" screen of a new world. Graphs read and change it with the game rule nodes,
/// by its name (the identifier).
/// </summary>
public sealed class GameRuleAsset : Asset
{
    private GameRuleKind _kind;
    private GameRuleCategory _category = GameRuleCategory.Misc;
    private bool _defaultBoolean = true;
    private int _defaultNumber;
    private string _description = string.Empty;

    public GameRuleAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.GameRule;

    /// <summary>Name typed after "/gamerule": the identifier.</summary>
    public string RuleName => ResourceId.Value;

    public GameRuleKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public GameRuleCategory Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    public bool DefaultBoolean
    {
        get => _defaultBoolean;
        set => SetField(ref _defaultBoolean, value);
    }

    public int DefaultNumber
    {
        get => _defaultNumber;
        set => SetField(ref _defaultNumber, value);
    }

    /// <summary>Explanation shown under the rule in the "Edit game rules" screen; empty for none.</summary>
    public string Description
    {
        get => _description;
        set => SetField(ref _description, value?.Trim() ?? string.Empty);
    }
}
