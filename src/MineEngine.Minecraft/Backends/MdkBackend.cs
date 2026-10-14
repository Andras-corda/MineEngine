using MineEngine.Core.IO;
using MineEngine.Core.Logging;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Minecraft.Backends;

/// <summary>
/// Base des backends qui s'appuient sur un MDK installé : les fichiers Gradle du
/// MDK sont recopiés dans le workspace (le dossier du projet), puis ses paramètres
/// sont adaptés au mod. Ils font partie des fichiers générés : verrouillés, ils ne
/// sont plus réécrits (pour ajouter une dépendance dans build.gradle, par exemple).
/// </summary>
public abstract class MdkBackend : IModLoaderBackend
{
    protected const string GradlePropertiesFileName = "gradle.properties";

    /// <summary>Fichier du workspace qui mémorise le MDK utilisé lors de la dernière génération.</summary>
    private const string WorkspaceMarkerFile = ".mineengine/mdk";

    /// <summary>Éléments du MDK jamais recopiés : exemples, documentation, dossiers de travail.</summary>
    private static readonly string[] CommonExclusions =
    [
        "src/main/java", "src/main/resources", ".github", ".gradle", "build", "run", ".idea",
        "README.md", "README.txt", "TEMPLATE_LICENSE.txt", "LICENSE.txt", "CREDITS.txt", "changelog.txt", ".gitignore", ".gitattributes",
        MdkManifestSerializer.FileName, GradlePropertiesFileName,
    ];

    /// <summary>Dossiers de travail de Gradle vidés quand on change de MDK (le reste suit la liste des fichiers générés).</summary>
    private static readonly string[] ClearedOnMdkChange = ["build", ".gradle"];

    protected MdkBackend(MdkDescriptor mdk)
    {
        Mdk = mdk ?? throw new ArgumentNullException(nameof(mdk));
    }

    public abstract string LoaderId { get; }

    public string DisplayName => Mdk.DisplayName;

    public MdkDescriptor Mdk { get; }

    public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

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

        CopyMdkFiles(context);

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
        string marker = context.Files.Resolve(WorkspaceMarkerFile);
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        await File.WriteAllTextAsync(marker, Mdk.Id, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Recopie le projet Gradle du MDK, sans ses exemples ni ses sources.</summary>
    private void CopyMdkFiles(GenerationContext context)
    {
        string[] exclusions = [.. CommonExclusions, .. AdditionalExclusions];
        var pending = new Stack<string>([string.Empty]);
        while (pending.Count > 0)
        {
            string relative = pending.Pop();
            string directory = Path.Combine(Mdk.Directory, relative);
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                string path = (relative.Length == 0 ? string.Empty : relative + "/") + Path.GetFileName(entry);
                if (exclusions.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    pending.Push(path);
                }
                else
                {
                    context.Files.CopyFile(entry, path);
                }
            }
        }
    }

    /// <summary>
    /// Si le workspace a été généré avec un autre MDK, les dossiers de travail de Gradle
    /// sont vidés pour ne pas mélanger deux versions ; les fichiers de l'ancien MDK absents
    /// du nouveau sont ensuite retirés avec les autres fichiers générés devenus inutiles.
    /// </summary>
    private void ResetWorkspaceIfMdkChanged(GenerationContext context)
    {
        string marker = context.Files.Resolve(WorkspaceMarkerFile);
        string? previous = File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
        if (previous is null || string.Equals(previous, Mdk.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.Log.Info($"Le MDK a changé ({previous} vers {Mdk.Id}) : les dossiers de travail de Gradle sont vidés.");
        foreach (string name in ClearedOnMdkChange)
        {
            string directory = Path.Combine(context.WorkspaceDirectory, name);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
