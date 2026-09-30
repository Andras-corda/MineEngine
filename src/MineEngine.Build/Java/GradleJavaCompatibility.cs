namespace MineEngine.Build.Java;

/// <summary>
/// Version de Java la plus récente capable d'exécuter une version donnée de Gradle
/// (tableau de compatibilité officiel de Gradle).
/// </summary>
public sealed class GradleJavaCompatibility
{
    private static readonly (Version Gradle, int MaxJava)[] Table =
    [
        (new Version(9, 1), 25),
        (new Version(8, 14), 24),
        (new Version(8, 10), 23),
        (new Version(8, 8), 22),
        (new Version(8, 5), 21),
        (new Version(8, 3), 20),
        (new Version(7, 6), 19),
        (new Version(7, 5), 18),
        (new Version(7, 3), 17),
    ];

    public const int MinimumJava = 17;

    /// <summary>Version de Java maximale ; la plus haute connue si la version de Gradle est inconnue.</summary>
    public int MaximumJavaFor(string? gradleVersion)
    {
        if (!Version.TryParse(gradleVersion, out Version? version))
        {
            return Table[0].MaxJava;
        }

        foreach ((Version gradle, int maxJava) in Table)
        {
            if (version >= gradle)
            {
                return maxJava;
            }
        }

        return 16;
    }
}
