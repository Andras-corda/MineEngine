using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.IO;
using MineEngine.Core.Json;

namespace MineEngine.Editor.Settings;

/// <summary>
/// Enregistre les préférences de l'éditeur dans un fichier settings.json.
/// Un fichier absent ou illisible donne les préférences par défaut.
/// </summary>
public sealed class EditorSettingsStore
{
    private readonly string _file;

    public EditorSettingsStore(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        _file = Path.GetFullPath(file);
    }

    public EditorSettings Load()
    {
        var settings = new EditorSettings();
        if (!File.Exists(_file))
        {
            return settings;
        }

        try
        {
            JsonObject root = JsonFormatting.ParseObject(File.ReadAllText(_file), "settings.json");
            if (Enum.TryParse(root["theme"]?.GetValue<string>(), ignoreCase: true, out AppTheme theme))
            {
                settings.Theme = theme;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or JsonException)
        {
            // Préférences illisibles : on repart des valeurs par défaut.
        }

        return settings;
    }

    public void Save(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var root = new JsonObject { ["theme"] = settings.Theme.ToString() };
        AtomicFile.WriteAllText(_file, JsonFormatting.ToText(root));
    }
}
