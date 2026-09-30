using System.Text;

namespace MineEngine.Minecraft.Generation;

/// <summary>
/// Écrit les fichiers générés dans le workspace (UTF-8 sans BOM, fins de ligne LF)
/// et garde la liste des fichiers produits.
/// </summary>
public sealed class GeneratedFileWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _rootDirectory;
    private readonly List<string> _writtenFiles = [];

    public GeneratedFileWriter(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <summary>Chemins relatifs au workspace des fichiers écrits, dans l'ordre d'écriture.</summary>
    public IReadOnlyList<string> WrittenFiles => _writtenFiles;

    public void WriteText(string relativePath, string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        WriteBytes(relativePath, Utf8WithoutBom.GetBytes(normalized));
    }

    public void WriteBytes(string relativePath, byte[] content)
    {
        string target = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, content);
        _writtenFiles.Add(relativePath);
    }

    public void CopyFile(string sourceFile, string relativePath)
    {
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
}
