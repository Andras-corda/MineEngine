using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>Tag of the Tags tab (V0.7.1): the entries without namespace are elements of the mod.</summary>
public sealed class TagLowering : AssetLowering<TagAsset>
{
    public override AssetType AssetType => AssetType.Tag;

    protected override void Lower(TagAsset asset, LoweringContext context)
    {
        string mod = context.Project.Settings.ModId.Value;
        string Full(string entry)
        {
            bool tag = entry.StartsWith('#');
            string id = tag ? entry[1..] : entry;
            return (tag ? "#" : string.Empty) + (id.Contains(':', StringComparison.Ordinal) ? id : $"{mod}:{id}");
        }

        context.AddTag(new IRTag(asset.Id, asset.Registry, asset.Namespace.Length == 0 ? mod : asset.Namespace, asset.TagPath, asset.Replace,
            [.. asset.Values.Where(v => v.Length > 0).Select(Full).Distinct(StringComparer.Ordinal)]));
    }
}

/// <summary>A plant becomes a block (drawn as a cross, without collision) with what makes it a plant.</summary>
public sealed class PlantLowering : AssetLowering<PlantAsset>
{
    public override AssetType AssetType => AssetType.Plant;

    protected override void Lower(PlantAsset asset, LoweringContext context)
    {
        var settings = new IRBlockSettings
        {
            Model = BlockModelKind.Cross,
            RenderType = BlockRenderType.Cutout,
            LightLevel = asset.LightLevel,
            Hardness = 0f,
            Resistance = 0f,
            Sound = asset.Sound,
        };
        var plant = new IRPlant(asset.Kind, asset.Kind == PlantKind.Crop ? PlantSoil.Farmland : asset.Soil)
        {
            TopTexture = asset.Kind == PlantKind.Tall ? context.ResolveOptionalTexture(asset, asset.TopTextureId) : null,
            Stages = asset.Kind == PlantKind.Crop ? [.. asset.StageTextures.Take(PlantAsset.MaxStages).Select(t => context.ResolveTexture(asset, t))] : [],
            MaxHeight = asset.MaxHeight,
            Produce = asset.Kind == PlantKind.Crop && asset.Produce is not null ? context.ResolveItem(asset, asset.Produce, Tr.T("Produce")) : null,
            ProduceMin = Math.Min(asset.ProduceMin, asset.ProduceMax),
            ProduceMax = Math.Max(asset.ProduceMin, asset.ProduceMax),
            Generation = asset.Kind == PlantKind.Crop ? new PlantGeneration() : asset.Generation,
        };
        IRItemForm form = context.CreateItemForm(asset);
        context.AddBlock(new IRBlock(asset.Id, asset.ResourceId, asset.DisplayName, context.ResolveTexture(asset, asset.TextureId), settings,
            new Dictionary<BlockTextureSlot, IRTexture>(), IRBlockDrop.Self, form)
        {
            Plant = plant,
            Options = new BlockOptions
            {
                WalkThrough = true,
                RandomOffset = asset.Kind is PlantKind.Flower or PlantKind.Tall ? BlockOffset.XZ : BlockOffset.None,
                PushReaction = BlockPushReaction.Destroy,
            },
        });
    }
}

/// <summary>Biome of the mod (V0.7.0).</summary>
public sealed class BiomeLowering : AssetLowering<BiomeAsset>
{
    public override AssetType AssetType => AssetType.Biome;

    protected override void Lower(BiomeAsset asset, LoweringContext context)
    {
        static string? Block(string text) => text.Length == 0 ? null : text.Contains(':', StringComparison.Ordinal) ? text : "minecraft:" + text;
        context.AddBiome(new IRBiome(asset.Id, asset.ResourceId, asset.DisplayName)
        {
            BaseBiome = asset.BaseBiome,
            Temperature = asset.Temperature,
            Downfall = asset.Downfall,
            HasPrecipitation = asset.HasPrecipitation,
            SkyColor = asset.SkyColor,
            FogColor = asset.FogColor,
            WaterColor = asset.WaterColor,
            WaterFogColor = asset.WaterFogColor,
            GrassColor = asset.GrassColor,
            FoliageColor = asset.FoliageColor,
            TopBlock = Block(asset.TopBlock),
            UnderBlock = Block(asset.UnderBlock),
            GeneratesInOverworld = asset.GeneratesInOverworld,
            ClimateTemperature = asset.ClimateTemperature,
            Humidity = asset.Humidity,
            Continentalness = asset.Continentalness,
            Erosion = asset.Erosion,
            Weirdness = asset.Weirdness,
        });
    }
}
