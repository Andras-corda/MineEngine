using System.Diagnostics;
using System.Text.RegularExpressions;
using MineEngine.Build.Java;
using MineEngine.Core.Logging;

namespace MineEngine.Build.Gradle;

/// <summary>Exécute le Gradle wrapper d'un workspace dans un processus séparé.</summary>
public sealed class GradleRunner
{
    private const string WrapperScript = "gradlew.bat";

    /// <summary>Codes couleur ANSI émis par certains outils NeoForge malgré --console=plain.</summary>
    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    /// <summary>
    /// Lance les tâches demandées et retourne le code de sortie de Gradle. Chaque ligne
    /// produite est écrite dans <paramref name="log"/> et transmise à <paramref name="outputObserver"/>.
    /// </summary>
    public async Task<int> RunAsync(
        string workspaceDirectory,
        JdkInstallation jdk,
        IReadOnlyList<string> tasks,
        ILog log,
        CancellationToken cancellationToken,
        Action<string>? outputObserver = null)
    {
        ArgumentNullException.ThrowIfNull(jdk);
        ArgumentNullException.ThrowIfNull(log);

        string wrapper = Path.Combine(workspaceDirectory, WrapperScript);
        if (!File.Exists(wrapper))
        {
            throw new FileNotFoundException(Tr.T("Gradle wrapper not found in the workspace."), wrapper);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = wrapper,
            WorkingDirectory = workspaceDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string task in tasks)
        {
            startInfo.ArgumentList.Add(task);
        }

        startInfo.ArgumentList.Add("--console=plain");
        // The NeoForge MDKs turn the configuration cache on, but their createMinecraftArtifacts task cannot be
        // stored in it with Gradle 9 (DefaultSetProperty): the build would stop before compiling.
        startInfo.ArgumentList.Add("--no-configuration-cache");
        if (Core.Diagnostics.EngineDebug.Enabled)
        {
            // Debug mode: Gradle prints the full stack of its errors.
            startInfo.ArgumentList.Add("--stacktrace");
        }
        startInfo.Environment["JAVA_HOME"] = jdk.HomeDirectory;

        log.Info($"> gradlew {string.Join(' ', startInfo.ArgumentList)}  [{jdk}]");

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Forward(log, outputObserver, e.Data, isError: false);
        process.ErrorDataReceived += (_, e) => Forward(log, outputObserver, e.Data, isError: true);

        if (!process.Start())
        {
            throw new InvalidOperationException(Tr.T("Unable to start Gradle."));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(() => Stop(process, log)))
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    private static void Forward(ILog log, Action<string>? observer, string? rawLine, bool isError)
    {
        if (string.IsNullOrEmpty(rawLine))
        {
            return;
        }

        string line = AnsiEscape.Replace(rawLine, string.Empty);
        observer?.Invoke(line);

        LogLevel level = line.Contains("error:", StringComparison.OrdinalIgnoreCase)
                         || line.Contains("erreur :", StringComparison.OrdinalIgnoreCase)
                         || line.StartsWith("FAILURE", StringComparison.Ordinal)
            ? LogLevel.Error
            : isError
                ? LogLevel.Warning
                : line.StartsWith("BUILD SUCCESSFUL", StringComparison.Ordinal) ? LogLevel.Success : LogLevel.Info;
        log.Write(level, line);
    }

    private static void Stop(Process process, ILog log)
    {
        try
        {
            if (!process.HasExited)
            {
                log.Warning(Tr.T("Gradle stop requested."));
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Le processus s'est terminé entre-temps.
        }
    }
}
