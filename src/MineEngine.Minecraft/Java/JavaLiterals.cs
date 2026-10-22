using System.Globalization;
using MineEngine.Assets.Graphs;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java code of a value typed on an input or given as the starting value of a global variable,
/// for the types of TypesVar.md (char, long, float, byte, identifier, fluid, equipment slot, hand,
/// rarity). The imports the code needs are given to <c>use</c>.
/// </summary>
public static class JavaLiterals
{
    /// <summary>Java expression of <paramref name="text"/> for this type, or null for another type.</summary>
    public static string? Literal(PinType kind, string text, JavaGameApi api, Action<string> use)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(use);
        text = text?.Trim() ?? string.Empty;
        switch (kind)
        {
            case PinType.Char:
                return CharLiteral(text.Length > 0 ? text[0] : ' ');
            case PinType.Long:
                return (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole) ? whole : 0L)
                    .ToString(CultureInfo.InvariantCulture) is var digits && digits.StartsWith('-') ? $"({digits}L)" : digits + "L";
            case PinType.Float:
                double single = GraphValidator.TryParseDecimal(text, out double parsed) ? parsed : 0;
                string number = ((float)single).ToString("R", CultureInfo.InvariantCulture);
                return single < 0 ? $"({number}f)" : number + "f";
            case PinType.Byte:
                return $"((byte) {(sbyte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte small) ? small : 0)})";
            case PinType.Identifier:
                use("net.minecraft.resources.ResourceLocation");
                return api.ParseResourceLocation(JavaNames.StringLiteral(Identifier(text, "minecraft:stone")));
            case PinType.Fluid:
                use("net.minecraft.core.registries.BuiltInRegistries");
                use("net.minecraft.resources.ResourceLocation");
                return $"BuiltInRegistries.FLUID.get({api.ParseResourceLocation(JavaNames.StringLiteral(Identifier(text, "minecraft:water")))})";
            case PinType.EquipmentSlot:
                use("net.minecraft.world.entity.EquipmentSlot");
                return "EquipmentSlot." + Named(kind, text).ToUpperInvariant();
            case PinType.Hand:
                use("net.minecraft.world.InteractionHand");
                return "InteractionHand." + Named(kind, text).ToUpperInvariant();
            case PinType.Rarity:
                use("net.minecraft.world.item.Rarity");
                return "Rarity." + Named(kind, text).ToUpperInvariant();
            default:
                return null;
        }
    }

    /// <summary>'a', '\'', 'é': a Java char literal.</summary>
    public static string CharLiteral(char c) => c switch
    {
        '\'' => "'\\''",
        '\\' => "'\\\\'",
        _ when c < 0x20 || c > 0x7E => $"'\\u{(int)c:x4}'",
        _ => $"'{c}'",
    };

    private static string Identifier(string text, string fallback) =>
        GraphValidator.IsIdentifier(text) ? (text.Contains(':', StringComparison.Ordinal) ? text : "minecraft:" + text) : fallback;

    private static string Named(PinType kind, string text)
    {
        IReadOnlyList<string> names = DataType.NamedValues(kind)!;
        string lower = text.ToLowerInvariant();
        return names.Contains(lower) ? lower : names[0];
    }
}
