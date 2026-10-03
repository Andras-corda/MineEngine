using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>
/// Cible du panneau Détails : un asset ou un onglet de code. Fournit l'en-tête "Cible"
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

/// <summary>
/// Un onglet de code : texte (document AvalonEdit), position du curseur, structure,
/// état modifié. Les sous-classes disent d'où vient le texte et comment l'enregistrer.
/// </summary>
public abstract class CodeDocumentViewModel : ObservableObject, IDetailsTarget
{
    private static readonly TimeSpan OutlineDelay = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _outlineTimer;
    private int _line = 1;
    private int _column = 1;
    private bool _isDirty;
    private IReadOnlyList<OutlineItem> _outline = [];

    protected CodeDocumentViewModel(string id, string displayPath, string extension)
    {
        Id = id;
        DisplayPath = displayPath.Replace('\\', '/');
        Extension = extension;
        Document = new TextDocument();
        Document.TextChanged += (_, _) => OnTextChanged();
        _outlineTimer = new DispatcherTimer { Interval = OutlineDelay };
        _outlineTimer.Tick += (_, _) =>
        {
            _outlineTimer.Stop();
            Outline = OutlineBuilder.Build(Extension, Document.Text);
        };
        GoToLineCommand = new RelayCommand<OutlineItem>(item => GoToLine(item.Line));
    }

    /// <summary>Demande à la vue d'afficher une ligne (clic dans la structure).</summary>
    public event EventHandler<int>? LineRequested;

    /// <summary>Identifiant de l'onglet (unique parmi les onglets ouverts).</summary>
    public string Id { get; }

    /// <summary>Chemin relatif au projet, avec des '/'.</summary>
    public string DisplayPath { get; }

    public string Extension { get; }

    public string FileName => Path.GetFileName(DisplayPath);

    public TextDocument Document { get; }

    public abstract bool IsReadOnly { get; }

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
        ".properties" => "Propriétés",
        ".gradle" => "Gradle",
        ".txt" or "" => "Texte",
        ".xml" => "XML",
        ".bat" or ".cmd" => "Script Windows",
        ".sh" => "Script shell",
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
                return "Tabulations";
            }

            // Les lignes de commentaire " * " (javadoc) ne comptent pas.
            int smallest = text.Split('\n')
                .Where(l => !l.TrimStart().StartsWith('*'))
                .Select(l => l.Length - l.TrimStart(' ').Length)
                .Where(n => n > 1)
                .DefaultIfEmpty(4)
                .Min();
            return $"{smallest} espaces";
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

    public string CaretLabel => string.Create(CultureInfo.CurrentCulture, $"Ln {_line}, Col {_column}");

    public int LineCount => Document.LineCount;

    public bool IsDirty
    {
        get => _isDirty;
        protected set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    /// <summary>Titre de l'onglet ; un point signale des modifications non enregistrées.</summary>
    public string Title => _isDirty ? FileName + " ●" : FileName;

    public IReadOnlyList<OutlineItem> Outline
    {
        get => _outline;
        private set => SetProperty(ref _outline, value);
    }

    public IReadOnlyList<BreadcrumbSegment> Breadcrumb
    {
        get
        {
            string[] parts = DisplayPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return [.. parts.Select((p, i) => new BreadcrumbSegment(p, i == parts.Length - 1))];
        }
    }

    public ICommand GoToLineCommand { get; }

    public virtual string DetailsTitle => FileName;

    public virtual string DetailsBadge => IsReadOnly ? "LECTURE SEULE" : "FICHIER";

    public string DetailsSubtitle => DisplayPath;

    /// <summary>Enregistre le texte ; faux si l'enregistrement a échoué ou est impossible.</summary>
    public abstract bool Save();

    public void GoToLine(int line) => LineRequested?.Invoke(this, Math.Clamp(line, 1, Math.Max(1, Document.LineCount)));

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

    private void OnTextChanged()
    {
        IsDirty = !IsReadOnly && !Document.UndoStack.IsOriginalFile;
        OnPropertyChanged(nameof(LineCount));
        _outlineTimer.Stop();
        _outlineTimer.Start();
    }
}
