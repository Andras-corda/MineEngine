using MineEngine.Assets.Graphs;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>
/// Comment or tooltip of the canvas, like in the graphs. A comment is a colored frame with a title
/// (moving it moves the emitters inside it); a tooltip is a bubble whose message shows over it and
/// over the emitters it describes. Their texts are edited in Properties.
/// </summary>
public sealed class SparkleNoteViewModel : ObservableObject
{
    private readonly SparkleDocumentViewModel _document;
    private SparkleNote _model;
    private string _text;
    private string _tooltip;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _isSelected;

    public SparkleNoteViewModel(SparkleDocumentViewModel document, SparkleNote model)
    {
        _document = document;
        _model = model;
        _text = model.Text;
        _tooltip = model.Tooltip;
        _x = model.X;
        _y = model.Y;
        _width = model.Width;
        _height = model.Height;
    }

    public Guid Id => _model.Id;

    public SparkleNote Model => _model;

    public bool IsComment => _model.Kind == SparkleNoteKind.Comment;

    public bool IsTooltip => _model.Kind == SparkleNoteKind.Tooltip;

    /// <summary>Title of a comment, message of a tooltip; typing makes a single undoable change.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                _document.ChangeNote(this, n => n with { Text = _text }, IsComment ? Tr.T("Rename a comment") : Tr.T("Change a tooltip"), "text");
                OnPropertyChanged(nameof(HoverText));
            }
        }
    }

    /// <summary>Message shown over the title of a comment.</summary>
    public string Tooltip
    {
        get => _tooltip;
        set
        {
            if (SetProperty(ref _tooltip, value ?? string.Empty))
            {
                _document.ChangeNote(this, n => n with { Tooltip = _tooltip }, Tr.T("Change the tooltip of a comment"), "tooltip");
                OnPropertyChanged(nameof(HoverText));
            }
        }
    }

    /// <summary>Text shown when the mouse is over the note (null: nothing).</summary>
    public string? HoverText => IsTooltip ? _text : string.IsNullOrWhiteSpace(_tooltip) ? null : _tooltip;

    public string Color
    {
        get => _model.Color;
        set
        {
            if (value is not null && value != _model.Color)
            {
                _document.ChangeNote(this, n => n with { Color = value }, Tr.T("Change the color of a comment"));
            }
        }
    }

    public IReadOnlyList<string> CommentColors => GraphAnnotations.CommentColors;

    /// <summary>Names of the emitters a tooltip describes.</summary>
    public string TargetNames => string.Join(", ", _document.Emitters.Where(e => _model.Targets.Contains(e.Id)).Select(e => e.Name));

    public bool HasTargets => _model.Targets.Count > 0;

    /// <summary>Position and size on the canvas; they follow the mouse while dragged, then are applied once.</summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, Math.Max(120, value));
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, Math.Max(80, value));
    }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>The note changed (edit, undo): the fields that do not hold the typed text are updated.</summary>
    internal void Update(SparkleNote model)
    {
        _model = model;
        if (_text != model.Text)
        {
            _text = model.Text;
            OnPropertyChanged(nameof(Text));
        }

        if (_tooltip != model.Tooltip)
        {
            _tooltip = model.Tooltip;
            OnPropertyChanged(nameof(Tooltip));
        }

        X = model.X;
        Y = model.Y;
        Width = model.Width;
        Height = model.Height;
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(HoverText));
        OnPropertyChanged(nameof(TargetNames));
        OnPropertyChanged(nameof(HasTargets));
    }

    public override string ToString() => _text;
}
