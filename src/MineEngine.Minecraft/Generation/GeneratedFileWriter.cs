using System.Text;

namespace MineEngine.Minecraft.Generation;

/// <summary>
/// Écrit les fichiers générés dans le workspace (UTF-8 sans BOM, fins de ligne LF)
/// et garde la liste des fichiers produits. Les fichiers verrouillés par l'utilisateur
/// ne sont jamais réécrits : ils sont seulement notés comme tels.
/// </summary>
public sealed class GeneratedFileWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _rootDirectory;
    private readonly HashSet<string> _lockedFiles;
    private readonly List<string> _writtenFiles = [];
    private readonly List<string> _keptLockedFiles = [];

    public GeneratedFileWriter(string rootDirectory, IEnumerable<string>? lockedFiles = null)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _lockedFiles = new HashSet<string>((lockedFiles ?? []).Select(GeneratedFileManifest.Normalize), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Chemins relatifs au workspace des fichiers écrits, dans l'ordre d'écriture.</summary>
    public IReadOnlyList<string> WrittenFiles => _writtenFiles;

    /// <summary>Fichiers qui auraient été générés mais que l'utilisateur a verrouillés.</summary>
    public IReadOnlyList<string> KeptLockedFiles => _keptLockedFiles;

    public bool IsLocked(string relativePath) => _lockedFiles.Contains(GeneratedFileManifest.Normalize(relativePath));

    public void WriteText(string relativePath, string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        WriteBytes(relativePath, Utf8WithoutBom.GetBytes(normalized));
    }

    public void WriteBytes(string relativePath, byte[] content)
    {
        if (KeepLocked(relativePath))
        {
            return;
        }

        string target = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        // Un fichier identique n'est pas réécrit : Gradle et l'éditeur ne le voient pas changer.
        if (!File.Exists(target) || !File.ReadAllBytes(target).AsSpan().SequenceEqual(content))
        {
            File.WriteAllBytes(target, content);
        }

        _writtenFiles.Add(relativePath);
    }

    public void CopyFile(string sourceFile, string relativePath)
    {
        if (KeepLocked(relativePath))
        {
            return;
        }

        string target = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(sourceFile, target, overwrite: true);
        _writtenFiles.Add(relativePath);
    }

    /// <summary>Résout un chemin relatif en refusant toute sortie du workspace.</summary>
    public string Resolve(string relativePath)
    {
        string target = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
        if (!target.StartsWith(_rootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Le chemin '{relativePath}' sort du dossier de génération.");
        }

        return target;
    }

    private bool KeepLocked(string relativePath)
    {
        if (!IsLocked(relativePath) || !File.Exists(Resolve(relativePath)))
        {
            return false;
        }

        _keptLockedFiles.Add(relativePath);
        return true;
    }
}
