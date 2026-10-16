namespace MineEngine.Editor.Settings;

/// <summary>
/// Programme externe proposé dans « Ouvrir avec » (éditeur d'images, de sons, de code...).
/// <see cref="Extensions"/> liste les extensions qu'il ouvre, séparées par des points-virgules
/// ("png;jpg") ; "*" : tous les fichiers.
/// </summary>
public sealed record ExternalProgram(string Name, string Path, string Extensions)
{
    public IReadOnlyList<string> ExtensionList =>
        [.. Extensions.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.TrimStart('.').ToLowerInvariant())];

    /// <summary>Vrai si le programme est proposé pour ce fichier.</summary>
    public bool Handles(string file)
    {
        string extension = System.IO.Path.GetExtension(file ?? string.Empty).TrimStart('.').ToLowerInvariant();
        return ExtensionList.Contains("*") || (extension.Length > 0 && ExtensionList.Contains(extension));
    }

    public bool Exists => File.Exists(Path);
}

/// <summary>Programmes courants pour faire un mod, cherchés à leur emplacement d'installation habituel.</summary>
public static class KnownPrograms
{
    /// <summary>Programmes installés sur cet ordinateur, avec les extensions qu'on leur confie d'habitude.</summary>
    public static IReadOnlyList<ExternalProgram> Detect()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string localPrograms = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

        var candidates = new List<ExternalProgram>
        {
            new("Blockbench", System.IO.Path.Combine(localPrograms, "Blockbench", "Blockbench.exe"), "bbmodel;png"),
            new("Paint.NET", System.IO.Path.Combine(programFiles, "paint.net", "paintdotnet.exe"), "png"),
            new("Aseprite", System.IO.Path.Combine(programFiles, "Aseprite", "Aseprite.exe"), "png"),
            new("Aseprite", System.IO.Path.Combine(programFilesX86, "Steam", "steamapps", "common", "Aseprite", "Aseprite.exe"), "png"),
            new("Audacity", System.IO.Path.Combine(programFiles, "Audacity", "Audacity.exe"), "ogg"),
            new("Visual Studio Code", System.IO.Path.Combine(localPrograms, "Microsoft VS Code", "Code.exe"), "java;json;gradle;properties;toml;txt;md"),
            new("Notepad++", System.IO.Path.Combine(programFiles, "Notepad++", "notepad++.exe"), "java;json;properties;toml;txt"),
        };

        // Les dossiers versionnés (« GIMP 2 », « IntelliJ IDEA 2026.1 ») sont parcourus.
        candidates.AddRange(Versioned(programFiles, "GIMP*", "bin", "gimp-*.exe", "GIMP", "png"));
        candidates.AddRange(Versioned(System.IO.Path.Combine(programFiles, "JetBrains"), "IntelliJ IDEA*", "bin", "idea64.exe", "IntelliJ IDEA", "java;gradle"));

        return [.. candidates.Where(p => p.Exists).DistinctBy(p => p.Name)];
    }

    private static IEnumerable<ExternalProgram> Versioned(string root, string folderPattern, string subfolder, string filePattern, string name, string extensions)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string folder in Directory.EnumerateDirectories(root, folderPattern).OrderDescending())
        {
            string bin = System.IO.Path.Combine(folder, subfolder);
            if (Directory.Exists(bin) && Directory.EnumerateFiles(bin, filePattern).FirstOrDefault() is { } exe)
            {
                yield return new ExternalProgram(name, exe, extensions);
            }
        }
    }
}
