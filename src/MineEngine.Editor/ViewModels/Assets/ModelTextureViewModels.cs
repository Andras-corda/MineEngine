using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>A variant of a model: another texture for the whole model, taken at random by each mob.</summary>
public sealed class ModelVariantViewModel : ObservableObject
{
    private readonly ModelAssetViewModel _owner;
    private readonly ModelAsset _model;
    private readonly int _index;

    public ModelVariantViewModel(ModelAssetViewModel owner, ModelAsset model, AssetEditingContext context, int index)
    {
        _owner = owner;
        _model = model;
        _index = index;
        Texture = new TextureSlotViewModel(
            model, context, nameof(ModelAsset.Variants), Name, Tr.T("(no texture)"),
            () => _index < _model.Variants.Count ? _model.Variants[_index].TextureId : null,
            v => _model.Variants = [.. _model.Variants.Select((variant, i) => i == _index ? variant with { TextureId = v } : variant)]);
        RemoveCommand = new RelayCommand(() => _owner.ChangeVariants(
            Tr.T("Remove a variant"), [.. _model.Variants.Where((_, i) => i != _index)]));
    }

    public string Name => _index < _model.Variants.Count ? _model.Variants[_index].Name : string.Empty;

    public TextureSlotViewModel Texture { get; }

    public ICommand RemoveCommand { get; }

    public void Refresh()
    {
        Texture.Label = Name;
        Texture.Refresh();
        OnPropertyChanged(nameof(Name));
    }
}

/// <summary>A layer of a model: a texture drawn over the mob with the same UVs; an emissive layer glows in the dark.</summary>
public sealed class ModelLayerViewModel : ObservableObject
{
    private readonly ModelAssetViewModel _owner;
    private readonly ModelAsset _model;
    private readonly int _index;

    public ModelLayerViewModel(ModelAssetViewModel owner, ModelAsset model, AssetEditingContext context, int index)
    {
        _owner = owner;
        _model = model;
        _index = index;
        Texture = new TextureSlotViewModel(
            model, context, nameof(ModelAsset.Layers), Name, Tr.T("(no texture)"),
            () => Layer?.TextureId,
            v => _model.Layers = [.. _model.Layers.Select((layer, i) => i == _index ? layer with { TextureId = v } : layer)]);
        RemoveCommand = new RelayCommand(() => _owner.ChangeLayers(
            Tr.T("Remove a layer"), [.. _model.Layers.Where((_, i) => i != _index)]));
    }

    public string Name => Layer?.Name ?? string.Empty;

    public bool Emissive
    {
        get => Layer?.Emissive ?? false;
        set
        {
            if (Layer is { } layer && layer.Emissive != value)
            {
                _owner.ChangeLayers(
                    value ? Tr.T("Make a layer emissive") : Tr.T("Make a layer normal"),
                    [.. _model.Layers.Select((l, i) => i == _index ? l with { Emissive = value } : l)]);
            }
        }
    }

    public TextureSlotViewModel Texture { get; }

    public ICommand RemoveCommand { get; }

    private ModelTextureLayer? Layer => _index < _model.Layers.Count ? _model.Layers[_index] : null;

    public void Refresh()
    {
        Texture.Label = Name;
        Texture.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Emissive));
    }
}
