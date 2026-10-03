using System.Globalization;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Différences de l'API Java de Minecraft selon la version (indépendantes du loader) :
/// builder de nourriture, signature de l'info-bulle, construction des blocs à expérience,
/// création des ResourceLocation, mise à feu et placement des apparitions.
/// </summary>
public abstract class JavaGameApi
{
    public static JavaGameApi For(MinecraftVersion version) =>
        version.IsAtLeast("1.20.5") ? new ModernJavaGameApi() : new LegacyJavaGameApi();

    /// <summary>Vrai si la viande est déclarée par le tag minecraft:meat (et non par le builder).</summary>
    public abstract bool MeatUsesItemTag { get; }

    /// <summary>Imports nécessaires à la redéfinition de l'info-bulle.</summary>
    public abstract IReadOnlyList<string> TooltipImports { get; }

    /// <summary>Expression Java "new FoodProperties.Builder()...build()".</summary>
    public abstract string FoodProperties(IRFood food);

    /// <summary>Signature de la méthode d'info-bulle d'un item.</summary>
    public abstract string TooltipMethodSignature { get; }

    /// <summary>Appel à la méthode parente de l'info-bulle.</summary>
    public abstract string TooltipSuperCall { get; }

    /// <summary>Début et fin de l'appel au constructeur d'un bloc à expérience, autour de ses propriétés.</summary>
    public abstract (string Head, string Tail) ExperienceBlockConstruction(int min, int max);

    /// <summary>Expression qui crée un ResourceLocation à partir d'un texte "espace:chemin".</summary>
    public abstract string ParseResourceLocation(string textExpression);

    /// <summary>Instruction qui enflamme l'entité courante pour un nombre de secondes.</summary>
    public abstract string IgniteSelf(int seconds);

    /// <summary>Type de placement "au sol" des règles d'apparition, et son import.</summary>
    public abstract (string Expression, string Import) OnGroundSpawnPlacement { get; }

    protected static string Float(float value) => JavaNames.FloatLiteral(value);

    protected static string Experience(int min, int max) =>
        string.Create(CultureInfo.InvariantCulture, $"UniformInt.of({min}, {max})");
}

/// <summary>Minecraft 1.20 à 1.20.4.</summary>
public sealed class LegacyJavaGameApi : JavaGameApi
{
    public override bool MeatUsesItemTag => false;

    public override IReadOnlyList<string> TooltipImports { get; } =
    [
        "java.util.List",
        "net.minecraft.network.chat.Component",
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.TooltipFlag",
        "net.minecraft.world.level.Level",
    ];

    public override string TooltipMethodSignature =>
        "public void appendHoverText(ItemStack stack, Level level, List<Component> tooltip, TooltipFlag flag)";

    public override string TooltipSuperCall => "super.appendHoverText(stack, level, tooltip, flag);";

    public override string FoodProperties(IRFood food) =>
        $"new FoodProperties.Builder().nutrition({food.Nutrition}).saturationMod({Float(food.Saturation)})"
        + (food.IsMeat ? ".meat()" : string.Empty)
        + (food.AlwaysEdible ? ".alwaysEat()" : string.Empty)
        + ".build()";

    public override (string Head, string Tail) ExperienceBlockConstruction(int min, int max) =>
        ("new DropExperienceBlock(", $", {Experience(min, max)})");

    public override string ParseResourceLocation(string textExpression) => $"new ResourceLocation({textExpression})";

    public override string IgniteSelf(int seconds) => $"this.setSecondsOnFire({seconds});";

    public override (string Expression, string Import) OnGroundSpawnPlacement =>
        ("SpawnPlacements.Type.ON_GROUND", "net.minecraft.world.entity.SpawnPlacements");
}

/// <summary>Minecraft 1.20.5 et plus (dont 1.21.1).</summary>
public sealed class ModernJavaGameApi : JavaGameApi
{
    public override bool MeatUsesItemTag => true;

    public override IReadOnlyList<string> TooltipImports { get; } =
    [
        "java.util.List",
        "net.minecraft.network.chat.Component",
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.TooltipFlag",
    ];

    public override string TooltipMethodSignature =>
        "public void appendHoverText(ItemStack stack, Item.TooltipContext context, List<Component> tooltip, TooltipFlag flag)";

    public override string TooltipSuperCall => "super.appendHoverText(stack, context, tooltip, flag);";

    public override string FoodProperties(IRFood food) =>
        $"new FoodProperties.Builder().nutrition({food.Nutrition}).saturationModifier({Float(food.Saturation)})"
        + (food.AlwaysEdible ? ".alwaysEdible()" : string.Empty)
        + ".build()";

    public override (string Head, string Tail) ExperienceBlockConstruction(int min, int max) =>
        ($"new DropExperienceBlock({Experience(min, max)}, ", ")");

    public override string ParseResourceLocation(string textExpression) => $"ResourceLocation.parse({textExpression})";

    public override string IgniteSelf(int seconds) => $"this.igniteForSeconds({seconds});";

    public override (string Expression, string Import) OnGroundSpawnPlacement =>
        ("SpawnPlacementTypes.ON_GROUND", "net.minecraft.world.entity.SpawnPlacementTypes");
}
