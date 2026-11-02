using MineEngine.Assets;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Properties of a GUI (V0.8.0): name, identifier, summary (size, components, inventory). The GUI
/// itself is edited in its tab, opened with a double-click or the button of the Properties.
/// </summary>
public sealed class GuiAssetViewModel : AssetViewModel
{
    public GuiAssetViewModel(GuiAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    private GuiAsset Gui => (GuiAsset)Model;

    /// <summary>"176 x 166, 5 components, with the player's inventory".</summary>
    public string Summary =>
        Tr.F("{0} x {1}, {2} component(s)", Gui.Width, Gui.Height, Gui.Components.Count)
        + (Gui.HasInventory ? Tr.T(", with the player's inventory") : string.Empty)
        + (Gui.PauseGame ? Tr.T(", pauses the game") : string.Empty);

    public override IReadOnlyList<SummaryLine> SummaryLines => [new(Tr.T("GUI"), Summary)];

    /// <summary>Events of the GUI and of its components, to open or create them from the Dashboard.</summary>
    public IReadOnlyList<ElementMethodViewModel> Events =>
        [.. GuiMethodSlots.For(Gui).Select(slot => new ElementMethodViewModel(Gui, Context, slot, () => Gui.Methods, v => Gui.Methods = v))];

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(SummaryLines));
    }
}
