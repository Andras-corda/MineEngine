using MineEngine.Core.Diagnostics;

namespace MineEngine.Generator;

/// <summary>Résultat d'une génération de projet Minecraft.</summary>
public sealed class GenerationResult
{
    public GenerationResult(string workspaceDirectory, IReadOnlyList<string> files, DiagnosticBag diagnostics)
    {
        WorkspaceDirectory = workspaceDirectory;
        Files = files;
        Diagnostics = diagnostics;
    }

    public string WorkspaceDirectory { get; }

    /// <summary>Fichiers écrits, relatifs au workspace.</summary>
    public IReadOnlyList<string> Files { get; }

    public DiagnosticBag Diagnostics { get; }

    public bool Success => !Diagnostics.HasErrors;
}
