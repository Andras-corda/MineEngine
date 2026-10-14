using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

public sealed class ItemAssetDefinition : InventoryAssetDefinition<ItemAsset>
{
    private const string DurabilityKey = "durability";
    private const string GlintKey = "glint";
    private const string FoodKey = "food";

    public override AssetType Type => AssetType.Item;

    public override string Label => "Item";

    public override string DefaultResourceIdBase => "new_item";

    public override string DefaultDisplayName => "Nouvel item";

    protected override ItemAsset Create(Guid id, ResourceId resourceId, string displayName) =>
        new(id, resourceId, displayName);

    /// <summary>Les propriétés ajoutées en V0.4.1 sont facultatives : un fichier plus ancien garde les valeurs par défaut.</summary>
    protected override void ReadProperties(ItemAsset asset, JsonPropertyReader reader)
    {
        ReadInventory(asset, reader);
        asset.Durability = reader.GetInt32(DurabilityKey, 0);
        asset.HasGlint = reader.GetBoolean(GlintKey, false);

        JsonPropertyReader? food = reader.GetObject(FoodKey);
        asset.IsFood = food is not null;
        if (food is not null)
        {
            asset.Nutrition = food.GetInt32("nutrition", 4);
            asset.Saturation = food.GetSingle("saturation", 0.3f);
            asset.AlwaysEdible = food.GetBoolean("alwaysEdible", false);
            asset.IsMeat = food.GetBoolean("meat", false);
            asset.FastFood = food.GetBoolean("fast", false);
            asset.FoodEffects = [.. food.GetObjectList("effects").Select(ReadEffect).OfType<FoodEffect>()];
        }

        if (reader.GetObject("equipment") is { } equipment)
        {
            asset.Kind = equipment.GetEnum("kind", ItemKind.Simple);
            asset.Tier = equipment.GetEnum("tier", ItemTier.Iron);
            asset.ArmorMaterial = equipment.GetEnum("armorMaterial", ArmorMaterialKind.Iron);
            asset.AttackDamage = equipment.GetSingle("attackDamage", 3f);
            asset.AttackSpeed = equipment.GetSingle("attackSpeed", -2.4f);
        }

        asset.Handheld = reader.GetBoolean("handheld", false);
        asset.CraftRemainder = reader.GetReference("craftRemainder");
        asset.NoRepair = reader.GetBoolean("noRepair", false);
        asset.BurnTime = reader.GetInt32("burnTime", 0);
        asset.ItemTags = [.. reader.GetStringList("tags")
            .Select(t => NamespacedId.TryParse(t, out NamespacedId? id) ? id : null)
            .OfType<NamespacedId>()];
    }

    protected override void WriteProperties(ItemAsset asset, JsonObject properties)
    {
        WriteInventory(asset, properties);
        properties[DurabilityKey] = asset.Durability;
        properties[GlintKey] = asset.HasGlint;
        properties[FoodKey] = asset.IsFood
            ? new JsonObject
            {
                ["nutrition"] = asset.Nutrition,
                ["saturation"] = asset.Saturation,
                ["alwaysEdible"] = asset.AlwaysEdible,
                ["meat"] = asset.IsMeat,
                ["fast"] = asset.FastFood,
                ["effects"] = new JsonArray([.. asset.FoodEffects.Select(WriteEffect)]),
            }
            : null;
        properties["equipment"] = new JsonObject
        {
            ["kind"] = asset.Kind.ToString(),
            ["tier"] = asset.Tier.ToString(),
            ["armorMaterial"] = asset.ArmorMaterial.ToString(),
            ["attackDamage"] = asset.AttackDamage,
            ["attackSpeed"] = asset.AttackSpeed,
        };
        properties["handheld"] = asset.Handheld;
        properties["craftRemainder"] = asset.CraftRemainder?.ToStorageText();
        properties["noRepair"] = asset.NoRepair;
        properties["burnTime"] = asset.BurnTime;
        properties["tags"] = new JsonArray([.. asset.ItemTags.Select(t => (JsonNode?)JsonValue.Create(t.ToString()))]);
    }

    /// <summary>Un effet inconnu (fichier plus récent) est ignoré.</summary>
    private static FoodEffect? ReadEffect(JsonPropertyReader reader)
    {
        string? name = reader.GetString("effect");
        return Enum.TryParse(name, ignoreCase: true, out FoodEffectKind effect) && Enum.IsDefined(effect)
            ? new FoodEffect(effect, reader.GetInt32("seconds", 5), reader.GetInt32("amplifier", 0), reader.GetSingle("probability", 1f))
            : null;
    }

    private static JsonNode WriteEffect(FoodEffect effect) => new JsonObject
    {
        ["effect"] = effect.Effect.ToString(),
        ["seconds"] = effect.DurationSeconds,
        ["amplifier"] = effect.Amplifier,
        ["probability"] = effect.Probability,
    };
}
