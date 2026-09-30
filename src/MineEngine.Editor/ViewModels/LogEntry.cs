using MineEngine.Core.Logging;

namespace MineEngine.Editor.ViewModels;

/// <summary>Une ligne de la console Output.</summary>
public sealed class LogEntry
{
    public LogEntry(DateTime timestamp, LogLevel level, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Message = message;
    }

    public DateTime Timestamp { get; }

    public LogLevel Level { get; }

    public string Message { get; }

    public string Text => $"{Timestamp:HH:mm:ss}  {Message}";
}
