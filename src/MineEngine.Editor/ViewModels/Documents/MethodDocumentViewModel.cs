using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>
/// The Java code of a method of an item (V0.7.13): the tab shows the body only, its signature is in
/// the banner. Saving puts the body in the item, as a step that can be undone; the text follows an
/// undo while it is not modified.
/// </summary>
public sealed class MethodDocumentViewModel : CodeDocumentViewModel, IDisposable
{
    private readonly ItemAsset _item;
    private readonly UndoHistory _history;

    public MethodDocumentViewModel(ItemAsset item, MethodSlot slot, string className, UndoHistory history)
        : base(Key(item, slot.Key), $"{className}/{slot.JavaName}.java", ".java")
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _item.Changed += OnItemChanged;
        LoadText(Body ?? string.Empty);
    }

    public MethodSlot Slot { get; }

    public Guid ItemId => _item.Id;

    /// <summary>The method is gone (removed, or made a graph): the tab only shows the last text.</summary>
    public override bool IsReadOnly => Body is null;

    public override string? Banner => IsReadOnly
        ? Tr.T("This method no longer exists in Java: it was removed or is now a graph.")
        : Tr.F("Body of {0} {{ ... }} in the class of the item. Save to put it in the item.", Slot.JavaSignature);

    public override string DetailsTitle => $"{_item.DisplayName} . {Slot.JavaName}";

    public override string DetailsBadge => Tr.T("METHOD");

    private string? Body => _item.FindMethod(Slot.Key) is { Language: MethodLanguage.Java } method ? method.JavaBody : null;

    public static string Key(Asset item, string slot) => $"method:{item.Id:N}:{slot}";

    public override bool Save()
    {
        if (IsReadOnly)
        {
            return false;
        }

        string text = Document.Text;
        IReadOnlyList<ElementMethod> methods = [.. _item.Methods.Select(m => m.Slot == Slot.Key ? m with { JavaBody = text } : m)];
        _history.Execute(new PropertyChangeCommand<IReadOnlyList<ElementMethod>>(
            Tr.F("Edit the method {0}", Slot.JavaName), _item, nameof(ItemAsset.Methods), () => _item.Methods, v => _item.Methods = v, methods));
        _history.Seal();
        MarkSaved();
        return true;
    }

    public void Dispose() => _item.Changed -= OnItemChanged;

    private void OnItemChanged(object? sender, AssetChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ItemAsset.Methods))
        {
            return;
        }

        if (!IsDirty && Body is { } body && body != Document.Text)
        {
            LoadText(body);
        }

        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(Banner));
    }
}
