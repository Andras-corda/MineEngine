using MineEngine.Assets;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Diagnostics;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Inspector of a Sparkle particle system: name, identifier played by the graphs, summary (emitters,
/// particles at once, problems). The system itself is edited in Sparkle, opened in a tab.
/// </summary>
public sealed class SparkleAssetViewModel : AssetViewModel
{
    public SparkleAssetViewModel(SparkleAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    private SparkleAsset System => (SparkleAsset)Model;

    public override string TypeIcon => "SparkleGeometry";

    /// <summary>Short label for the Dashboard badge.</summary>
    public override string TypeLabel => Tr.T("Particles");

    public int EmitterCount => System.Emitters.Count;

    public string PerformanceSummary =>
        SparklePerformance.Estimate(System.Emitters).Aggregate(new SparklePerformance(0, 0, 0), (a, b) => a + b).Summary;

    public IReadOnlyList<string> Problems
    {
        get
        {
            var diagnostics = new DiagnosticBag();
            System.Validate(diagnostics);
            return [.. diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info).Select(d => d.Message)];
        }
    }

    public bool HasProblems => Problems.Count > 0;

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(SparkleAsset.Emitters))
        {
            OnPropertyChanged(nameof(EmitterCount));
            OnPropertyChanged(nameof(PerformanceSummary));
            OnPropertyChanged(nameof(Problems));
            OnPropertyChanged(nameof(HasProblems));
        }
    }
}
