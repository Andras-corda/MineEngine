namespace MineEngine.Assets;

/// <summary>
/// An element that opens a GUI of the project when a player right-clicks it (V0.8.1): a block (whose
/// slots are kept in the block), an item in the hand, a mob.
/// </summary>
public interface IHasGui
{
    /// <summary>The GUI opened by a right click, or null.</summary>
    Guid? Gui { get; set; }
}
