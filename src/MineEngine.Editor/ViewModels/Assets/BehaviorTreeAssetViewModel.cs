using MineEngine.Assets;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Diagnostics;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Inspector of a behavior tree: name, identifier, summary (nodes, keys, problems);
/// the mobs that follow it appear in "Used by". The tree itself is edited in Puppeteer, opened in a tab.
/// </summary>
public sealed class BehaviorTreeAssetViewModel : AssetViewModel
{
    public BehaviorTreeAssetViewModel(BehaviorTreeAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    private BehaviorTreeAsset Tree => (BehaviorTreeAsset)Model;

    /// <summary>The tree does not exist in game under its identifier: it gives the name of the generated Java class.</summary>
    public override bool HasGameId => false;

    public override string TypeIcon => "BehaviorTreeGeometry";

    /// <summary>Short label for the Dashboard badge.</summary>
    public override string TypeLabel => Tr.T("AI tree");

    /// <summary>Nodes of the tree, root excluded.</summary>
    public int NodeCount => Tree.Tree.Nodes.Length - 1;

    public int TaskCount => Tree.Tree.Nodes.Count(n => n.Info.Category == BtCategory.Task);

    public int KeyCount => Tree.Keys.Count;

    public IReadOnlyList<string> Problems =>
        [.. BehaviorTreeValidator.Validate(Tree, id => Context.Project.Assets.Find(id) as BehaviorTreeAsset).Where(i => i.Severity != DiagnosticSeverity.Info).Select(i => i.Message)];

    public bool HasProblems => Problems.Count > 0;

    public string JavaClassName => JavaModNaming.BehaviorTreeClassName(Model.ResourceId);

    /// <summary>The tree can be run by other trees ("Run a goal").</summary>
    public bool IsPublicGoal
    {
        get => Tree.IsPublicGoal;
        set => ChangeIfDifferent(value ? Tr.T("Make the tree a public goal") : Tr.T("Make the tree private"),
            nameof(BehaviorTreeAsset.IsPublicGoal), () => Tree.IsPublicGoal, v => Tree.IsPublicGoal = v, value);
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(BehaviorTreeAsset.Tree) or nameof(BehaviorTreeAsset.Keys):
                OnPropertyChanged(nameof(NodeCount));
                OnPropertyChanged(nameof(TaskCount));
                OnPropertyChanged(nameof(KeyCount));
                OnPropertyChanged(nameof(Problems));
                OnPropertyChanged(nameof(HasProblems));
                break;
            case nameof(BehaviorTreeAsset.IsPublicGoal):
                OnPropertyChanged(nameof(IsPublicGoal));
                break;
            case nameof(BehaviorTreeAsset.ResourceId):
                OnPropertyChanged(nameof(JavaClassName));
                break;
        }
    }
}
