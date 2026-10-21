using System.Diagnostics;
using MineEngine.Core.Logging;

namespace MineEngine.Core.Diagnostics;

/// <summary>
/// Debug mode of Mine Engine (V0.6.4), set in the settings. When it is on, the detailed messages
/// (Debug level) reach the Output and the log file, each step of the validation, the generation
/// and the build gives its duration, and Gradle prints the full stack of its errors.
/// </summary>
public static class EngineDebug
{
    private static volatile bool _enabled;

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Measures a step: in debug mode, its duration is written to the log when the returned object is
    /// disposed ("using (EngineDebug.Measure(log, "Validation")) { ... }").
    /// </summary>
    public static IDisposable Measure(ILog log, string step) => _enabled ? new Measurement(log, step) : NoMeasurement.Instance;

    private sealed class Measurement(ILog log, string step) : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public void Dispose() => log.Debug(Tr.F("{0}: {1} ms", step, _stopwatch.ElapsedMilliseconds));
    }

    private sealed class NoMeasurement : IDisposable
    {
        public static NoMeasurement Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
