
namespace MineEngine.Editor;

/// <summary>Informations sur l'éditeur affichées dans l'interface (version...).</summary>
public static class EditorInfo
{
    /// <summary>Version de l'assemblage ("0.4.2"), définie dans Directory.Build.props.</summary>
    public static Version Version { get; } = typeof(EditorInfo).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Version affichée à l'utilisateur ("V0.4", ou "V0.4.2" pour une version intermédiaire).</summary>
    public static string VersionLabel => Version.Build > 0 ? $"V{Version.Major}.{Version.Minor}.{Version.Build}" : $"V{Version.Major}.{Version.Minor}";
}
