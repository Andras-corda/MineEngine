using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using MineEngine.Core.Logging;

namespace MineEngine.Core.Diagnostics;

/// <summary>How serious a crash report is.</summary>
public enum CrashKind
{
    /// <summary>Unexpected error caught by Mine Engine: the editor kept running.</summary>
    Error,

    /// <summary>Unhandled error: Mine Engine stopped.</summary>
    Fatal,

    /// <summary>The previous session did not close normally (killed, frozen, crashed without a report).</summary>
    AbnormalExit,

    /// <summary>Report written on purpose from the Debug menu, to check the reporter.</summary>
    Test,
}

/// <summary>A crash report on disk.</summary>
public sealed record CrashReport(string File, DateTime Time, CrashKind Kind, string Summary);

/// <summary>
/// Crash reports of Mine Engine (V0.6.4): one text file per crash in the crashes folder, with the
/// version, the system, the state of the editor (open project, active tab, debug mode), the last
/// lines of the log and the full error. A marker file tells, at the next start, that the previous
/// session ended without closing normally. The oldest reports are deleted.
/// </summary>
public sealed class CrashReporter
{
    private const string SessionMarker = "session.running";
    private readonly string _directory;
    private readonly RecentLog _recentLog;
    private readonly int _keep;
    private Func<IReadOnlyList<(string Name, string Value)>> _state = () => [];

    public CrashReporter(string directory, RecentLog recentLog, int keep = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _recentLog = recentLog ?? throw new ArgumentNullException(nameof(recentLog));
        _keep = Math.Max(1, keep);
    }

    public string Directory => _directory;

    /// <summary>Version of Mine Engine written in the reports.</summary>
    public string Version { get; init; } = "?";

    /// <summary>Gives the state of the editor at the time of the crash (it must not throw).</summary>
    public void SetStateProvider(Func<IReadOnlyList<(string Name, string Value)>> state) =>
        _state = state ?? throw new ArgumentNullException(nameof(state));

    /// <summary>
    /// Starts a session. Returns the report written for the previous session when it did not end
    /// normally (its marker is still there), or null.
    /// </summary>
    public CrashReport? BeginSession(string? previousLogFile = null)
    {
        System.IO.Directory.CreateDirectory(_directory);
        string marker = Path.Combine(_directory, SessionMarker);
        CrashReport? previous = null;
        if (File.Exists(marker))
        {
            string started = SafeRead(marker);
            IReadOnlyList<string> tail = previousLogFile is not null && File.Exists(previousLogFile)
                ? [.. ReadShared(previousLogFile).TakeLast(80)]
                : [];
            previous = WriteReport(CrashKind.AbnormalExit, null, Tr.F("The previous session ({0}) did not close normally.", started.Trim()), tail);
        }

        File.WriteAllText(marker, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} (process {Environment.ProcessId})");
        return previous;
    }

    /// <summary>The session closes normally: the marker is removed.</summary>
    public void EndSession()
    {
        try
        {
            File.Delete(Path.Combine(_directory, SessionMarker));
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Writes the report of an error and returns it; never throws (a reporter must not crash).</summary>
    public CrashReport? Report(Exception exception, CrashKind kind)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            if (kind == CrashKind.Fatal)
            {
                EndSession();
            }

            return WriteReport(kind, exception, $"{exception.GetType().Name}: {exception.Message}", _recentLog.Lines);
        }
        catch (Exception reporterError) when (reporterError is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reports on disk, newest first.</summary>
    public IReadOnlyList<CrashReport> Reports()
    {
        if (!System.IO.Directory.Exists(_directory))
        {
            return [];
        }

        return
        [
            .. System.IO.Directory.EnumerateFiles(_directory, "crash-*.txt")
                .Select(Read)
                .OfType<CrashReport>()
                .OrderByDescending(r => r.Time),
        ];
    }

    /// <summary>Text of the system information (also used by "Copy system information").</summary>
    public string SystemInformation() => SystemInformation(withEditorState: true);

    private string SystemInformation(bool withEditorState)
    {
        var text = new StringBuilder();
        foreach ((string name, string value) in EnvironmentLines(withEditorState))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{name}: {value}");
        }

        return text.ToString();
    }

    private IEnumerable<(string Name, string Value)> EnvironmentLines(bool withEditorState)
    {
        yield return ("Mine Engine", Version);
        yield return ("System", RuntimeInformation.OSDescription);
        yield return ("Architecture", RuntimeInformation.OSArchitecture.ToString());
        yield return (".NET", RuntimeInformation.FrameworkDescription);
        yield return ("Culture", CultureInfo.CurrentUICulture.Name);
        yield return ("Memory", $"{System.Environment.WorkingSet / (1024 * 1024)} MB");
        yield return ("Debug mode", EngineDebug.Enabled ? "on" : "off");
        if (!withEditorState)
        {
            yield break;
        }

        IReadOnlyList<(string, string)> state;
        try
        {
            state = _state();
        }
        catch (Exception exception)
        {
            state = [("State", "unavailable: " + exception.Message)];
        }

        foreach ((string name, string value) in state)
        {
            yield return (name, value);
        }
    }

    private CrashReport WriteReport(CrashKind kind, Exception? exception, string summary, IReadOnlyList<string> log)
    {
        System.IO.Directory.CreateDirectory(_directory);
        DateTime now = DateTime.Now;
        string name = $"crash-{now:yyyyMMdd-HHmmss-fff}-{kind.ToString().ToLowerInvariant()}";
        string file = Path.Combine(_directory, name + ".txt");
        for (int copy = 2; File.Exists(file); copy++)
        {
            // Two errors in the same millisecond: the second report must not replace the first.
            file = Path.Combine(_directory, $"{name}-{copy}.txt");
        }

        var text = new StringBuilder()
            .AppendLine("Mine Engine crash report")
            .AppendLine(CultureInfo.InvariantCulture, $"Time: {now:yyyy-MM-dd HH:mm:ss}")
            .AppendLine(CultureInfo.InvariantCulture, $"Kind: {kind}")
            .AppendLine(CultureInfo.InvariantCulture, $"Summary: {summary}")
            .AppendLine()
            .AppendLine("== Environment ==")
            // After an abnormal exit, the editor state is the one of the new session: it would mislead.
            .Append(SystemInformation(withEditorState: kind != CrashKind.AbnormalExit))
            .AppendLine();
        if (exception is not null)
        {
            text.AppendLine("== Error ==").AppendLine(exception.ToString()).AppendLine();
        }

        text.AppendLine("== Last log lines ==");
        foreach (string line in log)
        {
            text.AppendLine(line);
        }

        File.WriteAllText(file, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        DeleteOldReports();
        return new CrashReport(file, now, kind, summary);
    }

    private void DeleteOldReports()
    {
        foreach (CrashReport old in Reports().Skip(_keep))
        {
            try
            {
                File.Delete(old.File);
            }
            catch (IOException)
            {
            }
        }
    }

    private static CrashReport? Read(string file)
    {
        try
        {
            string[] head = [.. File.ReadLines(file).Take(4)];
            string Field(string name) => head.FirstOrDefault(l => l.StartsWith(name + ": ", StringComparison.Ordinal))?[(name.Length + 2)..] ?? string.Empty;
            DateTime time = DateTime.TryParse(Field("Time"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed) ? parsed : File.GetLastWriteTime(file);
            CrashKind kind = Enum.TryParse(Field("Kind"), out CrashKind k) ? k : CrashKind.Error;
            return new CrashReport(file, time, kind, Field("Summary"));
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string SafeRead(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>Lines of a file that another process (or this one) may still be writing.</summary>
    private static IEnumerable<string> ReadShared(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }
}
