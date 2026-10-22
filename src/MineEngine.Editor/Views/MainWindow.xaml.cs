using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using ICSharpCode.AvalonEdit.Editing;
using MineEngine.Editor.Services;
using MineEngine.Editor.ViewModels;
using MineEngine.Editor.ViewModels.Documents;
using MineEngine.Editor.ViewModels.Graphs;
using MineEngine.Editor.ViewModels.Puppeteer;
using MineEngine.Editor.Views.Documents;
using MineEngine.Editor.Views.Graphs;

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
    private readonly HashSet<DocumentViewModel> _closing = [];

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
                     ExplorerPanel, DashboardPanel, ResourcesPanel, GlobalsPanel, LanguagesPanel, OutputPanel, DiagnosticsPanel, InspectorPanel,
                 })
        {
            panel.DataContext = viewModel;
        }

        ApplyTheme();
        _themes.AppearanceChanged += OnAppearanceChanged;
        _viewModel.PanelRequested += (_, panelId) => ShowPanel(panelId);
        _viewModel.RestartRequested += (_, project) => Restart(project);
        _viewModel.Documents.CollectionChanged += OnDocumentsChanged;
        _viewModel.DocumentActivationRequested += (_, document) => ActivateDocument(document);
        _viewModel.ProjectTabRequested += (_, contentId) => ActivateTab(contentId);
        _viewModel.Shortcuts.PropertyChanged += (_, _) => ApplyShortcuts();
        ApplyShortcuts();
        DockManager.ActiveContentChanged += OnActiveContentChanged;

        // The side buttons of the mouse go back and forward, as in a web browser.
        PreviewMouseUp += (_, e) =>
        {
            ICommand? command = e.ChangedButton switch
            {
                MouseButton.XButton1 => _viewModel.GoBackCommand,
                MouseButton.XButton2 => _viewModel.GoForwardCommand,
                _ => null,
            };
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
                e.Handled = true;
            }
        };
        DockManager.DocumentClosing += OnDocumentClosing;
        InitializeTabMenu();
        Loaded += (_, _) =>
        {
            _layout.Initialize(DockManager);
            WatchLayout();
        };
        DockManager.LayoutChanged += (_, _) => WatchLayout();
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
        foreach (DocumentViewModel document in e.NewItems?.OfType<DocumentViewModel>() ?? [])
        {
            FrameworkElement view = document switch
            {
                GraphDocumentViewModel => new GraphDocumentView { DataContext = document },
                BehaviorTreeDocumentViewModel => new Puppeteer.BehaviorTreeView { DataContext = document },
                AssetEditorDocumentViewModel => new AssetEditorView { DataContext = document },
                _ => new CodeDocumentView { DataContext = document },
            };
            var layoutDocument = new LayoutDocument
            {
                ContentId = document.Id,
                Title = document.Title,
                ToolTip = document.DisplayPath,
                Content = view,
            };
            document.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DocumentViewModel.Title))
                {
                    layoutDocument.Title = document.Title;
                }
            };
            DocumentPane().Children.Add(layoutDocument);
        }

        foreach (DocumentViewModel document in e.OldItems?.OfType<DocumentViewModel>() ?? [])
        {
            if (!_closing.Contains(document) && FindLayoutDocument(document.Id) is { } layoutDocument)
            {
                layoutDocument.Close();
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (LayoutDocument stale in DockManager.Layout.Descendents().OfType<LayoutDocument>()
                         .Where(d => d.Content is CodeDocumentView or GraphDocumentView or AssetEditorView or Puppeteer.BehaviorTreeView).ToList())
            {
                stale.Close();
            }
        }
    }

    /// <summary>Fermeture d'un onglet par l'utilisateur : le ViewModel confirme s'il y a des modifications.</summary>
    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        if (e.Document.Content is not FrameworkElement { DataContext: DocumentViewModel document })
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

    private readonly List<InputBinding> _shortcutBindings = [];

    /// <summary>Raccourcis du catalogue, avec ceux choisis dans les paramètres (reconstruits à chaque changement).</summary>
    private void ApplyShortcuts()
    {
        foreach (InputBinding binding in _shortcutBindings)
        {
            InputBindings.Remove(binding);
        }

        _shortcutBindings.Clear();
        foreach (ShortcutDefinition definition in ShortcutCatalog.All)
        {
            if (ShortcutCatalog.Parse(ShortcutCatalog.Effective(definition, _viewModel.Shortcuts.Overrides)) is { } gesture)
            {
                var binding = new KeyBinding(definition.Command(_viewModel), gesture);
                InputBindings.Add(binding);
                _shortcutBindings.Add(binding);
            }
        }
    }

    /// <summary>Affiche un onglet permanent du projet (Ressources, Langues).</summary>
    private void ActivateTab(string contentId)
    {
        if (FindLayoutDocument(contentId) is { } tab)
        {
            tab.IsSelected = true;
            tab.IsActive = true;
        }
    }

    private void ActivateDocument(DocumentViewModel? document)
    {
        LayoutDocument? target = FindLayoutDocument(document?.Id ?? DashboardContentId);
        if (target is null)
        {
            return;
        }

        target.IsSelected = true;
        target.IsActive = true;
        switch (target.Content)
        {
            case CodeDocumentView view:
                Dispatcher.BeginInvoke(view.FocusEditor);
                break;
            case GraphDocumentView graph:
                Dispatcher.BeginInvoke(graph.FocusCanvas);
                break;
            case Puppeteer.BehaviorTreeView tree:
                Dispatcher.BeginInvoke(tree.FocusCanvas);
                break;
        }
    }

    /// <summary>L'onglet actif détermine le fichier affiché dans Détails et la barre de statut.</summary>
    private void OnActiveContentChanged(object? sender, EventArgs e)
    {
        switch (DockManager.ActiveContent)
        {
            case FrameworkElement { DataContext: DocumentViewModel document }:
                _viewModel.ActiveDocument = document;
                _viewModel.ActiveWorkspaceView = null;
                break;
            case Panels.DashboardPanel or Panels.ResourcesPanel or Panels.GlobalsPanel or Panels.LanguagesPanel:
                _viewModel.ActiveDocument = null;
                _viewModel.ActiveWorkspaceView = DockManager.ActiveContent switch
                {
                    Panels.ResourcesPanel => MainViewModel.ResourcesTabId,
                    Panels.LanguagesPanel => MainViewModel.LanguagesTabId,
                    Panels.GlobalsPanel => MainViewModel.GlobalsTabId,
                    _ => MainViewModel.DashboardTabId,
                };
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
        DockManager.Theme = new MineEngineDockTheme(_themes.IsDark, key => (System.Windows.Media.Brush)FindResource(key));
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
        // Disposition, comme dans VS Code : Ctrl+B (Explorateur), Ctrl+J (panneau du bas), Ctrl+Alt+B (Détails).
        if (_viewModel.HasProject && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            string[]? panels = (key, alt) switch
            {
                (Key.B, false) => [MainViewModel.ExplorerPanelId],
                (Key.J, false) => BottomPanels,
                (Key.B, true) => [MainViewModel.InspectorPanelId],
                _ => null,
            };
            if (panels is not null)
            {
                TogglePanels(panels);
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) is false || Keyboard.FocusedElement is TextArea)
        {
            return;
        }

        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (e.Key == Key.F && !shift && _viewModel.HasProject)
        {
            Toolbar.FocusSearch();
            e.Handled = true;
            return;
        }

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

    // ----- Boutons de disposition (barre de titre) -----

    private static readonly string[] BottomPanels = [MainViewModel.OutputPanelId, MainViewModel.DiagnosticsPanelId];

    private static readonly string[] AllPanels =
        [MainViewModel.ExplorerPanelId, MainViewModel.NodesPanelId, MainViewModel.OutputPanelId, MainViewModel.DiagnosticsPanelId, MainViewModel.InspectorPanelId];

    private LayoutRoot? _watchedLayout;

    /// <summary>Suit les changements de la disposition (panneau fermé, déplacé, disposition rechargée) pour l'état des boutons.</summary>
    private void WatchLayout()
    {
        if (_watchedLayout is not null)
        {
            _watchedLayout.Updated -= OnLayoutUpdated;
        }

        _watchedLayout = DockManager.Layout;
        if (_watchedLayout is not null)
        {
            _watchedLayout.Updated += OnLayoutUpdated;
        }

        UpdateLayoutButtons();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Dispatcher.BeginInvoke(DispatcherPriority.Background, UpdateLayoutButtons);

    private LayoutAnchorable? FindPanel(string contentId) =>
        DockManager.Layout?.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == contentId);

    private bool IsPanelVisible(string contentId) => FindPanel(contentId) is { IsHidden: false };

    /// <summary>Masque les panneaux s'il y en a un d'affiché, sinon les affiche (à leur dernière place).</summary>
    private void TogglePanels(params string[] contentIds)
    {
        if (contentIds.Any(IsPanelVisible))
        {
            HidePanels(contentIds);
        }
        else
        {
            foreach (string id in contentIds.Reverse())
            {
                ShowPanel(id);
            }
        }

        UpdateLayoutButtons();
    }

    private void HidePanels(IEnumerable<string> contentIds)
    {
        foreach (string id in contentIds)
        {
            if (FindPanel(id) is { IsHidden: false } panel)
            {
                panel.Hide();
            }
        }
    }

    private void UpdateLayoutButtons()
    {
        bool explorer = IsPanelVisible(MainViewModel.ExplorerPanelId);
        bool bottom = BottomPanels.Any(IsPanelVisible);
        bool inspector = IsPanelVisible(MainViewModel.InspectorPanelId);
        ExplorerToggle.Tag = explorer;
        BottomToggle.Tag = bottom;
        InspectorToggle.Tag = inspector;
        ExplorerMenuItem.IsChecked = explorer;
        BottomMenuItem.IsChecked = bottom;
        InspectorMenuItem.IsChecked = inspector;
    }

    private void OnToggleExplorerClick(object sender, RoutedEventArgs e) => TogglePanels(MainViewModel.ExplorerPanelId);

    private void OnToggleBottomClick(object sender, RoutedEventArgs e) => TogglePanels(BottomPanels);

    private void OnToggleInspectorClick(object sender, RoutedEventArgs e) => TogglePanels(MainViewModel.InspectorPanelId);

    /// <summary>Mode concentration : seuls les onglets restent, comme le mode Zen de VS Code.</summary>
    private void OnFocusModeClick(object sender, RoutedEventArgs e)
    {
        HidePanels(AllPanels);
        UpdateLayoutButtons();
    }

    private void OnShowAllPanelsClick(object sender, RoutedEventArgs e)
    {
        foreach (string id in AllPanels.Where(id => !IsPanelVisible(id)))
        {
            ShowPanel(id);
        }

        UpdateLayoutButtons();
    }

    private void OnLayoutMenuClick(object sender, RoutedEventArgs e)
    {
        UpdateLayoutButtons();
        if (LayoutMenuButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = LayoutMenuButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
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
        foreach (DocumentViewModel document in _viewModel.Documents.ToList())
        {
            if (!_viewModel.RequestCloseDocument(document))
            {
                return;
            }
        }

        _layout.Reset(DockManager);
        WatchLayout();
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

    /// <summary>
    /// Ferme la fenêtre (le projet modifié peut encore être enregistré ou la fermeture annulée),
    /// puis relance Mine Engine sur le même projet.
    /// </summary>
    private void Restart(string? project)
    {
        Closed += (_, _) =>
        {
            if (Environment.ProcessPath is not { } executable)
            {
                return;
            }

            var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false };
            if (project is not null)
            {
                start.ArgumentList.Add(project);
            }

            System.Diagnostics.Process.Start(start);
        };

        Close();
    }
}
