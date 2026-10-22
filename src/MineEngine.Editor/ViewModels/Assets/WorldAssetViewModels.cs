using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.GameData;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Properties of a plant (V0.7.0): kind, soil, textures, crop and generation.</summary>
public sealed class PlantAssetViewModel : InventoryAssetViewModel
{
    private readonly PlantAsset _plant;

    public PlantAssetViewModel(PlantAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _plant = model;
        TopTexture = new TextureSlotViewModel(model, context, nameof(PlantAsset.TopTextureId), Tr.T("Top"),
            Tr.T("Uses the bottom texture"), () => _plant.TopTextureId, v => _plant.TopTextureId = v);
        ProducePicker = new ReferencePickerViewModel(context, ReferenceDomain.Item, () => _plant.Produce,
            v => Change(Tr.T("Change the produce"), nameof(PlantAsset.Produce), () => _plant.Produce, r => _plant.Produce = r, v));
        AddStageCommand = new RelayCommand(AddStage, () => _plant.StageTextures.Count < PlantAsset.MaxStages);
        RemoveStageCommand = new RelayCommand(RemoveStage, () => _plant.StageTextures.Count > 0);
        RefreshStages();
    }

    public static IReadOnlyList<Choice<PlantKind>> Kinds { get; } =
    [
        new(PlantKind.Flower, Tr.T("Flower (a cross, like a poppy)")),
        new(PlantKind.Tall, Tr.T("Tall plant (two blocks, like a sunflower)")),
        new(PlantKind.Growing, Tr.T("Growing plant (grows upward, like sugar cane)")),
        new(PlantKind.Crop, Tr.T("Crop (stages on farmland, like wheat)")),
    ];

    public static IReadOnlyList<Choice<PlantSoil>> Soils { get; } =
    [
        new(PlantSoil.Dirt, Tr.T("Grass, dirt and farmland")),
        new(PlantSoil.Sand, Tr.T("Sand")),
        new(PlantSoil.Farmland, Tr.T("Farmland only")),
        new(PlantSoil.AnySolid, Tr.T("Any solid block")),
    ];

    public IReadOnlyList<Choice<BlockSoundType>> SoundChoices => GameDataChoices.Sounds;

    public override string? IconPath => MainTexture.FullPath;

    public PlantKind Kind
    {
        get => _plant.Kind;
        set => ChangeIfDifferent(Tr.T("Change the kind of plant"), nameof(PlantAsset.Kind), () => _plant.Kind, v => _plant.Kind = v, value);
    }

    public bool IsTall => _plant.Kind == PlantKind.Tall;

    public bool IsCrop => _plant.Kind == PlantKind.Crop;

    public bool IsGrowing => _plant.Kind == PlantKind.Growing;

    public bool HasSoilChoice => _plant.Kind != PlantKind.Crop;

    public bool CanGenerate => _plant.Kind != PlantKind.Crop;

    /// <summary>"Seeds" for a crop, "Plant" otherwise: what the main texture is.</summary>
    public string MainTextureHint => _plant.Kind switch
    {
        PlantKind.Crop => Tr.T("Main texture: the seeds (the item of the crop)."),
        PlantKind.Tall => Tr.T("Main texture: the bottom of the plant."),
        _ => Tr.T("Main texture: the plant, drawn as a cross and used for its item."),
    };

    public PlantSoil Soil
    {
        get => _plant.Soil;
        set => ChangeIfDifferent(Tr.T("Change the soil of the plant"), nameof(PlantAsset.Soil), () => _plant.Soil, v => _plant.Soil = v, value);
    }

    public TextureSlotViewModel TopTexture { get; }

    // ----- Stages of a crop -----

    public ObservableCollection<TextureSlotViewModel> Stages { get; } = [];

    public ICommand AddStageCommand { get; }

    public ICommand RemoveStageCommand { get; }

    private void AddStage() =>
        Change(Tr.T("Add a growth stage"), nameof(PlantAsset.StageTextures), () => _plant.StageTextures, v => _plant.StageTextures = v,
            [.. _plant.StageTextures, _plant.StageTextures.LastOrDefault(_plant.TextureId ?? Guid.Empty)]);

    private void RemoveStage() =>
        Change(Tr.T("Remove a growth stage"), nameof(PlantAsset.StageTextures), () => _plant.StageTextures, v => _plant.StageTextures = v,
            [.. _plant.StageTextures.Take(_plant.StageTextures.Count - 1)]);

    private void RefreshStages()
    {
        Stages.Clear();
        for (int i = 0; i < _plant.StageTextures.Count; i++)
        {
            int index = i;
            Stages.Add(new TextureSlotViewModel(_plant, Context, nameof(PlantAsset.StageTextures), Tr.F("Stage {0}", i + 1), Tr.T("No texture"),
                () => index < _plant.StageTextures.Count && _plant.StageTextures[index] != Guid.Empty ? _plant.StageTextures[index] : null,
                v => _plant.StageTextures = [.. _plant.StageTextures.Select((t, j) => j == index ? v ?? Guid.Empty : t)]));
        }
    }

    public double MaxHeight
    {
        get => _plant.MaxHeight;
        set => ChangeNumber(Tr.T("Change the maximum height"), nameof(PlantAsset.MaxHeight), value, () => _plant.MaxHeight, v => _plant.MaxHeight = v,
            v => Math.Clamp((int)Math.Round(v), 1, PlantAsset.MaxGrowingHeight));
    }

    public double LightLevel
    {
        get => _plant.LightLevel;
        set => ChangeNumber(Tr.T("Change light level"), nameof(PlantAsset.LightLevel), value, () => _plant.LightLevel, v => _plant.LightLevel = v,
            v => Math.Clamp((int)Math.Round(v), 0, BlockAsset.MaxLightLevel));
    }

    public BlockSoundType Sound
    {
        get => _plant.Sound;
        set => ChangeIfDifferent(Tr.T("Change sound"), nameof(PlantAsset.Sound), () => _plant.Sound, v => _plant.Sound = v, value);
    }

    /// <summary>Item dropped by a ripe crop.</summary>
    public ReferencePickerViewModel ProducePicker { get; }

    public double ProduceMin
    {
        get => _plant.ProduceMin;
        set => ChangeNumber(Tr.T("Change the produce"), nameof(PlantAsset.ProduceMin), value, () => _plant.ProduceMin, v => _plant.ProduceMin = v,
            v => Math.Clamp((int)Math.Round(v), 1, BlockAsset.MaxDropCount));
    }

    public double ProduceMax
    {
        get => _plant.ProduceMax;
        set => ChangeNumber(Tr.T("Change the produce"), nameof(PlantAsset.ProduceMax), value, () => _plant.ProduceMax, v => _plant.ProduceMax = v,
            v => Math.Clamp((int)Math.Round(v), 1, BlockAsset.MaxDropCount));
    }

    // ----- Generation -----

    public bool GenerationEnabled
    {
        get => _plant.Generation.Enabled;
        set => SetGeneration(Tr.T("Change the generation of the plant"), g => g with { Enabled = value });
    }

    public string GenerationBiomes
    {
        get => string.Join(", ", _plant.Generation.Biomes);
        set => SetGeneration(Tr.T("Change the biomes of the plant"), g => g with { Biomes = BlockOptionsViewModel.SplitList(value) });
    }

    public double GenerationRarity
    {
        get => _plant.Generation.Rarity;
        set => SetGeneration(Tr.T("Change the rarity of the patches"), g => g with { Rarity = double.IsFinite(value) ? Math.Clamp((int)Math.Round(value), 1, 256) : 8 });
    }

    public double GenerationPatchSize
    {
        get => _plant.Generation.PatchSize;
        set => SetGeneration(Tr.T("Change the size of the patches"), g => g with { PatchSize = double.IsFinite(value) ? Math.Clamp((int)Math.Round(value), 1, 256) : 32 });
    }

    private void SetGeneration(string description, Func<PlantGeneration, PlantGeneration> change) =>
        ChangeIfDifferent(description, nameof(PlantAsset.Generation), () => _plant.Generation, v => _plant.Generation = v, change(_plant.Generation));

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        TopTexture.Refresh();
        ProducePicker.Refresh();
        foreach (TextureSlotViewModel stage in Stages)
        {
            stage.Refresh();
        }
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(PlantAsset.Kind):
                OnPropertyChanged(string.Empty);
                break;
            case nameof(PlantAsset.TopTextureId):
                TopTexture.Refresh();
                break;
            case nameof(PlantAsset.StageTextures):
                RefreshStages();
                break;
            case nameof(PlantAsset.Produce):
                ProducePicker.Refresh();
                break;
            case nameof(PlantAsset.Generation):
                OnPropertyChanged(nameof(GenerationEnabled));
                OnPropertyChanged(nameof(GenerationBiomes));
                OnPropertyChanged(nameof(GenerationRarity));
                OnPropertyChanged(nameof(GenerationPatchSize));
                break;
        }
    }
}

/// <summary>A value of the climate where a biome appears, edited as a center and a spread.</summary>
public sealed class ClimateRangeViewModel : ObservableObject
{
    private readonly Func<ClimateRange> _getter;
    private readonly Action<ClimateRange> _change;

    public ClimateRangeViewModel(string label, string hint, Func<ClimateRange> getter, Action<ClimateRange> change)
    {
        Label = label;
        Hint = hint;
        _getter = getter;
        _change = change;
    }

    public string Label { get; }

    public string Hint { get; }

    public double Center
    {
        get => _getter().Center;
        set => Set(_getter() with { Center = double.IsFinite(value) ? Math.Clamp(Math.Round(value, 3), -1, 1) : 0 });
    }

    public double Spread
    {
        get => _getter().Spread;
        set => Set(_getter() with { Spread = double.IsFinite(value) ? Math.Clamp(Math.Round(value, 3), 0, 1) : 0.2 });
    }

    private void Set(ClimateRange range)
    {
        if (range != _getter())
        {
            _change(range);
        }
    }

    public void Refresh() => OnPropertyChanged(string.Empty);
}

/// <summary>Properties of a biome (V0.7.0): climate, colors, surface, place in the overworld.</summary>
public sealed class BiomeAssetViewModel : AssetViewModel
{
    private readonly BiomeAsset _biome;

    public BiomeAssetViewModel(BiomeAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _biome = model;
        Climate =
        [
            Range(Tr.T("Temperature"), Tr.T("-1: frozen, 1: hot"), nameof(BiomeAsset.ClimateTemperature), () => _biome.ClimateTemperature, v => _biome.ClimateTemperature = v),
            Range(Tr.T("Humidity"), Tr.T("-1: dry, 1: humid"), nameof(BiomeAsset.Humidity), () => _biome.Humidity, v => _biome.Humidity = v),
            Range(Tr.T("Continentalness"), Tr.T("-1: ocean, 1: far inland"), nameof(BiomeAsset.Continentalness), () => _biome.Continentalness, v => _biome.Continentalness = v),
            Range(Tr.T("Erosion"), Tr.T("-1: mountains, 1: flat"), nameof(BiomeAsset.Erosion), () => _biome.Erosion, v => _biome.Erosion = v),
            Range(Tr.T("Weirdness"), Tr.T("variants of the biomes"), nameof(BiomeAsset.Weirdness), () => _biome.Weirdness, v => _biome.Weirdness = v),
        ];
    }

    private ClimateRangeViewModel Range(string label, string hint, string property, Func<ClimateRange> getter, Action<ClimateRange> setter) =>
        new(label, hint, getter, v => ChangeIfDifferent(Tr.F("Change the climate: {0}", label.ToLowerInvariant()), property, getter, setter, v));

    public override bool HasGameId => true;

    public IReadOnlyList<string> BaseBiomes => BiomeAsset.BaseBiomes;

    public IReadOnlyList<ClimateRangeViewModel> Climate { get; }

    public string BaseBiome
    {
        get => _biome.BaseBiome;
        set => ChangeIfDifferent(Tr.T("Change the base biome"), nameof(BiomeAsset.BaseBiome), () => _biome.BaseBiome, v => _biome.BaseBiome = v,
            string.IsNullOrWhiteSpace(value) ? "minecraft:plains" : value.Trim().ToLowerInvariant());
    }

    public double Temperature
    {
        get => _biome.Temperature;
        set => ChangeNumber(Tr.T("Change the temperature"), nameof(BiomeAsset.Temperature), value, () => _biome.Temperature, v => _biome.Temperature = v, v => Math.Clamp(v, -2, 2));
    }

    public double Downfall
    {
        get => _biome.Downfall;
        set => ChangeNumber(Tr.T("Change the downfall"), nameof(BiomeAsset.Downfall), value, () => _biome.Downfall, v => _biome.Downfall = v, v => Math.Clamp(v, 0, 1));
    }

    public bool HasPrecipitation
    {
        get => _biome.HasPrecipitation;
        set => ChangeIfDifferent(Tr.T("Change the precipitation"), nameof(BiomeAsset.HasPrecipitation), () => _biome.HasPrecipitation, v => _biome.HasPrecipitation = v, value);
    }

    public bool GeneratesInOverworld
    {
        get => _biome.GeneratesInOverworld;
        set => ChangeIfDifferent(Tr.T("Change \"appears in the overworld\""), nameof(BiomeAsset.GeneratesInOverworld), () => _biome.GeneratesInOverworld,
            v => _biome.GeneratesInOverworld = v, value);
    }

    public string TopBlock
    {
        get => _biome.TopBlock;
        set => ChangeIfDifferent(Tr.T("Change the surface block"), nameof(BiomeAsset.TopBlock), () => _biome.TopBlock, v => _biome.TopBlock = v, (value ?? string.Empty).Trim().ToLowerInvariant());
    }

    public string UnderBlock
    {
        get => _biome.UnderBlock;
        set => ChangeIfDifferent(Tr.T("Change the block under the surface"), nameof(BiomeAsset.UnderBlock), () => _biome.UnderBlock, v => _biome.UnderBlock = v,
            (value ?? string.Empty).Trim().ToLowerInvariant());
    }

    public string SkyColor { get => Hex(_biome.SkyColor); set => SetColor(nameof(BiomeAsset.SkyColor), value, () => _biome.SkyColor, v => _biome.SkyColor = v); }

    public string FogColor { get => Hex(_biome.FogColor); set => SetColor(nameof(BiomeAsset.FogColor), value, () => _biome.FogColor, v => _biome.FogColor = v); }

    public string WaterColor { get => Hex(_biome.WaterColor); set => SetColor(nameof(BiomeAsset.WaterColor), value, () => _biome.WaterColor, v => _biome.WaterColor = v); }

    public string WaterFogColor
    {
        get => Hex(_biome.WaterFogColor);
        set => SetColor(nameof(BiomeAsset.WaterFogColor), value, () => _biome.WaterFogColor, v => _biome.WaterFogColor = v);
    }

    /// <summary>"#RRGGBB", or empty: the color is computed from the climate.</summary>
    public string GrassColor
    {
        get => _biome.GrassColor is { } color ? Hex(color) : string.Empty;
        set => SetOptionalColor(nameof(BiomeAsset.GrassColor), value, () => _biome.GrassColor, v => _biome.GrassColor = v);
    }

    public string FoliageColor
    {
        get => _biome.FoliageColor is { } color ? Hex(color) : string.Empty;
        set => SetOptionalColor(nameof(BiomeAsset.FoliageColor), value, () => _biome.FoliageColor, v => _biome.FoliageColor = v);
    }

    private static string Hex(int color) => "#" + color.ToString("X6", CultureInfo.InvariantCulture);

    /// <summary>"#7907327", "78A7FF" or "#78a7ff" read as a color, or null.</summary>
    public static int? ParseColor(string? text)
    {
        string value = (text ?? string.Empty).Trim().TrimStart('#');
        return value.Length == 6 && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int color) ? color : null;
    }

    private void SetColor(string property, string value, Func<int> getter, Action<int> setter)
    {
        if (ParseColor(value) is { } color)
        {
            ChangeIfDifferent(Tr.T("Change a color of the biome"), property, getter, setter, color);
        }
    }

    private void SetOptionalColor(string property, string value, Func<int?> getter, Action<int?> setter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ChangeIfDifferent(Tr.T("Change a color of the biome"), property, getter, setter, null);
        }
        else if (ParseColor(value) is { } color)
        {
            ChangeIfDifferent(Tr.T("Change a color of the biome"), property, getter, setter, color);
        }
    }

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        OnPropertyChanged(propertyName);
        foreach (ClimateRangeViewModel range in Climate)
        {
            range.Refresh();
        }
    }
}
