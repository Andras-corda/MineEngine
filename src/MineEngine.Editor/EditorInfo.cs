
namespace MineEngine.Editor;

/// <summary>Informations sur l'éditeur affichées dans l'interface (version...).</summary>
public static class EditorInfo
{
    /// <summary>Version de l'assemblage ("0.4.2"), définie dans Directory.Build.props.</summary>
    public static Version Version { get; } = typeof(EditorInfo).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Version affichée à l'utilisateur ("V0.4", ou "V0.4.2" pour une version intermédiaire).</summary>
    /// <summary>Dépôt GitHub du projet (code source, tickets, versions).</summary>
    public const string RepositoryUrl = "https://github.com/Andras-corda/MineEngine";

    /// <summary>Site web de Mine Engine ; null tant qu'il n'existe pas (l'entrée de menu est alors grisée).</summary>
    public const string? WebsiteUrl = null;

    public static string VersionLabel => Version.Build > 0 ? $"V{Version.Major}.{Version.Minor}.{Version.Build}" : $"V{Version.Major}.{Version.Minor}";
}
