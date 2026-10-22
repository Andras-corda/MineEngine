using System.Collections.ObjectModel;
using System.Windows;
using MineEngine.Assets.Puppeteer;
using GraphAnnotations = MineEngine.Assets.Graphs.GraphAnnotations;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Puppeteer;

/// <summary>Problems of an element of the tree, shown as a border and a tooltip.</summary>
public abstract class BtItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _issueText = string.Empty;
    private bool _hasError;
    private bool _hasWarning;

    protected BtItemViewModel(BehaviorTreeDocumentViewModel document)
    {
        Document = document;
    }

    public BehaviorTreeDocumentViewModel Document { get; }

    public abstract Guid Id { get; }

    public abstract BtNodeInfo Info { get; }

    public BtCategory Category => Info.Category;

    public string CategoryLabel => BtCatalog.CategoryLabel(Info.Category);

    public string Description => Info.Description;

    public abstract string Title { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string IssueText
    {
        get => _issueText;
        private set => SetProperty(ref _issueText, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool HasWarning
    {
        get => _hasWarning;
        private set => SetProperty(ref _hasWarning, value);
    }

    /// <summary>Stored setting (or default value of the catalog).</summary>
    public abstract string GetValue(string name);

    internal void SetIssues(IReadOnlyList<BtIssue> issues)
    {
        HasError = issues.Any(i => i.Severity == DiagnosticSeverity.Error);
        HasWarning = !HasError && issues.Any(i => i.Severity == DiagnosticSeverity.Warning);
        IssueText = string.Join(Environment.NewLine, issues.Select(i => i.Message));
    }
}

/// <summary>
/// A node on the Puppeteer canvas: colored header, summary of its settings, and the
/// decorators (above) and services (below) attached to it.
/// </summary>
public sealed class BtNodeViewModel : BtItemViewModel
{
    private BtNode _model;
    private double _x;
    private double _y;
    private double _width = 220;
    private double _height = 90;
    private int _order;
    private bool _isReachable = true;
    private IReadOnlyList<string> _summary = [];

    public BtNodeViewModel(BehaviorTreeDocumentViewModel document, BtNode model)
        : base(document)
    {
        _model = model;
        _x = model.X;
        _y = model.Y;
        Refresh();
    }

    public BtNode Model => _model;

    public override Guid Id => _model.Id;

    public override BtNodeInfo Info => _model.Info;

    public override string Title => Info.Title;

    public bool IsRoot => _model.Kind == BtNodeKind.Root;

    /// <summary>Root or composite: a pin at the bottom for its children.</summary>
    public bool HasChildrenPin => Info.HasChildren;

    /// <summary>Everything but the root (and the comments and tooltips): a pin at the top for its parent.</summary>
    public bool HasParentPin => !IsRoot && !IsAnnotation;

    // ----- Comments and tooltips (values: see GraphAnnotations) -----

    private double _commentWidth;
    private double _commentHeight;
    private string? _attachedTooltip;
    private bool _isEditingTitle;

    public bool IsComment => _model.Kind == BtNodeKind.Comment;

    public bool IsTooltip => _model.Kind == BtNodeKind.Tooltip;

    public bool IsAnnotation => Info.IsAnnotation;

    /// <summary>Title of a comment, message of a tooltip.</summary>
    public string AnnotationText
    {
        get => GraphAnnotations.Text(_model.Values);
        set => Document.SetValue(this, GraphAnnotations.TextKey, value ?? string.Empty);
    }

    public string CommentTooltip
    {
        get => GraphAnnotations.Text(_model.Values, GraphAnnotations.TooltipKey);
        set => Document.SetValue(this, GraphAnnotations.TooltipKey, value ?? string.Empty);
    }

    public string CommentColor
    {
        get => _model.Values.GetValueOrDefault(GraphAnnotations.ColorKey) ?? GraphAnnotations.CommentColors[0];
        set => Document.SetValue(this, GraphAnnotations.ColorKey, value ?? GraphAnnotations.CommentColors[0]);
    }

    public IReadOnlyList<string> CommentColors => GraphAnnotations.CommentColors;

    /// <summary>Size of a comment; follows the mouse while it is resized.</summary>
    public double CommentWidth
    {
        get => _commentWidth;
        set => SetProperty(ref _commentWidth, Math.Max(GraphAnnotations.MinWidth, value));
    }

    public double CommentHeight
    {
        get => _commentHeight;
        set => SetProperty(ref _commentHeight, Math.Max(GraphAnnotations.MinHeight, value));
    }

    /// <summary>The title of the comment is being edited on the canvas (double-click).</summary>
    public bool IsEditingTitle
    {
        get => _isEditingTitle;
        set => SetProperty(ref _isEditingTitle, value);
    }

    /// <summary>Messages of the tooltips that describe this node, or null.</summary>
    public string? AttachedTooltip
    {
        get => _attachedTooltip;
        internal set
        {
            if (SetProperty(ref _attachedTooltip, value))
            {
                OnPropertyChanged(nameof(HoverText));
            }
        }
    }

    /// <summary>What the mouse shows over the node: its tooltips, the message of a bubble, the tooltip of a comment.</summary>
    public string? HoverText => IsTooltip ? NullIfEmpty(AnnotationText) : IsComment ? NullIfEmpty(CommentTooltip) : _attachedTooltip;

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    public ObservableCollection<BtAttachmentViewModel> Decorators { get; } = [];

    public ObservableCollection<BtAttachmentViewModel> Services { get; } = [];

    public IEnumerable<BtAttachmentViewModel> Attachments => Decorators.Concat(Services);

    /// <summary>Settings summarized on the node.</summary>
    public IReadOnlyList<string> Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool HasSummary => _summary.Count > 0;

    /// <summary>Rank in the execution order (like Unreal's numbers); 0 when the node is not connected.</summary>
    public int Order
    {
        get => _order;
        set
        {
            if (SetProperty(ref _order, value))
            {
                OnPropertyChanged(nameof(HasOrder));
            }
        }
    }

    public bool HasOrder => _order > 0;

    /// <summary>False for a node that is not connected to the root (it is grayed out).</summary>
    public bool IsReachable
    {
        get => _isReachable;
        set => SetProperty(ref _isReachable, value);
    }

    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
            {
                OnAnchorsMoved();
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
                OnAnchorsMoved();
            }
        }
    }

    /// <summary>Displayed size (measured by the view): links attach to the middle of the edges.</summary>
    public void SetSize(double width, double height)
    {
        if (Math.Abs(width - _width) > 0.5 || Math.Abs(height - _height) > 0.5)
        {
            _width = width;
            _height = height;
            OnAnchorsMoved();
        }
    }

    public Rect Bounds => new(_x, _y, _width, _height);

    /// <summary>Top pin, where the link from the parent arrives.</summary>
    public Point TopAnchor => new(_x + _width / 2, _y + BehaviorTreeDocumentViewModel.PinOffset);

    /// <summary>Bottom pin, where the links to the children start.</summary>
    public Point BottomAnchor => new(_x + _width / 2, _y + _height - BehaviorTreeDocumentViewModel.PinOffset);

    public event EventHandler? AnchorsMoved;

    public override string GetValue(string name) => _model.GetValue(name);

    public BtAttachmentViewModel? FindAttachment(Guid id) => Attachments.FirstOrDefault(a => a.Id == id);

    /// <summary>The node changed in the tree (setting, move, undo).</summary>
    internal void Update(BtNode model)
    {
        _model = model;
        X = model.X;
        Y = model.Y;
        Refresh();
    }

    private void RefreshAnnotation()
    {
        if (IsComment)
        {
            (double width, double height) = GraphAnnotations.Size(_model.Values);
            CommentWidth = width;
            CommentHeight = height;
        }

        OnPropertyChanged(nameof(AnnotationText));
        OnPropertyChanged(nameof(CommentTooltip));
        OnPropertyChanged(nameof(CommentColor));
        OnPropertyChanged(nameof(HoverText));
    }

    /// <summary>Recomputes the summary and the attached pieces (key or asset names may have changed).</summary>
    internal void Refresh()
    {
        if (IsAnnotation)
        {
            RefreshAnnotation();
            return;
        }

        Summary = BtText.Summary(Info, _model.GetValue, Document.Keys.Select(k => k.Model).ToList(), Document.Assets);
        OnPropertyChanged(nameof(HasSummary));
        Sync(Decorators, _model.Decorators);
        Sync(Services, _model.Services);
    }

    private void Sync(ObservableCollection<BtAttachmentViewModel> target, IReadOnlyList<BtAttachment> models)
    {
        Dictionary<Guid, BtAttachmentViewModel> existing = target.ToDictionary(a => a.Id);
        target.Clear();
        foreach (BtAttachment model in models)
        {
            if (existing.TryGetValue(model.Id, out BtAttachmentViewModel? kept))
            {
                kept.Update(model);
                target.Add(kept);
            }
            else
            {
                target.Add(new BtAttachmentViewModel(Document, this, model));
            }
        }
    }

    private void OnAnchorsMoved()
    {
        OnPropertyChanged(nameof(TopAnchor));
        OnPropertyChanged(nameof(BottomAnchor));
        AnchorsMoved?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => Title;
}

/// <summary>Decorator or service attached to a node.</summary>
public sealed class BtAttachmentViewModel : BtItemViewModel
{
    private BtAttachment _model;
    private string _title = string.Empty;
    private string _summary = string.Empty;
    private string? _abortLabel;

    public BtAttachmentViewModel(BehaviorTreeDocumentViewModel document, BtNodeViewModel owner, BtAttachment model)
        : base(document)
    {
        Owner = owner;
        _model = model;
        Refresh();
    }

    public BtNodeViewModel Owner { get; }

    public BtAttachment Model => _model;

    public override Guid Id => _model.Id;

    public override BtNodeInfo Info => _model.Info;

    public override string Title => _title;

    public bool IsService => Info.Category == BtCategory.Service;

    /// <summary>Settings summarized on one line.</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>"Interrupts: ..." for a condition checked again while the tree runs, otherwise null.</summary>
    public string? AbortLabel
    {
        get => _abortLabel;
        private set => SetProperty(ref _abortLabel, value);
    }

    public override string GetValue(string name) => _model.GetValue(name);

    internal void Update(BtAttachment model)
    {
        _model = model;
        Refresh();
    }

    internal void Refresh()
    {
        _title = BtText.Title(Info, _model.GetValue);
        OnPropertyChanged(nameof(Title));
        Summary = string.Join(" · ", BtText.Summary(Info, _model.GetValue, Document.Keys.Select(k => k.Model).ToList(), Document.Assets));
        AbortLabel = BtText.AbortLabel(Info, _model.GetValue);
    }

    public override string ToString() => Title;
}
