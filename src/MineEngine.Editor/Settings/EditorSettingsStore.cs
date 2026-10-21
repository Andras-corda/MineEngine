using System.Text.Json;
using System.Text.Json.Nodes;
using MineEngine.Core.IO;
using MineEngine.Core.Json;
using MineEngine.Core.Theming;

namespace MineEngine.Editor.Settings;

/// <summary>
/// Enregistre les préférences de l'éditeur dans un fichier settings.json.
/// Un fichier absent ou illisible donne les préférences par défaut ; une valeur absente
/// garde sa valeur par défaut (fichiers des versions précédentes).
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

            // Avant la V0.5, "theme" valait System, Light ou Dark : ce sont aussi les identifiants actuels en minuscules.
            settings.ThemeId = root["theme"]?.GetValue<string>() ?? settings.ThemeId;
            settings.AccentColor = RgbColor.TryParse(root["accentColor"]?.GetValue<string>(), out RgbColor accent) ? accent : null;
            settings.LanguageCode = root["language"]?.GetValue<string>() ?? settings.LanguageCode;
            settings.UiScale = root["uiScale"]?.GetValue<double>() ?? settings.UiScale;
            settings.CodeFontSize = root["codeFontSize"]?.GetValue<int>() ?? settings.CodeFontSize;
            settings.CodeWordWrap = root["codeWordWrap"]?.GetValue<bool>() ?? settings.CodeWordWrap;
            settings.GraphShowGrid = root["graphShowGrid"]?.GetValue<bool>() ?? settings.GraphShowGrid;
            settings.GraphSnapToGrid = root["graphSnapToGrid"]?.GetValue<bool>() ?? settings.GraphSnapToGrid;
            settings.ReopenLastProject = root["reopenLastProject"]?.GetValue<bool>() ?? settings.ReopenLastProject;
            settings.HubSidebarCollapsed = root["hubSidebarCollapsed"]?.GetValue<bool>() ?? settings.HubSidebarCollapsed;
            settings.DebugMode = root["debugMode"]?.GetValue<bool>() ?? settings.DebugMode;
            settings.AutoSaveMinutes = root["autoSaveMinutes"]?.GetValue<int>() ?? settings.AutoSaveMinutes;
            settings.BackupsToKeep = root["backupsToKeep"]?.GetValue<int>() ?? settings.BackupsToKeep;
            if (root["externalPrograms"] is JsonArray programs)
            {
                settings.ExternalPrograms =
                [
                    .. programs.OfType<JsonObject>().Select(p => new ExternalProgram(
                        p["name"]?.GetValue<string>() ?? string.Empty,
                        p["path"]?.GetValue<string>() ?? string.Empty,
                        p["extensions"]?.GetValue<string>() ?? string.Empty)).Where(p => p.Path.Length > 0),
                ];
            }

            if (root["shortcuts"] is JsonObject shortcuts)
            {
                settings.Shortcuts = shortcuts.ToDictionary(s => s.Key, s => s.Value?.GetValue<string>() ?? string.Empty);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or FormatException or JsonException)
        {
            // Préférences illisibles : on repart des valeurs par défaut.
            return new EditorSettings();
        }

        return settings;
    }

    public void Save(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var root = new JsonObject
        {
            ["theme"] = settings.ThemeId,
            ["accentColor"] = settings.AccentColor?.ToString(),
            ["language"] = settings.LanguageCode,
            ["uiScale"] = settings.UiScale,
            ["codeFontSize"] = settings.CodeFontSize,
            ["codeWordWrap"] = settings.CodeWordWrap,
            ["graphShowGrid"] = settings.GraphShowGrid,
            ["graphSnapToGrid"] = settings.GraphSnapToGrid,
            ["reopenLastProject"] = settings.ReopenLastProject,
            ["debugMode"] = settings.DebugMode,
            ["hubSidebarCollapsed"] = settings.HubSidebarCollapsed,
            ["autoSaveMinutes"] = settings.AutoSaveMinutes,
            ["backupsToKeep"] = settings.BackupsToKeep,
        };

        if (settings.ExternalPrograms.Count > 0)
        {
            root["externalPrograms"] = new JsonArray([.. settings.ExternalPrograms.Select(p => (JsonNode)new JsonObject
            {
                ["name"] = p.Name,
                ["path"] = p.Path,
                ["extensions"] = p.Extensions,
            })]);
        }

        // Seuls les raccourcis changés par l'utilisateur sont écrits.
        if (settings.Shortcuts.Count > 0)
        {
            var shortcuts = new JsonObject();
            foreach ((string id, string gesture) in settings.Shortcuts.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                shortcuts[id] = gesture;
            }

            root["shortcuts"] = shortcuts;
        }

        AtomicFile.WriteAllText(_file, JsonFormatting.ToText(root));
    }
}
