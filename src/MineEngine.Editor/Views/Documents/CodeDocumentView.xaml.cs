using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Search;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.Views.Documents;

/// <summary>
/// Vue d'un onglet de code. Le code-behind relie l'éditeur AvalonEdit au ViewModel :
/// coloration selon le thème, position du curseur pour la barre de statut, saut de ligne
/// demandé par la structure ou les diagnostics. Pour Java, il ajoute ce qu'on attend
/// d'un éditeur de code : blocs repliables, indentation automatique, fermeture des
/// parenthèses et guillemets, auto-complétion (Ctrl+Espace ou en tapant), formatage
/// (Ctrl+Maj+F) et marques des erreurs de la dernière compilation.
/// </summary>
public partial class CodeDocumentView : UserControl
{
    /// <summary>Nombre de lettres tapées avant que la liste de complétion s'ouvre d'elle-même.</summary>
    private const int AutoCompletionPrefix = 3;

    private static readonly TimeSpan FoldingDelay = TimeSpan.FromMilliseconds(600);

    private static readonly Dictionary<char, char> ClosingPairs = new() { ['('] = ')', ['['] = ']', ['{'] = '}', ['"'] = '"', ['\''] = '\'' };

    private readonly SearchPanel _search;
    private readonly CodeMarkerRenderer _markerRenderer = new();
    private readonly CodeMarkerMargin _markerMargin = new();
    private readonly DispatcherTimer _foldingTimer;
    private readonly ToolTip _markerToolTip = new();
    private CodeDocumentViewModel? _document;
    private FoldingManager? _foldings;
    private BraceFoldingStrategy? _foldingStrategy;
    private CompletionWindow? _completion;

    public CodeDocumentView()
    {
        InitializeComponent();
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaret();
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.PreviewKeyDown += OnEditorKeyDown;
        Editor.TextArea.TextView.BackgroundRenderers.Add(_markerRenderer);
        Editor.TextArea.LeftMargins.Insert(0, _markerMargin);
        Editor.TextArea.TextView.MouseHover += OnTextViewMouseHover;
        Editor.TextArea.TextView.MouseHoverStopped += (_, _) => _markerToolTip.IsOpen = false;
        _search = SearchPanel.Install(Editor);

        _foldingTimer = new DispatcherTimer { Interval = FoldingDelay };
        _foldingTimer.Tick += (_, _) =>
        {
            _foldingTimer.Stop();
            UpdateFoldings();
        };
        Editor.TextChanged += (_, _) =>
        {
            _foldingTimer.Stop();
            _foldingTimer.Start();
        };

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

    private bool IsJava => _document?.IsJava == true;

    private void Attach(CodeDocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.LineRequested -= OnLineRequested;
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _completion?.Close();
        if (_foldings is not null)
        {
            FoldingManager.Uninstall(_foldings);
            _foldings = null;
        }

        // Le texte est affecté ici (et non par liaison) : le repli des blocs en a besoin tout de suite.
        _document = document;
        Editor.Document = document?.Document;
        if (_document is not null)
        {
            _document.LineRequested += OnLineRequested;
            _document.PropertyChanged += OnDocumentPropertyChanged;
            Editor.Options.IndentationSize = _document.IndentationLabel.StartsWith('2') ? 2 : 4;
            Editor.TextArea.IndentationStrategy = IsJava
                ? new CSharpIndentationStrategy(Editor.Options)
                : new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();

            _foldingStrategy = _document.Extension.ToLowerInvariant() switch
            {
                ".java" or ".gradle" => new BraceFoldingStrategy(includeBrackets: false),
                ".json" or ".mcmeta" => new BraceFoldingStrategy(includeBrackets: true),
                _ => null,
            };
            if (_foldingStrategy is not null)
            {
                _foldings = FoldingManager.Install(Editor.TextArea);
                UpdateFoldings();
            }

            ShowMarkers();
            if (_document.TakePendingLine() is > 0 and int line)
            {
                Dispatcher.BeginInvoke(() => OnLineRequested(this, line), DispatcherPriority.Loaded);
            }
        }

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        Editor.SyntaxHighlighting = CodeHighlighting.Get(_document?.HighlightingName);
        Editor.TextArea.TextView.CurrentLineBackground = Brush("CodeCurrentLineBrush");
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
        Editor.TextArea.SelectionBrush = Brush("CodeSelectionBrush");
        Editor.TextArea.SelectionForeground = null;
        Editor.TextArea.SelectionBorder = null;
        _search.MarkerBrush = Brush("CodeSelectionBrush");
        _markerRenderer.LineBrush = Brush("CodeErrorLineBrush");
        _markerRenderer.ErrorBrush = _markerMargin.ErrorBrush = Brush("LogErrorBrush");
        _markerRenderer.WarningBrush = _markerMargin.WarningBrush = Brush("LogWarningBrush");
        Editor.TextArea.TextView.Redraw();
        _markerMargin.InvalidateVisual();
    }

    private Brush Brush(string key) => TryFindResource(key) as Brush ?? Brushes.Transparent;

    private void OnThemeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(ApplyTheme);

    private void OnFindClick(object sender, RoutedEventArgs e) => _search.Open();

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CodeDocumentViewModel.Markers))
        {
            ShowMarkers();
        }
    }

    private void ShowMarkers()
    {
        _markerRenderer.Markers = _markerMargin.Markers = _document?.Markers ?? [];
        Editor.TextArea.TextView.InvalidateLayer(_markerRenderer.Layer);
    }

    private void UpdateFoldings()
    {
        if (_foldings is not null && _foldingStrategy is not null && Editor.Document is not null)
        {
            _foldingStrategy.UpdateFoldings(_foldings, Editor.Document);
        }
    }

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

    /// <summary>Message de l'erreur sous la souris.</summary>
    private void OnTextViewMouseHover(object sender, MouseEventArgs e)
    {
        var view = Editor.TextArea.TextView;
        if (_document is null || view.GetPositionFloor(e.GetPosition(view) + view.ScrollOffset) is not { } position)
        {
            return;
        }

        string[] messages = [.. _document.Markers.Where(m => m.Line == position.Line).Select(m => m.Message)];
        if (messages.Length > 0)
        {
            _markerToolTip.PlacementTarget = view;
            _markerToolTip.Content = string.Join("\n", messages);
            _markerToolTip.IsOpen = true;
            e.Handled = true;
        }
    }

    // ----- Raccourcis -----

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Space && IsJava)
        {
            ShowCompletion(force: true);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F)
        {
            if (_document?.FormatCommand.CanExecute(null) == true)
            {
                _document.FormatCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && _completion is null && IsJava && !Editor.IsReadOnly)
        {
            e.Handled = SplitBraces();
        }
    }

    /// <summary>Entrée entre "{" et "}" : l'accolade fermante passe deux lignes plus bas et le curseur au milieu, indenté.</summary>
    private bool SplitBraces()
    {
        TextDocument document = Editor.Document;
        int offset = Editor.CaretOffset;
        if (offset == 0 || offset >= document.TextLength || document.GetCharAt(offset - 1) != '{' || document.GetCharAt(offset) != '}')
        {
            return false;
        }

        DocumentLine line = document.GetLineByOffset(offset);
        string text = document.GetText(line);
        string indent = text[..(text.Length - text.TrimStart().Length)];
        string unit = new(' ', Editor.Options.IndentationSize);
        document.Insert(offset, "\n" + indent + unit + "\n" + indent);
        Editor.CaretOffset = offset + 1 + indent.Length + unit.Length;
        return true;
    }

    // ----- Fermeture automatique et complétion -----

    /// <summary>Taper un caractère fermant juste avant le même caractère passe par-dessus au lieu de le doubler.</summary>
    private void OnTextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 1 && _completion is not null && !IsIdentifierChar(e.Text[0]))
        {
            _completion.CompletionList.RequestInsertion(e);
        }

        if (!IsJava || e.Text.Length != 1 || e.Handled)
        {
            return;
        }

        char typed = e.Text[0];
        int offset = Editor.CaretOffset;
        if (typed is ')' or ']' or '}' or '"' or '\'' && offset < Editor.Document.TextLength && Editor.Document.GetCharAt(offset) == typed
            && Editor.SelectionLength == 0)
        {
            Editor.CaretOffset = offset + 1;
            e.Handled = true;
        }
    }

    private void OnTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (!IsJava || e.Text.Length != 1)
        {
            return;
        }

        char typed = e.Text[0];
        if (ClosingPairs.TryGetValue(typed, out char closing) && ShouldClose(typed))
        {
            int offset = Editor.CaretOffset;
            Editor.Document.Insert(offset, closing.ToString());
            Editor.CaretOffset = offset;
        }
        else if (IsIdentifierChar(typed) && _completion is null)
        {
            ShowCompletion(force: false);
        }
    }

    /// <summary>Pas de fermeture devant un mot, ni pour un guillemet collé à un mot ou échappé.</summary>
    private bool ShouldClose(char typed)
    {
        TextDocument document = Editor.Document;
        int offset = Editor.CaretOffset;
        char next = offset < document.TextLength ? document.GetCharAt(offset) : '\0';
        if (IsIdentifierChar(next))
        {
            return false;
        }

        if (typed is '"' or '\'')
        {
            char previous = offset >= 2 ? document.GetCharAt(offset - 2) : '\0';
            return !IsIdentifierChar(previous) && previous != '\\';
        }

        return true;
    }

    /// <summary>
    /// Ouvre la liste : mots-clés, types (avec import), extraits et identifiants déjà présents
    /// dans le fichier. Sans <paramref name="force"/>, seulement après quelques lettres et s'il y a une correspondance.
    /// </summary>
    private void ShowCompletion(bool force)
    {
        if (_document is null || Editor.IsReadOnly || _completion is not null)
        {
            return;
        }

        TextDocument document = Editor.Document;
        int start = Editor.CaretOffset;
        while (start > 0 && IsIdentifierChar(document.GetCharAt(start - 1)))
        {
            start--;
        }

        string prefix = document.GetText(start, Editor.CaretOffset - start);
        if ((!force && prefix.Length < AutoCompletionPrefix) || (prefix.Length > 0 && char.IsDigit(prefix[0])) || IsInCommentOrString(start))
        {
            return;
        }

        List<ICompletionData> entries = [.. Entries(document.Text, prefix)];
        if (prefix.Length > 0 && !entries.Any(e => e.Text.Contains(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var window = new CompletionWindow(Editor.TextArea) { StartOffset = start, CloseWhenCaretAtBeginning = true };
        foreach (ICompletionData entry in entries)
        {
            window.CompletionList.CompletionData.Add(entry);
        }

        StyleCompletionWindow(window);
        window.Closed += (_, _) => _completion = null;
        _completion = window;
        window.Show();
        if (prefix.Length > 0)
        {
            window.CompletionList.SelectItem(prefix);
        }
    }

    private IEnumerable<ICompletionData> Entries(string text, string prefix)
    {
        IReadOnlyList<JavaCompletionEntry> known = _document!.Completions;
        var labels = new HashSet<string>(known.Select(k => k.Label), StringComparer.Ordinal);
        IEnumerable<JavaCompletionEntry> words = Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]{2,}\b")
            .Select(m => m.Value)
            .Where(w => w != prefix && labels.Add(w))
            .Take(2000)
            .Select(w => new JavaCompletionEntry(w, JavaCompletionKind.Keyword, Tr.T("Present in the file"), w));
        return known.Concat(words).OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase).Select(e => new JavaCompletionItem(e));
    }

    private void StyleCompletionWindow(CompletionWindow window)
    {
        window.Background = Brush("CardBrush");
        window.BorderBrush = Brush("CardBorderBrush");
        window.Foreground = Brush("StrongTextBrush");
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStyle = WindowStyle.None;
        window.CompletionList.Background = Brush("CardBrush");
        window.CompletionList.Foreground = Brush("StrongTextBrush");
        if (window.CompletionList.ListBox is { } list)
        {
            list.Background = Brush("CardBrush");
            list.Foreground = Brush("StrongTextBrush");
            list.BorderThickness = new Thickness(0);
        }
    }

    /// <summary>Approximation : le début de la ligne contient un commentaire // ou un nombre impair de guillemets.</summary>
    private bool IsInCommentOrString(int offset)
    {
        DocumentLine line = Editor.Document.GetLineByOffset(offset);
        string before = Editor.Document.GetText(line.Offset, offset - line.Offset);
        string trimmed = before.TrimStart();
        return before.Contains("//", StringComparison.Ordinal) || trimmed.StartsWith('*') || trimmed.StartsWith("/*", StringComparison.Ordinal)
               || before.Count(c => c == '"') % 2 == 1;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';
}
