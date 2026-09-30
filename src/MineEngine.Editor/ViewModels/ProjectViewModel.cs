using System.Collections.ObjectModel;
using MineEngine.Assets.Definitions;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels;

/// <summary>Projet ouvert dans l'éditeur : liste du contenu et sélection courante.</summary>
public sealed class ProjectViewModel : ObservableObject, IDisposable
{
    private readonly AssetViewModelFactory _factory;
    private AssetViewModel? _selectedAsset;

    public ProjectViewModel(ModProject model, AssetViewModelFactory factory)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        foreach (Asset asset in model.Assets)
        {
            Assets.Add(factory.Create(asset, model));
        }

        Model.DirtyStateChanged += OnDirtyStateChanged;
    }

    public ModProject Model { get; }

    public string Name => Model.Name;

    public bool IsDirty => Model.IsDirty;

    public string TargetDescription =>
        $"{Model.Settings.ModId}  |  Minecraft {Model.Settings.MinecraftVersion}  |  {Model.Settings.LoaderId}  |  MDK {Model.Settings.MdkId ?? "non choisi"}";

    public ObservableCollection<AssetViewModel> Assets { get; } = [];

    public AssetViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => _selectedAsset is not null;

    /// <summary>Crée un nouvel asset avec un identifiant libre et le sélectionne.</summary>
    public AssetViewModel AddAsset(AssetType type)
    {
        IAssetDefinition definition = _factory.Catalog.Get(type);
        Asset asset = definition.CreateNew(
            Model.Assets.CreateUniqueResourceId(definition.DefaultResourceIdBase), definition.DefaultDisplayName);
        Model.Assets.Add(asset);

        AssetViewModel viewModel = _factory.Create(asset, Model);
        Assets.Add(viewModel);
        SelectedAsset = viewModel;
        return viewModel;
    }

    public void RemoveAsset(AssetViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        int index = Assets.IndexOf(viewModel);
        if (index < 0)
        {
            return;
        }

        Model.Assets.Remove(viewModel.Model);
        Assets.RemoveAt(index);
        SelectedAsset = Assets.Count == 0 ? null : Assets[Math.Min(index, Assets.Count - 1)];
    }

    /// <summary>À appeler après une modification des paramètres du projet.</summary>
    public void RefreshSettings()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TargetDescription));
    }

    public void Dispose() => Model.DirtyStateChanged -= OnDirtyStateChanged;

    private void OnDirtyStateChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(IsDirty));
}
