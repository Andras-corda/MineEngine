using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Models;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Services;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>What an imported model is created for.</summary>
public enum ModelUse
{
    None,
    Item,
    Block,
    Mob,
}

/// <summary>
/// A texture proposed in the import window: an image embedded in the model, a texture of the
/// project, a PNG file to import, the texture named by a Java model, or nothing.
/// </summary>
public sealed record ImportTextureChoice(string Label, Guid? TextureId = null, string? File = null, int? Embedded = null)
{
    public bool IsEmpty => TextureId is null && File is null && Embedded is null;
}

/// <summary>
/// The import window of a 3D model (Blockbench or Java JSON): before the model is added, each of
/// its textures can be bound to a texture of the project or to a PNG file, variants (a cat with
/// several coats) and layers (a collar, glowing eyes) can be added, and an item, a block or a mob
/// can be created with it. Nothing is added to the project until <see cref="CreateAssets"/>.
/// </summary>
public sealed class ModelImportViewModel : ObservableObject
{
    private readonly ModProject _project;
    private readonly AssetFileImporter _importer;
    private readonly IDialogService _dialogs;
    private readonly BlockbenchModel? _blockbench;
    private string _name;
    private ModelUse _use;

    public ModelImportViewModel(ModProject project, AssetFileImporter importer, IDialogService dialogs, string file, ModelUse use = ModelUse.None)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        SourceFile = file;
        Format = ModelFormats.FromPath(file) ?? ModelFormat.JavaJson;
        _name = Path.GetFileName(file)[..^ModelFormats.Extension(file).Length];
        if (Format == ModelFormat.Blockbench)
        {
            _blockbench = BlockbenchModel.Load(file);
            if (_blockbench.Name.Length > 0)
            {
                _name = _blockbench.Name;
            }
        }

        _use = use == ModelUse.Mob && !CanBeMob ? ModelUse.None : use;
        foreach (ModelTextureSlot slot in ModelFormats.ReadSlots(file))
        {
            int? embedded = EmbeddedIndex(slot.Key);
            ImportTextureChoice fileChoice = Format == ModelFormat.Blockbench
                ? embedded is not null
                    ? new ImportTextureChoice(Tr.F("Image embedded in the model ({0})", slot.Name), Embedded: embedded)
                    : new ImportTextureChoice(Tr.T("Placeholder texture"))
                : new ImportTextureChoice(Tr.F("Texture of the file ({0})", slot.Name));
            Slots.Add(new ImportTextureRowViewModel(this, slot.Name.Length > 0 ? slot.Name : slot.Key, slot.Key, fileChoice, removable: false));
        }

        AddVariantCommand = new RelayCommand(() => Variants.Add(NewRow(Tr.F("Variant {0}", Variants.Count + 1))));
        AddVariantFilesCommand = new RelayCommand(AddVariantFiles);
        AddEmbeddedVariantsCommand = new RelayCommand(AddEmbeddedVariants, () => EmbeddedTextures.Skip(1).Any());
        AddLayerCommand = new RelayCommand(() => Layers.Add(NewRow(Tr.F("Layer {0}", Layers.Count + 1))));
        ConfirmCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true), () => Name.Trim().Length > 0);
    }

    /// <summary>Raised when the window must close (true: import).</summary>
    public event EventHandler<bool>? CloseRequested;

    public string SourceFile { get; }

    public string FileName => Path.GetFileName(SourceFile);

    public ModelFormat Format { get; }

    public string FormatLabel => ModelFormats.Label(Format);

    /// <summary>Format of the Blockbench project, number of cubes and groups, texture size.</summary>
    public string Summary => _blockbench is { } model
        ? Tr.F("{0} · {1} cubes · texture {2} x {3}", ModelAssetViewModel.FormatName(model.Format), model.Cubes.Count(), model.TextureWidth, model.TextureHeight)
        : Tr.T("Minecraft Java JSON model, for items and blocks");

    /// <summary>Name of the model, and of the element created with it.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    /// <summary>Only a Blockbench model becomes a mob model.</summary>
    public bool CanBeMob => Format == ModelFormat.Blockbench;

    public IReadOnlyList<Choice<ModelUse>> UseChoices =>
    [
        new(ModelUse.None, Tr.T("Nothing: only add the model")),
        new(ModelUse.Item, Tr.T("A new item")),
        new(ModelUse.Block, Tr.T("A new block")),
        .. CanBeMob ? [new Choice<ModelUse>(ModelUse.Mob, Tr.T("A new mob"))] : Array.Empty<Choice<ModelUse>>(),
    ];

    public ModelUse Use
    {
        get => _use;
        set
        {
            if (SetProperty(ref _use, value))
            {
                OnPropertyChanged(nameof(VariantsHint));
            }
        }
    }

    /// <summary>Textures of the model file.</summary>
    public ObservableCollection<ImportTextureRowViewModel> Slots { get; } = [];

    public bool HasSlots => Slots.Count > 0;

    public ObservableCollection<ImportTextureRowViewModel> Variants { get; } = [];

    public ObservableCollection<ImportTextureRowViewModel> Layers { get; } = [];

    public string VariantsHint => Use is ModelUse.Item or ModelUse.Block
        ? Tr.T("Variants and layers are used by mobs only: an item or a block shows the textures above.")
        : Tr.T("Variants are other textures for the whole model: each mob takes one at random (a cat with several coats). Layers are drawn over it with the same UVs (a collar, a saddle); an emissive layer glows in the dark, like the eyes of a spider.");

    public ICommand AddVariantCommand { get; }

    public ICommand AddVariantFilesCommand { get; }

    /// <summary>Adds the other images embedded in the Blockbench file as variants.</summary>
    public ICommand AddEmbeddedVariantsCommand { get; }

    public ICommand AddLayerCommand { get; }

    public ICommand ConfirmCommand { get; }

    /// <summary>Choices of every row: images of the model, then the textures of the project.</summary>
    internal IEnumerable<ImportTextureChoice> SharedChoices =>
    [
        .. EmbeddedTextures.Select(t => new ImportTextureChoice(Tr.F("Image embedded in the model ({0})", t.Name), Embedded: t.Index)),
        .. _project.Assets.OfType<TextureAsset>().OrderBy(t => t.ResourceId).Select(t => new ImportTextureChoice(Tr.F("Texture {0}", t.ResourceId), TextureId: t.Id)),
    ];

    internal IDialogService Dialogs => _dialogs;

    private IEnumerable<BlockbenchTexture> EmbeddedTextures => _blockbench?.Textures.Where(t => t.Png is not null) ?? [];

    /// <summary>Path of an image to preview a choice (embedded images are written to a temporary file).</summary>
    internal string? PreviewPath(ImportTextureChoice? choice)
    {
        if (choice is null)
        {
            return null;
        }

        if (choice.File is { } file)
        {
            return file;
        }

        if (choice.TextureId is { } id)
        {
            return _project.Assets.Find(id) is TextureAsset texture ? AssetFileImporter.GetExistingFile(_project, texture) : null;
        }

        if (choice.Embedded is { } index && _blockbench?.Textures[index].Png is { } png)
        {
            string directory = Path.Combine(Path.GetTempPath(), "MineEngine", "previews");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Convert.ToHexStringLower(SHA256.HashData(png))[..16] + ".png");
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, png);
            }

            return path;
        }

        return null;
    }

    internal void Remove(ImportTextureRowViewModel row)
    {
        Variants.Remove(row);
        Layers.Remove(row);
    }

    /// <summary>
    /// Creates the textures to import (PNG files, embedded images used as variants or layers), the
    /// model and the element chosen, in that order, without adding them to the project.
    /// </summary>
    public IReadOnlyList<Asset> CreateAssets(ISet<ResourceId> reservedIds, string folder)
    {
        ArgumentNullException.ThrowIfNull(reservedIds);
        var created = new List<Asset>();
        var files = new Dictionary<string, TextureAsset>(StringComparer.OrdinalIgnoreCase);
        var embedded = new Dictionary<int, TextureAsset>();

        // A texture of the model keeps its embedded image; a variant or a layer needs a texture of the project.
        Guid? Resolve(ImportTextureChoice? choice, bool extractEmbedded)
        {
            switch (choice)
            {
                case { TextureId: { } id }:
                    return id;
                case { File: { } file }:
                    if (!files.TryGetValue(file, out TextureAsset? imported))
                    {
                        imported = Add(_importer.ImportTexture(_project, file, reservedIds));
                        files[file] = imported;
                    }

                    return imported.Id;
                case { Embedded: { } index } when extractEmbedded && _blockbench?.Textures[index] is { Png: { } png } texture:
                    if (!embedded.TryGetValue(index, out TextureAsset? extracted))
                    {
                        extracted = Add(_importer.ImportTexture(_project, png, $"{Name}_{texture.Stem}", reservedIds));
                        embedded[index] = extracted;
                    }

                    return extracted.Id;
                default:
                    return null;
            }
        }

        TextureAsset Add(TextureAsset texture)
        {
            texture.Folder = folder;
            created.Add(texture);
            return texture;
        }

        IReadOnlyList<ModelTextureSlot> slots = [.. Slots.Select(s => new ModelTextureSlot(s.Key, s.Label, Resolve(s.Selected, extractEmbedded: false)))];
        IReadOnlyList<ModelTextureVariant> variants = [.. Variants.Select(v => new ModelTextureVariant(v.Name.Trim(), Resolve(v.Selected, extractEmbedded: true))).Where(v => v.TextureId is not null)];
        IReadOnlyList<ModelTextureLayer> layers = [.. Layers.Select(l => new ModelTextureLayer(l.Name.Trim(), Resolve(l.Selected, extractEmbedded: true), l.Emissive)).Where(l => l.TextureId is not null)];

        ModelAsset model = _importer.ImportModel(_project, SourceFile, reservedIds);
        model.DisplayName = Name.Trim();
        model.Folder = folder;
        model.Slots = slots;
        model.Variants = variants;
        model.Layers = layers;
        created.Add(model);

        if (CreateElement(model, reservedIds) is { } element)
        {
            element.Folder = folder;
            created.Add(element);
        }

        return created;
    }

    private TexturedAsset? CreateElement(ModelAsset model, ISet<ResourceId> reservedIds)
    {
        AssetType? type = Use switch
        {
            ModelUse.Item => AssetType.Item,
            ModelUse.Block => AssetType.Block,
            ModelUse.Mob => AssetType.Mob,
            _ => null,
        };
        if (type is not { } assetType)
        {
            return null;
        }

        // Models and game content have separate identifiers: the element may take the name of its model.
        bool modelReserved = reservedIds.Remove(model.ResourceId);
        ResourceId id = _project.Assets.CreateUniqueResourceId(model.ResourceId.Value, assetType);
        for (int suffix = 2; !reservedIds.Add(id); suffix++)
        {
            id = _project.Assets.CreateUniqueResourceId($"{model.ResourceId.Value}_{suffix}", assetType);
        }

        if (modelReserved)
        {
            reservedIds.Add(model.ResourceId);
        }

        string name = Name.Trim();
        return Use switch
        {
            ModelUse.Item => new ItemAsset(Guid.NewGuid(), id, name) { CustomModelId = model.Id },
            ModelUse.Block => new BlockAsset(Guid.NewGuid(), id, name) { Model = BlockModelKind.Custom, CustomModelId = model.Id },
            _ => new MobAsset(Guid.NewGuid(), id, name) { Model = MobModel.Custom, CustomModelId = model.Id },
        };
    }

    private ImportTextureRowViewModel NewRow(string name) => new(this, name, null, new ImportTextureChoice(Tr.T("(no texture)")), removable: true);

    private void AddVariantFiles()
    {
        foreach (string file in _dialogs.AskTextureFiles())
        {
            ImportTextureRowViewModel row = NewRow(Path.GetFileNameWithoutExtension(file));
            row.UseFile(file);
            Variants.Add(row);
        }
    }

    private void AddEmbeddedVariants()
    {
        foreach (BlockbenchTexture texture in EmbeddedTextures.Skip(1))
        {
            ImportTextureRowViewModel row = NewRow(texture.Stem);
            row.Selected = row.Choices.FirstOrDefault(c => c.Embedded == texture.Index);
            Variants.Add(row);
        }
    }

    private int? EmbeddedIndex(string key) =>
        int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
        && _blockbench is { } model && index < model.Textures.Count && model.Textures[index].Png is not null
            ? index
            : null;
}

/// <summary>A texture row of the import window: a texture of the model, a variant or a layer.</summary>
public sealed class ImportTextureRowViewModel : ObservableObject
{
    private readonly ModelImportViewModel _owner;
    private string _name;
    private ImportTextureChoice? _selected;
    private bool _emissive;

    public ImportTextureRowViewModel(ModelImportViewModel owner, string name, string? key, ImportTextureChoice fileChoice, bool removable)
    {
        _owner = owner;
        _name = name;
        Key = key ?? string.Empty;
        IsRemovable = removable;
        Choices = [fileChoice, .. owner.SharedChoices.Where(c => c != fileChoice)];
        _selected = fileChoice;
        ImportFileCommand = new RelayCommand(() =>
        {
            if (_owner.Dialogs.AskTextureFile() is { } file)
            {
                UseFile(file);
            }
        });
        RemoveCommand = new RelayCommand(() => _owner.Remove(this), () => IsRemovable);
    }

    /// <summary>Key of the texture in the model ("0", "1"...); empty for a variant or a layer.</summary>
    public string Key { get; }

    /// <summary>Name of a texture of the model (fixed), or of a variant or a layer (editable).</summary>
    public string Label => _name;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public bool IsRemovable { get; }

    public ObservableCollection<ImportTextureChoice> Choices { get; }

    public ImportTextureChoice? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(PreviewPath));
            }
        }
    }

    public bool Emissive
    {
        get => _emissive;
        set => SetProperty(ref _emissive, value);
    }

    public string? PreviewPath => _owner.PreviewPath(_selected);

    public ICommand ImportFileCommand { get; }

    public ICommand RemoveCommand { get; }

    /// <summary>Chooses a PNG file, imported with the model.</summary>
    public void UseFile(string file)
    {
        ImportTextureChoice choice = Choices.FirstOrDefault(c => string.Equals(c.File, file, StringComparison.OrdinalIgnoreCase))
                                     ?? new ImportTextureChoice(Tr.F("File {0}", Path.GetFileName(file)), File: file);
        if (!Choices.Contains(choice))
        {
            Choices.Add(choice);
        }

        Selected = choice;
    }
}
