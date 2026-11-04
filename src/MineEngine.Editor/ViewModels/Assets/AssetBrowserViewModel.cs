using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;
using MineEngine.Project;
using MineEngine.Project.Vanilla;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>What the asset browser chooses.</summary>
public enum AssetBrowserKind
{
    Texture,
    Model,

    /// <summary>An item or a block with an item form, of the project or of Minecraft, or a tag of items.</summary>
    Item,
}

/// <summary>
/// A tile of the asset browser: a texture or a model of the project (<see cref="Id"/>), a texture or
/// a model of Minecraft (<see cref="Vanilla"/>), or the empty choice.
/// </summary>
public sealed record AssetBrowserItem(Guid Id, string Name, string? PreviewPath, string Details, string Folder, VanillaAsset? Vanilla = null)
{
    public bool IsNone => Id == Guid.Empty && Vanilla is null && Reference is null;

    /// <summary>Item of Minecraft or tag chosen by an item tile ("minecraft:diamond", "#minecraft:planks").</summary>
    public ContentReference? Reference { get; init; }

    /// <summary>A tag of items: shown with "#" instead of an image.</summary>
    public bool IsTag => Reference?.Kind == ContentReferenceKind.Tag;

    public bool IsVanilla => Vanilla is not null;

    /// <summary>Name with the extension of the file, shown by the tooltip ("ruby.png", "minecraft:block/stone.json").</summary>
    public string FileName { get; init; } = Name;

    /// <summary>Format of a model ("JSON", "OBJ"...), shown on its tile; empty for a texture.</summary>
    public string Badge { get; init; } = string.Empty;

    public bool IsModelTile => Badge.Length > 0;

    /// <summary>Keys of the filters that keep the tile ("project", "minecraft", "mc:block", "format:Obj"...).</summary>
    public IReadOnlyList<string> Filters { get; init; } = [];

    /// <summary>Preview decoded in advance (in the background for the files of Minecraft), or null.</summary>
    public System.Windows.Media.ImageSource? Image { get; init; }

    /// <summary>A frozen image of a PNG file (usable from any thread), or null; decoded at its own size so the pixels stay sharp.</summary>
    public static System.Windows.Media.ImageSource? LoadImage(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            using (FileStream stream = File.OpenRead(path))
            {
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Files of Minecraft a model comes from (it is read from them when it is chosen).</summary>
    public VanillaAssetLibrary? Library { get; init; }
}

/// <summary>
/// Window that shows the textures or the 3D models as a grid of icons, like the element selector
/// of MCreator: those of the project and those of Minecraft together, a filter (origin, folder of
/// the game or format), a search, the import of new files and the name of each file in its tooltip.
/// The textures of Minecraft are shipped with the editor (<see cref="MinecraftTextures"/>); its
/// models are read from the jar of the game. A texture or a model of the game is copied into the
/// project (folder "Minecraft") when it is chosen. Choosing a tile gives <see cref="Result"/>.
/// </summary>
public sealed class AssetBrowserViewModel : ObservableObject
{
    private const string All = "all";
    private const string ProjectFilter = "project";
    private const string MinecraftFilter = "minecraft";
    private const string TagFilter = "tags";

    /// <summary>Tags of items of the game offered for an ingredient, with those of the project.</summary>
    private static readonly string[] VanillaItemTags =
    [
        "minecraft:planks", "minecraft:logs", "minecraft:wool", "minecraft:coals", "minecraft:stone_crafting_materials",
        "minecraft:stone_tool_materials", "minecraft:wooden_slabs", "minecraft:sand", "minecraft:leaves", "minecraft:saplings",
        "minecraft:small_flowers", "minecraft:fishes", "minecraft:arrows", "minecraft:beds", "minecraft:candles",
    ];

    /// <summary>Faces and states of block textures that are not items ("oak_log_top", "furnace_front_on"...).</summary>
    private static readonly string[] BlockFaceSuffixes =
    [
        "_top", "_bottom", "_side", "_front", "_back", "_end", "_inner", "_outer", "_on", "_off", "_particle", "_open", "_still", "_flow",
        "_overlay", "_lit", "_stage0", "_stage1", "_stage2", "_stage3", "_stage4", "_stage5", "_stage6", "_stage7", "_bloom", "_moist",
    ];

    /// <summary>Tiles of the shipped textures, decoded once for the whole session.</summary>
    private static Task<List<AssetBrowserItem>>? _shippedTextures;

    /// <summary>Items of Minecraft, guessed from the shipped textures: item/*, and block/* without the faces.</summary>
    private static Task<List<AssetBrowserItem>>? _shippedItems;

    private readonly AssetEditingContext _context;
    private readonly string _emptyLabel;
    private readonly bool _entityModels;
    private readonly List<AssetBrowserItem> _projectItems = [];
    private readonly List<AssetBrowserItem> _vanillaItems = [];
    private string _search = string.Empty;
    private string _filter = All;
    private AssetBrowserItem? _selected;
    private bool _isLoading;
    private string? _vanillaMessage;
    private Guid? _result;
    private ContentReference? _referenceResult;
    private readonly ContentReference? _currentReference;
    private readonly bool _allowTags;
    private ICollectionView _view;

    /// <param name="entityModels">Models for a mob: only Blockbench models (the models of Minecraft are not offered).</param>
    public AssetBrowserViewModel(AssetEditingContext context, AssetBrowserKind kind, Guid? current, string slotLabel, string emptyLabel, bool entityModels = false)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Kind = kind;
        _emptyLabel = emptyLabel;
        _entityModels = entityModels;
        Title = kind == AssetBrowserKind.Texture ? Tr.F("Choose a texture: {0}", slotLabel) : Tr.F("Choose a 3D model: {0}", slotLabel);
        _view = new ListCollectionView(new List<AssetBrowserItem>());
        ImportCommand = new RelayCommand(Import);
        AcceptCommand = new RelayCommand(Accept, () => _selected is not null);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        Filters = [.. BuildFilters()];
        Reload(current ?? Guid.Empty);
        if (!_entityModels)
        {
            LoadVanilla();
        }
    }

    /// <summary>Chooses an item (a recipe slot...): of the project, of Minecraft or, with <paramref name="allowTags"/>, a tag of items.</summary>
    public AssetBrowserViewModel(AssetEditingContext context, ContentReference? current, string slotLabel, string emptyLabel, bool allowTags)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Kind = AssetBrowserKind.Item;
        _emptyLabel = emptyLabel;
        _currentReference = current;
        _allowTags = allowTags;
        Title = Tr.F("Choose an item: {0}", slotLabel);
        _view = new ListCollectionView(new List<AssetBrowserItem>());
        ImportCommand = new RelayCommand(() => { }, () => false);
        AcceptCommand = new RelayCommand(Accept, () => _selected is not null);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        Filters = [.. BuildFilters()];
        Reload(Guid.Empty);
        LoadVanilla();
    }

    /// <summary>The window closes (true: the selected asset is chosen).</summary>
    public event EventHandler<bool>? CloseRequested;

    public AssetBrowserKind Kind { get; }

    public bool IsModel => Kind == AssetBrowserKind.Model;

    public string Title { get; }

    /// <summary>Every tile, the filter and the search aside: the project first, then Minecraft.</summary>
    public IReadOnlyList<AssetBrowserItem> Items { get; private set; } = [];

    /// <summary>The tiles shown (filter and search applied).</summary>
    public ICollectionView View
    {
        get => _view;
        private set => SetProperty(ref _view, value);
    }

    /// <summary>Choices of the filter: everything, the project, Minecraft, then its folders (textures) or the formats (models).</summary>
    public IReadOnlyList<Choice<string>> Filters { get; }

    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value ?? All))
            {
                Refresh();
            }
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
            {
                Refresh();
            }
        }
    }

    public string SearchHint => Kind switch
    {
        AssetBrowserKind.Texture => Tr.T("Search a texture (name or folder)..."),
        AssetBrowserKind.Item => Tr.T("Search an item or a tag..."),
        _ => Tr.T("Search a 3D model (name or folder)..."),
    };

    /// <summary>Files can be imported (textures, models; not items).</summary>
    public bool CanImport => Kind != AssetBrowserKind.Item;

    public AssetBrowserItem? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(SelectionLabel));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Where the models of Minecraft come from, or why there are none.</summary>
    public string? VanillaMessage
    {
        get => _vanillaMessage;
        private set => SetProperty(ref _vanillaMessage, value);
    }

    public string CountLabel => Kind switch
    {
        AssetBrowserKind.Texture => Tr.F("{0} texture(s)", View.Cast<AssetBrowserItem>().Count(i => !i.IsNone)),
        AssetBrowserKind.Item => Tr.F("{0} item(s)", View.Cast<AssetBrowserItem>().Count(i => !i.IsNone)),
        _ => Tr.F("{0} model(s)", View.Cast<AssetBrowserItem>().Count(i => !i.IsNone)),
    };

    public string SelectionLabel => _selected switch
    {
        null => Kind switch
        {
            AssetBrowserKind.Texture => Tr.T("Click a texture, then Select (or double-click it)."),
            AssetBrowserKind.Item => Tr.T("Click an item, then Select (or double-click it)."),
            _ => Tr.T("Click a model, then Select (or double-click it)."),
        },
        { Reference: not null } item => item.FileName,
        { IsNone: true } => _emptyLabel,
        { IsVanilla: true } item => Tr.F("{0} · Minecraft: copied into the project when chosen", item.FileName),
        { } item => $"{item.FileName} · {item.Details}",
    };

    /// <summary>The asset chosen (a texture or a model of the project): null for the empty choice.</summary>
    public Guid? Result => _result;

    /// <summary>The item or tag chosen by an item browser: null for the empty choice.</summary>
    public ContentReference? ReferenceResult => _referenceResult;

    public ICommand ImportCommand { get; }

    public ICommand AcceptCommand { get; }

    public ICommand CancelCommand { get; }

    private IEnumerable<Choice<string>> BuildFilters()
    {
        yield return new Choice<string>(All, Tr.T("All"));
        yield return new Choice<string>(ProjectFilter, Tr.T("Elements of the project"));
        if (_entityModels)
        {
            yield break;
        }

        yield return new Choice<string>(MinecraftFilter, "Minecraft");
        if (Kind == AssetBrowserKind.Item)
        {
            yield return new Choice<string>("mc:item", "Minecraft · item");
            yield return new Choice<string>("mc:block", "Minecraft · block");
            if (_allowTags)
            {
                yield return new Choice<string>(TagFilter, Tr.T("Tags"));
            }
        }
        else if (Kind == AssetBrowserKind.Texture)
        {
            foreach (string folder in MinecraftTextures.All.Select(MinecraftTextures.TopFolder).Where(f => f.Length > 0).Distinct().Order(StringComparer.Ordinal))
            {
                yield return new Choice<string>("mc:" + folder, "Minecraft · " + folder);
            }
        }
        else
        {
            foreach (ModelFormat format in Enum.GetValues<ModelFormat>())
            {
                yield return new Choice<string>("format:" + format, ModelFormats.Label(format));
            }
        }
    }

    private void Reload(Guid selected)
    {
        _projectItems.Clear();
        _projectItems.Add(new AssetBrowserItem(Guid.Empty, _emptyLabel, null, string.Empty, string.Empty) { FileName = _emptyLabel });
        if (Kind == AssetBrowserKind.Item)
        {
            foreach (Core.Assets.Asset asset in _context.Project.Assets.Where(a => a is ItemAsset or BlockAsset { HasItemForm: true }).OrderBy(a => a.ResourceId))
            {
                ContentReference reference = ContentReference.ToAsset(asset.Id);
                string? icon = ItemIcons.ProjectFile(_context.Project, reference);
                _projectItems.Add(new AssetBrowserItem(asset.Id, asset.ResourceId.Value, icon, Details(asset is BlockAsset ? Tr.T("Block") : Tr.T("Item"), asset.Id), asset.Folder)
                {
                    FileName = $"{_context.Project.Settings.ModId}:{asset.ResourceId}",
                    Filters = [ProjectFilter],
                    Image = AssetBrowserItem.LoadImage(icon),
                });
            }

            if (_allowTags)
            {
                IEnumerable<string> tags = _context.Project.Assets.OfType<TagAsset>().Where(t => t.Registry == TagRegistry.Items)
                    .Select(t => t.Reference(_context.Project.Settings.ModId.Value).TrimStart('#'))
                    .Concat(VanillaItemTags).Distinct().Order(StringComparer.Ordinal);
                foreach (string tag in tags)
                {
                    if (Core.Identifiers.NamespacedId.TryParse(tag, out Core.Identifiers.NamespacedId? id))
                    {
                        _projectItems.Add(new AssetBrowserItem(Guid.Empty, tag, null, Tr.T("Tag of items: any of its items"), "tags")
                        {
                            FileName = "#" + tag,
                            Filters = [TagFilter],
                            Reference = ContentReference.ToTag(id!),
                        });
                    }
                }
            }
        }
        else if (Kind == AssetBrowserKind.Texture)
        {
            foreach (TextureAsset texture in _context.Project.Assets.OfType<TextureAsset>().OrderBy(t => t.ResourceId))
            {
                string? file = AssetFileImporter.GetExistingFile(_context.Project, texture);
                _projectItems.Add(new AssetBrowserItem(texture.Id, texture.ResourceId.Value, file,
                    Details(file is null ? Tr.T("file missing") : PngSize(file), texture.Id), texture.Folder)
                {
                    FileName = texture.ResourceId.Value + ".png",
                    Filters = [ProjectFilter],
                    Image = AssetBrowserItem.LoadImage(file),
                });
            }
        }
        else
        {
            foreach (ModelAsset model in _context.Project.Assets.OfType<ModelAsset>()
                         .Where(m => _entityModels ? m.CanBeEntityModel : m.IsUsedByTheGame).OrderBy(m => m.ResourceId))
            {
                string? preview = model.Slots.Select(s => s.TextureId).OfType<Guid>()
                    .Select(id => _context.Project.Assets.Find(id) as TextureAsset).OfType<TextureAsset>()
                    .Select(t => AssetFileImporter.GetExistingFile(_context.Project, t)).FirstOrDefault(f => f is not null);
                _projectItems.Add(new AssetBrowserItem(model.Id, model.ResourceId.Value, preview, Details(ModelFormats.Label(model.Format), model.Id), model.Folder)
                {
                    FileName = model.ResourceId.Value + ModelFormats.Extension(model.FilePath),
                    Badge = BadgeOf(model.Format),
                    Filters = [ProjectFilter, "format:" + model.Format],
                    Image = AssetBrowserItem.LoadImage(preview),
                });
            }
        }

        Fill();
        Selected = Kind == AssetBrowserKind.Item
            ? Items.FirstOrDefault(i => _currentReference is not null && (Equals(i.Reference, _currentReference) || (i.Id != Guid.Empty && i.Id == _currentReference.AssetId && _currentReference.Kind == ContentReferenceKind.Asset)))
              ?? (_currentReference is null ? Items.FirstOrDefault() : null)
            : Items.FirstOrDefault(i => i.Id == selected && !i.IsVanilla) ?? (selected == Guid.Empty ? Items.FirstOrDefault() : null);
    }

    /// <summary>Short name of a format on the tile of a model.</summary>
    private static string BadgeOf(ModelFormat format) => format switch
    {
        ModelFormat.JavaJson => "JSON",
        ModelFormat.Blockbench => "BB",
        ModelFormat.BedrockGeometry => "GEO",
        ModelFormat.Obj => "OBJ",
        _ => "GLTF",
    };

    private string Details(string first, Guid id)
    {
        int usages = _context.Project.Assets.FindReferencesTo(id).Select(r => r.Source).Distinct().Count();
        return string.Join(" · ", new[] { first, usages == 0 ? Tr.T("unused") : Tr.F("used by {0}", usages) }.Where(p => p.Length > 0));
    }

    /// <summary>The project first, then Minecraft.</summary>
    private void Fill()
    {
        // A new view at once: thousands of tiles would be slow to add one by one.
        AssetBrowserItem? selected = _selected;
        var items = new List<AssetBrowserItem>(_projectItems);
        items.AddRange(_vanillaItems);
        Items = items;
        View = new ListCollectionView(items) { Filter = Matches };
        Selected = selected is not null && Items.Contains(selected) ? selected : null;
        OnPropertyChanged(nameof(CountLabel));
    }

    private void Refresh()
    {
        View.Refresh();
        OnPropertyChanged(nameof(CountLabel));
    }

    /// <summary>Adds the assets of Minecraft in the background: the shipped textures, or the models of the jar.</summary>
    private async void LoadVanilla()
    {
        IsLoading = true;
        try
        {
            if (Kind == AssetBrowserKind.Item)
            {
                _shippedItems ??= Task.Run(ReadShippedItems);
                _vanillaItems.AddRange(await _shippedItems);
                VanillaMessage = null;
            }
            else if (Kind == AssetBrowserKind.Texture)
            {
                _shippedTextures ??= Task.Run(ReadShippedTextures);
                _vanillaItems.AddRange(await _shippedTextures);
                VanillaMessage = _vanillaItems.Count == 0 ? Tr.T("The textures of Minecraft are missing next to the application.") : null;
            }
            else
            {
                string version = _context.Project.Settings.MinecraftVersion;
                string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine", "vanilla");
                VanillaMessage = Tr.T("Reading the models of Minecraft...");
                _vanillaItems.AddRange(await Task.Run(() => ReadVanillaModels(version, cache)));
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            VanillaMessage = Tr.F("The files of Minecraft could not be read: {0}", exception.Message);
        }
        finally
        {
            IsLoading = false;
        }

        Fill();
    }

    private static List<AssetBrowserItem> ReadShippedTextures() =>
    [
        .. MinecraftTextures.All.Select(path =>
        {
            string file = MinecraftTextures.FileOf(path)!;
            var asset = new VanillaAsset(VanillaAssetKind.Texture, path);
            return new AssetBrowserItem(Guid.Empty, asset.Name, file, asset.Reference, "minecraft/" + asset.Folder, asset)
            {
                FileName = asset.Reference + ".png",
                Filters = [MinecraftFilter, "mc:" + MinecraftTextures.TopFolder(path)],
                Image = AssetBrowserItem.LoadImage(file),
            };
        }),
    ];

    private static List<AssetBrowserItem> ReadShippedItems()
    {
        var items = new List<AssetBrowserItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in MinecraftTextures.All)
        {
            string folder = MinecraftTextures.TopFolder(path);
            string name = path[(path.LastIndexOf('/') + 1)..];
            bool item = folder == "item" && path.Count(c => c == '/') == 1;
            bool block = folder == "block" && path.Count(c => c == '/') == 1 && !BlockFaceSuffixes.Any(s => name.EndsWith(s, StringComparison.Ordinal));
            if (!(item || block) || !seen.Add(name) || !Core.Identifiers.NamespacedId.TryParse("minecraft:" + name, out Core.Identifiers.NamespacedId? id))
            {
                continue;
            }

            string file = MinecraftTextures.FileOf(path)!;
            items.Add(new AssetBrowserItem(Guid.Empty, name, file, "minecraft:" + name, "minecraft/" + folder)
            {
                FileName = "minecraft:" + name,
                Filters = [MinecraftFilter, "mc:" + folder],
                Reference = ContentReference.ToId(id!),
                Image = AssetBrowserItem.LoadImage(file),
            });
        }

        return items;
    }

    private List<AssetBrowserItem> ReadVanillaModels(string version, string cache)
    {
        if (VanillaAssetLibrary.For(version, cache) is not { } library)
        {
            Application.Current?.Dispatcher.Invoke(() => VanillaMessage =
                Tr.T("The models of Minecraft come from the game: launch it once with the launcher, or prepare an MDK."));
            return [];
        }

        string source = library.Version == version
            ? Tr.F("Models of Minecraft {0}", library.Version)
            : Tr.F("Models of Minecraft {0} (the files of {1} were not found)", library.Version, version);
        Application.Current?.Dispatcher.Invoke(() => VanillaMessage = source);
        return
        [
            .. library.Models.Select(model =>
            {
                // The icon of a model is its first texture, shipped with the editor (or read from the jar).
                VanillaAsset? texture = library.PreviewTexture(model);
                string? preview = texture is null ? null : MinecraftTextures.FileOf(texture.Path) ?? library.FileOf(texture);
                return new AssetBrowserItem(Guid.Empty, model.Name, preview, model.Reference, "minecraft/" + model.Folder, model)
                {
                    FileName = model.Reference + ".json",
                    Badge = BadgeOf(ModelFormat.JavaJson),
                    Filters = [MinecraftFilter, "format:" + ModelFormat.JavaJson],
                    Library = library,
                    Image = AssetBrowserItem.LoadImage(preview),
                };
            }),
        ];
    }

    private bool Matches(object item)
    {
        if (item is not AssetBrowserItem asset)
        {
            return false;
        }

        if (asset.IsNone)
        {
            return true;
        }

        if (_filter != All && !asset.Filters.Contains(_filter))
        {
            return false;
        }

        string search = _search.Trim();
        return search.Length == 0
               || asset.FileName.Contains(search, StringComparison.OrdinalIgnoreCase)
               || asset.Folder.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Chooses the selected tile; a texture or a model of Minecraft is first copied into the project (one undoable step).</summary>
    private void Accept()
    {
        if (_selected is not { } item)
        {
            return;
        }

        if (Kind == AssetBrowserKind.Item)
        {
            _referenceResult = item.IsNone ? null : item.Reference ?? ContentReference.ToAsset(item.Id);
            CloseRequested?.Invoke(this, true);
            return;
        }

        if (item.Vanilla is not { } vanilla)
        {
            _result = item.IsNone ? null : item.Id;
            CloseRequested?.Invoke(this, true);
            return;
        }

        try
        {
            _result = Kind == AssetBrowserKind.Texture ? CopyVanillaTexture(vanilla, item) : CopyVanillaModel(vanilla, item);
            CloseRequested?.Invoke(this, true);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _context.Dialogs.ShowError(Kind == AssetBrowserKind.Texture ? Tr.T("Texture") : Tr.T("3D model"), exception.Message);
        }
    }

    private static string VanillaFolder(VanillaAsset vanilla) => "Minecraft/" + vanilla.Folder;

    private Guid CopyVanillaTexture(VanillaAsset vanilla, AssetBrowserItem item)
    {
        // The same texture of the game is copied only once.
        if (_context.Project.Assets.OfType<TextureAsset>().FirstOrDefault(t => t.Folder == VanillaFolder(vanilla) && t.DisplayName == vanilla.Reference) is { } existing)
        {
            return existing.Id;
        }

        string file = item.PreviewPath ?? throw new IOException(Tr.F("'{0}' was not found in the files of Minecraft.", vanilla.Reference));
        TextureAsset texture = _context.Importer.ImportTexture(_context.Project, File.ReadAllBytes(file), vanilla.Name);
        texture.Folder = VanillaFolder(vanilla);
        texture.DisplayName = vanilla.Reference;
        _context.History.Execute(new AddAssetCommand(_context.Project.Assets, texture, Tr.T("Texture")));
        _context.History.Seal();
        return texture.Id;
    }

    private Guid CopyVanillaModel(VanillaAsset vanilla, AssetBrowserItem item)
    {
        if (_context.Project.Assets.OfType<ModelAsset>().FirstOrDefault(m => m.Folder == VanillaFolder(vanilla) && m.DisplayName == vanilla.Reference) is { } existing)
        {
            return existing.Id;
        }

        string file = item.Library?.FileOf(vanilla) ?? throw new IOException(Tr.F("'{0}' was not found in the files of Minecraft.", vanilla.Reference));
        ModelAsset model = _context.Importer.ImportModel(_context.Project, file);
        model.Folder = VanillaFolder(vanilla);
        model.DisplayName = vanilla.Reference;
        _context.History.Execute(new AddAssetCommand(_context.Project.Assets, model, Tr.T("3D model")));
        _context.History.Seal();
        return model.Id;
    }

    /// <summary>Imports files (PNG images, or 3D models) as assets of the project (one step to undo) and selects the last one.</summary>
    private void Import()
    {
        IReadOnlyList<string> files = Kind == AssetBrowserKind.Texture ? _context.Dialogs.AskTextureFiles() : _context.Dialogs.AskModelFiles();
        if (files.Count == 0)
        {
            return;
        }

        var added = new List<Core.Assets.Asset>();
        var errors = new List<string>();
        foreach (string file in files)
        {
            try
            {
                if (Kind == AssetBrowserKind.Model && ModelFormats.DescribeProblem(file) is { } problem)
                {
                    throw new InvalidDataException(problem);
                }

                added.Add(Kind == AssetBrowserKind.Texture
                    ? _context.Importer.ImportTexture(_context.Project, file)
                    : _context.Importer.ImportModel(_context.Project, file));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(file)}: {exception.Message}");
            }
        }

        if (added.Count > 0)
        {
            string label = Kind == AssetBrowserKind.Texture ? Tr.T("Texture") : Tr.T("3D model");
            _context.History.Execute(new CompositeCommand(
                Kind == AssetBrowserKind.Texture ? Tr.T("Import textures") : Tr.T("Import 3D models"),
                [.. added.Select(a => (IUndoableCommand)new AddAssetCommand(_context.Project.Assets, a, label))]));
            _context.History.Seal();
            Search = string.Empty;
            Filter = All;
            Reload(added[^1].Id);
        }

        if (errors.Count > 0)
        {
            _context.Dialogs.ShowError(Kind == AssetBrowserKind.Texture ? Tr.T("Texture") : Tr.T("3D model"), string.Join("\n", errors));
        }
    }

    /// <summary>"16 x 16" from the header of a PNG file, or an empty text.</summary>
    private static string PngSize(string file)
    {
        try
        {
            using FileStream stream = File.OpenRead(file);
            Span<byte> header = stackalloc byte[24];
            if (stream.Read(header) < 24)
            {
                return string.Empty;
            }

            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return string.Create(CultureInfo.InvariantCulture, $"{width} x {height}");
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}
