using System.Collections.Immutable;
using System.Text.Json.Nodes;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Reading and writing of a Sparkle particle system: its emitters, each with its head (preview,
/// name) and its modules (on or off, settings). A module of an unknown kind (newer file) is
/// skipped; a missing one takes its default settings.
/// </summary>
public sealed class SparkleAssetDefinition : AssetDefinition<SparkleAsset>
{
    public override AssetType Type => AssetType.Sparkle;

    public override string Label => Tr.T("Particle system");

    public override string DefaultResourceIdBase => "new_sparkle";

    public override string DefaultDisplayName => Tr.T("New particle system");

    /// <summary>A new system starts with one emitter; reading replaces it.</summary>
    protected override SparkleAsset Create(Guid id, ResourceId resourceId, string displayName)
    {
        var asset = new SparkleAsset(id, resourceId, displayName);
        asset.ApplyStarterSystem();
        return asset;
    }

    protected override void ReadProperties(SparkleAsset asset, JsonPropertyReader reader)
    {
        var emitters = new List<SparkleEmitter>();
        foreach (JsonPropertyReader emitter in reader.GetObjectList("emitters"))
        {
            if (emitter.GetGuid("id") is not { } id)
            {
                continue;
            }

            var modules = new Dictionary<SparkleModuleKind, SparkleModule>();
            foreach (JsonPropertyReader module in emitter.GetObjectList("modules"))
            {
                if (Enum.TryParse(module.GetString("kind"), ignoreCase: false, out SparkleModuleKind kind) && Enum.IsDefined(kind))
                {
                    SparkleModuleInfo info = SparkleCatalog.Get(kind);
                    bool enabled = !info.IsOptional || module.GetBoolean("enabled", info.EnabledByDefault);
                    modules[kind] = new SparkleModule(kind, enabled, module.GetStringMap("values").ToImmutableDictionary());
                }
            }

            // A file without positions places the emitters side by side, in their order.
            emitters.Add(new SparkleEmitter(id, emitter.GetString("name") ?? string.Empty, emitter.GetBoolean("preview", true),
                [.. SparkleCatalog.All.Select(i => modules.GetValueOrDefault(i.Kind) ?? SparkleModule.Create(i.Kind))])
            {
                X = emitter.GetDouble("x", emitters.Count * SparkleEmitter.NodeSpacing),
                Y = emitter.GetDouble("y", 0),
            });
        }

        asset.Emitters = emitters;
        asset.UserParameters =
        [
            .. reader.GetObjectList("userParameters")
                .Where(p => p.GetGuid("id") is not null && Enum.TryParse(p.GetString("type"), ignoreCase: false, out SparkleUserParameterType _))
                .Select(p =>
                {
                    SparkleUserParameterType type = Enum.Parse<SparkleUserParameterType>(p.GetString("type")!);
                    return new SparkleUserParameter(p.GetGuid("id")!.Value, p.GetString("name") ?? string.Empty, type,
                        p.GetString("value") ?? SparkleUserParameter.DefaultValue(type));
                }),
        ];
        asset.Notes =
        [
            .. reader.GetObjectList("notes")
                .Where(n => n.GetGuid("id") is not null && Enum.TryParse(n.GetString("kind"), ignoreCase: false, out SparkleNoteKind _))
                .Select(n => new SparkleNote(n.GetGuid("id")!.Value, Enum.Parse<SparkleNoteKind>(n.GetString("kind")!))
                {
                    X = n.GetDouble("x", 0),
                    Y = n.GetDouble("y", 0),
                    Width = n.GetDouble("width", SparkleNote.DefaultWidth),
                    Height = n.GetDouble("height", SparkleNote.DefaultHeight),
                    Text = n.GetString("text") ?? string.Empty,
                    Color = n.GetString("color") ?? Graphs.GraphAnnotations.CommentColors[0],
                    Tooltip = n.GetString("tooltip") ?? string.Empty,
                    Targets = [.. (n.GetString("targets") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => Guid.TryParse(t, out Guid id) ? id : Guid.Empty).Where(id => id != Guid.Empty)],
                }),
        ];
    }

    protected override void WriteProperties(SparkleAsset asset, JsonObject properties)
    {
        properties["emitters"] = new JsonArray([.. asset.Emitters.Select(WriteEmitter)]);
        properties["userParameters"] = new JsonArray([.. asset.UserParameters.Select(p => (JsonNode)new JsonObject
        {
            ["id"] = p.Id.ToString("D"),
            ["name"] = p.Name,
            ["type"] = p.Type.ToString(),
            ["value"] = p.Value,
        })]);
        properties["notes"] = new JsonArray([.. asset.Notes.Select(n => (JsonNode)new JsonObject
        {
            ["id"] = n.Id.ToString("D"),
            ["kind"] = n.Kind.ToString(),
            ["x"] = Math.Round(n.X, 1),
            ["y"] = Math.Round(n.Y, 1),
            ["width"] = Math.Round(n.Width, 1),
            ["height"] = Math.Round(n.Height, 1),
            ["text"] = n.Text,
            ["color"] = n.Color,
            ["tooltip"] = n.Tooltip,
            ["targets"] = string.Join(';', n.Targets.Select(t => t.ToString("D"))),
        })]);
    }

    private static JsonNode WriteEmitter(SparkleEmitter emitter) => new JsonObject
    {
        ["id"] = emitter.Id.ToString("D"),
        ["name"] = emitter.Name,
        ["preview"] = emitter.Preview,
        ["x"] = Math.Round(emitter.X, 1),
        ["y"] = Math.Round(emitter.Y, 1),
        ["modules"] = new JsonArray([.. emitter.Modules.OrderBy(m => m.Kind).Select(WriteModule)]),
    };

    private static JsonNode WriteModule(SparkleModule module)
    {
        var json = new JsonObject { ["kind"] = module.Kind.ToString() };
        if (module.Info.IsOptional)
        {
            json["enabled"] = module.Enabled;
        }

        var values = new JsonObject();
        foreach ((string name, string value) in module.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
        {
            values[name] = value;
        }

        json["values"] = values;
        return json;
    }
}
