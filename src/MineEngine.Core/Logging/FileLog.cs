using System.Text;

namespace MineEngine.Core.Logging;

/// <summary>
/// Journal écrit dans un fichier par jour (mineengine-AAAAMMJJ.log). Les fichiers
/// plus anciens que la durée de conservation sont supprimés au démarrage.
/// Utilisable depuis plusieurs threads.
/// </summary>
public sealed class FileLog : ILog, IDisposable
{
    private readonly object _lock = new();
    private readonly string _directory;
    private readonly TimeProvider _clock;
    private StreamWriter? _writer;
    private DateOnly _currentDay;

    public FileLog(string directory, int retentionDays = 14, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _clock = clock ?? TimeProvider.System;
        Directory.CreateDirectory(_directory);
        DeleteOldFiles(retentionDays);
    }

    public string CurrentFile => Path.Combine(_directory, $"mineengine-{Today():yyyyMMdd}.log");

    public void Write(LogLevel level, string message)
    {
        DateTimeOffset now = _clock.GetLocalNow();
        string line = $"{now:HH:mm:ss.fff} [{level.ToString().ToUpperInvariant(),-7}] {message}";

        lock (_lock)
        {
            try
            {
                EnsureWriter().WriteLine(line);
            }
            catch (IOException)
            {
                // Un journal qui ne peut pas écrire ne doit jamais faire échouer l'application.
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    private StreamWriter EnsureWriter()
    {
        DateOnly today = Today();
        if (_writer is null || today != _currentDay)
        {
            _writer?.Dispose();
            var stream = new FileStream(CurrentFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
            _currentDay = today;
        }

        return _writer;
    }

    private void DeleteOldFiles(int retentionDays)
    {
        DateTime limit = _clock.GetLocalNow().DateTime.AddDays(-retentionDays);
        foreach (string file in Directory.EnumerateFiles(_directory, "mineengine-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < limit)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
