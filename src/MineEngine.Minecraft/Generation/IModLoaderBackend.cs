using MineEngine.IR.Model;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Minecraft.Generation;

/// <summary>
/// Tout ce qui est propre à un loader et à une version de Minecraft : structure
/// du projet Gradle (fournie par un MDK), code Java d'enregistrement, tâches de
/// build. Le reste de Mine Engine ne dépend que de cette interface.
/// </summary>
public interface IModLoaderBackend
{
    /// <summary>Identifiant du loader enregistré dans les projets ("forge", "neoforge").</summary>
    string LoaderId { get; }

    string DisplayName { get; }

    /// <summary>MDK installé sur lequel s'appuie ce backend.</summary>
    MdkDescriptor Mdk { get; }

    MinecraftVersion MinecraftVersion { get; }

    /// <summary>Dossiers du workspace entièrement réécrits à chaque génération.</summary>
    IReadOnlyList<string> GeneratedSourceRoots { get; }

    /// <summary>Tâches Gradle qui produisent le .jar du mod.</summary>
    IReadOnlyList<string> BuildTasks { get; }

    /// <summary>Tâches Gradle qui lancent Minecraft avec le mod.</summary>
    IReadOnlyList<string> RunClientTasks { get; }

    /// <summary>Installe ou met à jour les fichiers de build du workspace à partir du MDK.</summary>
    Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken);

    /// <summary>Générateurs de fichiers propres au loader (code Java, métadonnées...).</summary>
    IReadOnlyList<IFileEmitter> CreateEmitters();

    /// <summary>Chemin du .jar produit par <see cref="BuildTasks"/>.</summary>
    string GetOutputJarPath(string workspaceDirectory, IRModInfo mod);
}

/// <summary>Fabrique de backends : sait si un MDK est pris en charge et crée le backend adapté.</summary>
public interface IModLoaderBackendProvider
{
    /// <summary>Description des MDK pris en charge, affichée à l'utilisateur.</summary>
    string SupportDescription { get; }

    bool Supports(MdkDescriptor mdk);

    IModLoaderBackend Create(MdkDescriptor mdk);
}
