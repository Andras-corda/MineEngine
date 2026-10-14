using MineEngine.Core.Identifiers;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Minecraft.Java;

/// <summary>Sortes de classes Java proposées à la création (équivalent des "Custom elements" de MCreator).</summary>
public enum JavaClassKind
{
    /// <summary>Classe abonnée aux événements du jeu : ses méthodes @SubscribeEvent sont appelées sans autre réglage.</summary>
    EventListener,

    /// <summary>Classe de méthodes statiques utilitaires.</summary>
    Utility,

    /// <summary>Classe vide.</summary>
    Empty,
}

/// <summary>
/// Contenu des classes Java écrites par l'utilisateur. Elles sont rangées dans src/main/java
/// avec le code généré et compilées avec lui ; Mine Engine ne les réécrit jamais.
/// </summary>
public static class JavaClassTemplates
{
    /// <summary>Package proposé pour le code de l'utilisateur ("com.mineengine.mods.ruby.custom").</summary>
    public static string DefaultPackage(ModId modId) => $"{JavaModNaming.BasePackage}.{JavaNames.ToPackageSegment(modId)}.custom";

    /// <summary>Chemin relatif au projet du fichier d'une classe ("src/main/java/com/.../Foo.java").</summary>
    public static string SourcePath(string package, string className) =>
        $"src/main/java/{package.Replace('.', '/')}/{className}.java";

    /// <summary>Package déduit d'un dossier situé sous src/main/java ("src/main/java/a/b" donne "a.b").</summary>
    public static string? PackageForFolder(string projectRelativeFolder)
    {
        const string root = "src/main/java";
        string folder = projectRelativeFolder.Replace('\\', '/').Trim('/');
        if (string.Equals(folder, root, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return folder.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)
            ? folder[(root.Length + 1)..].Replace('/', '.')
            : null;
    }

    public static bool IsValidClassName(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsLetterOrDigit(c) || c == '_')
        && !JavaNames.IsKeyword(name);

    public static string Create(JavaClassKind kind, string package, string className, ModId modId, ModLoaderKind loader)
    {
        string header = package.Length == 0 ? string.Empty : $"package {package};\n\n";
        return header + kind switch
        {
            JavaClassKind.EventListener => EventListener(className, modId, loader),
            JavaClassKind.Utility => Utility(className),
            _ => $"public class {className} {{\n}}\n",
        };
    }

    private static string EventListener(string className, ModId modId, ModLoaderKind loader)
    {
        (string[] imports, string annotation) = loader == ModLoaderKind.NeoForge
            ? (new[]
            {
                "net.minecraft.network.chat.Component",
                "net.neoforged.bus.api.SubscribeEvent",
                "net.neoforged.fml.common.EventBusSubscriber",
                "net.neoforged.neoforge.event.entity.player.PlayerEvent",
            }, $"@EventBusSubscriber(modid = {JavaNames.StringLiteral(modId.Value)})")
            : (new[]
            {
                "net.minecraft.network.chat.Component",
                "net.minecraftforge.event.entity.player.PlayerEvent",
                "net.minecraftforge.eventbus.api.SubscribeEvent",
                "net.minecraftforge.fml.common.Mod",
            }, $"@Mod.EventBusSubscriber(modid = {JavaNames.StringLiteral(modId.Value)})");

        return string.Concat(imports.Select(i => $"import {i};\n")) + $$"""

            /**
             * Réagit aux événements du jeu. Chaque méthode statique annotée @SubscribeEvent
             * est appelée quand l'événement de son paramètre se produit.
             */
            {{annotation}}
            public final class {{className}} {
                private {{className}}() {
                }

                @SubscribeEvent
                public static void onPlayerLoggedIn(PlayerEvent.PlayerLoggedInEvent event) {
                    event.getEntity().sendSystemMessage(Component.literal("Bienvenue !"));
                }
            }

            """;
    }

    private static string Utility(string className) => $$"""
        /** Méthodes utilitaires du mod. */
        public final class {{className}} {
            private {{className}}() {
            }

            public static int clamp(int value, int min, int max) {
                return Math.max(min, Math.min(max, value));
            }
        }

        """;
}
