using System.Text;

namespace MineEngine.Minecraft.Backends;

/// <summary>
/// Modification d'un fichier gradle.properties en conservant ses commentaires.
/// Gradle lit ce fichier en ISO-8859-1 : les caractères non ASCII sont donc
/// écrits sous la forme \uXXXX.
/// </summary>
public sealed class GradlePropertiesFile
{
    private readonly List<string> _lines;

    public GradlePropertiesFile(string content)
    {
        _lines = [.. content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')];
        if (_lines.Count > 0 && _lines[^1].Length == 0)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        string line = key + "=" + Escape(value ?? string.Empty);

        int index = _lines.FindIndex(l => IsKeyLine(l, key));
        if (index >= 0)
        {
            _lines[index] = line;
        }
        else
        {
            _lines.Add(line);
        }
    }

    /// <summary>
    /// Ajoute un argument à la JVM qui exécute Gradle (clé org.gradle.jvmargs),
    /// s'il n'y figure pas déjà. La valeur est écrite telle quelle.
    /// </summary>
    public void AddJvmArgument(string argument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);
        const string key = "org.gradle.jvmargs";

        int index = _lines.FindIndex(l => IsKeyLine(l, key));
        if (index < 0)
        {
            _lines.Insert(0, $"{key}={argument}");
            return;
        }

        if (!_lines[index].Contains(argument, StringComparison.Ordinal))
        {
            _lines[index] = _lines[index].TrimEnd() + " " + argument;
        }
    }

    public override string ToString() => string.Join('\n', _lines) + "\n";

    private static bool IsKeyLine(string line, string key)
    {
        string trimmed = line.TrimStart();
        if (!trimmed.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        string rest = trimmed[key.Length..].TrimStart();
        return rest.StartsWith('=') || rest.StartsWith(':');
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c == '\\')
            {
                builder.Append("\\\\");
            }
            else if (c == '\n')
            {
                builder.Append("\\n");
            }
            else if (c < 0x20 || c > 0x7E)
            {
                builder.Append($"\\u{(int)c:x4}");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
