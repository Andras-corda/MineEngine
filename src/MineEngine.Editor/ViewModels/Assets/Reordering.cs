namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>A row of a list that the user can drag to another place (see Views.RowReorder).</summary>
public interface IReorderableRow
{
    /// <summary>Place of the row in its list, from 0.</summary>
    int Index { get; }
}

/// <summary>A list whose rows can be dragged to another place.</summary>
public interface IReorderableList
{
    void MoveItem(int from, int to);
}
