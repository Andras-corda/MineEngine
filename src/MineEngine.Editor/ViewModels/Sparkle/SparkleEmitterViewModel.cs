using System.Windows.Input;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>
/// An emitter of the system, shown as a node of the canvas like in Niagara: its head (box "shown
/// in the preview", name, ID) and its stack of modules by part. A click selects it, or one of its
/// modules, and the Properties panel edits it.
/// </summary>
public sealed class SparkleEmitterViewModel : ObservableObject
{
    private SparkleEmitter _model;
    private string _name;
    private double _x;
    private double _y;
    private bool _isSelected;
    private SparklePerformance _performance = new(0, 0, 0);
    private string _attachedTooltip = string.Empty;

    public SparkleEmitterViewModel(SparkleDocumentViewModel document, SparkleEmitter model)
    {
        Document = document;
        _model = model;
        _name = model.Name;
        _x = model.X;
        _y = model.Y;
        Sections = [.. Enum.GetValues<SparkleSection>().Select(s => new SparkleSectionViewModel(this, s))];
        DuplicateCommand = new RelayCommand(() => Document.DuplicateEmitter(this));
        DeleteCommand = new RelayCommand(() => Document.DeleteEmitter(this));
        SelectCommand = new RelayCommand(() => Document.Select(this, null));
    }

    public SparkleDocumentViewModel Document { get; }

    public SparkleEmitter Model => _model;

    public Guid Id => _model.Id;

    /// <summary>Name of the head; typing makes a single undoable change.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                Document.ChangeEmitter(this, e => e with { Name = _name }, Tr.T("Rename an emitter"), "name");
            }
        }
    }

    /// <summary>Box of the head: the emitter is drawn in the preview.</summary>
    public bool Preview
    {
        get => _model.Preview;
        set => Document.ChangeEmitter(this, e => e with { Preview = value }, value ? Tr.T("Show an emitter in the preview") : Tr.T("Hide an emitter from the preview"));
    }

    /// <summary>Position of the node on the canvas; it follows the mouse while dragged, then is applied once (<see cref="SparkleDocumentViewModel.MoveEmitter"/>).</summary>
    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
            {
                Document.UpdateLinks();
            }
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (SetProperty(ref _y, value))
            {
                Document.UpdateLinks();
            }
        }
    }

    /// <summary>Width of a node on the canvas (the links start and end on its sides).</summary>
    public const double NodeWidth = 250;

    /// <summary>Height of the head, whose middle the links join.</summary>
    public const double HeadHeight = 44;

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    public int EventId => _model.EventId;

    /// <summary>Messages of the tooltips that describe the emitter (its head shows a small i).</summary>
    public string AttachedTooltip
    {
        get => _attachedTooltip;
        internal set
        {
            if (SetProperty(ref _attachedTooltip, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasTooltip));
            }
        }
    }

    public bool HasTooltip => _attachedTooltip.Length > 0;

    public IReadOnlyList<SparkleSectionViewModel> Sections { get; }

    /// <summary>Parts that have modules shown on the node.</summary>
    public IEnumerable<SparkleSectionViewModel> NodeSections => Sections.Where(s => s.Modules.Count > 0);

    public SparklePerformance Performance
    {
        get => _performance;
        internal set
        {
            if (SetProperty(ref _performance, value))
            {
                OnPropertyChanged(nameof(PerformanceSummary));
                foreach (SparkleModuleViewModel module in Sections.SelectMany(s => s.Modules).Where(m => m.IsPerformance))
                {
                    module.Refresh();
                }
            }
        }
    }

    public string PerformanceSummary => _performance.Summary;

    /// <summary>Role shown under the head: driven by its lifecycle or by the events of another emitter.</summary>
    public string Role => (_model.EventSource, _model.ReceivedEvent) switch
    {
        ({ } source, SparkleEventKind.Time) => Tr.F("ID {0} · spawns on the time events of the ID {1}", EventId, source),
        ({ } source, SparkleEventKind.Location) => Tr.F("ID {0} · spawns on the location events of the ID {1}", EventId, source),
        ({ } source, _) => Tr.F("ID {0} · spawns on the deaths of the ID {1}", EventId, source),
        _ => Tr.F("ID {0}", EventId),
    };

    public ICommand DuplicateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand SelectCommand { get; }

    /// <summary>The emitter changed (edit, undo): the fields that do not hold the typed text are updated.</summary>
    internal void Update(SparkleEmitter model)
    {
        _model = model;
        if (_name != model.Name)
        {
            _name = model.Name;
            OnPropertyChanged(nameof(Name));
        }

        X = model.X;
        Y = model.Y;
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(EventId));
        OnPropertyChanged(nameof(Role));
        foreach (SparkleSectionViewModel section in Sections)
        {
            section.Refresh();
        }
    }

    public override string ToString() => _name;
}

/// <summary>A part of the stack (Properties, Emitter spawn...): a title on the node, a folder in Properties.</summary>
public sealed class SparkleSectionViewModel : ObservableObject
{
    private bool _isExpanded = true;

    public SparkleSectionViewModel(SparkleEmitterViewModel emitter, SparkleSection section)
    {
        Section = section;
        Title = SparkleCatalog.SectionTitle(section);
        Modules = [.. SparkleCatalog.InSection(section).Select(i => new SparkleModuleViewModel(emitter, i))];
    }

    public SparkleSection Section { get; }

    public string Title { get; }

    public IReadOnlyList<SparkleModuleViewModel> Modules { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    internal void Refresh()
    {
        foreach (SparkleModuleViewModel module in Modules)
        {
            module.Refresh();
        }
    }
}

/// <summary>A module: a line of the node (title and summary of its settings), its settings in Properties.</summary>
public sealed class SparkleModuleViewModel : ObservableObject
{
    private bool _isSelected;

    public SparkleModuleViewModel(SparkleEmitterViewModel emitter, SparkleModuleInfo info)
    {
        Emitter = emitter;
        Info = info;
        Parameters = [.. info.Parameters.Select(p => new SparkleParameterViewModel(this, p))];
        SelectCommand = new RelayCommand(() => Emitter.Document.Select(Emitter, this));
    }

    public SparkleEmitterViewModel Emitter { get; }

    public SparkleModuleInfo Info { get; }

    public SparkleModule Model => Emitter.Model.Get(Info.Kind);

    public string Title => Info.Title;

    public string Description => Info.Description;

    public bool IsOptional => Info.IsOptional;

    /// <summary>The Performance module shows a summary instead of settings.</summary>
    public bool IsPerformance => Info.Kind == SparkleModuleKind.Performance;

    public bool Enabled
    {
        get => Emitter.Model.Uses(Info.Kind);
        set => Emitter.Document.ChangeEmitter(Emitter, e => e.With(Info.Kind, m => m with { Enabled = value }),
            value ? Tr.F("Switch on \"{0}\"", Info.Title) : Tr.F("Switch off \"{0}\"", Info.Title));
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Main settings in a few words, shown on the line of the node.</summary>
    public string Summary => IsPerformance
        ? Tr.F("Up to {0} particles", Emitter.Performance.MaxParticles)
        : string.Join(" · ", Parameters.Where(p => p.IsVisible).Select(p => p.ShortValue).Where(v => v.Length > 0).Take(3));

    public IReadOnlyList<SparkleParameterViewModel> Parameters { get; }

    public ICommand SelectCommand { get; }

    internal void SetValue(string name, string value)
    {
        if (Model.GetValue(name) != value)
        {
            Emitter.Document.ChangeEmitter(Emitter, e => e.With(Info.Kind, m => m.WithValue(name, value)),
                Tr.F("Change \"{0}\"", Info.FindParameter(name)?.Label ?? name), Info.Kind + "." + name);
        }
    }

    internal void Refresh()
    {
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(Summary));
        foreach (SparkleParameterViewModel parameter in Parameters)
        {
            parameter.Refresh();
        }
    }

    public override string ToString() => Title;
}
