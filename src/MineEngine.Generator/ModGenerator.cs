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

    /// <summary>Générateur avec les ressources standard : items, blocs, langues anglaise et française.</summary>
    public static ModGenerator CreateDefault(IModLoaderBackend backend)
    {
        var placeholder = new PlaceholderTexture(new PngEncoder());
        return new ModGenerator(backend,
        [
            new ItemResourcesEmitter(placeholder),
            new BlockResourcesEmitter(placeholder),
            new LanguageEmitter(["en_us", "fr_fr"]),
        ]);
    }

    public async Task<GenerationResult> GenerateAsync(
        ModIR ir, string workspaceDirectory, ILog log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(log);

        Directory.CreateDirectory(workspaceDirectory);
        var context = new GenerationContext(ir, workspaceDirectory, Backend.MinecraftVersion, log);

        if (!string.Equals(ir.Mod.LoaderId, Backend.LoaderId, StringComparison.OrdinalIgnoreCase)
            || !MinecraftVersion.TryParse(ir.Mod.MinecraftVersion, out MinecraftVersion? target)
            || !target!.Equals(Backend.MinecraftVersion))
        {
            context.Diagnostics.Error(
                $"Le projet cible {ir.Mod.LoaderId} {ir.Mod.MinecraftVersion}, mais le MDK choisi est {Backend.DisplayName}. " +
                "Choisissez un MDK correspondant dans Projet > Paramètres du projet.");
            return CreateResult(context);
        }

        await Backend.PrepareWorkspaceAsync(context, cancellationToken).ConfigureAwait(false);
        CleanGeneratedSources(context);

        foreach (IFileEmitter emitter in _commonEmitters.Concat(Backend.CreateEmitters()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            log.Debug("Génération : " + emitter.Name);
            emitter.Emit(context);
        }

        return CreateResult(context);
    }

    private void CleanGeneratedSources(GenerationContext context)
    {
        foreach (string root in Backend.GeneratedSourceRoots)
        {
            string directory = context.Files.Resolve(root);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static GenerationResult CreateResult(GenerationContext context) =>
        new(context.WorkspaceDirectory, context.Files.WrittenFiles, context.Diagnostics);
}
