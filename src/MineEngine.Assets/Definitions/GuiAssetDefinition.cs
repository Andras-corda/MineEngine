using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>GUI (V0.8.0): its size, its options, the editor grid, then its components.</summary>
public sealed class GuiAssetDefinition : AssetDefinition<GuiAsset>
{
    public override AssetType Type => AssetType.Gui;

    public override string Label => Tr.T("GUI");

    public override string DefaultResourceIdBase => "new_gui";

    public override string DefaultDisplayName => Tr.T("New GUI");

    protected override GuiAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(GuiAsset asset, JsonPropertyReader reader)
    {
        asset.Width = reader.GetInt32("width", 176);
        asset.Height = reader.GetInt32("height", 166);
        asset.HasInventory = reader.GetBoolean("inventory", false);
        asset.InventoryOffsetX = reader.GetInt32("inventoryOffsetX", 0);
        asset.InventoryOffsetY = reader.GetInt32("inventoryOffsetY", 0);
        asset.PauseGame = reader.GetBoolean("pause", false);
        asset.RenderBackground = reader.GetBoolean("background", true);
        asset.SnapToGrid = reader.GetBoolean("snap", true);
        asset.GridX = reader.GetInt32("gridX", 2);
        asset.GridY = reader.GetInt32("gridY", 2);
        asset.GridOffsetX = reader.GetInt32("gridOffsetX", 0);
        asset.GridOffsetY = reader.GetInt32("gridOffsetY", 0);
        asset.Components = [.. reader.GetObjectList("components").Select(ReadComponent)];
        asset.Methods = [.. reader.GetObjectList("methods").Select(ItemAssetDefinition.ReadMethod).OfType<ElementMethod>()];
    }

    protected override void WriteProperties(GuiAsset asset, JsonObject properties)
    {
        properties["width"] = asset.Width;
        properties["height"] = asset.Height;
        properties["inventory"] = asset.HasInventory;
        properties["inventoryOffsetX"] = asset.InventoryOffsetX;
        properties["inventoryOffsetY"] = asset.InventoryOffsetY;
        properties["pause"] = asset.PauseGame;
        properties["background"] = asset.RenderBackground;
        properties["snap"] = asset.SnapToGrid;
        properties["gridX"] = asset.GridX;
        properties["gridY"] = asset.GridY;
        properties["gridOffsetX"] = asset.GridOffsetX;
        properties["gridOffsetY"] = asset.GridOffsetY;
        properties["components"] = new JsonArray([.. asset.Components.Select(WriteComponent)]);
        properties["methods"] = new JsonArray([.. asset.Methods.Select(ItemAssetDefinition.WriteMethod)]);
    }

    private static GuiComponent ReadComponent(JsonPropertyReader c) =>
        new GuiComponent(
            c.GetGuid("id") ?? Guid.NewGuid(),
            c.GetEnum("kind", GuiComponentKind.Text),
            c.GetString("name") ?? string.Empty,
            c.GetInt32("x", 0),
            c.GetInt32("y", 0),
            c.GetInt32("width", 1),
            c.GetInt32("height", 1))
        {
            Text = c.GetString("text") ?? string.Empty,
            Color = c.GetString("color") ?? "#404040",
            TextureId = c.GetGuid("texture"),
            Tooltip = c.GetString("tooltip") ?? string.Empty,
            SlotMode = c.GetEnum("slotMode", GuiSlotMode.Input),
            Checked = c.GetBoolean("checked", false),
            MaxLength = c.GetInt32("maxLength", 32),
            IsVariable = c.GetBoolean("variable", false),
            Style = ReadStyle(c.GetObject("style"), c.GetEnum("kind", GuiComponentKind.Text)),
        };

    /// <summary>The look of a component: what the file does not give is the default of its kind.</summary>
    private static GuiStyle ReadStyle(JsonPropertyReader? s, GuiComponentKind kind)
    {
        GuiStyle d = GuiStyle.For(kind);
        return s is null ? d : new GuiStyle
        {
            Horizontal = s.GetEnum("horizontal", d.Horizontal),
            Vertical = s.GetEnum("vertical", d.Vertical),
            Justify = s.GetEnum("justify", d.Justify),
            Padding = s.GetInt32("padding", d.Padding),
            Opacity = s.GetInt32("opacity", d.Opacity),
            Rotation = s.GetInt32("rotation", d.Rotation),
            FontScale = s.GetInt32("fontScale", d.FontScale),
            Wrap = s.GetBoolean("wrap", d.Wrap),
            HoverText = s.GetString("hoverText") ?? d.HoverText,
            HoverColor = s.GetString("hoverColor") ?? d.HoverColor,
            ImageSize = s.GetEnum("imageSize", d.ImageSize),
            Tint = s.GetString("tint") ?? d.Tint,
            Tiling = s.GetBoolean("tiling", d.Tiling),
            Border = s.GetInt32("border", d.Border),
            BorderColor = s.GetString("borderColor") ?? d.BorderColor,
            Blur = s.GetBoolean("blur", d.Blur),
            Highlight = s.GetBoolean("highlight", d.Highlight),
            HoverEffect = s.GetEnum("hoverEffect", d.HoverEffect),
            HoverTint = s.GetString("hoverTint") ?? d.HoverTint,
        };
    }

    /// <summary>Only what differs from the default of the kind; null when nothing does.</summary>
    private static JsonObject? WriteStyle(GuiStyle s, GuiComponentKind kind)
    {
        GuiStyle d = GuiStyle.For(kind);
        var node = new JsonObject();
        void Put<T>(string name, T value, T fallback)
        {
            if (!EqualityComparer<T>.Default.Equals(value, fallback))
            {
                node[name] = value is Enum ? JsonValue.Create(value.ToString()) : JsonValue.Create(value);
            }
        }

        Put("horizontal", s.Horizontal, d.Horizontal);
        Put("vertical", s.Vertical, d.Vertical);
        Put("justify", s.Justify, d.Justify);
        Put("padding", s.Padding, d.Padding);
        Put("opacity", s.Opacity, d.Opacity);
        Put("rotation", s.Rotation, d.Rotation);
        Put("fontScale", s.FontScale, d.FontScale);
        Put("wrap", s.Wrap, d.Wrap);
        Put("hoverText", s.HoverText, d.HoverText);
        Put("hoverColor", s.HoverColor, d.HoverColor);
        Put("imageSize", s.ImageSize, d.ImageSize);
        Put("tint", s.Tint, d.Tint);
        Put("tiling", s.Tiling, d.Tiling);
        Put("border", s.Border, d.Border);
        Put("borderColor", s.BorderColor, d.BorderColor);
        Put("blur", s.Blur, d.Blur);
        Put("highlight", s.Highlight, d.Highlight);
        Put("hoverEffect", s.HoverEffect, d.HoverEffect);
        Put("hoverTint", s.HoverTint, d.HoverTint);
        return node.Count == 0 ? null : node;
    }

    private static JsonNode WriteComponent(GuiComponent c)
    {
        var node = new JsonObject
        {
            ["id"] = c.Id.ToString("D"),
            ["kind"] = c.Kind.ToString(),
            ["name"] = c.Name,
            ["x"] = c.X,
            ["y"] = c.Y,
            ["width"] = c.Width,
            ["height"] = c.Height,
        };

        // Only what the kind uses, so that the file stays short.
        if (c.Kind is GuiComponentKind.Text or GuiComponentKind.Button or GuiComponentKind.Checkbox or GuiComponentKind.TextInput)
        {
            node["text"] = c.Text;
        }

        if (c.Kind is GuiComponentKind.Text or GuiComponentKind.Checkbox)
        {
            node["color"] = c.Color;
        }

        // A button can be an image too (V0.8.1).
        if (c.Kind is GuiComponentKind.Image or GuiComponentKind.Button && c.TextureId is not null)
        {
            node["texture"] = c.TextureId?.ToString("D");
        }

        if (WriteStyle(c.Style, c.Kind) is { } style)
        {
            node["style"] = style;
        }

        if (c.Kind == GuiComponentKind.Slot)
        {
            node["slotMode"] = c.SlotMode.ToString();
        }

        if (c.Kind == GuiComponentKind.Checkbox)
        {
            node["checked"] = c.Checked;
        }

        if (c.Kind == GuiComponentKind.TextInput)
        {
            node["maxLength"] = c.MaxLength;
        }

        if (c.Tooltip.Length > 0)
        {
            node["tooltip"] = c.Tooltip;
        }

        if (c.IsVariable)
        {
            node["variable"] = true;
        }

        return node;
    }
}
