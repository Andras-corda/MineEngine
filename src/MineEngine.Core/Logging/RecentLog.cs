namespace MineEngine.Core.Logging;

/// <summary>
/// Keeps the last messages of the log in memory (all levels), to put them in a crash report: what
/// Mine Engine was doing just before. Usable from several threads.
/// </summary>
public sealed class RecentLog : ILog
{
    private readonly object _lock = new();
    private readonly Queue<string> _lines = new();
    private readonly int _capacity;

    public RecentLog(int capacity = 200)
    {
        _capacity = Math.Max(1, capacity);
    }

    public void Write(LogLevel level, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} [{level.ToString().ToUpperInvariant(),-7}] {message}";
        lock (_lock)
        {
            _lines.Enqueue(line);
            while (_lines.Count > _capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    /// <summary>The kept messages, oldest first.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lock)
            {
                return [.. _lines];
            }
        }
    }
}
