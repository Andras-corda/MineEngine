namespace MineEngine.Core.Logging;

/// <summary>Transmet chaque message à plusieurs journaux (console de l'éditeur, fichier...).</summary>
public sealed class CompositeLog : ILog
{
    private readonly IReadOnlyList<ILog> _logs;

    public CompositeLog(params ILog[] logs)
    {
        ArgumentNullException.ThrowIfNull(logs);
        _logs = [.. logs];
    }

    public void Write(LogLevel level, string message)
    {
        foreach (ILog log in _logs)
        {
            log.Write(level, message);
        }
    }
}
