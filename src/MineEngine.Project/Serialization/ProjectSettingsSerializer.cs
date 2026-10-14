using System.Text.Json.Nodes;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Json;

namespace MineEngine.Project.Serialization;

/// <summary>Lecture et écriture du fichier projet (.mineengine, ou Project.json avant la 0.4.2).</summary>
public sealed class ProjectSettingsSerializer
{
    public string Serialize(ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var root = new JsonObject
        {
            ["formatVersion"] = ProjectSettings.CurrentFormatVersion,
            ["modId"] = settings.ModId.Value,
            ["modName"] = settings.ModName,
            ["modVersion"] = settings.ModVersion,
            ["authors"] = settings.Authors,
            ["description"] = settings.Description,
            ["license"] = settings.License,
            ["website"] = settings.Website,
            ["mdk"] = settings.MdkId,
            ["minecraftVersion"] = settings.MinecraftVersion,
            ["loader"] = settings.LoaderId,
            ["folders"] = new JsonArray([.. settings.Folders.Select(f => (JsonNode?)JsonValue.Create(f))]),
            ["lockedFiles"] = new JsonArray([.. settings.LockedFiles.Select(f => (JsonNode?)JsonValue.Create(f))]),
        };

        return JsonFormatting.ToText(root);
    }

    public ProjectSettings Deserialize(string json, string sourceName)
    {
        JsonObject root = JsonFormatting.ParseObject(json, sourceName);

        int version = root["formatVersion"]?.GetValue<int>() ?? 0;
        if (version > ProjectSettings.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Ce projet a été créé par une version plus récente de Mine Engine (format {version}).");
        }

        string modIdText = root["modId"]?.GetValue<string>()
            ?? throw new InvalidDataException($"{sourceName} : la propriété 'modId' est obligatoire.");
        if (!ModId.TryParse(modIdText, out ModId? modId))
        {
            throw new InvalidDataException($"{sourceName} : identifiant de mod '{modIdText}' invalide.");
        }

        var settings = new ProjectSettings(modId!, ReadString(root, "modName", modIdText));
        settings.ModVersion = ReadString(root, "modVersion", settings.ModVersion);
        settings.Authors = ReadString(root, "authors", settings.Authors);
        settings.Description = ReadString(root, "description", settings.Description);
        settings.License = ReadString(root, "license", settings.License);
        settings.Website = ReadString(root, "website", settings.Website);
        settings.MdkId = root["mdk"]?.GetValue<string>();
        settings.MinecraftVersion = ReadString(root, "minecraftVersion", settings.MinecraftVersion);
        settings.LoaderId = ReadString(root, "loader", settings.LoaderId);
        settings.Folders = root["folders"] is JsonArray folders
            ? [.. folders.Select(f => f?.GetValue<string>()).OfType<string>()]
            : [];
        settings.LockedFiles = root["lockedFiles"] is JsonArray locked
            ? [.. locked.Select(f => f?.GetValue<string>()).OfType<string>()]
            : [];
        return settings;
    }

    private static string ReadString(JsonObject root, string name, string defaultValue) =>
        root[name]?.GetValue<string>() ?? defaultValue;
}
