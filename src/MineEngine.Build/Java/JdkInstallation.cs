namespace MineEngine.Build.Java;

/// <summary>Un JDK installé sur la machine.</summary>
public sealed class JdkInstallation
{
    public JdkInstallation(string homeDirectory, int majorVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        HomeDirectory = Path.GetFullPath(homeDirectory);
        MajorVersion = majorVersion;
    }

    public string HomeDirectory { get; }

    public int MajorVersion { get; }

    public override string ToString() => $"JDK {MajorVersion} ({HomeDirectory})";
}
