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

    public override string Label => Tr.T("Item");

    public override string DefaultResourceIdBase => "new_item";

    public override string DefaultDisplayName => Tr.T("New item");

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
            asset.EatSeconds = food.GetSingle("seconds", food.GetBoolean("fast", false) ? 0.8f : ItemAsset.DefaultEatSeconds);
            asset.EatResult = food.GetReference("result");
            asset.UseAnimation = food.GetEnum("animation", ItemUseAnimation.Eat);
            asset.FoodEffects = [.. food.GetObjectList("effects").Select(ReadEffect).OfType<FoodEffect>()];
        }

        if (reader.GetObject("equipment") is { } equipment)
        {
            asset.Kind = equipment.GetEnum("kind", ItemKind.Simple);
            asset.Tier = equipment.GetEnum("tier", ItemTier.Iron);
            asset.ArmorMaterial = equipment.GetEnum("armorMaterial", ArmorMaterialKind.Iron);
            asset.AttackDamage = equipment.GetSingle("attackDamage", 3f);
            asset.AttackSpeed = equipment.GetSingle("attackSpeed", -2.4f);
            if (equipment.GetObject("customTier") is { } tier)
            {
                asset.CustomTier = new ItemCustomTier
                {
                    Uses = tier.GetInt32("uses", 500),
                    Speed = tier.GetSingle("speed", 6f),
                    Enchantability = tier.GetInt32("enchantability", 14),
                    MiningLevel = tier.GetEnum("level", ItemTier.Iron),
                };
            }
        }

        asset.Handheld = reader.GetBoolean("handheld", false);
        asset.CraftRemainder = reader.GetReference("craftRemainder");
        asset.Remainder = reader.GetEnum("remainder", asset.CraftRemainder is null ? ItemRemainderKind.Consumed : ItemRemainderKind.OtherItem);
        asset.GuiTextureId = reader.GetGuid("guiTexture");
        asset.GlintCondition = reader.GetGuid("glintCondition");
        asset.RepairItem = reader.GetReference("repairItem");
        asset.HarvestsAnyBlock = reader.GetBoolean("harvestsAnyBlock", false);
        asset.BurnTimeFunction = reader.GetGuid("burnTimeFunction");
        asset.BurnCondition = reader.GetGuid("burnCondition");
        asset.CompostChance = reader.GetInt32("compostChance", 0);
        asset.CompostCondition = reader.GetGuid("compostCondition");
        asset.PiglinCurrency = reader.GetBoolean("piglinCurrency", false);
        asset.Methods = [.. reader.GetObjectList("methods").Select(ReadMethod).OfType<ElementMethod>()];
        if (reader.GetObject("ranged") is { } ranged)
        {
            asset.Ranged = new ItemRanged
            {
                Kind = ranged.GetEnum("kind", RangedItemKind.None),
                Projectile = ranged.GetEnum("projectile", RangedProjectile.Arrow),
                Ammo = ranged.GetReference("ammo"),
                NeedsAmmo = ranged.GetBoolean("needsAmmo", true),
                Mode = ranged.GetEnum("mode", RangedFireMode.Charge),
                RateTicks = ranged.GetInt32("rate", 10),
                Speed = ranged.GetSingle("speed", 3f),
                Damage = ranged.GetSingle("damage", 2f),
            };
        }
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
                ["seconds"] = asset.EatSeconds,
                ["result"] = asset.EatResult?.ToStorageText(),
                ["animation"] = asset.UseAnimation.ToString(),
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
            ["customTier"] = new JsonObject
            {
                ["uses"] = asset.CustomTier.Uses,
                ["speed"] = asset.CustomTier.Speed,
                ["enchantability"] = asset.CustomTier.Enchantability,
                ["level"] = asset.CustomTier.MiningLevel.ToString(),
            },
        };
        properties["handheld"] = asset.Handheld;
        properties["craftRemainder"] = asset.CraftRemainder?.ToStorageText();
        properties["remainder"] = asset.Remainder.ToString();
        properties["guiTexture"] = asset.GuiTextureId?.ToString("D");
        properties["glintCondition"] = asset.GlintCondition?.ToString("D");
        properties["repairItem"] = asset.RepairItem?.ToStorageText();
        properties["harvestsAnyBlock"] = asset.HarvestsAnyBlock;
        properties["burnTimeFunction"] = asset.BurnTimeFunction?.ToString("D");
        properties["burnCondition"] = asset.BurnCondition?.ToString("D");
        properties["compostChance"] = asset.CompostChance;
        properties["compostCondition"] = asset.CompostCondition?.ToString("D");
        properties["piglinCurrency"] = asset.PiglinCurrency;
        properties["methods"] = new JsonArray([.. asset.Methods.Select(WriteMethod)]);
        properties["ranged"] = new JsonObject
        {
            ["kind"] = asset.Ranged.Kind.ToString(),
            ["projectile"] = asset.Ranged.Projectile.ToString(),
            ["ammo"] = asset.Ranged.Ammo?.ToStorageText(),
            ["needsAmmo"] = asset.Ranged.NeedsAmmo,
            ["mode"] = asset.Ranged.Mode.ToString(),
            ["rate"] = asset.Ranged.RateTicks,
            ["speed"] = asset.Ranged.Speed,
            ["damage"] = asset.Ranged.Damage,
        };
        properties["noRepair"] = asset.NoRepair;
        properties["burnTime"] = asset.BurnTime;
        properties["tags"] = new JsonArray([.. asset.ItemTags.Select(t => (JsonNode?)JsonValue.Create(t.ToString()))]);
    }

    /// <summary>A method without a place is ignored.</summary>
    private static ElementMethod? ReadMethod(JsonPropertyReader reader) =>
        reader.GetString("slot") is { Length: > 0 } slot
            ? new ElementMethod(slot, reader.GetEnum("language", MethodLanguage.Java), reader.GetGuid("graph"), reader.GetString("java") ?? string.Empty)
            : null;

    private static JsonNode WriteMethod(ElementMethod method) => new JsonObject
    {
        ["slot"] = method.Slot,
        ["language"] = method.Language.ToString(),
        ["graph"] = method.GraphId?.ToString("D"),
        ["java"] = method.JavaBody,
    };

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
