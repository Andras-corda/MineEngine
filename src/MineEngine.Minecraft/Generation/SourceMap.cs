namespace MineEngine.Minecraft.Generation;

/// <summary>Plage de lignes d'un fichier généré qui provient d'un asset donné.</summary>
public sealed class SourceMapEntry
{
    public SourceMapEntry(string file, int firstLine, int lastLine, Guid assetId, string label)
    {
        File = file;
        FirstLine = firstLine;
        LastLine = lastLine;
        AssetId = assetId;
        Label = label;
    }

    /// <summary>Chemin relatif au workspace, séparateur '/'.</summary>
    public string File { get; }

    public int FirstLine { get; }

    public int LastLine { get; }

    public Guid AssetId { get; }

    /// <summary>Nom lisible de l'élément ("Item 'magic_sword'").</summary>
    public string Label { get; }

    public bool Contains(string file, int line) =>
        string.Equals(File, file, StringComparison.OrdinalIgnoreCase) && line >= FirstLine && line <= LastLine;
}

/// <summary>
/// Correspondance entre les lignes des fichiers générés et les assets du projet.
/// Elle permet de rattacher une erreur de compilation Java à l'asset qui l'a causée.
/// </summary>
public sealed class SourceMap
{
    private readonly List<SourceMapEntry> _entries = [];

    public IReadOnlyList<SourceMapEntry> Entries => _entries;

    public void Add(string file, int firstLine, int lastLine, Guid assetId, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        _entries.Add(new SourceMapEntry(file.Replace('\\', '/'), firstLine, lastLine, assetId, label));
    }

    public SourceMapEntry? Find(string file, int line)
    {
        string normalized = file.Replace('\\', '/');
        return _entries.Find(e => e.Contains(normalized, line));
    }
}
