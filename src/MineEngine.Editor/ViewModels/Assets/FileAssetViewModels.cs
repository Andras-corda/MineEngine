using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Models;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;
using MineEngine.Project;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Asset dont le contenu est un fichier : chemin, remplacement et ouverture du fichier.</summary>
public abstract class FileAssetViewModel : AssetViewModel
{
    private readonly FileAsset _file;

    protected FileAssetViewModel(FileAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _file = model;
        ReplaceCommand = new RelayCommand(Replace);
        OpenFileCommand = new RelayCommand(() => Context.Shell.OpenFile(FullPath!), () => FullPath is not null);
    }

    public string FilePath => _file.FilePath;

    /// <summary>Chemin absolu du fichier, ou null s'il a disparu.</summary>
    public string? FullPath => AssetFileImporter.GetExistingFile(Context.Project, _file);

    public bool IsFileMissing => FullPath is null;

    public ICommand ReplaceCommand { get; }

    public ICommand OpenFileCommand { get; }

    /// <summary>Demande le fichier de remplacement ; null si l'utilisateur annule.</summary>
    protected abstract string? AskReplacementFile();

    /// <summary>Copie le fichier dans le projet et retourne son chemin relatif.</summary>
    protected abstract string CopyIntoProject(string sourceFile);

    /// <summary>Other changes that come with a new file (the texture slots of a model), undone with it.</summary>
    protected virtual IEnumerable<IUndoableCommand> ReplacementCommands(string sourceFile) => [];

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(FileAsset.FilePath))
        {
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(IsFileMissing));
        }
    }

    /// <summary>Le nouveau fichier est copié à côté de l'ancien : l'annulation retrouve l'ancien.</summary>
    private void Replace()
    {
        string? source = AskReplacementFile();
        if (source is null)
        {
            return;
        }

        try
        {
            string path = CopyIntoProject(source);
            Context.History.Execute(new CompositeCommand(
                Tr.T("Replace file"),
                [
                    new PropertyChangeCommand<string>(Tr.T("Replace file"), Model, nameof(FileAsset.FilePath), () => _file.FilePath, v => _file.FilePath = v, path),
                    .. ReplacementCommands(source),
                ]));
            Context.History.Seal();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Context.Dialogs.ShowError(TypeLabel, exception.Message);
        }
    }
}

/// <summary>Inspector d'une texture : aperçu, taille de l'image et assets qui l'utilisent.</summary>
public sealed class TextureAssetViewModel : FileAssetViewModel
{
    public TextureAssetViewModel(TextureAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
    }

    public override string? IconPath => FullPath;

    /// <summary>La texture est copiée sous le nom de chaque élément qui l'utilise : son identifiant reste interne.</summary>
    public override bool HasGameId => false;

    /// <summary>Dimensions lues dans l'en-tête PNG ("16 x 16 pixels").</summary>
    public string SizeLabel => FullPath is { } path && TryReadPngSize(path, out int width, out int height)
        ? $"{width} x {height} pixels"
        : Tr.T("File not found");

    protected override string? AskReplacementFile() => Context.Dialogs.AskTextureFile();

    protected override string CopyIntoProject(string sourceFile) =>
        Context.Importer.CopyTexture(Context.Project, sourceFile, Model.ResourceId.Value);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName == nameof(FileAsset.FilePath))
        {
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    /// <summary>Largeur et hauteur sont les deux entiers big-endian qui suivent la signature et l'en-tête IHDR.</summary>
    private static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            Span<byte> header = stackalloc byte[24];
            using FileStream stream = File.OpenRead(path);
            if (stream.Read(header) < header.Length)
            {
                return false;
            }

            width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
            height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
            return width > 0 && height > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>Inspector d'un son : fichier, sous-titre, écoute.</summary>
/// <summary>
/// Inspector of a 3D model: format, file, reference in the mod, replacement and opening in its
/// program, and the textures bound to it: one per texture of the file, then the variants and
/// layers used by mobs.
/// </summary>
public sealed class ModelAssetViewModel : FileAssetViewModel
{
    private readonly ModelAsset _model;
    private BlockbenchModel? _blockbench;

    public ModelAssetViewModel(ModelAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _model = model;
        AddVariantCommand = new RelayCommand(() => ChangeVariants(Tr.T("Add a variant"), [.. _model.Variants, new ModelTextureVariant(Tr.F("Variant {0}", _model.Variants.Count + 1), null)]));
        AddLayerCommand = new RelayCommand(() => ChangeLayers(Tr.T("Add a layer"), [.. _model.Layers, new ModelTextureLayer(Tr.F("Layer {0}", _model.Layers.Count + 1), null, false)]));
        ReadFile();
        RebuildSlots();
        RebuildVariants();
        RebuildLayers();
    }

    /// <summary>Textures of the file, each bound to a texture of the project or left as in the file.</summary>
    public ObservableCollection<TextureSlotViewModel> TextureSlots { get; } = [];

    public ObservableCollection<ModelVariantViewModel> Variants { get; } = [];

    public ObservableCollection<ModelLayerViewModel> Layers { get; } = [];

    public bool HasTextureSlots => TextureSlots.Count > 0;

    public bool CanBeEntityModel => _model.CanBeEntityModel;

    /// <summary>"Modded entity · 9 cubes · 5 groups · texture 64 x 32" for a Blockbench model, empty otherwise.</summary>
    public string Summary => _blockbench is { } model
        ? Tr.F("{0} · {1} cubes · {2} groups · texture {3} x {4}", FormatName(model.Format), model.Cubes.Count(), CountGroups(model.Groups), model.TextureWidth, model.TextureHeight)
        : string.Empty;

    public bool HasSummary => _blockbench is not null;

    public ICommand AddVariantCommand { get; }

    public ICommand AddLayerCommand { get; }

    public static string FormatName(string format) => format switch
    {
        "java_block" => Tr.T("Java block/item"),
        "modded_entity" => Tr.T("Modded entity"),
        "bedrock" or "bedrock_old" => Tr.T("Bedrock model"),
        "free" => Tr.T("Generic model"),
        _ => format,
    };

    public override void OnProjectContentChanged()
    {
        base.OnProjectContentChanged();
        foreach (TextureSlotViewModel slot in AllTextureSlots())
        {
            slot.Refresh();
        }
    }

    internal void ChangeVariants(string description, IReadOnlyList<ModelTextureVariant> variants)
    {
        Change(description, nameof(ModelAsset.Variants), () => _model.Variants, v => _model.Variants = v, variants);
        Context.History.Seal();
    }

    internal void ChangeLayers(string description, IReadOnlyList<ModelTextureLayer> layers)
    {
        Change(description, nameof(ModelAsset.Layers), () => _model.Layers, v => _model.Layers = v, layers);
        Context.History.Seal();
    }

    protected override IEnumerable<IUndoableCommand> ReplacementCommands(string sourceFile)
    {
        IReadOnlyList<ModelTextureSlot> slots = ModelFormats.MergeSlots(ModelFormats.ReadSlots(sourceFile), _model.Slots);
        yield return new PropertyChangeCommand<IReadOnlyList<ModelTextureSlot>>(
            Tr.T("Replace file"), _model, nameof(ModelAsset.Slots), () => _model.Slots, v => _model.Slots = v, slots);
    }

    private IEnumerable<TextureSlotViewModel> AllTextureSlots() =>
        TextureSlots.Concat(Variants.Select(v => v.Texture)).Concat(Layers.Select(l => l.Texture));

    private void ReadFile()
    {
        _blockbench = null;
        if (_model.Format == ModelFormat.Blockbench && FullPath is { } file)
        {
            try
            {
                _blockbench = BlockbenchModel.Load(file);
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                _blockbench = null;
            }
        }
    }

    private void RebuildSlots()
    {
        TextureSlots.Clear();
        foreach (ModelTextureSlot slot in _model.Slots)
        {
            string key = slot.Key;
            bool embedded = _blockbench?.Textures.FirstOrDefault(t => t.Index.ToString(System.Globalization.CultureInfo.InvariantCulture) == key)?.Png is not null;
            string empty = _model.Format == ModelFormat.Blockbench
                ? embedded ? Tr.T("(image embedded in the model)") : Tr.T("(placeholder texture)")
                : Tr.F("(texture of the file: {0})", slot.Name);
            TextureSlots.Add(new TextureSlotViewModel(
                _model, Context, nameof(ModelAsset.Slots), slot.Name.Length > 0 ? slot.Name : key, empty,
                () => _model.Slots.FirstOrDefault(s => s.Key == key)?.TextureId,
                v => _model.SetSlotTexture(key, v)));
        }

        OnPropertyChanged(nameof(HasTextureSlots));
    }

    private void RebuildVariants()
    {
        if (Variants.Count == _model.Variants.Count)
        {
            foreach (ModelVariantViewModel variant in Variants)
            {
                variant.Refresh();
            }

            return;
        }

        Variants.Clear();
        for (int i = 0; i < _model.Variants.Count; i++)
        {
            Variants.Add(new ModelVariantViewModel(this, _model, Context, i));
        }
    }

    private void RebuildLayers()
    {
        if (Layers.Count == _model.Layers.Count)
        {
            foreach (ModelLayerViewModel layer in Layers)
            {
                layer.Refresh();
            }

            return;
        }

        Layers.Clear();
        for (int i = 0; i < _model.Layers.Count; i++)
        {
            Layers.Add(new ModelLayerViewModel(this, _model, Context, i));
        }
    }

    private static int CountGroups(IEnumerable<BlockbenchGroup> groups) => groups.Sum(g => 1 + CountGroups(g.Children));

    public override string TypeIcon => "ModelGeometry";

    /// <summary>The model is not an element of the game under its identifier.</summary>
    public override bool HasGameId => false;

    public string FormatLabel => ModelFormats.Label(_model.Format);

    public bool IsUsedByTheGame => _model.IsUsedByTheGame;

    /// <summary>Reference to use in a block or item model ("magicmod:custom/altar").</summary>
    public string GameReference => $"{Context.Project.Settings.ModId}:custom/{Model.ResourceId}";

    protected override string? AskReplacementFile() => Context.Dialogs.AskModelFiles().FirstOrDefault();

    protected override string CopyIntoProject(string sourceFile) =>
        Context.Importer.CopyModel(Context.Project, sourceFile, Model.ResourceId.Value);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        if (propertyName is nameof(FileAsset.FilePath))
        {
            ReadFile();
            OnPropertyChanged(nameof(FormatLabel));
            OnPropertyChanged(nameof(IsUsedByTheGame));
            OnPropertyChanged(nameof(CanBeEntityModel));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(HasSummary));
        }
        else if (propertyName is nameof(ModelAsset.Slots))
        {
            if (TextureSlots.Count == _model.Slots.Count && TextureSlots.Zip(_model.Slots).All(p => p.First.Label == (p.Second.Name.Length > 0 ? p.Second.Name : p.Second.Key)))
            {
                foreach (TextureSlotViewModel slot in TextureSlots)
                {
                    slot.Refresh();
                }
            }
            else
            {
                RebuildSlots();
            }
        }
        else if (propertyName is nameof(ModelAsset.Variants))
        {
            RebuildVariants();
        }
        else if (propertyName is nameof(ModelAsset.Layers))
        {
            RebuildLayers();
        }
        else if (propertyName is nameof(Core.Assets.Asset.ResourceId))
        {
            OnPropertyChanged(nameof(GameReference));
        }
    }
}

public sealed class SoundAssetViewModel : FileAssetViewModel
{
    private readonly SoundAsset _sound;

    public SoundAssetViewModel(SoundAsset model, AssetEditingContext context, string typeLabel)
        : base(model, context, typeLabel)
    {
        _sound = model;
    }

    public string Subtitle
    {
        get => _sound.Subtitle;
        set
        {
            string text = value ?? string.Empty;
            if (text.Trim() != _sound.Subtitle)
            {
                Change(Tr.T("Change subtitle"), nameof(SoundAsset.Subtitle), () => _sound.Subtitle, v => _sound.Subtitle = v, text);
            }
        }
    }

    /// <summary>Identifiant à utiliser dans les commandes du jeu (/playsound).</summary>
    public string PlaySoundCommand => $"/playsound {FullResourceId} master @s";

    protected override string? AskReplacementFile() => Context.Dialogs.AskSoundFiles().FirstOrDefault();

    protected override string CopyIntoProject(string sourceFile) =>
        Context.Importer.CopySound(Context.Project, sourceFile, Model.ResourceId.Value);

    protected override void OnModelPropertyChanged(string propertyName)
    {
        base.OnModelPropertyChanged(propertyName);
        switch (propertyName)
        {
            case nameof(SoundAsset.Subtitle):
                OnPropertyChanged(nameof(Subtitle));
                break;

            case nameof(Core.Assets.Asset.ResourceId):
                OnPropertyChanged(nameof(PlaySoundCommand));
                break;
        }
    }
}
