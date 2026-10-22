using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
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
}

/// <summary>
/// A tile of the asset browser: a texture or a model of the project (<see cref="Id"/>), a texture or
/// a model of Minecraft (<see cref="Vanilla"/>), or the empty choice.
/// </summary>
public sealed record AssetBrowserItem(Guid Id, string Name, string? PreviewPath, string Details, string Folder, VanillaAsset? Vanilla = null)
{
    public bool IsNone => Id == Guid.Empty && Vanilla is null;

    public bool IsVanilla => Vanilla is not null;

    /// <summary>Files of Minecraft the asset comes from (a model is read from it when it is chosen).</summary>
    public VanillaAssetLibrary? Library { get; init; }
}

/// <summary>
/// Window that shows the textures or the 3D models as a grid of tiles (double-click on a texture
/// slot, Browse... of a 3D model), with a search, the size of the tiles, the import of new files,
/// and those of Minecraft itself: a texture or a model of the game is copied into the project
/// (folder "Minecraft") when it is chosen. Choosing a tile gives <see cref="Result"/>.
/// </summary>
public sealed class AssetBrowserViewModel : ObservableObject
{
    private readonly AssetEditingContext _context;
    private readonly string _emptyLabel;
    private readonly bool _entityModels;
    private readonly List<AssetBrowserItem> _vanillaItems = [];
    private string _search = string.Empty;
    private AssetBrowserItem? _selected;
    private double _tileSize = 96;
    private bool _showsMinecraft;
    private bool _isLoading;
    private string? _vanillaMessage;
    private Guid? _result;

    /// <param name="entityModels">Models for a mob: only Blockbench models (the models of Minecraft are not offered).</param>
    public AssetBrowserViewModel(AssetEditingContext context, AssetBrowserKind kind, Guid? current, string slotLabel, string emptyLabel, bool entityModels = false)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Kind = kind;
        _emptyLabel = emptyLabel;
        _entityModels = entityModels;
        Title = kind == AssetBrowserKind.Texture ? Tr.F("Choose a texture: {0}", slotLabel) : Tr.F("Choose a 3D model: {0}", slotLabel);
        View = new ListCollectionView(Items) { Filter = Matches };
        ImportCommand = new RelayCommand(Import);
        AcceptCommand = new RelayCommand(Accept, () => _selected is not null);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        Reload(current ?? Guid.Empty);
    }

    /// <summary>The window closes (true: the selected asset is chosen).</summary>
    public event EventHandler<bool>? CloseRequested;

    public AssetBrowserKind Kind { get; }

    public bool IsModel => Kind == AssetBrowserKind.Model;

    public string Title { get; }

    public ObservableCollection<AssetBrowserItem> Items { get; } = [];

    public ICollectionView View { get; }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
            {
                View.Refresh();
                OnPropertyChanged(nameof(CountLabel));
            }
        }
    }

    public string SearchHint => Kind == AssetBrowserKind.Texture ? Tr.T("Search a texture (name or folder)...") : Tr.T("Search a 3D model (name or folder)...");

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

    /// <summary>Width of the tiles (48 to 192 pixels).</summary>
    public double TileSize
    {
        get => _tileSize;
        set => SetProperty(ref _tileSize, Math.Clamp(value, 48, 192));
    }

    /// <summary>The tiles show the assets of Minecraft instead of those of the project.</summary>
    public bool ShowsMinecraft
    {
        get => _showsMinecraft;
        set
        {
            if (SetProperty(ref _showsMinecraft, value))
            {
                OnPropertyChanged(nameof(ShowsProject));
                if (value && _vanillaItems.Count == 0)
                {
                    LoadVanilla();
                }

                Fill();
            }
        }
    }

    public bool ShowsProject
    {
        get => !_showsMinecraft;
        set => ShowsMinecraft = !value;
    }

    /// <summary>Minecraft can be browsed (not for the model of a mob).</summary>
    public bool CanShowMinecraft => !_entityModels;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Where the files of Minecraft come from, or why there are none.</summary>
    public string? VanillaMessage
    {
        get => _vanillaMessage;
        private set => SetProperty(ref _vanillaMessage, value);
    }

    public string CountLabel => Kind == AssetBrowserKind.Texture
        ? Tr.F("{0} texture(s)", View.Cast<AssetBrowserItem>().Count(i => !i.IsNone))
        : Tr.F("{0} model(s)", View.Cast<AssetBrowserItem>().Count(i => !i.IsNone));

    public string SelectionLabel => _selected switch
    {
        null => Kind == AssetBrowserKind.Texture ? Tr.T("Click a texture, then Choose (or double-click it).") : Tr.T("Click a model, then Choose (or double-click it)."),
        { IsNone: true } => _emptyLabel,
        { IsVanilla: true } item => Tr.F("{0} · Minecraft: copied into the project when chosen", item.Vanilla!.Reference),
        { } item => $"{item.Name} · {item.Details}",
    };

    /// <summary>The asset chosen (a texture or a model of the project): null for the empty choice.</summary>
    public Guid? Result => _result;

    public ICommand ImportCommand { get; }

    public ICommand AcceptCommand { get; }

    public ICommand CancelCommand { get; }

    private readonly List<AssetBrowserItem> _projectItems = [];

    private void Reload(Guid selected)
    {
        _projectItems.Clear();
        _projectItems.Add(new AssetBrowserItem(Guid.Empty, _emptyLabel, null, string.Empty, string.Empty));
        if (Kind == AssetBrowserKind.Texture)
        {
            foreach (TextureAsset texture in _context.Project.Assets.OfType<TextureAsset>().OrderBy(t => t.ResourceId))
            {
                string? file = AssetFileImporter.GetExistingFile(_context.Project, texture);
                _projectItems.Add(new AssetBrowserItem(texture.Id, texture.ResourceId.Value, file,
                    Details(file is null ? Tr.T("file missing") : PngSize(file), texture.Id), texture.Folder));
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
                _projectItems.Add(new AssetBrowserItem(model.Id, model.ResourceId.Value, preview, Details(ModelFormats.Label(model.Format), model.Id), model.Folder));
            }
        }

        Fill();
        Selected = Items.FirstOrDefault(i => i.Id == selected && !i.IsVanilla) ?? (selected == Guid.Empty ? Items.FirstOrDefault() : null);
    }

    private string Details(string first, Guid id)
    {
        int usages = _context.Project.Assets.FindReferencesTo(id).Select(r => r.Source).Distinct().Count();
        return string.Join(" · ", new[] { first, usages == 0 ? Tr.T("unused") : Tr.F("used by {0}", usages) }.Where(p => p.Length > 0));
    }

    private void Fill()
    {
        AssetBrowserItem? selected = _selected;
        Items.Clear();
        foreach (AssetBrowserItem item in _showsMinecraft ? _vanillaItems : _projectItems)
        {
            Items.Add(item);
        }

        Selected = selected is not null && Items.Contains(selected) ? selected : null;
        OnPropertyChanged(nameof(CountLabel));
    }

    /// <summary>Reads the assets of Minecraft in the background (the textures are copied to a cache once).</summary>
    private async void LoadVanilla()
    {
        string version = _context.Project.Settings.MinecraftVersion;
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine", "vanilla");
        IsLoading = true;
        VanillaMessage = Kind == AssetBrowserKind.Texture ? Tr.T("Reading the textures of Minecraft...") : Tr.T("Reading the models of Minecraft...");
        try
        {
            List<AssetBrowserItem> items = await Task.Run(() => ReadVanilla(version, cache));
            _vanillaItems.Clear();
            _vanillaItems.AddRange(items);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            VanillaMessage = Tr.F("The files of Minecraft could not be read: {0}", exception.Message);
        }
        finally
        {
            IsLoading = false;
        }

        if (_showsMinecraft)
        {
            Fill();
        }
    }

    private List<AssetBrowserItem> ReadVanilla(string version, string cache)
    {
        if (VanillaAssetLibrary.For(version, cache) is not { } library)
        {
            Application.Current?.Dispatcher.Invoke(() => VanillaMessage =
                Tr.T("Minecraft was not found on this computer: launch the game once with the launcher, or prepare an MDK."));
            return [];
        }

        library.ExtractTextures();
        string source = library.Version == version
            ? Tr.F("Minecraft {0}", library.Version)
            : Tr.F("Minecraft {0} (the files of {1} were not found)", library.Version, version);
        Application.Current?.Dispatcher.Invoke(() => VanillaMessage = source);
        return Kind == AssetBrowserKind.Texture
            ? [.. library.Textures.Select(t => new AssetBrowserItem(Guid.Empty, t.Name, library.FileOf(t), t.Reference, "minecraft/" + t.Folder, t))]
            : [.. library.Models.Select(m => new AssetBrowserItem(Guid.Empty, m.Name,
                library.PreviewTexture(m) is { } texture ? library.FileOf(texture) : null, m.Reference, "minecraft/" + m.Folder, m) { Library = library })];
    }

    private bool Matches(object item)
    {
        if (item is not AssetBrowserItem asset)
        {
            return false;
        }

        string search = _search.Trim();
        return search.Length == 0 || asset.IsNone
               || asset.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
               || asset.Folder.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Chooses the selected tile; a texture or a model of Minecraft is first copied into the project (one undoable step).</summary>
    private void Accept()
    {
        if (_selected is not { } item)
        {
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
            ShowsMinecraft = false;
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
