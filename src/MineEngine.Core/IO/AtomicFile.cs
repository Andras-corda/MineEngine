using System.Text;

namespace MineEngine.Core.IO;

/// <summary>
/// Écriture de fichiers sans risque de corruption : le contenu est écrit dans un
/// fichier temporaire, puis celui-ci remplace la cible en une seule opération.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void WriteAllText(string path, string content)
    {
        WriteAllBytes(path, Utf8WithoutBom.GetBytes(content));
    }

    public static void WriteAllBytes(string path, byte[] content)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = fullPath + ".tmp";
        File.WriteAllBytes(temporaryPath, content);
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
