using MineEngine.Assets;

namespace MineEngine.Project;

/// <summary>Copie une image PNG dans le dossier Textures/ du projet et l'associe à un asset.</summary>
public sealed class TextureImporter
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public void Import(ModProject project, TexturedAsset asset, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        if (!IsPng(sourceFile))
        {
            throw new InvalidDataException($"'{Path.GetFileName(sourceFile)}' n'est pas une image PNG valide.");
        }

        string targetFile = Path.Combine(project.Layout.TexturesDirectory, asset.ResourceId.Value + ".png");
        Directory.CreateDirectory(project.Layout.TexturesDirectory);

        if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFile, targetFile, overwrite: true);
        }

        asset.TexturePath = project.Layout.ToRelativePath(targetFile);
    }

    public string? GetAbsolutePath(ModProject project, TexturedAsset asset) =>
        asset.TexturePath is null ? null : project.Layout.ToAbsolutePath(asset.TexturePath);

    private static bool IsPng(string file)
    {
        if (!File.Exists(file))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[PngSignature.Length];
        using FileStream stream = File.OpenRead(file);
        return stream.Read(header) == header.Length && header.SequenceEqual(PngSignature);
    }
}
