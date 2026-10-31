using System.Text.Json.Nodes;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>Command of the mod (V0.6.2).</summary>
public sealed class CommandAssetDefinition : AssetDefinition<CommandAsset>
{
    public override AssetType Type => AssetType.Command;

    public override string Label => Tr.T("Command");

    public override string DefaultResourceIdBase => "new_command";

    public override string DefaultDisplayName => Tr.T("New command");

    protected override CommandAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(CommandAsset asset, JsonPropertyReader reader)
    {
        asset.PermissionLevel = reader.GetInt32("permission", 0);
        asset.Argument = reader.GetEnum("argument", CommandArgumentKind.None);
        asset.ArgumentName = reader.GetString("argumentName") ?? "value";
        asset.Minimum = reader.GetInt32("minimum", 0);
        asset.Maximum = reader.GetInt32("maximum", 100);
        asset.Feedback = reader.GetString("feedback") ?? string.Empty;
    }

    protected override void WriteProperties(CommandAsset asset, JsonObject properties)
    {
        properties["permission"] = asset.PermissionLevel;
        properties["argument"] = asset.Argument.ToString();
        properties["argumentName"] = asset.ArgumentName;
        properties["minimum"] = asset.Minimum;
        properties["maximum"] = asset.Maximum;
        properties["feedback"] = asset.Feedback;
    }
}

/// <summary>Creative tab (V0.6.2).</summary>
public sealed class CreativeTabAssetDefinition : AssetDefinition<CreativeTabAsset>
{
    public override AssetType Type => AssetType.CreativeTab;

    public override string Label => Tr.T("Creative tab");

    public override string DefaultResourceIdBase => "new_tab";

    public override string DefaultDisplayName => Tr.T("New tab");

    protected override CreativeTabAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(CreativeTabAsset asset, JsonPropertyReader reader)
    {
        asset.Icon = reader.GetReference("icon");
        asset.Items = [.. reader.GetReferenceList("items").OfType<ContentReference>()];
    }

    protected override void WriteProperties(CreativeTabAsset asset, JsonObject properties)
    {
        properties["icon"] = asset.Icon?.ToStorageText();
        properties["items"] = new JsonArray([.. asset.Items.Select(i => (JsonNode)JsonValue.Create(i.ToStorageText()))]);
    }
}

/// <summary>Keybind (V0.6.2).</summary>
public sealed class KeybindAssetDefinition : AssetDefinition<KeybindAsset>
{
    public override AssetType Type => AssetType.Keybind;

    public override string Label => Tr.T("Keybind");

    public override string DefaultResourceIdBase => "new_key";

    public override string DefaultDisplayName => Tr.T("New key");

    protected override KeybindAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(KeybindAsset asset, JsonPropertyReader reader) =>
        asset.DefaultKey = reader.GetString("key") ?? "R";

    protected override void WriteProperties(KeybindAsset asset, JsonObject properties) =>
        properties["key"] = asset.DefaultKey;
}

/// <summary>Enchantment (V0.6.3).</summary>
public sealed class EnchantmentAssetDefinition : AssetDefinition<EnchantmentAsset>
{
    public override AssetType Type => AssetType.Enchantment;

    public override string Label => Tr.T("Enchantment");

    public override string DefaultResourceIdBase => "new_enchantment";

    public override string DefaultDisplayName => Tr.T("New enchantment");

    protected override EnchantmentAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(EnchantmentAsset asset, JsonPropertyReader reader)
    {
        asset.Target = reader.GetEnum("target", EnchantmentTarget.Weapon);
        asset.Rarity = reader.GetEnum("rarity", EnchantmentRarity.Uncommon);
        asset.MaxLevel = reader.GetInt32("maxLevel", 3);
        asset.Treasure = reader.GetBoolean("treasure", false);
        asset.Curse = reader.GetBoolean("curse", false);
        asset.DamageBonus = reader.GetSingle("damageBonus", 0f);
    }

    protected override void WriteProperties(EnchantmentAsset asset, JsonObject properties)
    {
        properties["target"] = asset.Target.ToString();
        properties["rarity"] = asset.Rarity.ToString();
        properties["maxLevel"] = asset.MaxLevel;
        properties["treasure"] = asset.Treasure;
        properties["curse"] = asset.Curse;
        properties["damageBonus"] = asset.DamageBonus;
    }
}

/// <summary>Fluid (V0.6.3).</summary>
public sealed class FluidAssetDefinition : AssetDefinition<FluidAsset>
{
    public override AssetType Type => AssetType.Fluid;

    public override string Label => Tr.T("Fluid");

    public override string DefaultResourceIdBase => "new_fluid";

    public override string DefaultDisplayName => Tr.T("New fluid");

    protected override FluidAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(FluidAsset asset, JsonPropertyReader reader)
    {
        asset.Color = reader.GetString("color") ?? "#3F76E4";
        asset.LightLevel = reader.GetInt32("light", 0);
        asset.FlowSpeed = reader.GetInt32("flowSpeed", 5);
        asset.SpreadDistance = reader.GetInt32("spreadDistance", 4);
        asset.LevelDecrease = reader.GetInt32("levelDecrease", 1);
        asset.Infinite = reader.GetBoolean("infinite", true);
        asset.Density = reader.GetInt32("density", 1000);
        asset.Viscosity = reader.GetInt32("viscosity", 1000);
        asset.HasBucket = reader.GetBoolean("bucket", true);
        asset.StillTextureId = reader.GetGuid("stillTexture");
        asset.FlowingTextureId = reader.GetGuid("flowingTexture");
        asset.BucketTextureId = reader.GetGuid("bucketTexture");
    }

    protected override void WriteProperties(FluidAsset asset, JsonObject properties)
    {
        properties["color"] = asset.Color;
        properties["light"] = asset.LightLevel;
        properties["flowSpeed"] = asset.FlowSpeed;
        properties["spreadDistance"] = asset.SpreadDistance;
        properties["levelDecrease"] = asset.LevelDecrease;
        properties["infinite"] = asset.Infinite;
        properties["density"] = asset.Density;
        properties["viscosity"] = asset.Viscosity;
        properties["bucket"] = asset.HasBucket;
        properties["stillTexture"] = asset.StillTextureId?.ToString("D");
        properties["flowingTexture"] = asset.FlowingTextureId?.ToString("D");
        properties["bucketTexture"] = asset.BucketTextureId?.ToString("D");
    }
}

/// <summary>Global variable (V0.6.7).</summary>
public sealed class GlobalVariableAssetDefinition : AssetDefinition<GlobalVariableAsset>
{
    public override AssetType Type => AssetType.GlobalVariable;

    public override string Label => Tr.T("Global variable");

    public override string DefaultResourceIdBase => "new_variable";

    public override string DefaultDisplayName => Tr.T("new variable");

    protected override GlobalVariableAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(GlobalVariableAsset asset, JsonPropertyReader reader)
    {
        asset.ValueType = DataType.Parse(reader.GetString("type")) ?? DataType.Number;
        asset.Scope = reader.GetEnum("scope", GlobalScope.Map);
        asset.DefaultValue = reader.GetString("default") ?? string.Empty;
    }

    protected override void WriteProperties(GlobalVariableAsset asset, JsonObject properties)
    {
        properties["type"] = asset.ValueType.ToStorageText();
        properties["scope"] = asset.Scope.ToString();
        properties["default"] = asset.DefaultValue;
    }
}

/// <summary>Game rule (V0.6.3).</summary>
public sealed class GameRuleAssetDefinition : AssetDefinition<GameRuleAsset>
{
    public override AssetType Type => AssetType.GameRule;

    public override string Label => Tr.T("Game rule");

    public override string DefaultResourceIdBase => "new_rule";

    public override string DefaultDisplayName => Tr.T("New game rule");

    protected override GameRuleAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(GameRuleAsset asset, JsonPropertyReader reader)
    {
        asset.Kind = reader.GetEnum("kind", GameRuleKind.Boolean);
        asset.Category = reader.GetEnum("category", GameRuleCategory.Misc);
        asset.DefaultBoolean = reader.GetBoolean("defaultBoolean", true);
        asset.DefaultNumber = reader.GetInt32("defaultNumber", 0);
        asset.Description = reader.GetString("description") ?? string.Empty;
    }

    protected override void WriteProperties(GameRuleAsset asset, JsonObject properties)
    {
        properties["kind"] = asset.Kind.ToString();
        properties["category"] = asset.Category.ToString();
        properties["defaultBoolean"] = asset.DefaultBoolean;
        properties["defaultNumber"] = asset.DefaultNumber;
        properties["description"] = asset.Description;
    }
}

/// <summary>Damage type (V0.7.10).</summary>
public sealed class DamageTypeAssetDefinition : AssetDefinition<DamageTypeAsset>
{
    public override AssetType Type => AssetType.DamageType;

    public override string Label => Tr.T("Damage type");

    public override string DefaultResourceIdBase => "new_damage";

    public override string DefaultDisplayName => Tr.T("New damage type");

    protected override DamageTypeAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(DamageTypeAsset asset, JsonPropertyReader reader)
    {
        asset.DeathMessage = reader.GetString("deathMessage") ?? "{victim} died";
        asset.KillerMessage = reader.GetString("killerMessage") ?? string.Empty;
        asset.Exhaustion = reader.GetSingle("exhaustion", 0.1f);
        asset.Scaling = reader.GetEnum("scaling", DamageScaling.WhenCausedByLivingNonPlayer);
        asset.Effects = reader.GetEnum("effects", DamageEffects.Hurt);
        asset.MessageType = reader.GetEnum("messageType", DeathMessageType.Default);
        asset.Flags = new HashSet<DamageTypeFlag>(reader.GetStringList("flags")
            .Select(f => Enum.TryParse(f, out DamageTypeFlag flag) && Enum.IsDefined(flag) ? flag : (DamageTypeFlag?)null)
            .OfType<DamageTypeFlag>());
    }

    protected override void WriteProperties(DamageTypeAsset asset, JsonObject properties)
    {
        properties["deathMessage"] = asset.DeathMessage;
        properties["killerMessage"] = asset.KillerMessage;
        properties["exhaustion"] = asset.Exhaustion;
        properties["scaling"] = asset.Scaling.ToString();
        properties["effects"] = asset.Effects.ToString();
        properties["messageType"] = asset.MessageType.ToString();
        properties["flags"] = new JsonArray([.. asset.Flags.Order().Select(f => (JsonNode?)JsonValue.Create(f.ToString()))]);
    }
}

/// <summary>Drop table (V0.7.10): its kind, then its pools and their entries.</summary>
public sealed class LootTableAssetDefinition : AssetDefinition<LootTableAsset>
{
    public override AssetType Type => AssetType.LootTable;

    public override string Label => Tr.T("Drop table");

    public override string DefaultResourceIdBase => "new_drops";

    public override string DefaultDisplayName => Tr.T("New drop table");

    protected override LootTableAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(LootTableAsset asset, JsonPropertyReader reader)
    {
        asset.Kind = reader.GetEnum("kind", LootTableKind.Generic);
        asset.Pools = [.. reader.GetObjectList("pools").Select(ReadPool)];
    }

    protected override void WriteProperties(LootTableAsset asset, JsonObject properties)
    {
        properties["kind"] = asset.Kind.ToString();
        properties["pools"] = new JsonArray([.. asset.Pools.Select(WritePool)]);
    }

    private static LootPool ReadPool(JsonPropertyReader pool) => new()
    {
        RollsMin = pool.GetInt32("rollsMin", 1),
        RollsMax = pool.GetInt32("rollsMax", 1),
        BonusRolls = pool.GetSingle("bonusRolls", 0f),
        Entries = [.. pool.GetObjectList("entries").Select(ReadEntry)],
    };

    private static LootEntry ReadEntry(JsonPropertyReader entry) => new()
    {
        Kind = entry.GetEnum("kind", LootEntryKind.Item),
        Item = entry.GetReference("item"),
        Weight = entry.GetInt32("weight", 1),
        CountMin = entry.GetInt32("countMin", 1),
        CountMax = entry.GetInt32("countMax", 1),
        Chance = entry.GetInt32("chance", 100),
        KilledByPlayer = entry.GetBoolean("killedByPlayer", false),
        SilkTouch = entry.GetEnum("silkTouch", LootSilkTouch.Any),
        SurvivesExplosion = entry.GetBoolean("survivesExplosion", false),
        LootingBonus = entry.GetInt32("looting", 0),
        SmeltIfOnFire = entry.GetBoolean("smeltIfOnFire", false),
        EnchantRandomly = entry.GetBoolean("enchantRandomly", false),
    };

    private static JsonNode WritePool(LootPool pool) => new JsonObject
    {
        ["rollsMin"] = pool.RollsMin,
        ["rollsMax"] = pool.RollsMax,
        ["bonusRolls"] = pool.BonusRolls,
        ["entries"] = new JsonArray([.. pool.Entries.Select(WriteEntry)]),
    };

    private static JsonNode WriteEntry(LootEntry entry) => new JsonObject
    {
        ["kind"] = entry.Kind.ToString(),
        ["item"] = entry.Item?.ToStorageText(),
        ["weight"] = entry.Weight,
        ["countMin"] = entry.CountMin,
        ["countMax"] = entry.CountMax,
        ["chance"] = entry.Chance,
        ["killedByPlayer"] = entry.KilledByPlayer,
        ["silkTouch"] = entry.SilkTouch.ToString(),
        ["survivesExplosion"] = entry.SurvivesExplosion,
        ["looting"] = entry.LootingBonus,
        ["smeltIfOnFire"] = entry.SmeltIfOnFire,
        ["enchantRandomly"] = entry.EnchantRandomly,
    };
}

/// <summary>Status effect (V0.6.3).</summary>
public sealed class EffectAssetDefinition : AssetDefinition<EffectAsset>
{
    public override AssetType Type => AssetType.Effect;

    public override string Label => Tr.T("Effect");

    public override string DefaultResourceIdBase => "new_effect";

    public override string DefaultDisplayName => Tr.T("New effect");

    protected override EffectAsset Create(Guid id, ResourceId resourceId, string displayName) => new(id, resourceId, displayName);

    protected override void ReadProperties(EffectAsset asset, JsonPropertyReader reader)
    {
        asset.Category = reader.GetEnum("category", EffectCategory.Beneficial);
        asset.Color = reader.GetString("color") ?? "#9B59D0";
        asset.IconTextureId = reader.GetGuid("icon");
        asset.SpeedPercent = reader.GetInt32("speedPercent", 0);
        asset.AttackDamage = reader.GetSingle("attackDamage", 0f);
        asset.Armor = reader.GetSingle("armor", 0f);
        asset.MaxHealth = reader.GetSingle("maxHealth", 0f);
        asset.HealAmount = reader.GetSingle("heal", 0f);
        asset.DamageAmount = reader.GetSingle("damage", 0f);
        asset.Interval = reader.GetInt32("interval", 40);
        asset.HasPotion = reader.GetBoolean("potion", true);
        asset.PotionSeconds = reader.GetInt32("potionSeconds", 60);
    }

    protected override void WriteProperties(EffectAsset asset, JsonObject properties)
    {
        properties["category"] = asset.Category.ToString();
        properties["color"] = asset.Color;
        properties["icon"] = asset.IconTextureId?.ToString("D");
        properties["speedPercent"] = asset.SpeedPercent;
        properties["attackDamage"] = asset.AttackDamage;
        properties["armor"] = asset.Armor;
        properties["maxHealth"] = asset.MaxHealth;
        properties["heal"] = asset.HealAmount;
        properties["damage"] = asset.DamageAmount;
        properties["interval"] = asset.Interval;
        properties["potion"] = asset.HasPotion;
        properties["potionSeconds"] = asset.PotionSeconds;
    }
}
