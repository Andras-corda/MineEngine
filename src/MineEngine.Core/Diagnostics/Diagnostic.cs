namespace MineEngine.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Un problème détecté pendant la validation, la génération ou le build,
/// rattaché si possible à l'élément qui l'a causé.
/// </summary>
public sealed class Diagnostic
{
    public Diagnostic(DiagnosticSeverity severity, string message, string? source = null, Guid? assetId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Severity = severity;
        Message = message;
        Source = source;
        AssetId = assetId;
    }

    public DiagnosticSeverity Severity { get; }

    public string Message { get; }

    /// <summary>Élément à l'origine du problème (asset, fichier...), ou null.</summary>
    public string? Source { get; }

    /// <summary>Asset à l'origine du problème, pour pouvoir y naviguer depuis l'éditeur.</summary>
    public Guid? AssetId { get; }

    public override string ToString()
    {
        string label = Severity switch
        {
            DiagnosticSeverity.Error => "Erreur",
            DiagnosticSeverity.Warning => "Avertissement",
            _ => "Info",
        };

        return Source is null ? $"[{label}] {Message}" : $"[{label}] {Source} : {Message}";
    }
}
