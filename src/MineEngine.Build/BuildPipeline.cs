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

    /// <summary>Produit le .jar du mod dans le dossier build/libs du projet.</summary>
    public Task<BuildResult> BuildAsync(ModProject project, ILog log, CancellationToken cancellationToken) =>
        RunAsync(project, log, GradleMode.Build, cancellationToken);

    /// <summary>Génère le mod puis lance Minecraft avec celui-ci (tâche runClient).</summary>
    public Task<BuildResult> RunClientAsync(ModProject project, ILog log, CancellationToken cancellationToken) =>
        RunAsync(project, log, GradleMode.RunClient, cancellationToken);

    /// <summary>
    /// Régénère le projet puis demande à Gradle de retélécharger les dépendances et de relire la
    /// configuration (comme « Reload Gradle Project » dans IntelliJ), sans compiler le mod.
    /// </summary>
    public Task<BuildResult> ReloadGradleAsync(ModProject project, ILog log, CancellationToken cancellationToken) =>
        RunAsync(project, log, GradleMode.Reload, cancellationToken);

    /// <summary>Tâches Gradle qui rechargent le projet : dépendances relues pour la compilation.</summary>
    private static readonly IReadOnlyList<string> ReloadTasks = ["--refresh-dependencies", "dependencies", "--configuration", "compileClasspath"];

    private enum GradleMode
    {
        Build,
        RunClient,
        Reload,
    }

    /// <summary>
    /// Valide le projet et écrit le code du mod dans le workspace, sans compiler : sert à montrer
    /// le Java d'un élément. Null si le projet contient des erreurs ou si aucun MDK n'est utilisable.
    /// </summary>
    public async Task<GenerationResult?> GenerateAsync(ModProject project, ILog log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(log);
        GenerationStep step = await GenerateCoreAsync(project, log, new DiagnosticBag(), cancellationToken).ConfigureAwait(false);
        return step.Failure is null ? step.Generation : null;
    }

    private async Task<BuildResult> RunAsync(ModProject project, ILog log, GradleMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(log);

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new DiagnosticBag();
        GenerationStep step = await GenerateCoreAsync(project, log, diagnostics, cancellationToken).ConfigureAwait(false);
        if (step.Failure is not null)
        {
            return Fail(log, diagnostics, stopwatch, step.Failure);
        }

        IModLoaderBackend backend = step.Backend!;
        ModIR ir = step.Ir!;
        GenerationResult generation = step.Generation!;
        string workspace = project.Layout.WorkspaceDirectory;
        IReadOnlyList<string> lockedFiles = project.Settings.LockedFiles;
        log.Info(Tr.F("{0} fichiers générés dans {1}", generation.Files.Count, workspace));
        if (lockedFiles.Count > 0)
        {
            log.Info(Tr.F("{0} fichier(s) verrouillé(s) conservé(s) tel(s) quel(s).", lockedFiles.Count));
        }

        log.Info(Tr.F("[3/{0}] Recherche d'un JDK", StepCount));
        JdkInstallation? jdk = FindJdk(backend.Mdk, log, diagnostics);
        if (jdk is null)
        {
            return Fail(log, diagnostics, stopwatch, Tr.T("Java est introuvable."));
        }

        IReadOnlyList<string> tasks = mode switch
        {
            GradleMode.RunClient => backend.RunClientTasks,
            GradleMode.Reload => ReloadTasks,
            _ => backend.BuildTasks,
        };
        log.Info(mode switch
        {
            GradleMode.RunClient => Tr.F("[4/{0}] Lancement de Minecraft avec Gradle (le premier build peut prendre plusieurs minutes)", StepCount),
            GradleMode.Reload => Tr.F("[4/{0}] Rechargement du projet Gradle et de ses dépendances", StepCount),
            _ => Tr.F("[4/{0}] Compilation avec Gradle (le premier build peut prendre plusieurs minutes)", StepCount),
        });
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
                log.Write(message.Severity == DiagnosticSeverity.Error ? LogLevel.Error : LogLevel.Warning, Tr.T("Origine : ") + message);
            }
        }

        if (exitCode != 0)
        {
            ReportError(log, diagnostics, Tr.F("Gradle s'est terminé avec le code {0}. Consultez la sortie ci-dessus.", exitCode));
            return Fail(log, diagnostics, stopwatch, Tr.T("Gradle a signalé une erreur."));
        }

        string? jarPath = mode == GradleMode.Build ? FindJar(backend, ir.Mod, workspace, log, diagnostics) : null;
        if (diagnostics.HasErrors)
        {
            return Fail(log, diagnostics, stopwatch, Tr.T("le .jar est introuvable."));
        }

        stopwatch.Stop();
        log.Success(mode switch
        {
            GradleMode.RunClient => Tr.F("Minecraft fermé ({0:mm\\:ss}).", stopwatch.Elapsed),
            GradleMode.Reload => Tr.F("Projet Gradle rechargé en {0:mm\\:ss}.", stopwatch.Elapsed),
            _ => Tr.F("Build réussi en {0:mm\\:ss} : {1}", stopwatch.Elapsed, jarPath),
        });
        return BuildResult.Succeeded(jarPath, diagnostics, stopwatch.Elapsed);
    }

    /// <summary>Résultat des étapes 1 et 2 : backend, Mod IR et fichiers générés, ou la raison de l'échec.</summary>
    private sealed record GenerationStep(IModLoaderBackend? Backend, ModIR? Ir, GenerationResult? Generation, string? Failure);

    /// <summary>Étapes 1 (validation, Mod IR) et 2 (génération du projet Minecraft).</summary>
    private async Task<GenerationStep> GenerateCoreAsync(ModProject project, ILog log, DiagnosticBag diagnostics, CancellationToken cancellationToken)
    {
        log.Info(Tr.F("[1/{0}] Validation du projet {1}", StepCount, project.Name));
        IModLoaderBackend? backend = ResolveBackend(project, log, diagnostics);
        if (backend is null)
        {
            return new GenerationStep(null, null, null, Tr.T("aucun MDK utilisable."));
        }

        var validation = new DiagnosticBag();
        ModIR? ir = await Task.Run(() => _irBuilder.Build(project, validation), cancellationToken).ConfigureAwait(false);
        Report(log, validation);
        diagnostics.AddRange(validation);
        if (ir is null)
        {
            return new GenerationStep(backend, null, null, Tr.T("le projet contient des erreurs."));
        }

        log.Info(Tr.F("[2/{0}] Génération du projet {1}", StepCount, backend.DisplayName));
        string workspace = project.Layout.WorkspaceDirectory;
        ModGenerator generator = _generatorFactory(backend);
        IReadOnlyList<string> lockedFiles = project.Settings.LockedFiles;
        GenerationResult generation = await Task.Run(
            () => generator.GenerateAsync(ir, workspace, log, cancellationToken, lockedFiles), cancellationToken).ConfigureAwait(false);
        Report(log, generation.Diagnostics);
        diagnostics.AddRange(generation.Diagnostics);
        return generation.Success
            ? new GenerationStep(backend, ir, generation, null)
            : new GenerationStep(backend, ir, generation, Tr.T("la génération a échoué."));
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
                    Tr.F("Le MDK '{0}' n'est pas installé. Installez-le avec Outils > Gestionnaire de MDK, ", wanted) +
                    Tr.T("ou choisissez-en un autre dans Espace de travail > Paramètres du projet."));
                return null;
            }

            log.Warning(Tr.F("Le MDK '{0}' est introuvable ; {1} est utilisé à la place.", settings.MdkId, mdk.DisplayName));
        }

        if (!_backends.Supports(mdk))
        {
            ReportError(log, diagnostics,
                Tr.F("Le générateur ne prend pas encore en charge {0}. Versions prises en charge : {1}.", mdk.DisplayName, _backends.SupportDescription));
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
                Tr.F("Aucun JDK entre {0} et {1} n'a été trouvé (Gradle {2}). ", GradleJavaCompatibility.MinimumJava, maximum, mdk.GradleVersion ?? "?") +
                Tr.F("Installez un JDK {0} (par exemple Eclipse Temurin) puis relancez le build.", mdk.JavaVersion));
            return null;
        }

        if (jdk.MajorVersion != mdk.JavaVersion)
        {
            log.Warning(Tr.F("{0} sera utilisé pour lancer Gradle ; Gradle téléchargera Java {1} pour compiler.", jdk, mdk.JavaVersion));
        }

        return jdk;
    }

    /// <summary>Le .jar reste là où Gradle l'a produit : build/libs du projet.</summary>
    private static string? FindJar(IModLoaderBackend backend, IRModInfo mod, string workspace, ILog log, DiagnosticBag diagnostics)
    {
        string builtJar = backend.GetOutputJarPath(workspace, mod);
        if (!File.Exists(builtJar))
        {
            ReportError(log, diagnostics, Tr.F("Le fichier attendu '{0}' n'a pas été produit par Gradle.", builtJar));
            return null;
        }

        return builtJar;
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
        log.Error(Tr.F("Build interrompu : {0}", reason));
        return BuildResult.Failed(diagnostics, stopwatch.Elapsed);
    }
}
