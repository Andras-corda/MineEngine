
namespace MineEngine.Editor;

/// <summary>Informations sur l'éditeur affichées dans l'interface (version...).</summary>
public static class EditorInfo
{
    /// <summary>Version de l'assemblage ("0.3.0"), définie dans Directory.Build.props.</summary>
    public static Version Version { get; } = typeof(EditorInfo).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Version courte affichée à l'utilisateur ("V0.3").</summary>
    public static string VersionLabel => $"V{Version.Major}.{Version.Minor}";
}
