using MineEngine.Core.Diagnostics;
using MineEngine.Core.Logging;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Generation;

/// <summary>État partagé par les générateurs de fichiers pendant une génération.</summary>
public sealed class GenerationContext
{
    public GenerationContext(ModIR ir, string workspaceDirectory, MinecraftVersion minecraftVersion, ILog log)
    {
        Ir = ir ?? throw new ArgumentNullException(nameof(ir));
        WorkspaceDirectory = Path.GetFullPath(workspaceDirectory);
        Log = log ?? throw new ArgumentNullException(nameof(log));
        Files = new GeneratedFileWriter(WorkspaceDirectory);
        MinecraftVersion = minecraftVersion ?? throw new ArgumentNullException(nameof(minecraftVersion));
        Paths = new ResourcePaths(ir.Mod.ModId, minecraftVersion);
    }

    public ModIR Ir { get; }

    public IRModInfo Mod => Ir.Mod;

    public string WorkspaceDirectory { get; }

    /// <summary>Version de Minecraft ciblée (influence l'emplacement de certaines ressources).</summary>
    public MinecraftVersion MinecraftVersion { get; }

    public GeneratedFileWriter Files { get; }

    public ResourcePaths Paths { get; }

    public DiagnosticBag Diagnostics { get; } = new();

    public ILog Log { get; }
}
