using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Tab of the creative inventory (V0.6.2): its title (the display name), the item shown on the
/// tab and the items it contains, in order. Items of the mod or of Minecraft can be listed.
/// </summary>
public sealed class CreativeTabAsset : Asset
{
    private ContentReference? _icon;
    private IReadOnlyList<ContentReference> _items = [];

    public CreativeTabAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.CreativeTab;

    /// <summary>Item shown on the tab; null for the first item of the tab.</summary>
    public ContentReference? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    public IReadOnlyList<ContentReference> Items
    {
        get => _items;
        set
        {
            IReadOnlyList<ContentReference> list = value is null ? [] : [.. value];
            if (!_items.SequenceEqual(list))
            {
                _items = list;
                RaiseChanged(nameof(Items));
            }
        }
    }

    public override IEnumerable<AssetReference> GetReferences()
    {
        if (Icon is { Kind: ContentReferenceKind.Asset } icon)
        {
            yield return new AssetReference(icon.AssetId, AssetReferenceKind.Item, Tr.T("icon of the tab"));
        }

        foreach (ContentReference item in Items.Where(i => i.Kind == ContentReferenceKind.Asset))
        {
            yield return new AssetReference(item.AssetId, AssetReferenceKind.Item, Tr.T("item of the tab"));
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        if (Items.Count == 0)
        {
            diagnostics.Warning(Tr.T("The tab is empty: add items to it."), ToString(), Id);
        }

        if (Items.Any(i => i.Kind == ContentReferenceKind.Tag))
        {
            diagnostics.Error(Tr.T("A creative tab lists items, not tags."), ToString(), Id);
        }
    }
}
