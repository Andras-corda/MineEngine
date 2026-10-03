using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Themes;
using ICSharpCode.AvalonEdit.Editing;
using MineEngine.Editor.Services;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.Views.Documents;

namespace MineEngine.Editor.Views;

/// <summary>
/// Fenêtre principale. Le code-behind ne contient que ce qui relève de la vue :
/// panneaux ancrables (affichage, thème, disposition enregistrée), onglets de code
/// (un onglet AvalonDock par document ouvert), raccourcis annuler/rétablir et
/// confirmation à la fermeture.
/// </summary>
public partial class MainWindow : Window
{
    private const string DashboardContentId = "home";

    private readonly MainViewModel _viewModel;
    private readonly IThemeService _themes;
    private readonly DockLayoutService _layout;

    /// <summary>Onglets en cours de fermeture par l'utilisateur (pour ne pas les refermer une seconde fois).</summary>
    private readonly HashSet<CodeDocumentViewModel> _closing = [];

    public MainWindow(MainViewModel viewModel, IThemeService themes, DockLayoutService layout)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _themes = themes ?? throw new ArgumentNullException(nameof(themes));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        InitializeComponent();
        DataContext = viewModel;

        // Un panneau détaché dans une fenêtre flottante ne garde pas toujours le DataContext
        // de la fenêtre principale : chaque panneau le reçoit donc explicitement.
        foreach (FrameworkElement panel in new FrameworkElement[]
                 {
                     ExplorerPanel, DashboardPanel, OutputPanel, DiagnosticsPanel, InspectorPanel,
                 })
        {
            panel.DataContext = viewModel;
        }

        ApplyTheme();
        _themes.AppearanceChanged += OnAppearanceChanged;
        _viewModel.PanelRequested += (_, panelId) => ShowPanel(panelId);
        _viewModel.Documents.CollectionChanged += OnDocumentsChanged;
        _viewModel.DocumentActivationRequested += (_, document) => ActivateDocument(document);
        DockManager.ActiveContentChanged += OnActiveContentChanged;
        DockManager.DocumentClosing += OnDocumentClosing;
        Loaded += (_, _) => _layout.Initialize(DockManager);
        // AvalonDock répartit d'abord la place lui-même : l'ajustement passe après.
        SizeChanged += (_, e) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            FitSidePanels(e.NewSize.Width);
            FitBottomPanel(e.NewSize.Height);
        });
    }

    // ----- Adaptation à la largeur -----

    /// <summary>Largeur souhaitée des panneaux latéraux (Explorateur, Détails) sur un grand écran.</summary>
    private static readonly IReadOnlyDictionary<string, double> PreferredSideWidth = new Dictionary<string, double>
    {
        [MainViewModel.ExplorerPanelId] = 270,
        [MainViewModel.InspectorPanelId] = 380,
    };

    /// <summary>Part maximale de la fenêtre occupée par les panneaux latéraux.</summary>
    private const double MaxSideShare = 0.5;

    /// <summary>Hauteur habituelle du panneau Sortie / Diagnostics.</summary>
    private const double PreferredBottomHeight = 220;

    /// <summary>Fenêtre basse : le panneau du bas ne prend pas plus du quart de la hauteur.</summary>
    private void FitBottomPanel(double windowHeight)
    {
        LayoutAnchorablePane? pane = DockManager.Layout.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == MainViewModel.OutputPanelId && !a.IsHidden && !a.IsFloating)?.Parent as LayoutAnchorablePane;
        if (pane is not { Parent: LayoutPanel { Orientation: Orientation.Vertical } })
        {
            return;
        }

        double height = Math.Clamp(windowHeight * 0.25, 90, PreferredBottomHeight);
        if (Math.Abs(pane.DockHeight.Value - height) > 1 && (pane.DockHeight.Value > height || height == PreferredBottomHeight))
        {
            pane.DockHeight = new GridLength(height);
        }
    }

    /// <summary>
    /// Fenêtre étroite : les panneaux latéraux rétrécissent pour laisser au moins la moitié
    /// de la largeur aux onglets ; fenêtre large : ils retrouvent leur taille habituelle.
    /// </summary>
    private void FitSidePanels(double windowWidth)
    {
        var panes = PreferredSideWidth.Keys
            .Select(id => DockManager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == id && !a.IsHidden && !a.IsFloating))
            .Select(a => a?.Parent as LayoutAnchorablePane)
            .Where(p => p is { Parent: LayoutPanel { Orientation: Orientation.Horizontal } })
            .Distinct()
            .ToList();
        if (panes.Count == 0)
        {
            return;
        }

        double preferredTotal = panes.Sum(p => PreferredSideWidth[p!.Children[0].ContentId]);
        double scale = Math.Min(1, windowWidth * MaxSideShare / preferredTotal);
        foreach (LayoutAnchorablePane pane in panes!)
        {
            double width = Math.Max(160, PreferredSideWidth[pane.Children[0].ContentId] * scale);
            if (Math.Abs(pane.DockWidth.Value - width) > 1 && (scale < 1 || pane.DockWidth.Value < width))
            {
                pane.DockWidth = new GridLength(width);
            }
        }
    }

    // ----- Onglets de code -----

    private LayoutDocument? FindLayoutDocument(string contentId) =>
        DockManager.Layout.Descendents().OfType<LayoutDocument>().FirstOrDefault(d => d.ContentId == contentId);

    /// <summary>Panneau d'onglets du Dashboard (recréé si la disposition l'a perdu).</summary>
    private LayoutDocumentPane DocumentPane()
    {
        if (FindLayoutDocument(DashboardContentId)?.Parent is LayoutDocumentPane pane)
        {
            return pane;
        }

        LayoutDocumentPane? existing = DockManager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new LayoutDocumentPane();
        DockManager.Layout.RootPanel.Children.Add(created);
        return created;
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (CodeDocumentViewModel document in e.NewItems?.OfType<CodeDocumentViewModel>() ?? [])
        {
            var view = new CodeDocumentView { DataContext = document };
            var layoutDocument = new LayoutDocument
            {
                ContentId = document.Id,
                Title = document.Title,
                ToolTip = document.DisplayPath,
                Content = view,
            };
            document.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(CodeDocumentViewModel.Title))
                {
                    layoutDocument.Title = document.Title;
                }
            };
            DocumentPane().Children.Add(layoutDocument);
        }

        foreach (CodeDocumentViewModel document in e.OldItems?.OfType<CodeDocumentViewModel>() ?? [])
        {
            if (!_closing.Contains(document) && FindLayoutDocument(document.Id) is { } layoutDocument)
            {
                layoutDocument.Close();
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (LayoutDocument stale in DockManager.Layout.Descendents().OfType<LayoutDocument>()
                         .Where(d => d.Content is CodeDocumentView).ToList())
            {
                stale.Close();
            }
        }
    }

    /// <summary>Fermeture d'un onglet par l'utilisateur : le ViewModel confirme s'il y a des modifications.</summary>
    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (e.Document.Content is not CodeDocumentView { DataContext: CodeDocumentViewModel document })
        {
            return;
        }

        _closing.Add(document);
        try
        {
            e.Cancel = !_viewModel.RequestCloseDocument(document);
        }
        finally
        {
            _closing.Remove(document);
        }
    }

    private void ActivateDocument(CodeDocumentViewModel? document)
    {
        LayoutDocument? target = FindLayoutDocument(document?.Id ?? DashboardContentId);
        if (target is null)
        {
            return;
        }

        target.IsSelected = true;
        target.IsActive = true;
        if (target.Content is CodeDocumentView view)
        {
            Dispatcher.BeginInvoke(view.FocusEditor);
        }
    }

    /// <summary>L'onglet actif détermine le fichier affiché dans Détails et la barre de statut.</summary>
    private void OnActiveContentChanged(object? sender, EventArgs e)
    {
        switch (DockManager.ActiveContent)
        {
            case CodeDocumentView { DataContext: CodeDocumentViewModel document }:
                _viewModel.ActiveDocument = document;
                break;
            case Panels.DashboardPanel:
                _viewModel.ActiveDocument = null;
                break;
        }
    }

    // ----- Panneaux -----

    private void ShowPanel(string contentId)
    {
        LayoutAnchorable? anchorable = DockManager.Layout.Descendents()
            .OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == contentId);
        if (anchorable is null)
        {
            return;
        }

        if (anchorable.IsHidden)
        {
            // Un panneau masqué retourne normalement à son ancien emplacement. Si celui-ci
            // était une fenêtre flottante, ou n'existe plus (fenêtre vide supprimée au
            // chargement), on le replace dans la fenêtre principale.
            bool canRestore = ((ILayoutPreviousContainer)anchorable).PreviousContainer is ILayoutElement previous
                              && previous.Root is not null
                              && previous.FindParent<LayoutFloatingWindow>() is null;
            if (canRestore)
            {
                anchorable.Show();
            }
            else
            {
                DockManager.Layout.Hidden.Remove(anchorable);
                DockInMainWindow(anchorable, contentId);
            }
        }

        anchorable.IsActive = true;
    }

    /// <summary>
    /// Ancre un panneau à sa place par défaut : à côté d'un panneau de son groupe s'il
    /// est visible, sinon dans un nouvel emplacement à gauche, à droite ou en bas.
    /// </summary>
    private void DockInMainWindow(LayoutAnchorable anchorable, string contentId)
    {
        LayoutRoot root = DockManager.Layout;
        string[] group = PanelGroup(contentId);

        LayoutAnchorablePane? siblingPane = root.Descendents()
            .OfType<LayoutAnchorable>()
            .Where(a => a != anchorable && group.Contains(a.ContentId) && !a.IsHidden && !a.IsFloating)
            .Select(a => a.Parent)
            .OfType<LayoutAnchorablePane>()
            .FirstOrDefault();
        if (siblingPane is not null)
        {
            siblingPane.Children.Add(anchorable);
            return;
        }

        var pane = new LayoutAnchorablePane(anchorable);
        LayoutPanel rootPanel = root.RootPanel;
        bool bottom = contentId is MainViewModel.OutputPanelId or MainViewModel.DiagnosticsPanelId;
        if (bottom && root.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault()?.Parent is LayoutPanel { Orientation: Orientation.Vertical } documentColumn)
        {
            pane.DockHeight = new GridLength(220);
            documentColumn.Children.Add(pane);
        }
        else if (rootPanel.Orientation == Orientation.Horizontal)
        {
            pane.DockWidth = new GridLength(contentId == MainViewModel.InspectorPanelId ? 380 : 270);
            if (contentId == MainViewModel.InspectorPanelId || bottom)
            {
                rootPanel.Children.Add(pane);
            }
            else
            {
                rootPanel.Children.Insert(0, pane);
            }
        }
        else
        {
            rootPanel.Children.Add(pane);
        }
    }

    /// <summary>Panneaux qui partagent le même emplacement dans la disposition par défaut.</summary>
    private static string[] PanelGroup(string contentId) => contentId switch
    {
        MainViewModel.InspectorPanelId => [MainViewModel.InspectorPanelId],
        MainViewModel.OutputPanelId or MainViewModel.DiagnosticsPanelId => [MainViewModel.OutputPanelId, MainViewModel.DiagnosticsPanelId],
        _ => [MainViewModel.ExplorerPanelId],
    };

    // ----- Thème, clavier, fermeture -----

    private void ApplyTheme()
    {
        DockManager.Theme = _themes.IsDark ? new Vs2013DarkTheme() : new Vs2013LightTheme();
        CodeHighlighting.IsDark = _themes.IsDark;
    }

    private void OnAppearanceChanged(object? sender, EventArgs e) => ApplyTheme();

    /// <summary>
    /// Ctrl+Z et Ctrl+Y agissent sur l'historique du projet, même quand un champ de
    /// saisie a le focus (chaque frappe y est déjà enregistrée) ; dans un onglet de code,
    /// ils restent à l'éditeur de texte, qui a son propre historique.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) is false || Keyboard.FocusedElement is TextArea)
        {
            return;
        }

        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        ICommand? command = e.Key switch
        {
            Key.Z when !shift => _viewModel.UndoCommand,
            Key.Y => _viewModel.RedoCommand,
            Key.Z when shift => _viewModel.RedoCommand,
            _ => null,
        };

        if (command is not null)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            e.Handled = true;
        }
    }

    private void OnShowPanelClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string contentId })
        {
            ShowPanel(contentId);
        }
    }

    /// <summary>La disposition d'origine n'a pas d'onglets de code : ils sont d'abord fermés.</summary>
    private void OnResetLayoutClick(object sender, RoutedEventArgs e)
    {
        foreach (CodeDocumentViewModel document in _viewModel.Documents.ToList())
        {
            if (!_viewModel.RequestCloseDocument(document))
            {
                return;
            }
        }

        _layout.Reset(DockManager);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmShutdown())
        {
            e.Cancel = true;
            return;
        }

        _layout.Save(DockManager);
        _themes.AppearanceChanged -= OnAppearanceChanged;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
