namespace MineEngine.Core.Identifiers;

/// <summary>
/// Référence complète à un élément du jeu, "espace:chemin" ("minecraft:diamond",
/// "monmod:ruby"). Sans espace de noms, "minecraft" est sous-entendu, comme dans le jeu.
/// </summary>
public sealed class NamespacedId : IEquatable<NamespacedId>
{
    public const string DefaultNamespace = "minecraft";

    private NamespacedId(string ns, string path)
    {
        Namespace = ns;
        Path = path;
    }

    public string Namespace { get; }

    public string Path { get; }

    public static bool TryParse(string? text, out NamespacedId? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        int separator = trimmed.IndexOf(':');
        string ns = separator < 0 ? DefaultNamespace : trimmed[..separator];
        string path = separator < 0 ? trimmed : trimmed[(separator + 1)..];

        if (ns.Length == 0 || path.Length == 0
            || !ns.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '-' or '.')
            || !path.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '-' or '.' or '/'))
        {
            return false;
        }

        result = new NamespacedId(ns, path);
        return true;
    }

    public bool Equals(NamespacedId? other) => other is not null && other.Namespace == Namespace && other.Path == Path;

    public override bool Equals(object? obj) => Equals(obj as NamespacedId);

    public override int GetHashCode() => HashCode.Combine(Namespace, Path);

    public override string ToString() => $"{Namespace}:{Path}";
}
