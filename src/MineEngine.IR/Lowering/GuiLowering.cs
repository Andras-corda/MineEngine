using System.Buffers.Binary;
using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Theming;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>
/// GUI (V0.8.0): the components keep their place; the colors become numbers, the images their texture
/// file and its size (read in the PNG), and the player's inventory its place in the GUI.
/// </summary>
public sealed class GuiLowering : AssetLowering<GuiAsset>
{
    public override AssetType AssetType => AssetType.Gui;

    protected override void Lower(GuiAsset asset, LoweringContext context)
    {
        (int inventoryX, int inventoryY) = asset.InventoryOrigin;
        context.AddGui(new IRGui(
            asset.Id, asset.ResourceId, string.IsNullOrWhiteSpace(asset.DisplayName) ? asset.ResourceId.Value : asset.DisplayName,
            asset.Width, asset.Height, asset.HasInventory, inventoryX, inventoryY, asset.PauseGame, asset.RenderBackground,
            [.. asset.Components.Select(c => Component(asset, c, context))])
        {
            Methods = Methods(asset, context),
        });
    }

    /// <summary>
    /// Events of the GUI (V0.8.0): a method in Java keeps its code, a nodal one needs its graph (a procedure).
    /// The method of a component that no longer exists is left out.
    /// </summary>
    private static List<IRElementMethod> Methods(GuiAsset asset, LoweringContext context)
    {
        var methods = new List<IRElementMethod>();
        IReadOnlyList<MethodSlot> slots = GuiMethodSlots.For(asset);
        foreach (ElementMethod method in asset.Methods.DistinctBy(m => m.Slot))
        {
            if (slots.FirstOrDefault(s => s.Key == method.Slot) is not { } slot)
            {
                continue;
            }

            if (method.Language == MethodLanguage.Java)
            {
                methods.Add(new IRElementMethod(slot.Key, slot.JavaName, slot.Return, method.JavaBody, null));
            }
            else if (method.GraphId is { } id && context.Project.Assets.Find(id) is GraphAsset graph && graph.Kind == slot.GraphKind)
            {
                methods.Add(new IRElementMethod(slot.Key, slot.JavaName, slot.Return, null, id));
            }
            else
            {
                context.Diagnostics.Error(Tr.F("The event \"{0}\" of the GUI has no graph.", slot.Title), asset.ToString(), asset.Id);
            }
        }

        return methods;
    }

    private static IRGuiComponent Component(GuiAsset gui, GuiComponent component, LoweringContext context)
    {
        IRTexture? texture = component.Kind is GuiComponentKind.Image or GuiComponentKind.Button && component.TextureId is not null
            ? context.ResolveOptionalTexture(gui, component.TextureId)
            : null;
        (int textureWidth, int textureHeight) = texture is null ? (16, 16) : PngSize(texture);
        return new IRGuiComponent(component.Kind, component.Name, component.X, component.Y, component.Width, component.Height)
        {
            Id = component.Id,
            Style = component.Style,
            Text = component.Text,
            Color = RgbColor.TryParse(component.Color, out RgbColor color) ? color.R << 16 | color.G << 8 | color.B : 0x404040,
            Tooltip = component.Tooltip,
            SlotMode = component.SlotMode,
            Checked = component.Checked,
            MaxLength = component.MaxLength,
            Texture = texture,
            TextureWidth = textureWidth,
            TextureHeight = textureHeight,
        };
    }

    /// <summary>Width and height written in the header of a PNG; 16 x 16 when it cannot be read.</summary>
    public static (int Width, int Height) PngSize(IRTexture texture)
    {
        try
        {
            byte[] header = new byte[24];
            if (texture.Png is { } png)
            {
                if (png.Length < 24)
                {
                    return (16, 16);
                }

                Array.Copy(png, header, 24);
            }
            else if (texture.SourceFile is { } file)
            {
                using FileStream stream = File.OpenRead(file);
                if (stream.Read(header, 0, 24) < 24)
                {
                    return (16, 16);
                }
            }
            else
            {
                return (16, 16);
            }

            int width = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
            int height = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
            return width > 0 && height > 0 ? (width, height) : (16, 16);
        }
        catch (IOException)
        {
            return (16, 16);
        }
    }
}
