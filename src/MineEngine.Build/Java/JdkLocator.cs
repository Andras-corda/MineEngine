namespace MineEngine.Build.Java;

/// <summary>
/// Recherche un JDK capable de lancer Gradle. Le MDK compile
/// ensuite avec sa propre version de Java, que Gradle télécharge si besoin (toolchains).
/// </summary>
public sealed class JdkLocator
{

    private static readonly string[] VendorFolders =
    [
        "Eclipse Adoptium", "Java", "Microsoft", "Zulu", "BellSoft", "Amazon Corretto", "Semeru", "OpenJDK",
    ];

    /// <summary>
    /// Retourne le JDK préféré : exactement <paramref name="preferredVersion"/> si présent,
    /// sinon la version la plus récente entre <paramref name="minimumVersion"/> et
    /// <paramref name="maximumVersion"/>, sinon null.
    /// </summary>
    public JdkInstallation? Find(int preferredVersion, int minimumVersion, int maximumVersion)
    {
        List<JdkInstallation> candidates = FindAll()
            .Where(j => j.MajorVersion >= minimumVersion && j.MajorVersion <= maximumVersion)
            .ToList();

        return candidates.FirstOrDefault(j => j.MajorVersion == preferredVersion)
               ?? candidates.OrderByDescending(j => j.MajorVersion).FirstOrDefault();
    }

    /// <summary>Tous les JDK trouvés, JAVA_HOME en premier.</summary>
    public IEnumerable<JdkInstallation> FindAll()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string home in CandidateHomes())
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(home);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!seen.Add(fullPath) || !File.Exists(Path.Combine(fullPath, "bin", "java.exe")))
            {
                continue;
            }

            int? version = ReadMajorVersion(fullPath);
            if (version is not null)
            {
                yield return new JdkInstallation(fullPath, version.Value);
            }
        }
    }

    /// <summary>Lit la version majeure dans le fichier "release" du JDK (JAVA_VERSION="21.0.5").</summary>
    public static int? ReadMajorVersion(string jdkHome)
    {
        string releaseFile = Path.Combine(jdkHome, "release");
        if (!File.Exists(releaseFile))
        {
            return null;
        }

        foreach (string line in File.ReadLines(releaseFile))
        {
            if (!line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal))
            {
                continue;
            }

            string version = line["JAVA_VERSION=".Length..].Trim('"', ' ');
            string[] parts = version.Split('.', '_', '+', '-');
            if (!int.TryParse(parts[0], out int major))
            {
                return null;
            }

            // Ancien schéma "1.8.0" : la version majeure est le second nombre.
            return major == 1 && parts.Length > 1 && int.TryParse(parts[1], out int legacy) ? legacy : major;
        }

        return null;
    }

    private static IEnumerable<string> CandidateHomes()
    {
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            yield return javaHome;
        }

        var roots = new List<string>();
        foreach (Environment.SpecialFolder folder in new[]
                 {
                     Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
                 })
        {
            string programFiles = Environment.GetFolderPath(folder);
            if (programFiles.Length > 0)
            {
                roots.AddRange(VendorFolders.Select(v => Path.Combine(programFiles, v)));
            }
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        roots.Add(Path.Combine(userProfile, ".jdks"));
        roots.Add(Path.Combine(userProfile, ".gradle", "jdks"));

        foreach (string root in roots.Where(Directory.Exists))
        {
            foreach (string directory in Directory.EnumerateDirectories(root))
            {
                yield return directory;
            }
        }
    }
}
