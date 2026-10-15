using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.Mvvm;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>
/// Cible du panneau Détails : un asset ou un onglet. Fournit l'en-tête "Cible"
/// affiché en haut du panneau.
/// </summary>
public interface IDetailsTarget
{
    string DetailsTitle { get; }

    /// <summary>Badge en majuscules ("ITEM", "FICHIER", "LECTURE SEULE").</summary>
    string DetailsBadge { get; }

    /// <summary>Ligne secondaire (identifiant en jeu, chemin du fichier).</summary>
    string DetailsSubtitle { get; }
}

/// <summary>Un segment du fil d'Ariane au-dessus de l'éditeur ("Projet > scripts > terrain.java").</summary>
public sealed record BreadcrumbSegment(string Name, bool IsLast);

/// <summary>Problème signalé sur une ligne du fichier (erreur javac de la dernière compilation).</summary>
public sealed record CodeMarker(int Line, DiagnosticSeverity Severity, string Message);

/// <summary>
/// Un onglet de code : texte (document AvalonEdit), position du curseur, structure,
/// état modifié. Les sous-classes disent d'où vient le texte et comment l'enregistrer.
/// </summary>
public abstract class CodeDocumentViewModel : DocumentViewModel
{
    private static readonly TimeSpan OutlineDelay = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _outlineTimer;
    private int _line = 1;
    private int _column = 1;
    private int _pendingLine;
    private IReadOnlyList<CodeMarker> _markers = [];

    protected CodeDocumentViewModel(string id, string displayPath, string extension)
        : base(id, displayPath)
    {
        Extension = extension;
        Document = new TextDocument();
        Document.TextChanged += (_, _) => OnTextChanged();
        _outlineTimer = new DispatcherTimer { Interval = OutlineDelay };
        _outlineTimer.Tick += (_, _) =>
        {
            _outlineTimer.Stop();
            Outline = OutlineBuilder.Build(Extension, Document.Text);
        };
        FormatCommand = new RelayCommand(Format, () => CanFormat);
        SaveCommand = new RelayCommand(() => Save(), () => !IsReadOnly && IsDirty);
    }

    /// <summary>Demande à la vue d'afficher une ligne (clic dans la structure).</summary>
    public event EventHandler<int>? LineRequested;

    public string Extension { get; }

    public bool IsJava => string.Equals(Extension, ".java", StringComparison.OrdinalIgnoreCase);

    public bool CanFormat => IsJava && !IsReadOnly;

    /// <summary>Enregistre cet onglet.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Réindente tout le fichier Java (une seule étape d'annulation).</summary>
    public ICommand FormatCommand { get; }

    /// <summary>Propositions de l'auto-complétion (fichiers Java du projet), vide sinon.</summary>
    public IReadOnlyList<JavaCompletionEntry> Completions { get; set; } = [];

    /// <summary>Problèmes de la dernière compilation situés dans ce fichier.</summary>
    public IReadOnlyList<CodeMarker> Markers
    {
        get => _markers;
        set => SetProperty(ref _markers, value ?? []);
    }

    public TextDocument Document { get; }

    /// <summary>Explication affichée en bandeau au-dessus du texte, ou null.</summary>
    public abstract string? Banner { get; }

    /// <summary>Type de fichier lisible ("Java", "JSON"...).</summary>
    public string Language => Extension.ToLowerInvariant() switch
    {
        ".java" => "Java",
        ".json" => "JSON",
        ".mcmeta" => "JSON (mcmeta)",
        ".md" => "Markdown",
        ".toml" => "TOML",
        ".properties" => Tr.T("Propriétés"),
        ".gradle" => "Gradle",
        ".txt" or "" => Tr.T("Texte"),
        ".xml" => "XML",
        ".bat" or ".cmd" => Tr.T("Script Windows"),
        ".sh" => Tr.T("Script shell"),
        _ => Extension.TrimStart('.').ToUpperInvariant(),
    };

    /// <summary>Nom de la coloration AvalonEdit, ou null pour du texte brut.</summary>
    public string? HighlightingName => Extension.ToLowerInvariant() switch
    {
        ".java" or ".gradle" => "Java",
        ".json" or ".mcmeta" => "Json",
        ".md" => "MarkDown",
        ".xml" => "XML",
        ".ps1" => "PowerShell",
        ".py" => "Python",
        _ => null,
    };

    public virtual string EncodingLabel => "UTF-8";

    /// <summary>Indentation détectée ("4 espaces", "Tabulations").</summary>
    public string IndentationLabel
    {
        get
        {
            string text = Document.Text;
            if (text.Contains("\n\t", StringComparison.Ordinal))
            {
                return Tr.T("Tabulations");
            }

            // Les lignes de commentaire " * " (javadoc) ne comptent pas.
            int smallest = text.Split('\n')
                .Where(l => !l.TrimStart().StartsWith('*'))
                .Select(l => l.Length - l.TrimStart(' ').Length)
                .Where(n => n > 1)
                .DefaultIfEmpty(4)
                .Min();
            return Tr.F("{0} espaces", smallest);
        }
    }

    public int Line
    {
        get => _line;
        set
        {
            if (SetProperty(ref _line, value))
            {
                OnPropertyChanged(nameof(CaretLabel));
            }
        }
    }

    public int Column
    {
        get => _column;
        set
        {
            if (SetProperty(ref _column, value))
            {
                OnPropertyChanged(nameof(CaretLabel));
            }
        }
    }

    public string CaretLabel => Tr.F("Ln {0}, Col {1}", _line, _column);

    public int LineCount => Document.LineCount;

    public IReadOnlyList<BreadcrumbSegment> Breadcrumb
    {
        get
        {
            string[] parts = DisplayPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return [.. parts.Select((p, i) => new BreadcrumbSegment(p, i == parts.Length - 1))];
        }
    }

    /// <summary>Affiche une ligne ; si la vue n'existe pas encore, elle l'affichera à son ouverture.</summary>
    public void GoToLine(int line)
    {
        int target = Math.Clamp(line, 1, Math.Max(1, Document.LineCount));
        if (LineRequested is null)
        {
            _pendingLine = target;
            return;
        }

        LineRequested(this, target);
    }

    /// <summary>Ligne demandée avant que la vue soit prête (0 si aucune) ; elle n'est rendue qu'une fois.</summary>
    public int TakePendingLine()
    {
        int line = _pendingLine;
        _pendingLine = 0;
        return line;
    }

    public override void Navigate(OutlineItem item) => GoToLine(item.Line);

    /// <summary>Remplace le texte sans le marquer comme modifié (chargement, rechargement).</summary>
    protected void LoadText(string text)
    {
        Document.Text = text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        IsDirty = false;
        Outline = OutlineBuilder.Build(Extension, text);
        OnPropertyChanged(nameof(IndentationLabel));
        OnPropertyChanged(nameof(LineCount));
    }

    protected void MarkSaved()
    {
        Document.UndoStack.MarkAsOriginalFile();
        IsDirty = false;
    }

    private void Format()
    {
        string formatted = new JavaFormatter(IndentationLabel.StartsWith('2') ? 2 : 4).Format(Document.Text);
        if (formatted != Document.Text)
        {
            Document.Replace(0, Document.TextLength, formatted);
        }
    }

    private void OnTextChanged()
    {
        IsDirty = !IsReadOnly && !Document.UndoStack.IsOriginalFile;
        OnPropertyChanged(nameof(LineCount));
        _outlineTimer.Stop();
        _outlineTimer.Start();
    }
}
