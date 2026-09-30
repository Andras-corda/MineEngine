namespace MineEngine.Core.IO;

/// <summary>Copie récursive d'un dossier, avec un filtre sur les chemins relatifs.</summary>
public sealed class DirectoryCopier
{
    private readonly Func<string, bool> _include;

    /// <param name="include">
    /// Reçoit le chemin relatif (séparateur '/') de chaque fichier ou dossier et
    /// indique s'il doit être copié. Un dossier exclu n'est pas parcouru.
    /// </param>
    public DirectoryCopier(Func<string, bool>? include = null)
    {
        _include = include ?? (_ => true);
    }

    public void Copy(string sourceDirectory, string targetDirectory, bool overwrite)
    {
        CopyRecursive(Path.GetFullPath(sourceDirectory), Path.GetFullPath(targetDirectory), string.Empty, overwrite);
    }

    private void CopyRecursive(string source, string target, string relative, bool overwrite)
    {
        Directory.CreateDirectory(target);

        foreach (string file in Directory.EnumerateFiles(source))
        {
            string name = Path.GetFileName(file);
            string relativePath = Combine(relative, name);
            string targetFile = Path.Combine(target, name);
            if (_include(relativePath) && (overwrite || !File.Exists(targetFile)))
            {
                File.Copy(file, targetFile, overwrite: true);
            }
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            string name = Path.GetFileName(directory);
            string relativePath = Combine(relative, name);
            if (_include(relativePath))
            {
                CopyRecursive(directory, Path.Combine(target, name), relativePath, overwrite);
            }
        }
    }

    private static string Combine(string relative, string name) => relative.Length == 0 ? name : relative + "/" + name;
}
