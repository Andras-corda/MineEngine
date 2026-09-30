using System.Text;

namespace MineEngine.Minecraft.Backends;

/// <summary>
/// Adapte le fichier de métadonnées (mods.toml ou neoforge.mods.toml) fourni
/// par le MDK. Ce fichier passe ensuite par le moteur de modèles de Gradle,
/// qui interprète '$' et '\' : les valeurs écrites ici sont donc échappées.
/// </summary>
public sealed class ModsTomlTemplate
{
    private const string ExampleDescription = "Example mod description.";

    private string _text;

    public ModsTomlTemplate(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _text = template.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Remplace la description d'exemple du MDK NeoForge (écrite en dur dans le fichier).</summary>
    public ModsTomlTemplate ReplaceExampleDescription(string description)
    {
        string safe = string.IsNullOrWhiteSpace(description)
            ? "Mod créé avec Mine Engine."
            : description.Trim().Replace("'''", "'", StringComparison.Ordinal);
        _text = _text.Replace(ExampleDescription, EscapeForGradle(safe), StringComparison.Ordinal);
        return this;
    }

    /// <summary>
    /// Active une clé optionnelle commentée dans le MDK (par exemple <c>#displayURL="..."</c>)
    /// avec la valeur donnée. Sans effet si la valeur est vide ou si la clé est absente.
    /// </summary>
    public ModsTomlTemplate SetOptionalString(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        string[] lines = _text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("#" + key + "=", StringComparison.Ordinal) || trimmed.StartsWith("#" + key + " =", StringComparison.Ordinal))
            {
                string indent = lines[i][..(lines[i].Length - trimmed.Length)];
                lines[i] = $"{indent}{key}=\"{EscapeForGradle(EscapeTomlBasicString(value.Trim()))}\"";
                break;
            }
        }

        _text = string.Join('\n', lines);
        return this;
    }

    public override string ToString() => _text;

    private static string EscapeTomlBasicString(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' or '\r' or '\t' => " ",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    private static string EscapeForGradle(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal);
}
