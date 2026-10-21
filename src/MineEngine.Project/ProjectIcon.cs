namespace MineEngine.Project;

/// <summary>
/// Icon of the mod: a PNG file of the project referenced by <see cref="ProjectSettings.IconFile"/>.
/// An image chosen outside the project is copied to its root as icon.png, so the project stays
/// self-contained (it can be moved or shared with its icon).
/// </summary>
public static class ProjectIcon
{
    public const string DefaultFileName = "icon.png";

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>True if the file exists and starts like a PNG image.</summary>
    public static bool IsPng(string? file)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(file);
            var header = new byte[PngSignature.Length];
            return stream.Read(header, 0, header.Length) == header.Length && header.AsSpan().SequenceEqual(PngSignature);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Absolute path of an icon setting (relative to the project, or already absolute), or null when empty.</summary>
    public static string? Resolve(string? projectRoot, string iconFile)
    {
        if (string.IsNullOrWhiteSpace(iconFile))
        {
            return null;
        }

        return Path.IsPathRooted(iconFile) || projectRoot is null ? iconFile : Path.GetFullPath(Path.Combine(projectRoot, iconFile));
    }

    /// <summary>
    /// Makes the icon setting relative to the project: an absolute path inside the project becomes
    /// relative, a file outside is copied to the project root as icon.png.
    /// </summary>
    public static void Adopt(ProjectLayout layout, ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(settings);
        if (!Path.IsPathRooted(settings.IconFile))
        {
            return;
        }

        string source = Path.GetFullPath(settings.IconFile);
        if (ProjectLayout.IsWithin(source, layout.RootDirectory))
        {
            settings.IconFile = Path.GetRelativePath(layout.RootDirectory, source).Replace(Path.DirectorySeparatorChar, '/');
            return;
        }

        string target = Path.Combine(layout.RootDirectory, DefaultFileName);
        File.Copy(source, target, overwrite: true);
        settings.IconFile = DefaultFileName;
    }
}
