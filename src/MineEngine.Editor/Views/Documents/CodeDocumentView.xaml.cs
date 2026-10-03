using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MineEngine.Editor.ViewModels.Documents;

namespace MineEngine.Editor.Views.Documents;

/// <summary>
/// Vue d'un onglet de code. Le code-behind relie l'éditeur AvalonEdit au ViewModel :
/// coloration selon le thème, position du curseur pour la barre de statut, saut de ligne
/// demandé par la structure.
/// </summary>
public partial class CodeDocumentView : UserControl
{
    private CodeDocumentViewModel? _document;

    public CodeDocumentView()
    {
        InitializeComponent();
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaret();
        DataContextChanged += (_, _) => Attach(DataContext as CodeDocumentViewModel);
        Loaded += (_, _) =>
        {
            CodeHighlighting.ThemeChanged += OnThemeChanged;
            ApplyTheme();
        };
        Unloaded += (_, _) => CodeHighlighting.ThemeChanged -= OnThemeChanged;
    }

    /// <summary>Donne le focus au texte (onglet activé).</summary>
    public void FocusEditor() => Editor.TextArea.Focus();

    private void Attach(CodeDocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.LineRequested -= OnLineRequested;
        }

        _document = document;
        if (_document is not null)
        {
            _document.LineRequested += OnLineRequested;
            Editor.Options.IndentationSize = _document.IndentationLabel.StartsWith('2') ? 2 : 4;
        }

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        Editor.SyntaxHighlighting = CodeHighlighting.Get(_document?.HighlightingName);
        Editor.TextArea.TextView.CurrentLineBackground = Brush("CardBrush");
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brush("CardBorderBrush"), 1);
        Editor.TextArea.SelectionBrush = Brush("NavSelectedBrush");
        Editor.TextArea.SelectionForeground = null;
        Editor.TextArea.SelectionBorder = null;
        Editor.TextArea.TextView.Redraw();
    }

    private Brush Brush(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private void OnThemeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(ApplyTheme);

    private void UpdateCaret()
    {
        if (_document is not null)
        {
            _document.Line = Editor.TextArea.Caret.Line;
            _document.Column = Editor.TextArea.Caret.Column;
        }
    }

    private void OnLineRequested(object? sender, int line)
    {
        Editor.TextArea.Caret.Line = line;
        Editor.TextArea.Caret.Column = 1;
        Editor.ScrollToLine(line);
        Editor.TextArea.Focus();
    }
}
