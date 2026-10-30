using System.Collections.Immutable;
using System.Text.Json.Nodes;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class MobAssetDefinition : AssetDefinition<MobAsset>
{
    public override AssetType Type => AssetType.Mob;

    public override string Label => Tr.T("Mob");

    public override string DefaultResourceIdBase => "new_mob";

    public override string DefaultDisplayName => Tr.T("New mob");

    protected override MobAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    protected override void ReadProperties(MobAsset asset, JsonPropertyReader reader)
    {
        asset.TextureId = reader.GetGuid("texture");
        asset.CustomModelId = reader.GetGuid("customModel");
        asset.Appearances = AppearanceJson.Read(reader);
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
        asset.Persistent = reader.GetBoolean("persistent", false);

        // Before V0.7.9, fire resistance and burning in the sun were two boxes.
        HashSet<MobImmunity> immunities = [.. reader.GetEnumSet<MobImmunity>("immunities")];
        HashSet<MobVulnerability> vulnerabilities = [.. reader.GetEnumSet<MobVulnerability>("vulnerabilities")];
        if (reader.GetBoolean("fireImmune", false))
        {
            immunities.Add(MobImmunity.Fire);
            immunities.Add(MobImmunity.Lava);
        }

        if (reader.GetBoolean("burnsInDaylight", false))
        {
            vulnerabilities.Add(MobVulnerability.Sun);
        }

        asset.Immunities = immunities;
        asset.Vulnerabilities = vulnerabilities;
        ReadEntity(asset, reader);

        asset.Goals = [.. reader.GetObjectList("goals").Select(ReadGoal).OfType<MobGoal>()];
        asset.BehaviorTreeId = reader.GetGuid("behaviorTree");
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
            asset.EggCreativeTab = egg.GetGuid("creativeTab");
            asset.EggTextureId = egg.GetGuid("texture");
        }

        if (reader.GetObject("spawning") is { } spawning)
        {
            asset.SpawnsNaturally = spawning.GetBoolean("enabled", false);
            IReadOnlySet<SpawnBiome> biomes = spawning.GetEnumSet<SpawnBiome>("biomes");
            asset.SpawnWeight = spawning.GetInt32("weight", 10);
            asset.SpawnMinGroup = spawning.GetInt32("minGroup", 1);
            asset.SpawnMaxGroup = spawning.GetInt32("maxGroup", 3);
            asset.GroupMode = spawning.GetEnum("groupMode", MobGroupMode.Range);
            asset.GroupFunction = spawning.GetGuid("groupFunction");
            asset.DespawnFunction = spawning.GetGuid("despawnFunction");
            asset.InStructures = spawning.GetBoolean("inStructures", false);
            asset.Structures = new HashSet<string>(spawning.GetStringList("structures"));

            // Before V0.7.9 the dimensions were among the biomes (Overworld, Nether, End).
            IReadOnlySet<MobDimension> dimensions = spawning.GetEnumSet<MobDimension>("dimensions");
            if (dimensions.Count > 0)
            {
                asset.Dimensions = dimensions;
                asset.SpawnBiomes = biomes;
            }
            else
            {
                var found = new HashSet<MobDimension>();
                if (biomes.Any(b => b is not (SpawnBiome.Nether or SpawnBiome.End)))
                {
                    found.Add(MobDimension.Overworld);
                }

                if (biomes.Contains(SpawnBiome.Nether))
                {
                    found.Add(MobDimension.Nether);
                }

                if (biomes.Contains(SpawnBiome.End))
                {
                    found.Add(MobDimension.End);
                }

                asset.Dimensions = found;
                asset.SpawnBiomes = new HashSet<SpawnBiome>(biomes.Where(b => b is not (SpawnBiome.Overworld or SpawnBiome.Nether or SpawnBiome.End)));
            }
        }
    }

    /// <summary>The sections of V0.7.9 (Entity.md): visual, variations and layers, attributes, special properties.</summary>
    private static void ReadEntity(MobAsset asset, JsonPropertyReader reader)
    {
        if (reader.GetObject("visual") is { } visual)
        {
            asset.VisualScale = ReadScale(visual.GetObject("scale"));
            asset.TextureTransparent = visual.GetBoolean("transparent", false);
            asset.ShadowRadius = visual.GetDouble("shadowRadius", 0.5);
            asset.PassengerOffset = visual.GetDouble("passengerOffset", 1.8);
            asset.NoCollision = visual.GetBoolean("noCollision", false);
            asset.HitboxScale = ReadScale(visual.GetObject("hitboxScale"));
            asset.Variations = [.. visual.GetStringList("variations").Select(t => Guid.TryParse(t, out Guid id) ? id : Guid.Empty).Where(id => id != Guid.Empty)];
            asset.Layers =
            [
                .. visual.GetObjectList("layers").Where(l => l.GetGuid("texture") is not null).Select(l => new MobLayer(l.GetGuid("texture")!.Value)
                {
                    Glow = l.GetBoolean("glow", false),
                    NoHurtOverlay = l.GetBoolean("noHurtOverlay", false),
                    VisibleFunction = l.GetGuid("visibleFunction"),
                    GlowFunction = l.GetGuid("glowFunction"),
                    HurtFunction = l.GetGuid("hurtFunction"),
                }),
            ];
        }

        if (reader.GetObject("entity") is { } entity)
        {
            asset.CreatureType = entity.GetEnum("creatureType", MobCreatureType.Undefined);
            asset.StepHeight = entity.GetDouble("stepHeight", 0.6);
            asset.TrackingRange = entity.GetInt32("trackingRange", 8);
            asset.Knockback = entity.GetDouble("knockback", 0);
        }

        if (reader.GetObject("taming") is { } taming)
        {
            asset.Taming = new MobTaming(
                taming.GetBoolean("enabled", false),
                [.. taming.GetObjectList("items").Where(i => i.GetReference("item") is not null)
                    .Select(i => new MobTamingItem(i.GetReference("item")!, Math.Clamp(i.GetDouble("chance", MobTamingItem.DefaultChance), 0, 100)))]);
        }

        if (reader.GetObject("riding") is { } riding)
        {
            asset.Riding = new MobRiding(riding.GetBoolean("enabled", false))
            {
                Forward = riding.GetBoolean("forward", true),
                Strafe = riding.GetBoolean("strafe", true),
                NeedsSaddle = riding.GetBoolean("needsSaddle", false),
                SaddleLayer = riding.GetInt32("saddleLayer", -1),
            };
        }

        if (reader.GetObject("water") is { } water)
        {
            asset.Water = new MobWater(water.GetBoolean("enabled", false))
            {
                Breathes = water.GetBoolean("breathes", true),
                BreathesFunction = water.GetGuid("breathesFunction"),
                PushedByFluid = water.GetBoolean("pushed", true),
                PushedFunction = water.GetGuid("pushedFunction"),
                Fluid = water.GetReference("fluid"),
            };
        }

        if (reader.GetObject("boss") is { } boss)
        {
            asset.Boss = new MobBoss(boss.GetBoolean("enabled", false))
            {
                ShowBar = boss.GetBoolean("showBar", true),
                Color = boss.GetEnum("color", MobBossColor.Purple),
                Overlay = boss.GetEnum("overlay", MobBossOverlay.Progress),
            };
        }

        if (reader.GetObject("golem") is { } golem)
        {
            asset.Golem = new MobGolem(golem.GetBoolean("enabled", false))
            {
                Activator = golem.GetReference("activator") ?? MobGolem.DefaultActivator,
                Function = golem.GetGuid("function"),
            };
        }

        if (reader.GetObject("vehicle") is { } vehicle)
        {
            asset.Vehicle = new MobVehicle(vehicle.GetEnum("kind", MobVehicleKind.None))
            {
                Rarity = vehicle.GetEnum("rarity", ItemRarity.Common),
                ItemTextureId = vehicle.GetGuid("itemTexture"),
            };
        }
    }

    private static MobScale ReadScale(JsonPropertyReader? reader) => reader is null
        ? MobScale.Default
        : new MobScale(reader.GetEnum("mode", MobValueMode.Fixed), Math.Clamp(reader.GetDouble("value", 1), MobScale.Min, MobScale.Max), reader.GetGuid("function"));

    private static JsonObject WriteScale(MobScale scale) => new()
    {
        ["mode"] = scale.Mode.ToString(),
        ["value"] = scale.Value,
        ["function"] = scale.Function?.ToString("D"),
    };

    private static JsonArray Names<T>(IEnumerable<T> values) => new([.. values.Select(v => (JsonNode?)JsonValue.Create(v!.ToString()))]);

    protected override void WriteProperties(MobAsset asset, JsonObject properties)
    {
        properties["texture"] = asset.TextureId?.ToString("D");
        properties["customModel"] = asset.CustomModelId?.ToString("D");
        AppearanceJson.Write(asset, properties);
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
        properties["persistent"] = asset.Persistent;
        properties["immunities"] = Names(asset.Immunities.Order());
        properties["vulnerabilities"] = Names(asset.Vulnerabilities.Order());
        properties["visual"] = new JsonObject
        {
            ["scale"] = WriteScale(asset.VisualScale),
            ["transparent"] = asset.TextureTransparent,
            ["shadowRadius"] = asset.ShadowRadius,
            ["passengerOffset"] = asset.PassengerOffset,
            ["noCollision"] = asset.NoCollision,
            ["hitboxScale"] = WriteScale(asset.HitboxScale),
            ["variations"] = Names(asset.Variations.Select(v => v.ToString("D"))),
            ["layers"] = new JsonArray([.. asset.Layers.Select(l => (JsonNode)new JsonObject
            {
                ["texture"] = l.TextureId.ToString("D"),
                ["glow"] = l.Glow,
                ["noHurtOverlay"] = l.NoHurtOverlay,
                ["visibleFunction"] = l.VisibleFunction?.ToString("D"),
                ["glowFunction"] = l.GlowFunction?.ToString("D"),
                ["hurtFunction"] = l.HurtFunction?.ToString("D"),
            })]),
        };
        properties["entity"] = new JsonObject
        {
            ["creatureType"] = asset.CreatureType.ToString(),
            ["stepHeight"] = asset.StepHeight,
            ["trackingRange"] = asset.TrackingRange,
            ["knockback"] = asset.Knockback,
        };
        properties["taming"] = new JsonObject
        {
            ["enabled"] = asset.Taming.Enabled,
            ["items"] = new JsonArray([.. asset.Taming.Items.Select(i => (JsonNode)new JsonObject { ["item"] = i.Item.ToStorageText(), ["chance"] = i.Chance })]),
        };
        properties["riding"] = new JsonObject
        {
            ["enabled"] = asset.Riding.Enabled,
            ["forward"] = asset.Riding.Forward,
            ["strafe"] = asset.Riding.Strafe,
            ["needsSaddle"] = asset.Riding.NeedsSaddle,
            ["saddleLayer"] = asset.Riding.SaddleLayer,
        };
        properties["water"] = new JsonObject
        {
            ["enabled"] = asset.Water.Enabled,
            ["breathes"] = asset.Water.Breathes,
            ["breathesFunction"] = asset.Water.BreathesFunction?.ToString("D"),
            ["pushed"] = asset.Water.PushedByFluid,
            ["pushedFunction"] = asset.Water.PushedFunction?.ToString("D"),
            ["fluid"] = asset.Water.Fluid?.ToStorageText(),
        };
        properties["boss"] = new JsonObject
        {
            ["enabled"] = asset.Boss.Enabled,
            ["showBar"] = asset.Boss.ShowBar,
            ["color"] = asset.Boss.Color.ToString(),
            ["overlay"] = asset.Boss.Overlay.ToString(),
        };
        properties["golem"] = new JsonObject
        {
            ["enabled"] = asset.Golem.Enabled,
            ["activator"] = asset.Golem.Activator.ToStorageText(),
            ["function"] = asset.Golem.Function?.ToString("D"),
        };
        properties["vehicle"] = new JsonObject
        {
            ["kind"] = asset.Vehicle.Kind.ToString(),
            ["rarity"] = asset.Vehicle.Rarity.ToString(),
            ["itemTexture"] = asset.Vehicle.ItemTextureId?.ToString("D"),
        };

        properties["goals"] = new JsonArray([.. asset.Goals.Select(WriteGoal)]);
        if (asset.BehaviorTreeId is { } tree)
        {
            properties["behaviorTree"] = tree.ToString("D");
        }

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
            ["creativeTab"] = asset.EggCreativeTab?.ToString("D"),
            ["texture"] = asset.EggTextureId?.ToString("D"),
        };
        properties["spawning"] = new JsonObject
        {
            ["enabled"] = asset.SpawnsNaturally,
            ["biomes"] = new JsonArray([.. asset.SpawnBiomes.Order().Select(b => (JsonNode?)JsonValue.Create(b.ToString()))]),
            ["weight"] = asset.SpawnWeight,
            ["minGroup"] = asset.SpawnMinGroup,
            ["maxGroup"] = asset.SpawnMaxGroup,
            ["groupMode"] = asset.GroupMode.ToString(),
            ["groupFunction"] = asset.GroupFunction?.ToString("D"),
            ["despawnFunction"] = asset.DespawnFunction?.ToString("D"),
            ["dimensions"] = Names(asset.Dimensions.Order()),
            ["inStructures"] = asset.InStructures,
            ["structures"] = Names(asset.Structures.Order(StringComparer.Ordinal)),
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
