using System.Text.Json.Nodes;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class MobAssetDefinition : AssetDefinition<MobAsset>
{
    public override AssetType Type => AssetType.Mob;

    public override string Label => "Mob";

    public override string DefaultResourceIdBase => "new_mob";

    public override string DefaultDisplayName => "Nouveau mob";

    protected override MobAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    protected override void ReadProperties(MobAsset asset, JsonPropertyReader reader)
    {
        asset.TextureId = reader.GetGuid("texture");
        asset.Model = reader.GetEnum("model", MobModel.Zombie);
        asset.Kind = reader.GetEnum("kind", MobKind.Monster);
        if (reader.GetObject("hitbox") is { } hitbox)
        {
            asset.UseCustomHitbox = hitbox.GetBoolean("custom", false);
            asset.Width = hitbox.GetSingle("width", 0.6f);
            asset.Height = hitbox.GetSingle("height", 1.8f);
        }

        if (reader.GetObject("attributes") is { } attributes)
        {
            asset.Health = attributes.GetDouble("health", 20);
            asset.Armor = attributes.GetDouble("armor", 0);
            asset.MovementSpeed = attributes.GetDouble("movementSpeed", 0.25);
            asset.AttackDamage = attributes.GetDouble("attackDamage", 3);
            asset.FollowRange = attributes.GetDouble("followRange", 16);
            asset.KnockbackResistance = attributes.GetDouble("knockbackResistance", 0);
        }

        asset.Experience = reader.GetInt32("experience", 5);
        asset.FireImmune = reader.GetBoolean("fireImmune", false);
        asset.BurnsInDaylight = reader.GetBoolean("burnsInDaylight", false);
        asset.Persistent = reader.GetBoolean("persistent", false);

        asset.Goals = [.. reader.GetObjectList("goals").Select(ReadGoal).OfType<MobGoal>()];
        asset.BreedingItem = reader.GetReference("breedingItem");

        if (reader.GetObject("sounds") is { } sounds)
        {
            asset.AmbientSound = sounds.GetReference("ambient");
            asset.HurtSound = sounds.GetReference("hurt");
            asset.DeathSound = sounds.GetReference("death");
        }

        asset.Drops = [.. reader.GetObjectList("drops").Select(ReadDrop).OfType<MobDrop>()];

        if (reader.GetObject("spawnEgg") is { } egg)
        {
            asset.HasSpawnEgg = egg.GetBoolean("enabled", true);
            asset.EggPrimaryColor = egg.GetInt32("primaryColor", asset.EggPrimaryColor);
            asset.EggSecondaryColor = egg.GetInt32("secondaryColor", asset.EggSecondaryColor);
        }

        if (reader.GetObject("spawning") is { } spawning)
        {
            asset.SpawnsNaturally = spawning.GetBoolean("enabled", false);
            asset.SpawnBiomes = spawning.GetEnumSet<SpawnBiome>("biomes");
            asset.SpawnWeight = spawning.GetInt32("weight", 10);
            asset.SpawnMinGroup = spawning.GetInt32("minGroup", 1);
            asset.SpawnMaxGroup = spawning.GetInt32("maxGroup", 3);
        }
    }

    protected override void WriteProperties(MobAsset asset, JsonObject properties)
    {
        properties["texture"] = asset.TextureId?.ToString("D");
        properties["model"] = asset.Model.ToString();
        properties["kind"] = asset.Kind.ToString();
        properties["hitbox"] = new JsonObject
        {
            ["custom"] = asset.UseCustomHitbox,
            ["width"] = asset.Width,
            ["height"] = asset.Height,
        };
        properties["attributes"] = new JsonObject
        {
            ["health"] = asset.Health,
            ["armor"] = asset.Armor,
            ["movementSpeed"] = asset.MovementSpeed,
            ["attackDamage"] = asset.AttackDamage,
            ["followRange"] = asset.FollowRange,
            ["knockbackResistance"] = asset.KnockbackResistance,
        };
        properties["experience"] = asset.Experience;
        properties["fireImmune"] = asset.FireImmune;
        properties["burnsInDaylight"] = asset.BurnsInDaylight;
        properties["persistent"] = asset.Persistent;

        properties["goals"] = new JsonArray([.. asset.Goals.Select(WriteGoal)]);
        properties["breedingItem"] = asset.BreedingItem?.ToStorageText();
        properties["sounds"] = new JsonObject
        {
            ["ambient"] = asset.AmbientSound?.ToStorageText(),
            ["hurt"] = asset.HurtSound?.ToStorageText(),
            ["death"] = asset.DeathSound?.ToStorageText(),
        };
        properties["drops"] = new JsonArray([.. asset.Drops.Select(WriteDrop)]);
        properties["spawnEgg"] = new JsonObject
        {
            ["enabled"] = asset.HasSpawnEgg,
            ["primaryColor"] = asset.EggPrimaryColor,
            ["secondaryColor"] = asset.EggSecondaryColor,
        };
        properties["spawning"] = new JsonObject
        {
            ["enabled"] = asset.SpawnsNaturally,
            ["biomes"] = new JsonArray([.. asset.SpawnBiomes.Order().Select(b => (JsonNode?)JsonValue.Create(b.ToString()))]),
            ["weight"] = asset.SpawnWeight,
            ["minGroup"] = asset.SpawnMinGroup,
            ["maxGroup"] = asset.SpawnMaxGroup,
        };
    }

    /// <summary>Un comportement de type inconnu (fichier plus récent) est ignoré.</summary>
    private static MobGoal? ReadGoal(JsonPropertyReader reader)
    {
        string? kindText = reader.GetString("kind");
        if (!Enum.TryParse(kindText, ignoreCase: true, out MobGoalKind kind) || !Enum.IsDefined(kind))
        {
            return null;
        }

        MobGoal defaults = MobGoal.Create(kind);
        return defaults with
        {
            Speed = Math.Clamp(reader.GetDouble("speed", defaults.Speed), 0, MobGoal.MaxSpeed),
            Distance = Math.Clamp(reader.GetDouble("distance", defaults.Distance), 0, MobGoal.MaxDistance),
            Target = reader.GetEnum("target", defaults.Target),
            Item = reader.GetReference("item"),
            Flag = reader.GetBoolean("flag", defaults.Flag),
        };
    }

    private static JsonNode WriteGoal(MobGoal goal)
    {
        MobGoalInfo info = goal.Info;
        var node = new JsonObject { ["kind"] = goal.Kind.ToString() };
        if (info.UsesSpeed)
        {
            node["speed"] = goal.Speed;
        }

        if (info.UsesDistance)
        {
            node["distance"] = goal.Distance;
        }

        if (info.UsesTarget)
        {
            node["target"] = goal.Target.ToString();
        }

        if (info.UsesItem)
        {
            node["item"] = goal.Item?.ToStorageText();
        }

        if (info.UsesFlag)
        {
            node["flag"] = goal.Flag;
        }

        return node;
    }

    private static MobDrop? ReadDrop(JsonPropertyReader reader) =>
        reader.GetReference("item") is { } item
            ? new MobDrop(item, Math.Clamp(reader.GetInt32("min", 1), 0, MobDrop.MaxCount), Math.Clamp(reader.GetInt32("max", 1), 0, MobDrop.MaxCount))
            : null;

    private static JsonNode WriteDrop(MobDrop drop) => new JsonObject
    {
        ["item"] = drop.Item.ToStorageText(),
        ["min"] = drop.Min,
        ["max"] = drop.Max,
    };
}
