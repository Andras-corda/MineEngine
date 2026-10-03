using MineEngine.Core.Diagnostics;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Generator;

/// <summary>Résultat d'une génération de projet Minecraft.</summary>
public sealed class GenerationResult
{
    public GenerationResult(string workspaceDirectory, IReadOnlyList<string> files, DiagnosticBag diagnostics, SourceMap sourceMap)
    {
        WorkspaceDirectory = workspaceDirectory;
        Files = files;
        Diagnostics = diagnostics;
        SourceMap = sourceMap;
    }

    public string WorkspaceDirectory { get; }

    /// <summary>Fichiers écrits, relatifs au workspace.</summary>
    public IReadOnlyList<string> Files { get; }

    public DiagnosticBag Diagnostics { get; }

    /// <summary>Origine des lignes de code générées, pour relier les erreurs de compilation aux assets.</summary>
    public SourceMap SourceMap { get; }

    public bool Success => !Diagnostics.HasErrors;
}
