using MineEngine.Core.Diagnostics;
using MineEngine.Core.Logging;

namespace MineEngine.Tests;

public sealed class CrashReporterTests
{
    [Fact]
    public void A_report_holds_the_error_the_last_log_lines_and_the_editor_state()
    {
        using var temp = new TemporaryDirectory();
        var recent = new RecentLog();
        recent.Write(LogLevel.Info, "Build started");
        var reporter = new CrashReporter(temp.Path, recent) { Version = "0.6.5" };
        reporter.SetStateProvider(() => [("Project", "Ruby mod")]);

        CrashReport? report = reporter.Report(new InvalidOperationException("Broken generator"), CrashKind.Error);

        Assert.NotNull(report);
        string text = File.ReadAllText(report.File);
        Assert.Contains("Broken generator", text);
        Assert.Contains("Build started", text);
        Assert.Contains("Ruby mod", text);
        Assert.Contains("0.6.5", text);
        Assert.Equal(CrashKind.Error, Assert.Single(reporter.Reports()).Kind);
    }

    [Fact]
    public void A_session_that_did_not_end_is_reported_at_the_next_start()
    {
        using var temp = new TemporaryDirectory();
        var reporter = new CrashReporter(temp.Path, new RecentLog());
        reporter.SetStateProvider(() => [("Project", "New session")]);

        Assert.Null(reporter.BeginSession());
        reporter.EndSession();
        Assert.Null(reporter.BeginSession());

        // The process is killed: no EndSession.
        var restarted = new CrashReporter(temp.Path, new RecentLog());
        restarted.SetStateProvider(() => [("Project", "New session")]);
        CrashReport? previous = restarted.BeginSession();

        Assert.NotNull(previous);
        Assert.Equal(CrashKind.AbnormalExit, previous.Kind);
        Assert.DoesNotContain("New session", File.ReadAllText(previous.File));
    }

    [Fact]
    public void Only_the_newest_reports_are_kept()
    {
        using var temp = new TemporaryDirectory();
        var reporter = new CrashReporter(temp.Path, new RecentLog(), keep: 3);

        for (int i = 0; i < 6; i++)
        {
            Assert.NotNull(reporter.Report(new InvalidOperationException($"Error {i}"), CrashKind.Test));
        }

        Assert.Equal(3, Directory.GetFiles(temp.Path, "crash-*.txt").Length);
    }
}
