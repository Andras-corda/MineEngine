using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.NeoForge;

/// <summary>
/// Combustibles pour NeoForge 1.21 : la durée de combustion n'est plus une méthode de
/// l'item mais une "data map" (data/neoforge/data_maps/item/furnace_fuels.json).
/// </summary>
public sealed class FuelDataMapEmitter : IFileEmitter
{
    public string Name => Tr.T("Combustibles (data map NeoForge)");

    public void Emit(GenerationContext context)
    {
        List<IRItem> fuels = [.. context.Ir.Content.Items.Where(i => i.BurnTime > 0)];
        if (fuels.Count == 0)
        {
            return;
        }

        var values = new JsonObject();
        foreach (IRItem item in fuels)
        {
            values[context.Paths.Reference(item.Id.Value)] = new JsonObject { ["burn_time"] = item.BurnTime };
        }

        context.Files.WriteText(context.Paths.NeoForgeItemDataMap("furnace_fuels"), JsonFormatting.ToText(new JsonObject { ["values"] = values }));
    }
}
