using MineEngine.Core.IO;
using MineEngine.Core.Logging;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Minecraft.Backends;

/// <summary>
/// Base des backends qui s'appuient sur un MDK installé : le projet Gradle du
/// MDK est recopié dans le workspace, puis ses paramètres sont adaptés au mod.
/// </summary>
public abstract class MdkBackend : IModLoaderBackend
{
    protected const string GradlePropertiesFileName = "gradle.properties";

    /// <summary>Fichier du workspace qui mémorise le MDK utilisé lors de la dernière génération.</summary>
    private const string WorkspaceMarkerFile = ".mineengine-mdk";

    /// <summary>Éléments du MDK jamais recopiés : exemples, documentation, dossiers de travail.</summary>
    private static readonly string[] CommonExclusions =
    [
        "src/main/java", "src/main/resources", ".github", ".gradle", "build", "run", ".idea",
        "README.md", "README.txt", "TEMPLATE_LICENSE.txt", "LICENSE.txt", "CREDITS.txt", "changelog.txt",
        MdkManifestSerializer.FileName, GradlePropertiesFileName,
    ];

    /// <summary>Éléments du workspace conservés quand on change de MDK (mondes et réglages du jeu).</summary>
    private static readonly string[] PreservedOnMdkChange = ["run"];

    protected MdkBackend(MdkDescriptor mdk)
    {
        Mdk = mdk ?? throw new ArgumentNullException(nameof(mdk));
    }

    public abstract string LoaderId { get; }

    public string DisplayName => Mdk.DisplayName;

    public MdkDescriptor Mdk { get; }

    public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

    public IReadOnlyList<string> GeneratedSourceRoots { get; } = ["src/main/java", "src/main/resources"];

    public virtual IReadOnlyList<string> BuildTasks { get; } = ["build"];

    public virtual IReadOnlyList<string> RunClientTasks { get; } = ["runClient"];

    /// <summary>Fichiers supplémentaires du MDK que ce backend réécrit lui-même.</summary>
    protected virtual IReadOnlyList<string> AdditionalExclusions { get; } = [];

    public async Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Directory.Exists(Mdk.Directory))
        {
            throw new DirectoryNotFoundException(
                $"Le MDK {Mdk.DisplayName} est introuvable ({Mdk.Directory}). Réinstallez-le depuis le gestionnaire de MDK.");
        }

        ResetWorkspaceIfMdkChanged(context);

        string[] exclusions = [.. CommonExclusions, .. AdditionalExclusions];
        new DirectoryCopier(path => !exclusions.Contains(path, StringComparer.OrdinalIgnoreCase))
            .Copy(Mdk.Directory, context.WorkspaceDirectory, overwrite: true);

        IRModInfo mod = context.Mod;
        var properties = new GradlePropertiesFile(await ReadMdkFileAsync(GradlePropertiesFileName, cancellationToken).ConfigureAwait(false));
        properties.Set("mod_id", mod.ModId.Value);
        properties.Set("mod_name", mod.Name.Replace('"', '\''));
        properties.Set("mod_license", string.IsNullOrWhiteSpace(mod.License) ? "All Rights Reserved" : mod.License.Replace('"', '\''));
        properties.Set("mod_version", mod.Version);
        properties.Set("mod_group_id", new JavaModNaming(mod).RootPackage);

        // Sans cela, Gradle lancé avec Java 17 sous Windows écrit mods.toml en Windows-1252
        // et les accents du nom ou de la description sont corrompus dans le mod.
        properties.AddJvmArgument("-Dfile.encoding=UTF-8");
        ConfigureProperties(properties, mod);
        context.Files.WriteText(GradlePropertiesFileName, properties.ToString());

        await ConfigureWorkspaceAsync(context, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(context.WorkspaceDirectory, WorkspaceMarkerFile), Mdk.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public abstract IReadOnlyList<IFileEmitter> CreateEmitters();

    public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) =>
        Path.Combine(workspaceDirectory, "build", "libs", $"{mod.ModId}-{mod.Version}.jar");

    /// <summary>Réglages propres au loader dans gradle.properties.</summary>
    protected virtual void ConfigureProperties(GradlePropertiesFile properties, IRModInfo mod)
    {
    }

    /// <summary>Adaptations supplémentaires du workspace (fichiers de métadonnées...).</summary>
    protected virtual Task ConfigureWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected Task<string> ReadMdkFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        string file = Path.Combine(Mdk.Directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"Le MDK {Mdk.DisplayName} ne contient pas le fichier attendu '{relativePath}'.", file);
        }

        return File.ReadAllTextAsync(file, cancellationToken);
    }

    /// <summary>
    /// Si le workspace a été généré avec un autre MDK, il est vidé (sauf le dossier
    /// run/ qui contient les mondes de test) pour ne pas mélanger deux projets Gradle.
    /// </summary>
    private void ResetWorkspaceIfMdkChanged(GenerationContext context)
    {
        string marker = Path.Combine(context.WorkspaceDirectory, WorkspaceMarkerFile);
        string? previous = File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
        if (previous is null || string.Equals(previous, Mdk.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Log.Info($"Le MDK a changé ({previous} vers {Mdk.Id}) : le dossier Generated est réinitialisé.");
        foreach (string entry in Directory.EnumerateFileSystemEntries(context.WorkspaceDirectory))
        {
            if (PreservedOnMdkChange.Contains(Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }
    }
}
