namespace MineEngine.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Destination des messages produits par les opérations longues (génération, build).</summary>
public interface ILog
{
    void Write(LogLevel level, string message);
}

public static class LogExtensions
{
    public static void Debug(this ILog log, string message) => log.Write(LogLevel.Debug, message);

    public static void Info(this ILog log, string message) => log.Write(LogLevel.Info, message);

    public static void Success(this ILog log, string message) => log.Write(LogLevel.Success, message);

    public static void Warning(this ILog log, string message) => log.Write(LogLevel.Warning, message);

    public static void Error(this ILog log, string message) => log.Write(LogLevel.Error, message);
}

/// <summary>Journal qui ignore tous les messages.</summary>
public sealed class NullLog : ILog
{
    public static NullLog Instance { get; } = new();

    private NullLog()
    {
    }

    public void Write(LogLevel level, string message)
    {
    }
}
