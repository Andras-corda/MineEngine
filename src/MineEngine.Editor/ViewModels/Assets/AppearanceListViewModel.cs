using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// Appearances of an item, a block or a mob (section of its tab): other looks, numbered from 1,
/// that the appearance nodes of the graphs switch while the game runs. Every change goes
/// through the history.
/// </summary>
public sealed class AppearanceListViewModel : ObservableObject
{
    private readonly TexturedAsset _asset;
    private readonly AssetEditingContext _context;

    public AppearanceListViewModel(TexturedAsset asset, AssetEditingContext context)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        AddCommand = new RelayCommand(Add);
        Refresh();
    }

    public ObservableCollection<AppearanceRowViewModel> Rows { get; } = [];

    public ICommand AddCommand { get; }

    /// <summary>A mob only changes its texture; an item or a block can also take a 3D model.</summary>
    public bool CanUseModels => _asset.AppearancesCanUseModels;

    public string Hint => _asset switch
    {
        ItemAsset => Tr.T("Other looks of the item, switched in the graphs with \"Set item appearance\" (number shown on the left; 0: the normal look). Each appearance is a texture or a 3D model."),
        BlockAsset => Tr.T("Other looks of the block, switched in the graphs with \"Set block appearance\" for one block at a time (0: the normal look). Each appearance is a texture (on every face) or a 3D model."),
        _ => Tr.T("Other textures of the mob, switched in the graphs with \"Set creature appearance\" (0: the normal texture). The model of the mob stays the same."),
    };

    /// <summary>3D models an appearance can take.</summary>
    public IReadOnlyList<ModelOption> ModelOptions =>
    [
        new ModelOption(Guid.Empty, Tr.T("(none: the texture)")),
        .. _context.Project.Assets.OfType<ModelAsset>().Where(m => m.IsUsedByTheGame).OrderBy(m => m.ResourceId)
            .Select(m => new ModelOption(m.Id, $"{m.DisplayName} ({m.ResourceId})")),
    ];

    /// <summary>Rows follow the list of the asset (change, undo); rows of unchanged appearances are kept.</summary>
    public void Refresh()
    {
        IReadOnlyList<AssetAppearance> appearances = _asset.Appearances;
        Dictionary<Guid, AppearanceRowViewModel> existing = Rows.ToDictionary(r => r.Id);
        Rows.Clear();
        for (int i = 0; i < appearances.Count; i++)
        {
            AppearanceRowViewModel row = existing.TryGetValue(appearances[i].Id, out AppearanceRowViewModel? kept)
                ? kept
                : new AppearanceRowViewModel(this, _asset, _context, appearances[i].Id);
            row.Update(i + 1);
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(ModelOptions));
    }

    internal void Replace(Guid id, Func<AssetAppearance, AssetAppearance> change, string description) =>
        Change([.. _asset.Appearances.Select(a => a.Id == id ? change(a) : a)], description);

    internal void Remove(Guid id) => Change([.. _asset.Appearances.Where(a => a.Id != id)], Tr.T("Remove an appearance"));

    internal void Move(Guid id, int offset)
    {
        List<AssetAppearance> list = [.. _asset.Appearances];
        int index = list.FindIndex(a => a.Id == id);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return;
        }

        (list[index], list[target]) = (list[target], list[index]);
        Change(list, Tr.T("Change the order of the appearances"));
    }

    private void Add() =>
        Change([.. _asset.Appearances, AssetAppearance.Create(Tr.F("Appearance {0}", _asset.Appearances.Count + 1))], Tr.T("Add an appearance"));

    private void Change(IReadOnlyList<AssetAppearance> value, string description)
    {
        _context.History.Execute(new PropertyChangeCommand<IReadOnlyList<AssetAppearance>>(
            description, _asset, nameof(TexturedAsset.Appearances), () => _asset.Appearances, v => _asset.Appearances = v, value));
        _context.History.Seal();
    }
}

/// <summary>An appearance in the list: its number, its name, its texture and, for an item or a block, its 3D model.</summary>
public sealed class AppearanceRowViewModel : ObservableObject
{
    private readonly AppearanceListViewModel _owner;
    private readonly TexturedAsset _asset;
    private int _number;

    public AppearanceRowViewModel(AppearanceListViewModel owner, TexturedAsset asset, AssetEditingContext context, Guid id)
    {
        _owner = owner;
        _asset = asset;
        Id = id;
        Texture = new TextureSlotViewModel(
            asset, context, nameof(TexturedAsset.Appearances), Tr.T("Texture"), Tr.T("None"),
            () => Model?.TextureId,
            // The slot puts the change in the history itself: the setter only writes the list.
            v => asset.Appearances = [.. asset.Appearances.Select(a => a.Id == Id ? a with { TextureId = v } : a)]);
        RemoveCommand = new RelayCommand(() => _owner.Remove(Id));
        MoveUpCommand = new RelayCommand(() => _owner.Move(Id, -1), () => _number > 1);
        MoveDownCommand = new RelayCommand(() => _owner.Move(Id, 1), () => _number < _asset.Appearances.Count);
    }

    public Guid Id { get; }

    /// <summary>Number given to the appearance nodes (1, 2...).</summary>
    public int Number => _number;

    public string Name
    {
        get => Model?.Name ?? string.Empty;
        set => _owner.Replace(Id, a => a with { Name = (value ?? string.Empty).Trim() }, Tr.T("Rename an appearance"));
    }

    public TextureSlotViewModel Texture { get; }

    public bool CanUseModels => _owner.CanUseModels;

    public IReadOnlyList<ModelOption> ModelOptions => _owner.ModelOptions;

    public Guid ModelId
    {
        get => Model?.ModelId ?? Guid.Empty;
        set => _owner.Replace(Id, a => a with { ModelId = value == Guid.Empty ? null : value }, Tr.T("Change the 3D model of an appearance"));
    }

    public ICommand RemoveCommand { get; }

    public ICommand MoveUpCommand { get; }

    public ICommand MoveDownCommand { get; }

    private AssetAppearance? Model => _asset.Appearances.FirstOrDefault(a => a.Id == Id);

    internal void Update(int number)
    {
        _number = number;
        OnPropertyChanged(nameof(Number));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ModelId));
        OnPropertyChanged(nameof(ModelOptions));
        Texture.Refresh();
        CommandManager.InvalidateRequerySuggested();
    }
}
