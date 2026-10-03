using System.Collections;

namespace MineEngine.Core.Diagnostics;

/// <summary>Collection de diagnostics accumulés au cours d'une opération.</summary>
public sealed class DiagnosticBag : IReadOnlyCollection<Diagnostic>
{
    private readonly List<Diagnostic> _items = [];

    public int Count => _items.Count;

    public bool HasErrors => _items.Exists(d => d.Severity == DiagnosticSeverity.Error);

    public int ErrorCount => _items.Count(d => d.Severity == DiagnosticSeverity.Error);

    public int WarningCount => _items.Count(d => d.Severity == DiagnosticSeverity.Warning);

    public void Add(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _items.Add(diagnostic);
    }

    public void AddRange(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            Add(diagnostic);
        }
    }

    public void Error(string message, string? source = null, Guid? assetId = null) =>
        Add(new Diagnostic(DiagnosticSeverity.Error, message, source, assetId));

    public void Warning(string message, string? source = null, Guid? assetId = null) =>
        Add(new Diagnostic(DiagnosticSeverity.Warning, message, source, assetId));

    public void Info(string message, string? source = null, Guid? assetId = null) =>
        Add(new Diagnostic(DiagnosticSeverity.Info, message, source, assetId));

    public IEnumerator<Diagnostic> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
