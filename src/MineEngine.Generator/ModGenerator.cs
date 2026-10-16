using MineEngine.Core.Logging;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Generator;

/// <summary>
/// Transforme un Mod IR en projet Minecraft complet dans un dossier "workspace".
/// Les fichiers communs à tous les loaders (modèles, langues...) sont produits
/// par des générateurs partagés ; le reste est délégué au backend.
/// </summary>
public sealed class ModGenerator
{
    private readonly IReadOnlyList<IFileEmitter> _commonEmitters;

    public ModGenerator(IModLoaderBackend backend, IEnumerable<IFileEmitter> commonEmitters)
    {
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _commonEmitters = [.. commonEmitters];
    }

    public IModLoaderBackend Backend { get; }

    /// <summary>Générateur avec les ressources standard : items, blocs, mobs, recettes, sons, langues du mod.</summary>
    public static ModGenerator CreateDefault(IModLoaderBackend backend)
    {
        var placeholder = new PlaceholderTexture(new PngEncoder());
        return new ModGenerator(backend,
        [
            new ItemResourcesEmitter(placeholder),
            new BlockResourcesEmitter(placeholder),
            new MobResourcesEmitter(placeholder),
            new RecipesEmitter(),
            new SoundsEmitter(),
            new LanguageEmitter(),
            new TagsEmitter(),
        ]);
    }

    /// <summary>
    /// Génère le mod dans le workspace. Les fichiers de <paramref name="lockedFiles"/> (chemins
    /// relatifs au workspace) ne sont pas réécrits ; les fichiers générés la fois précédente
    /// qui ne sont plus produits sont supprimés ; les autres fichiers ne sont jamais touchés.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        ModIR ir, string workspaceDirectory, ILog log, CancellationToken cancellationToken, IEnumerable<string>? lockedFiles = null)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(log);

        Directory.CreateDirectory(workspaceDirectory);
        var context = new GenerationContext(ir, workspaceDirectory, Backend.MinecraftVersion, log, lockedFiles);

        if (!string.Equals(ir.Mod.LoaderId, Backend.LoaderId, StringComparison.OrdinalIgnoreCase)
            || !MinecraftVersion.TryParse(ir.Mod.MinecraftVersion, out MinecraftVersion? target)
            || !target!.Equals(Backend.MinecraftVersion))
        {
            context.Diagnostics.Error(
                Tr.F("Le projet cible {0} {1}, mais le MDK choisi est {2}. ", ir.Mod.LoaderId, ir.Mod.MinecraftVersion, Backend.DisplayName) +
                Tr.T("Choisissez un MDK correspondant dans Espace de travail > Paramètres du projet."));
            return CreateResult(context);
        }

        GeneratedFileManifest previous = GeneratedFileManifest.Load(context.WorkspaceDirectory);
        await Backend.PrepareWorkspaceAsync(context, cancellationToken).ConfigureAwait(false);

        foreach (IFileEmitter emitter in _commonEmitters.Concat(Backend.CreateEmitters()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            log.Debug(Tr.T("Génération : ") + emitter.Name);
            emitter.Emit(context);
        }

        var current = new GeneratedFileManifest(context.Files.WrittenFiles.Concat(context.Files.KeptLockedFiles));
        RemoveStaleFiles(context, previous, current);
        current.Save(context.WorkspaceDirectory);
        return CreateResult(context);
    }

    /// <summary>Supprime ce qui avait été généré la fois précédente et ne l'est plus (asset supprimé, renommé...).</summary>
    private static void RemoveStaleFiles(GenerationContext context, GeneratedFileManifest previous, GeneratedFileManifest current)
    {
        foreach (string stale in previous.Files.Where(f => !current.Contains(f) && !context.Files.IsLocked(f)))
        {
            string file = context.Files.Resolve(stale);
            if (!File.Exists(file))
            {
                continue;
            }

            File.Delete(file);
            context.Log.Debug(Tr.T("Fichier généré supprimé : ") + stale);

            // Les dossiers devenus vides disparaissent aussi (package d'un graphe supprimé...).
            for (string? directory = Path.GetDirectoryName(file);
                 directory is not null && directory.Length > context.WorkspaceDirectory.Length
                 && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any();
                 directory = Path.GetDirectoryName(directory))
            {
                Directory.Delete(directory);
            }
        }
    }

    private static GenerationResult CreateResult(GenerationContext context) =>
        new(context.WorkspaceDirectory, context.Files.WrittenFiles, context.Diagnostics, context.SourceMap);
}
