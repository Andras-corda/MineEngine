using System.Diagnostics;
using MineEngine.Build.Gradle;
using MineEngine.Build.Java;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Project;

namespace MineEngine.Build;

/// <summary>
/// Chaîne complète : validation, Mod IR, choix du MDK, génération du projet
/// Minecraft, compilation Gradle, puis copie du .jar ou lancement de Minecraft.
/// </summary>
public sealed class BuildPipeline
{
    private const int StepCount = 4;

    private readonly ModIRBuilder _irBuilder;
    private readonly MdkLibrary _mdkLibrary;
    private readonly ModLoaderBackendRegistry _backends;
    private readonly Func<IModLoaderBackend, ModGenerator> _generatorFactory;
    private readonly JdkLocator _jdkLocator;
    private readonly GradleJavaCompatibility _gradleJava;
    private readonly GradleRunner _gradle;
    private readonly CompilerErrorMapper _errorMapper = new();

    public BuildPipeline(
        ModIRBuilder irBuilder,
        MdkLibrary mdkLibrary,
        ModLoaderBackendRegistry backends,
        Func<IModLoaderBackend, ModGenerator> generatorFactory,
        JdkLocator jdkLocator,
        GradleJavaCompatibility gradleJava,
        GradleRunner gradle)
    {
        _irBuilder = irBuilder ?? throw new ArgumentNullException(nameof(irBuilder));
        _mdkLibrary = mdkLibrary ?? throw new ArgumentNullException(nameof(mdkLibrary));
        _backends = backends ?? throw new ArgumentNullException(nameof(backends));
        _generatorFactory = generatorFactory ?? throw new ArgumentNullException(nameof(generatorFactory));
        _jdkLocator = jdkLocator ?? throw new ArgumentNullException(nameof(jdkLocator));
        _gradleJava = gradleJava ?? throw new ArgumentNullException(nameof(gradleJava));
        _gradle = gradle ?? throw new ArgumentNullException(nameof(gradle));
    }

    /// <summary>Produit le .jar du mod et le copie dans le dossier Build/ du projet.</summary>
    public Task<BuildResult> BuildAsync(ModProject project, ILog log, CancellationToken cancellationToken) =>
        RunAsync(project, log, runClient: false, cancellationToken);

    /// <summary>Génère le mod puis lance Minecraft avec celui-ci (tâche runClient).</summary>
    public Task<BuildResult> RunClientAsync(ModProject project, ILog log, CancellationToken cancellationToken) =>
        RunAsync(project, log, runClient: true, cancellationToken);

    private async Task<BuildResult> RunAsync(ModProject project, ILog log, bool runClient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(log);

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new DiagnosticBag();

        log.Info($"[1/{StepCount}] Validation du projet {project.Name}");
        IModLoaderBackend? backend = ResolveBackend(project, log, diagnostics);
        if (backend is null)
        {
            return Fail(log, diagnostics, stopwatch, "aucun MDK utilisable.");
        }

        var validation = new DiagnosticBag();
        ModIR? ir = await Task.Run(() => _irBuilder.Build(project, validation), cancellationToken).ConfigureAwait(false);
        Report(log, validation);
        diagnostics.AddRange(validation);
        if (ir is null)
        {
            return Fail(log, diagnostics, stopwatch, "le projet contient des erreurs.");
        }

        log.Info($"[2/{StepCount}] Génération du projet {backend.DisplayName}");
        string workspace = project.Layout.GeneratedDirectory;
        ModGenerator generator = _generatorFactory(backend);
        GenerationResult generation = await Task.Run(
            () => generator.GenerateAsync(ir, workspace, log, cancellationToken), cancellationToken).ConfigureAwait(false);
        Report(log, generation.Diagnostics);
        diagnostics.AddRange(generation.Diagnostics);
        if (!generation.Success)
        {
            return Fail(log, diagnostics, stopwatch, "la génération a échoué.");
        }

        log.Info($"{generation.Files.Count} fichiers générés dans {workspace}");

        log.Info($"[3/{StepCount}] Recherche d'un JDK");
        JdkInstallation? jdk = FindJdk(backend.Mdk, log, diagnostics);
        if (jdk is null)
        {
            return Fail(log, diagnostics, stopwatch, "Java est introuvable.");
        }

        IReadOnlyList<string> tasks = runClient ? backend.RunClientTasks : backend.BuildTasks;
        log.Info($"[4/{StepCount}] {(runClient ? "Lancement de Minecraft" : "Compilation")} avec Gradle (le premier build peut prendre plusieurs minutes)");
        var gradleOutput = new List<string>();
        int exitCode = await _gradle
            .RunAsync(workspace, jdk, tasks, log, cancellationToken, line => { lock (gradleOutput) { gradleOutput.Add(line); } })
            .ConfigureAwait(false);

        IReadOnlyList<Diagnostic> compilerMessages;
        lock (gradleOutput)
        {
            compilerMessages = _errorMapper.Map(gradleOutput, workspace, generation.SourceMap);
        }

        foreach (Diagnostic message in compilerMessages)
        {
            diagnostics.Add(message);
            if (message.AssetId is not null)
            {
                log.Write(message.Severity == DiagnosticSeverity.Error ? LogLevel.Error : LogLevel.Warning, "Origine : " + message);
            }
        }

        if (exitCode != 0)
        {
            ReportError(log, diagnostics, $"Gradle s'est terminé avec le code {exitCode}. Consultez la sortie ci-dessus.");
            return Fail(log, diagnostics, stopwatch, "Gradle a signalé une erreur.");
        }

        string? jarPath = runClient ? null : CopyJar(project, backend, ir.Mod, workspace, log, diagnostics);
        if (diagnostics.HasErrors)
        {
            return Fail(log, diagnostics, stopwatch, "le .jar est introuvable.");
        }

        stopwatch.Stop();
        log.Success(runClient
            ? $"Minecraft fermé ({stopwatch.Elapsed:mm\\:ss})."
            : $"Build réussi en {stopwatch.Elapsed:mm\\:ss} : {jarPath}");
        return BuildResult.Succeeded(jarPath, diagnostics, stopwatch.Elapsed);
    }

    /// <summary>Trouve le MDK du projet et le backend capable de l'utiliser.</summary>
    private IModLoaderBackend? ResolveBackend(ModProject project, ILog log, DiagnosticBag diagnostics)
    {
        ProjectSettings settings = project.Settings;
        MdkDescriptor? mdk = _mdkLibrary.Find(settings.MdkId);

        if (mdk is null)
        {
            // Projet sans MDK enregistré (ou MDK supprimé) : on cherche un MDK installé équivalent.
            mdk = _mdkLibrary.Installed.FirstOrDefault(m =>
                string.Equals(m.Loader.ToId(), settings.LoaderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.MinecraftVersion.Id, settings.MinecraftVersion, StringComparison.Ordinal)
                && _backends.Supports(m));

            if (mdk is null)
            {
                string wanted = settings.MdkId ?? $"{settings.LoaderId} {settings.MinecraftVersion}";
                ReportError(log, diagnostics,
                    $"Le MDK '{wanted}' n'est pas installé. Installez-le avec Outils > Gestionnaire de MDK, " +
                    "ou choisissez-en un autre dans Projet > Paramètres du projet.");
                return null;
            }

            log.Warning($"Le MDK '{settings.MdkId}' est introuvable ; {mdk.DisplayName} est utilisé à la place.");
        }

        if (!_backends.Supports(mdk))
        {
            ReportError(log, diagnostics,
                $"Le générateur ne prend pas encore en charge {mdk.DisplayName}. Versions prises en charge : {_backends.SupportDescription}.");
            return null;
        }

        return _backends.Create(mdk);
    }

    private JdkInstallation? FindJdk(MdkDescriptor mdk, ILog log, DiagnosticBag diagnostics)
    {
        int maximum = _gradleJava.MaximumJavaFor(mdk.GradleVersion);
        JdkInstallation? jdk = _jdkLocator.Find(mdk.JavaVersion, GradleJavaCompatibility.MinimumJava, maximum);
        if (jdk is null)
        {
            ReportError(log, diagnostics,
                $"Aucun JDK entre {GradleJavaCompatibility.MinimumJava} et {maximum} n'a été trouvé (Gradle {mdk.GradleVersion ?? "?"}). " +
                $"Installez un JDK {mdk.JavaVersion} (par exemple Eclipse Temurin) puis relancez le build.");
            return null;
        }

        if (jdk.MajorVersion != mdk.JavaVersion)
        {
            log.Warning($"{jdk} sera utilisé pour lancer Gradle ; Gradle téléchargera Java {mdk.JavaVersion} pour compiler.");
        }

        return jdk;
    }

    private static string? CopyJar(
        ModProject project, IModLoaderBackend backend, IRModInfo mod, string workspace, ILog log, DiagnosticBag diagnostics)
    {
        string builtJar = backend.GetOutputJarPath(workspace, mod);
        if (!File.Exists(builtJar))
        {
            ReportError(log, diagnostics, $"Le fichier attendu '{builtJar}' n'a pas été produit par Gradle.");
            return null;
        }

        Directory.CreateDirectory(project.Layout.BuildDirectory);
        string target = Path.Combine(project.Layout.BuildDirectory, Path.GetFileName(builtJar));
        File.Copy(builtJar, target, overwrite: true);
        return target;
    }

    private static void Report(ILog log, DiagnosticBag diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            LogLevel level = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => LogLevel.Error,
                DiagnosticSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Info,
            };
            log.Write(level, diagnostic.ToString());
        }
    }

    private static void ReportError(ILog log, DiagnosticBag diagnostics, string message)
    {
        var diagnostic = new Diagnostic(DiagnosticSeverity.Error, message);
        diagnostics.Add(diagnostic);
        log.Error(diagnostic.ToString());
    }

    private static BuildResult Fail(ILog log, DiagnosticBag diagnostics, Stopwatch stopwatch, string reason)
    {
        stopwatch.Stop();
        log.Error($"Build interrompu : {reason}");
        return BuildResult.Failed(diagnostics, stopwatch.Elapsed);
    }
}
