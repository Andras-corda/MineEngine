using MineEngine.Core.Diagnostics;

namespace MineEngine.Build;

/// <summary>Résultat d'un build (ou d'un lancement) de mod.</summary>
public sealed class BuildResult
{
    private BuildResult(bool success, string? jarPath, DiagnosticBag diagnostics, TimeSpan duration)
    {
        Success = success;
        JarPath = jarPath;
        Diagnostics = diagnostics;
        Duration = duration;
    }

    public bool Success { get; }

    /// <summary>Chemin du .jar copié dans le dossier Build/ du projet, si le build a réussi.</summary>
    public string? JarPath { get; }

    public DiagnosticBag Diagnostics { get; }

    public TimeSpan Duration { get; }

    public static BuildResult Succeeded(string? jarPath, DiagnosticBag diagnostics, TimeSpan duration) =>
        new(true, jarPath, diagnostics, duration);

    public static BuildResult Failed(DiagnosticBag diagnostics, TimeSpan duration) =>
        new(false, null, diagnostics, duration);
}
