using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Core.Commands;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>A 3D model proposed for an element.</summary>
public sealed record ModelOption(Guid Id, string Label);

/// <summary>
/// Choice of the 3D model of an item, a block or a mob among the models of the project, or import
/// of a new one through the import window (its textures, variants and layers are set there).
/// </summary>
public sealed class ModelPickerViewModel : ObservableObject
{
    private readonly TexturedAsset _asset;
    private readonly AssetEditingContext _context;
    private readonly bool _entityModel;

    public ModelPickerViewModel(TexturedAsset asset, AssetEditingContext context, bool entityModel)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _entityModel = entityModel;
        ImportCommand = new RelayCommand(Import);
        OpenCommand = new RelayCommand(() => _context.SelectAsset(_asset.CustomModelId!.Value), () => Selected is not null);
    }

    /// <summary>Models the element can use: Blockbench ones for a mob, Blockbench and Java JSON ones otherwise.</summary>
    public IReadOnlyList<ModelOption> Options =>
    [
        new ModelOption(Guid.Empty, Tr.T("(none)")),
        .. _context.Project.Assets.OfType<ModelAsset>()
            .Where(m => _entityModel ? m.CanBeEntityModel : m.IsUsedByTheGame)
            .OrderBy(m => m.ResourceId)
            .Select(m => new ModelOption(m.Id, $"{m.DisplayName} ({m.ResourceId})")),
    ];

    public Guid SelectedModelId
    {
        get => _asset.CustomModelId ?? Guid.Empty;
        set
        {
            Guid? id = value == Guid.Empty ? null : value;
            if (id != _asset.CustomModelId)
            {
                _context.History.Execute(new PropertyChangeCommand<Guid?>(
                    Tr.T("Change the 3D model"), _asset, nameof(TexturedAsset.CustomModelId), () => _asset.CustomModelId, v => _asset.CustomModelId = v, id));
                _context.History.Seal();
            }
        }
    }

    /// <summary>The model chosen, or null.</summary>
    public ModelAsset? Selected => _asset.CustomModelId is { } id ? _context.Project.Assets.Find(id) as ModelAsset : null;

    public string Hint => Selected switch
    {
        null when _asset.CustomModelId is not null => Tr.T("The model was deleted: choose another one."),
        null => _entityModel
            ? Tr.T("Import a Blockbench model (.bbmodel): its textures, variants and layers are set in the import window.")
            : Tr.T("Import a Blockbench (.bbmodel) or Java JSON model; its textures are set in the import window."),
        { } model => _entityModel && (model.Variants.Count > 0 || model.Layers.Count > 0)
            ? Tr.F("{0} variant(s), {1} layer(s): change them in the Properties of the model.", model.Variants.Count, model.Layers.Count)
            : _entityModel
                ? Tr.T("Its textures, variants and layers are set in the Properties of the model.")
                : Tr.T("Its textures are set in the Properties of the model."),
    };

    public ICommand ImportCommand { get; }

    public ICommand OpenCommand { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Options));
        OnPropertyChanged(nameof(SelectedModelId));
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(Hint));
    }

    /// <summary>Imports a model through its import window and chooses it, in one undoable step.</summary>
    private void Import()
    {
        string? file = _context.Dialogs.AskModelFiles().FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            if (ModelFormats.DescribeProblem(file) is { } problem)
            {
                throw new InvalidDataException(problem);
            }

            if (_entityModel && ModelFormats.FromPath(file) != ModelFormat.Blockbench)
            {
                throw new InvalidDataException(Tr.T("A mob needs a Blockbench model (.bbmodel)."));
            }

            var import = new ModelImportViewModel(_context.Project, _context.Importer, _context.Dialogs, file);
            if (!_context.Dialogs.ShowModelImport(import))
            {
                return;
            }

            IReadOnlyList<Core.Assets.Asset> assets = import.CreateAssets(new HashSet<Core.Identifiers.ResourceId>(), _asset.Folder);
            ModelAsset model = assets.OfType<ModelAsset>().Single();
            _context.History.Execute(new CompositeCommand(
                Tr.T("Import a 3D model"),
                [
                    .. assets.Select(a => (IUndoableCommand)new AddAssetCommand(_context.Project.Assets, a, a.Type.ToString())),
                    new PropertyChangeCommand<Guid?>(Tr.T("Change the 3D model"), _asset, nameof(TexturedAsset.CustomModelId),
                        () => _asset.CustomModelId, v => _asset.CustomModelId = v, model.Id),
                ]));
            _context.History.Seal();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _context.Dialogs.ShowError(Tr.T("3D model"), exception.Message);
        }
    }
}
