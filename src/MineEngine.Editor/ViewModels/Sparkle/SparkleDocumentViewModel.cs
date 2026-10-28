using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Assets;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;
using MineEngine.Project.Vanilla;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>
/// Sparkle tab: the editor of a particle system, like Unreal's Niagara. The emitters are nodes of a
/// canvas (moved with the mouse, added with a right-click, linked by their events, framed by
/// comments and described by tooltips), next to a square 3D preview that can be detached, with the
/// user parameters of the system under it, above a timeline. The selected emitter, module or note
/// is edited in the Properties panel. Every gesture becomes a change applied by the project
/// history: everything can be undone, and undoing updates the editor.
/// </summary>
public sealed class SparkleDocumentViewModel : DocumentViewModel, IDisposable
{
    private readonly SparkleAsset _asset;
    private readonly AssetEditingContext _context;
    private IReadOnlyList<GraphIssueItem> _issues = [];
    private SparklePerformance _performance = new(0, 0, 0);
    private (Guid Emitter, string Key)? _lastEdit;
    private SparkleEmitterViewModel? _selectedEmitter;
    private SparkleModuleViewModel? _selectedModule;
    private bool _isPreviewDetached;
    private SparkleNoteViewModel? _selectedNote;
    private bool _snapToGrid;
    private bool _showAxes = true;
    private bool _showZones = true;
    private bool _showBeams = true;
    private bool _showVelocity = true;
    private bool _showCollision;

    /// <summary>Emitter copied with Ctrl+C, pasted with Ctrl+V in this system or another one.</summary>
    private static SparkleEmitter? _clipboard;

    public SparkleDocumentViewModel(SparkleAsset asset, AssetEditingContext context, string displayPath)
        : base(IdFor(asset.Id), displayPath)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _asset.Changed += OnAssetChanged;
        DeleteSelectedCommand = new RelayCommand(DeleteSelection, () => _selectedEmitter is not null || _selectedNote is not null);
        DuplicateSelectedCommand = new RelayCommand(() => DuplicateEmitter(_selectedEmitter!), () => _selectedEmitter is not null);
        CopyCommand = new RelayCommand(() => _clipboard = _selectedEmitter!.Model, () => _selectedEmitter is not null);
        DetachPreviewCommand = new RelayCommand(() => IsPreviewDetached = !IsPreviewDetached);
        AddUserParameterCommand = new RelayCommand<SparkleUserParameterType>(AddUserParameter);
        SyncParameters();
        SyncNotes();
        Sync();
        Select(Emitters.FirstOrDefault(), null);
    }

    public static string IdFor(Guid assetId) => "sparkle:" + assetId.ToString("N");

    public SparkleAsset Asset => _asset;

    public Guid AssetId => _asset.Id;

    public AssetRegistry Assets => _context.Project.Assets;

    public AssetEditingContext Context => _context;

    public override string FileName => string.IsNullOrWhiteSpace(_asset.DisplayName) ? _asset.ResourceId.Value : _asset.DisplayName;

    public override bool IsReadOnly => false;

    public override string DetailsBadge => "SPARKLE";

    /// <summary>Nodes of the canvas.</summary>
    public ObservableCollection<SparkleEmitterViewModel> Emitters { get; } = [];

    /// <summary>Wires of the events, from the emitter that sends them to the one that receives them.</summary>
    public ObservableCollection<SparkleLinkViewModel> Links { get; } = [];

    /// <summary>Named values of the system, shown under the preview; module settings are linked to them.</summary>
    public ObservableCollection<SparkleUserParameterViewModel> UserParameters { get; } = [];

    /// <summary>Comments and tooltips of the canvas.</summary>
    public ObservableCollection<SparkleNoteViewModel> Notes { get; } = [];

    /// <summary>Types offered by the "Add a parameter" menu.</summary>
    public IReadOnlyList<Choice<SparkleUserParameterType>> UserParameterTypes => SparkleUserParameterViewModel.Types;

    public bool HasUserParameters => UserParameters.Count > 0;

    /// <summary>Playback of the preview (shared by the docked and the detached preview, driven by the timeline).</summary>
    public SparklePlayer Player { get; } = new();

    /// <summary>Identifier played by the graph nodes.</summary>
    public string SystemId => _asset.ResourceId.Value;

    public bool IsEmpty => Emitters.Count == 0;

    /// <summary>Emitter edited in Properties (its head was clicked, or one of its modules).</summary>
    public SparkleEmitterViewModel? SelectedEmitter
    {
        get => _selectedEmitter;
        private set
        {
            if (SetProperty(ref _selectedEmitter, value))
            {
                OnPropertyChanged(nameof(HasSelectedEmitter));
            }
        }
    }

    public bool HasSelectedEmitter => _selectedEmitter is not null;

    /// <summary>Comment or tooltip edited in Properties.</summary>
    public SparkleNoteViewModel? SelectedNote
    {
        get => _selectedNote;
        private set
        {
            if (SetProperty(ref _selectedNote, value))
            {
                OnPropertyChanged(nameof(HasSelectedNote));
                OnPropertyChanged(nameof(HasNothingSelected));
            }
        }
    }

    public bool HasSelectedNote => _selectedNote is not null;

    public bool HasNothingSelected => _selectedEmitter is null && _selectedNote is null;

    /// <summary>An emitter was copied (Ctrl+C): it can be pasted.</summary>
    public static bool CanPaste => _clipboard is not null;

    /// <summary>Nodes and notes moved on the canvas stop on the lines of the grid.</summary>
    public bool SnapToGrid
    {
        get => _snapToGrid;
        set => SetProperty(ref _snapToGrid, value);
    }

    // ----- What the preview draws besides the particles (its right-click menu) -----

    /// <summary>Orientation of the camera: the X, Y and Z axes in a corner.</summary>
    public bool ShowAxes
    {
        get => _showAxes;
        set => SetProperty(ref _showAxes, value);
    }

    /// <summary>Spheres and boxes of the location events.</summary>
    public bool ShowZones
    {
        get => _showZones;
        set => SetProperty(ref _showZones, value);
    }

    /// <summary>Start and end of the emitters with the Line shape.</summary>
    public bool ShowBeams
    {
        get => _showBeams;
        set => SetProperty(ref _showBeams, value);
    }

    /// <summary>Arrow of a fixed direction, cone of a Cone velocity.</summary>
    public bool ShowVelocity
    {
        get => _showVelocity;
        set => SetProperty(ref _showVelocity, value);
    }

    /// <summary>A small box around the particles that collide (off at first: many boxes hide the particles).</summary>
    public bool ShowCollision
    {
        get => _showCollision;
        set => SetProperty(ref _showCollision, value);
    }

    /// <summary>Module edited in Properties; null: the whole stack of the selected emitter.</summary>
    public SparkleModuleViewModel? SelectedModule
    {
        get => _selectedModule;
        private set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                OnPropertyChanged(nameof(HasSelectedModule));
                OnPropertyChanged(nameof(ShowsWholeEmitter));
            }
        }
    }

    public bool HasSelectedModule => _selectedModule is not null;

    /// <summary>The head of the emitter is selected: Properties shows all its modules.</summary>
    public bool ShowsWholeEmitter => _selectedModule is null;

    /// <summary>The preview is in its own window (it can go to another screen); otherwise next to the canvas.</summary>
    public bool IsPreviewDetached
    {
        get => _isPreviewDetached;
        set => SetProperty(ref _isPreviewDetached, value);
    }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand DuplicateSelectedCommand { get; }

    public ICommand CopyCommand { get; }

    /// <summary>Parameter: the type of the new parameter.</summary>
    public ICommand AddUserParameterCommand { get; }

    public ICommand DetachPreviewCommand { get; }

    /// <summary>File of a texture setting, for the preview: image of the project or particle texture of Minecraft.</summary>
    public Func<string, string?> SpriteResolver => SpriteFile;

    private string? SpriteFile(string reference)
    {
        if (!ContentReference.TryParseStorageText(reference, out ContentReference? parsed))
        {
            return null;
        }

        if (parsed!.Kind == ContentReferenceKind.Asset)
        {
            return Assets.Find(parsed.AssetId) is TextureAsset { FilePath.Length: > 0 } texture ? _context.Project.Layout.ToAbsolutePath(texture.FilePath) : null;
        }

        if (parsed.Kind != ContentReferenceKind.Id || parsed.Id!.Namespace != "minecraft")
        {
            return null;
        }

        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineEngine", "vanilla");
        try
        {
            return VanillaAssetLibrary.For(_context.Project.Settings.MinecraftVersion, cache) is { } library
                ? library.FileOf(new VanillaAsset(VanillaAssetKind.Texture, "particle/" + parsed.Id.Path))
                : null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public IReadOnlyList<GraphIssueItem> Issues
    {
        get => _issues;
        private set
        {
            if (SetProperty(ref _issues, value))
            {
                OnPropertyChanged(nameof(HasIssues));
                OnPropertyChanged(nameof(IssueSummary));
            }
        }
    }

    public bool HasIssues => _issues.Count > 0;

    public string IssueSummary
    {
        get
        {
            int errors = _issues.Count(i => i.IsError);
            int warnings = _issues.Count - errors;
            return (errors, warnings) switch
            {
                (0, 0) => Tr.T("No problems"),
                _ => string.Join(", ", new[]
                {
                    errors > 0 ? (errors == 1 ? Tr.T("1 error") : Tr.F("{0} errors", errors)) : null,
                    warnings > 0 ? (warnings == 1 ? Tr.T("1 warning") : Tr.F("{0} warnings", warnings)) : null,
                }.Where(s => s is not null)),
            };
        }
    }

    /// <summary>Resources of the whole system.</summary>
    public SparklePerformance Performance
    {
        get => _performance;
        private set
        {
            if (SetProperty(ref _performance, value))
            {
                OnPropertyChanged(nameof(PerformanceSummary));
            }
        }
    }

    public string PerformanceSummary => _performance.Summary;

    // ----- Gestures -----

    /// <summary>
    /// Adds the emitters of a template (one, or several linked by their events) with the first node at
    /// this point of the canvas (right-click), as one undoable step, and selects the first one.
    /// </summary>
    public void AddEmitter(SparkleTemplate template, double x, double y)
    {
        var used = _asset.Emitters.Select(e => e.EventId).ToHashSet();
        int NextId()
        {
            int id = 1;
            while (!used.Add(id))
            {
                id++;
            }

            return id;
        }

        string name = template == SparkleTemplate.Empty ? Tr.F("Emitter {0}", _asset.Emitters.Count + 1) : SparkleTemplates.Info(template).Title;
        IReadOnlyList<SparkleEmitter> added = SparkleTemplates.Create(template, name, NextId, Snap(x), Snap(y));
        Apply([.. _asset.Emitters, .. added], added.Count == 1 ? Tr.T("Add an emitter") : Tr.F("Add the template \"{0}\"", name));
        Select(Emitters.FirstOrDefault(e => e.Id == added[0].Id), null);
    }

    public void DuplicateEmitter(SparkleEmitterViewModel emitter)
    {
        SparkleEmitter copy = emitter.Model.Duplicate(Tr.F("{0} (copy)", emitter.Model.Name), _asset.NextEventId()) with
        {
            X = emitter.Model.X + 40,
            Y = emitter.Model.Y + 40,
        };
        Apply([.. _asset.Emitters, copy], Tr.T("Duplicate an emitter"));
        Select(Emitters.FirstOrDefault(e => e.Id == copy.Id), null);
    }

    public void DeleteEmitter(SparkleEmitterViewModel emitter) =>
        Apply([.. _asset.Emitters.Where(e => e.Id != emitter.Id)], Tr.T("Delete an emitter"));

    /// <summary>Del or Backspace: the selected emitter, or the selected comment or tooltip.</summary>
    public void DeleteSelection()
    {
        if (_selectedNote is { } note)
        {
            ApplyNotes([.. _asset.Notes.Where(n => n.Id != note.Id)], note.IsComment ? Tr.T("Delete a comment") : Tr.T("Delete a tooltip"));
        }
        else if (_selectedEmitter is { } emitter)
        {
            DeleteEmitter(emitter);
        }
    }

    /// <summary>
    /// Ctrl+V: a copy of the copied emitter (new identifiers) with its node at this point, selected.
    /// Its links to user parameters of another system are kept only if this one has them.
    /// </summary>
    public void Paste(double x, double y)
    {
        if (_clipboard is not { } copied)
        {
            return;
        }

        SparkleEmitter copy = copied.Duplicate(copied.Name, _asset.NextEventId()) with { X = Snap(x), Y = Snap(y) };
        Apply([.. _asset.Emitters, copy], Tr.T("Paste an emitter"));
        Select(Emitters.FirstOrDefault(e => e.Id == copy.Id), null);
    }

    /// <summary>Position on the grid when snapping is on.</summary>
    public double Snap(double value) => _snapToGrid ? Math.Round(value / GridSize) * GridSize : Math.Round(value);

    /// <summary>Side of a square of the grid of the canvas.</summary>
    public const double GridSize = 32;

    /// <summary>
    /// Applies the position (and the size of the comments) of what was dragged on the canvas, as
    /// one undoable step: a comment moves with the emitters inside it.
    /// </summary>
    public void MoveItems(IEnumerable<SparkleEmitterViewModel> emitters, IEnumerable<SparkleNoteViewModel> notes)
    {
        Dictionary<Guid, SparkleEmitterViewModel> movedEmitters = emitters.ToDictionary(e => e.Id);
        Dictionary<Guid, SparkleNoteViewModel> movedNotes = notes.ToDictionary(n => n.Id);
        bool emittersChanged = _asset.Emitters.Any(e => movedEmitters.TryGetValue(e.Id, out SparkleEmitterViewModel? node) && (node.X != e.X || node.Y != e.Y));
        bool notesChanged = _asset.Notes.Any(n => movedNotes.TryGetValue(n.Id, out SparkleNoteViewModel? view)
                                                  && (view.X != n.X || view.Y != n.Y || view.Width != n.Width || view.Height != n.Height));
        var commands = new List<IUndoableCommand>();
        if (emittersChanged)
        {
            IReadOnlyList<SparkleEmitter> value = [.. _asset.Emitters.Select(e => movedEmitters.TryGetValue(e.Id, out SparkleEmitterViewModel? node) ? e with { X = node.X, Y = node.Y } : e)];
            commands.Add(new PropertyChangeCommand<IReadOnlyList<SparkleEmitter>>(
                Tr.T("Move"), _asset, nameof(SparkleAsset.Emitters), () => _asset.Emitters, v => _asset.Emitters = v, value));
        }

        if (notesChanged)
        {
            IReadOnlyList<SparkleNote> value = [.. _asset.Notes.Select(n => movedNotes.TryGetValue(n.Id, out SparkleNoteViewModel? view)
                ? n with { X = view.X, Y = view.Y, Width = view.Width, Height = view.Height }
                : n)];
            commands.Add(new PropertyChangeCommand<IReadOnlyList<SparkleNote>>(
                Tr.T("Move"), _asset, nameof(SparkleAsset.Notes), () => _asset.Notes, v => _asset.Notes = v, value));
        }

        if (commands.Count == 0)
        {
            return;
        }

        string description = (movedEmitters.Count, movedNotes.Count) switch
        {
            (1, 0) => Tr.T("Move an emitter"),
            (_, 0) => Tr.T("Move emitters"),
            _ => movedNotes.Values.First().IsComment ? Tr.T("Move a comment") : Tr.T("Move a tooltip"),
        };
        Execute(new CompositeCommand(description, commands));
    }

    // ----- Notes (comments and tooltips) -----

    /// <summary>
    /// C: a comment around the selected emitter (or at this point of the canvas). T: a tooltip next
    /// to it, that describes it. The note is selected, to be written in Properties.
    /// </summary>
    public void AddNote(SparkleNoteKind kind, double x, double y)
    {
        SparkleNote note;
        if (kind == SparkleNoteKind.Comment)
        {
            note = _selectedEmitter is { } around
                ? new SparkleNote(Guid.NewGuid(), kind) { X = around.X - 30, Y = around.Y - 60, Width = SparkleEmitterViewModel.NodeWidth + 60, Height = 520 }
                : new SparkleNote(Guid.NewGuid(), kind) { X = Snap(x), Y = Snap(y) };
            note = note with { Text = Tr.T("Comment") };
        }
        else
        {
            note = _selectedEmitter is { } described
                ? new SparkleNote(Guid.NewGuid(), kind) { X = described.X + SparkleEmitterViewModel.NodeWidth + 16, Y = described.Y - 30, Targets = [described.Id] }
                : new SparkleNote(Guid.NewGuid(), kind) { X = Snap(x), Y = Snap(y) };
            note = note with { Text = Tr.T("Describe the emitter here.") };
        }

        ApplyNotes([.. _asset.Notes, note], kind == SparkleNoteKind.Comment ? Tr.T("Add a comment") : Tr.T("Add a tooltip"));
        SelectNote(Notes.FirstOrDefault(n => n.Id == note.Id));
    }

    /// <summary>Changes a note (title, color, message); successive keystrokes make a single undoable step.</summary>
    internal void ChangeNote(SparkleNoteViewModel note, Func<SparkleNote, SparkleNote> change, string description, string? mergeKey = null)
    {
        if (_asset.Notes.All(n => n.Id != note.Id))
        {
            return;
        }

        bool merge = mergeKey is not null && _lastEdit == (note.Id, mergeKey);
        ApplyNotes([.. _asset.Notes.Select(n => n.Id == note.Id ? change(n) : n)], description, merge);
        _lastEdit = mergeKey is null ? null : (note.Id, mergeKey);
    }

    public void SelectNote(SparkleNoteViewModel? note)
    {
        Select(null, null);
        SelectedNote = note;
        if (note is not null)
        {
            note.IsSelected = true;
        }
    }

    // ----- User parameters -----

    public void AddUserParameter(SparkleUserParameterType type)
    {
        string name;
        int n = _asset.UserParameters.Count + 1;
        do
        {
            name = Tr.F("Parameter {0}", n++);
        }
        while (_asset.UserParameters.Any(p => p.Name == name));

        ApplyParameters([.. _asset.UserParameters, SparkleUserParameter.Create(name, type)], Tr.T("Add a user parameter"));
    }

    /// <summary>Changes a parameter (name, type, value); successive keystrokes make a single undoable step.</summary>
    internal void ChangeUserParameter(Guid id, Func<SparkleUserParameter, SparkleUserParameter> change, string description, string? mergeKey = null)
    {
        if (_asset.UserParameters.All(p => p.Id != id))
        {
            return;
        }

        bool merge = mergeKey is not null && _lastEdit == (id, mergeKey);
        ApplyParameters([.. _asset.UserParameters.Select(p => p.Id == id ? change(p) : p)], description, merge);
        _lastEdit = mergeKey is null ? null : (id, mergeKey);
    }

    /// <summary>Deletes a parameter; the settings linked to it keep its value, in one undoable step.</summary>
    public void DeleteUserParameter(Guid id)
    {
        if (_asset.UserParameters.FirstOrDefault(p => p.Id == id) is not { } parameter)
        {
            return;
        }

        string binding = parameter.Binding;
        IReadOnlyList<SparkleEmitter> emitters =
        [
            .. _asset.Emitters.Select(e => e with
            {
                Modules = [.. e.Modules.Select(m => m with
                {
                    Values = m.Values.SetItems(m.Values.Where(v => v.Value == binding).Select(v => KeyValuePair.Create(v.Key, parameter.Value))),
                })],
            }),
        ];
        IReadOnlyList<SparkleUserParameter> parameters = [.. _asset.UserParameters.Where(p => p.Id != id)];
        Execute(new CompositeCommand(Tr.F("Delete the user parameter \"{0}\"", parameter.Name),
        [
            new PropertyChangeCommand<IReadOnlyList<SparkleEmitter>>(Tr.T("Delete"), _asset, nameof(SparkleAsset.Emitters), () => _asset.Emitters, v => _asset.Emitters = v, emitters),
            new PropertyChangeCommand<IReadOnlyList<SparkleUserParameter>>(Tr.T("Delete"), _asset, nameof(SparkleAsset.UserParameters), () => _asset.UserParameters, v => _asset.UserParameters = v, parameters),
        ]));
    }

    /// <summary>Selects an emitter (its head) or one of its modules, edited in Properties; null: nothing.</summary>
    public void Select(SparkleEmitterViewModel? emitter, SparkleModuleViewModel? module)
    {
        if (_selectedNote is { } note)
        {
            note.IsSelected = false;
            SelectedNote = null;
        }

        if (_selectedModule is { } previousModule)
        {
            previousModule.IsSelected = false;
        }

        if (_selectedEmitter is { } previous)
        {
            previous.IsSelected = false;
        }

        SelectedEmitter = emitter;
        SelectedModule = emitter is null ? null : module;
        OnPropertyChanged(nameof(HasNothingSelected));
        if (emitter is not null)
        {
            emitter.IsSelected = true;
        }

        if (_selectedModule is { } selected)
        {
            selected.IsSelected = true;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>A node moved: the wires that touch it follow.</summary>
    internal void UpdateLinks()
    {
        foreach (SparkleLinkViewModel link in Links)
        {
            link.Update();
        }
    }

    /// <summary>
    /// Changes an emitter. With <paramref name="mergeKey"/>, successive changes of the same field
    /// (keystrokes) make a single undoable step.
    /// </summary>
    internal void ChangeEmitter(SparkleEmitterViewModel emitter, Func<SparkleEmitter, SparkleEmitter> change, string description, string? mergeKey = null)
    {
        SparkleEmitter? current = _asset.FindEmitter(emitter.Id);
        if (current is null)
        {
            return;
        }

        bool merge = mergeKey is not null && _lastEdit == (emitter.Id, mergeKey);
        Apply([.. _asset.Emitters.Select(e => e.Id == emitter.Id ? change(e) : e)], description, merge);
        _lastEdit = mergeKey is null ? null : (emitter.Id, mergeKey);
    }

    public override bool Save() => true;

    public override void Navigate(OutlineItem item)
    {
    }

    /// <summary>The tab closes: its detached preview closes with it.</summary>
    public void Dispose()
    {
        _asset.Changed -= OnAssetChanged;
        IsPreviewDetached = false;
    }

    // ----- Interne -----

    private void Apply(IReadOnlyList<SparkleEmitter> emitters, string description, bool merge = false) =>
        Execute(new PropertyChangeCommand<IReadOnlyList<SparkleEmitter>>(
            description, _asset, nameof(SparkleAsset.Emitters), () => _asset.Emitters, v => _asset.Emitters = v, emitters), merge);

    private void ApplyNotes(IReadOnlyList<SparkleNote> notes, string description, bool merge = false) =>
        Execute(new PropertyChangeCommand<IReadOnlyList<SparkleNote>>(
            description, _asset, nameof(SparkleAsset.Notes), () => _asset.Notes, v => _asset.Notes = v, notes), merge);

    private void ApplyParameters(IReadOnlyList<SparkleUserParameter> parameters, string description, bool merge = false) =>
        Execute(new PropertyChangeCommand<IReadOnlyList<SparkleUserParameter>>(
            description, _asset, nameof(SparkleAsset.UserParameters), () => _asset.UserParameters, v => _asset.UserParameters = v, parameters), merge);

    /// <summary>A change of the project history; without <paramref name="merge"/>, an undoable step of its own.</summary>
    private void Execute(IUndoableCommand command, bool merge = false)
    {
        UndoHistory history = _context.History;
        if (!merge)
        {
            history.Seal();
            _lastEdit = null;
        }

        history.Execute(command);
        if (!merge)
        {
            history.Seal();
        }
    }

    private void OnAssetChanged(object? sender, AssetChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SparkleAsset.Emitters):
                Sync();
                break;
            case nameof(SparkleAsset.UserParameters):
                SyncParameters();
                Sync();
                break;
            case nameof(SparkleAsset.Notes):
                SyncNotes();
                break;
            case nameof(Asset.DisplayName) or nameof(Asset.ResourceId):
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DetailsTitle));
                OnPropertyChanged(nameof(SystemId));
                break;
        }
    }

    /// <summary>Follows the emitters of the asset, keeping the nodes of those that stay (selection, folded parts).</summary>
    private void Sync()
    {
        Dictionary<Guid, SparkleEmitterViewModel> existing = Emitters.ToDictionary(e => e.Id);
        var ordered = new List<SparkleEmitterViewModel>();
        foreach (SparkleEmitter emitter in _asset.Emitters)
        {
            if (existing.TryGetValue(emitter.Id, out SparkleEmitterViewModel? kept))
            {
                kept.Update(emitter);
                ordered.Add(kept);
            }
            else
            {
                ordered.Add(new SparkleEmitterViewModel(this, emitter));
            }
        }

        if (!ordered.SequenceEqual(Emitters))
        {
            Emitters.Clear();
            foreach (SparkleEmitterViewModel emitter in ordered)
            {
                Emitters.Add(emitter);
            }

            OnPropertyChanged(nameof(IsEmpty));
            if (_selectedEmitter is { } selected && !Emitters.Contains(selected))
            {
                Select(null, null);
            }
        }

        SyncLinks();

        // What plays: the linked settings take the values of the user parameters.
        IReadOnlyList<SparkleEmitter> resolved = _asset.ResolvedEmitters;
        IReadOnlyList<SparklePerformance> performance = SparklePerformance.Estimate(resolved);
        for (int i = 0; i < Emitters.Count; i++)
        {
            Emitters[i].Performance = performance[i];
        }

        Performance = performance.Aggregate(new SparklePerformance(0, 0, 0), (a, b) => a + b);
        var diagnostics = new DiagnosticBag();
        _asset.Validate(diagnostics);
        Issues = [.. diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info).Select(d => new GraphIssueItem(d.Message, null, d.Severity == DiagnosticSeverity.Error))];
        foreach (SparkleEmitterViewModel emitter in Emitters)
        {
            emitter.AttachedTooltip = string.Join("\n\n", _asset.Notes.Where(n => n.Kind == SparkleNoteKind.Tooltip && n.Targets.Contains(emitter.Id)).Select(n => n.Text));
        }

        Player.Load(resolved);
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Follows the user parameters, keeping the rows of those that stay (typed text).</summary>
    private void SyncParameters()
    {
        Dictionary<Guid, SparkleUserParameterViewModel> existing = UserParameters.ToDictionary(p => p.Id);
        var ordered = new List<SparkleUserParameterViewModel>();
        foreach (SparkleUserParameter parameter in _asset.UserParameters)
        {
            if (existing.TryGetValue(parameter.Id, out SparkleUserParameterViewModel? kept))
            {
                kept.Update(parameter);
                ordered.Add(kept);
            }
            else
            {
                ordered.Add(new SparkleUserParameterViewModel(this, parameter));
            }
        }

        if (!ordered.SequenceEqual(UserParameters))
        {
            UserParameters.Clear();
            foreach (SparkleUserParameterViewModel parameter in ordered)
            {
                UserParameters.Add(parameter);
            }

            OnPropertyChanged(nameof(HasUserParameters));
        }

        // The settings offer the parameters of their type, and show the name of the linked ones.
        foreach (SparkleParameterViewModel setting in Emitters.SelectMany(e => e.Sections).SelectMany(s => s.Modules).SelectMany(m => m.Parameters))
        {
            setting.RefreshBinding();
        }
    }

    /// <summary>Follows the comments and tooltips, keeping the ones that stay (selection).</summary>
    private void SyncNotes()
    {
        Dictionary<Guid, SparkleNoteViewModel> existing = Notes.ToDictionary(n => n.Id);
        var ordered = new List<SparkleNoteViewModel>();
        foreach (SparkleNote note in _asset.Notes)
        {
            if (existing.TryGetValue(note.Id, out SparkleNoteViewModel? kept))
            {
                kept.Update(note);
                ordered.Add(kept);
            }
            else
            {
                ordered.Add(new SparkleNoteViewModel(this, note));
            }
        }

        if (!ordered.SequenceEqual(Notes))
        {
            Notes.Clear();
            foreach (SparkleNoteViewModel note in ordered)
            {
                Notes.Add(note);
            }

            if (_selectedNote is { } selected && !Notes.Contains(selected))
            {
                SelectedNote = null;
            }
        }

        foreach (SparkleEmitterViewModel emitter in Emitters)
        {
            emitter.AttachedTooltip = string.Join("\n\n", _asset.Notes.Where(n => n.Kind == SparkleNoteKind.Tooltip && n.Targets.Contains(emitter.Id)).Select(n => n.Text));
        }
    }

    /// <summary>One wire per emitter driven by the death events of another emitter of the system.</summary>
    private void SyncLinks()
    {
        var wanted = Emitters
            .Where(e => e.Model.EventSource is not null)
            .Select(e => (Source: Emitters.FirstOrDefault(s => s != e && s.EventId == e.Model.EventSource), Target: e))
            .Where(l => l.Source is not null)
            .ToList();
        if (wanted.Count == Links.Count && wanted.Zip(Links).All(p => p.First.Source == p.Second.Source && p.First.Target == p.Second.Target))
        {
            UpdateLinks();
            return;
        }

        Links.Clear();
        foreach (var (source, target) in wanted)
        {
            Links.Add(new SparkleLinkViewModel(source!, target));
        }
    }
}
