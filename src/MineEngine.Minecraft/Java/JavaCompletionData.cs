using MineEngine.Minecraft.Mdk;

namespace MineEngine.Minecraft.Java;

/// <summary>Sorte d'entrée proposée par l'auto-complétion.</summary>
public enum JavaCompletionKind
{
    Keyword,
    Type,
    Snippet,
}

/// <summary>
/// Une proposition d'auto-complétion. Un type porte son nom complet : l'accepter ajoute
/// l'import manquant. Un extrait remplace le mot tapé par <see cref="InsertText"/>.
/// </summary>
public sealed record JavaCompletionEntry(string Label, JavaCompletionKind Kind, string Description, string InsertText, string? Import = null);

/// <summary>
/// Ce que l'éditeur Java propose sans analyser le code : mots-clés, extraits (méthode
/// d'événement, boucle...), classes Minecraft courantes selon le loader, et classes
/// générées du mod (ModItems, ModBlocks...).
/// </summary>
public static class JavaCompletionData
{
    private static readonly string[] Keywords =
    [
        "abstract", "boolean", "break", "byte", "case", "catch", "char", "class", "continue", "default", "do", "double",
        "else", "enum", "extends", "false", "final", "finally", "float", "for", "if", "implements", "import", "instanceof",
        "int", "interface", "long", "new", "null", "package", "private", "protected", "public", "record", "return",
        "short", "static", "super", "switch", "this", "throw", "throws", "true", "try", "var", "void", "while",
    ];

    /// <summary>Classes de Minecraft communes à Forge et NeoForge (mêmes noms en 1.20.1 et 1.21.1).</summary>
    private static readonly string[] MinecraftTypes =
    [
        "net.minecraft.core.BlockPos",
        "net.minecraft.core.Direction",
        "net.minecraft.network.chat.Component",
        "net.minecraft.resources.ResourceLocation",
        "net.minecraft.server.level.ServerLevel",
        "net.minecraft.server.level.ServerPlayer",
        "net.minecraft.sounds.SoundEvents",
        "net.minecraft.sounds.SoundSource",
        "net.minecraft.util.RandomSource",
        "net.minecraft.world.InteractionHand",
        "net.minecraft.world.InteractionResult",
        "net.minecraft.world.effect.MobEffectInstance",
        "net.minecraft.world.effect.MobEffects",
        "net.minecraft.world.entity.Entity",
        "net.minecraft.world.entity.EntityType",
        "net.minecraft.world.entity.LivingEntity",
        "net.minecraft.world.entity.item.ItemEntity",
        "net.minecraft.world.entity.player.Player",
        "net.minecraft.world.item.Item",
        "net.minecraft.world.item.ItemStack",
        "net.minecraft.world.item.Items",
        "net.minecraft.world.item.context.UseOnContext",
        "net.minecraft.world.level.Level",
        "net.minecraft.world.level.block.Block",
        "net.minecraft.world.level.block.Blocks",
        "net.minecraft.world.level.block.state.BlockState",
        "net.minecraft.world.phys.Vec3",
    ];

    private static readonly string[] ForgeTypes =
    [
        "net.minecraftforge.common.MinecraftForge",
        "net.minecraftforge.event.TickEvent",
        "net.minecraftforge.event.entity.living.LivingDeathEvent",
        "net.minecraftforge.event.entity.living.LivingHurtEvent",
        "net.minecraftforge.event.entity.player.PlayerEvent",
        "net.minecraftforge.event.entity.player.PlayerInteractEvent",
        "net.minecraftforge.event.level.BlockEvent",
        "net.minecraftforge.eventbus.api.SubscribeEvent",
        "net.minecraftforge.fml.common.Mod",
    ];

    private static readonly string[] NeoForgeTypes =
    [
        "net.neoforged.bus.api.SubscribeEvent",
        "net.neoforged.fml.common.EventBusSubscriber",
        "net.neoforged.neoforge.common.NeoForge",
        "net.neoforged.neoforge.event.entity.living.LivingDeathEvent",
        "net.neoforged.neoforge.event.entity.living.LivingIncomingDamageEvent",
        "net.neoforged.neoforge.event.entity.player.PlayerEvent",
        "net.neoforged.neoforge.event.entity.player.PlayerInteractEvent",
        "net.neoforged.neoforge.event.level.BlockEvent",
        "net.neoforged.neoforge.event.tick.PlayerTickEvent",
    ];

    private static readonly JavaCompletionEntry[] Snippets =
    [
        new("sout", JavaCompletionKind.Snippet, "Écrit une ligne dans la console", "System.out.println();"),
        new("fori", JavaCompletionKind.Snippet, "Boucle for avec un compteur", "for (int i = 0; i < count; i++) {\n}"),
        new("subscribe", JavaCompletionKind.Snippet, "Méthode appelée par un événement du jeu",
            "@SubscribeEvent\npublic static void onEvent(PlayerEvent.PlayerLoggedInEvent event) {\n}"),
        new("psvm", JavaCompletionKind.Snippet, "Méthode statique publique", "public static void method() {\n}"),
    ];

    /// <summary>Toutes les propositions pour un loader ; <paramref name="modTypes"/> : classes générées du mod (noms complets).</summary>
    public static IReadOnlyList<JavaCompletionEntry> For(ModLoaderKind loader, IEnumerable<string> modTypes)
    {
        IEnumerable<string> loaderTypes = loader == ModLoaderKind.NeoForge ? NeoForgeTypes : ForgeTypes;
        IEnumerable<JavaCompletionEntry> types = MinecraftTypes.Concat(loaderTypes).Concat(modTypes ?? [])
            .Distinct(StringComparer.Ordinal)
            .Select(TypeEntry);
        return
        [
            .. Keywords.Select(k => new JavaCompletionEntry(k, JavaCompletionKind.Keyword, "Mot-clé Java", k)),
            .. types,
            .. Snippets,
        ];
    }

    /// <summary>Classes générées pour un mod : registres et classe principale.</summary>
    public static IEnumerable<string> ModTypes(JavaModNaming naming) =>
    [
        $"{naming.RootPackage}.{naming.MainClass}",
        $"{naming.RegistryPackage}.{naming.ItemsClass}",
        $"{naming.RegistryPackage}.{naming.BlocksClass}",
        $"{naming.RegistryPackage}.{naming.CreativeTabsClass}",
    ];

    private static JavaCompletionEntry TypeEntry(string fullName)
    {
        string simple = fullName[(fullName.LastIndexOf('.') + 1)..];
        return new JavaCompletionEntry(simple, JavaCompletionKind.Type, fullName, simple, fullName);
    }
}
