using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using MineEngine.Core.Logging;

namespace MineEngine.Editor.Services;

/// <summary>
/// Enregistre la disposition des panneaux (taille, position, panneaux masqués ou
/// flottants) à la fermeture et la restaure au démarrage. La disposition d'origine
/// reste disponible pour "Réinitialiser la disposition".
/// </summary>
public sealed class DockLayoutService
{
    private readonly string _layoutFile;
    private readonly ILog _log;
    private string? _defaultLayout;

    /// <summary>
    /// Contenu de chaque panneau et onglet permanent au démarrage : un onglet absent d'une
    /// disposition enregistrée (nouvelle version) retrouve ainsi son contenu.
    /// </summary>
    private Dictionary<string, object> _initialContents = [];

    /// <summary>
    /// Titles of the panels and permanent tabs in the current language: a saved layout keeps the
    /// titles of the language it was saved in, they are replaced when it is applied.
    /// </summary>
    private Dictionary<string, string> _initialTitles = [];

    // Side panels (anchorables) of the original layout, as opposed to the fixed tabs (documents).
    private HashSet<string> _initialAnchorables = [];

    public DockLayoutService(string layoutFile, ILog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutFile);
        _layoutFile = Path.GetFullPath(layoutFile);
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Mémorise la disposition d'origine puis applique celle enregistrée, si elle existe.</summary>
    public void Initialize(DockingManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        _defaultLayout = Serialize(manager);
        _initialContents = Contents(manager);
        _initialTitles = manager.Layout.Descendents().OfType<LayoutContent>()
            .Where(c => c.ContentId is not null && c.Title is not null)
            .ToDictionary(c => c.ContentId!, c => c.Title);
        _initialAnchorables = [.. manager.Layout.Descendents().OfType<LayoutAnchorable>().Select(a => a.ContentId).OfType<string>()];

        if (!File.Exists(_layoutFile))
        {
            return;
        }

        try
        {
            Apply(manager, File.ReadAllText(_layoutFile));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.Xml.XmlException)
        {
            _log.Warning(Tr.T("Unreadable panel layout, default layout used: ") + exception.Message);
            Reset(manager);
        }
    }

    public void Save(DockingManager manager)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_layoutFile)!);
            File.WriteAllText(_layoutFile, Serialize(manager));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _log.Warning(Tr.T("Unable to save the panel layout: ") + exception.Message);
        }
    }

    public void Reset(DockingManager manager)
    {
        if (_defaultLayout is not null)
        {
            Apply(manager, _defaultLayout);
        }
    }

    private static string Serialize(DockingManager manager)
    {
        using var writer = new StringWriter();
        new XmlLayoutSerializer(manager).Serialize(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Applique une disposition en conservant le contenu actuel de chaque panneau
    /// (retrouvé par son ContentId). Si un panneau manque dans la disposition lue
    /// (nouvelle version de l'éditeur), la disposition par défaut est utilisée.
    /// </summary>
    private void Apply(DockingManager manager, string layout)
    {
        Dictionary<string, object> contents = Contents(manager);
        foreach ((string id, object content) in _initialContents)
        {
            contents.TryAdd(id, content);
        }

        var serializer = new XmlLayoutSerializer(manager);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            if (e.Model.ContentId is { } id && contents.TryGetValue(id, out object? content))
            {
                e.Content = content;
                if (_initialTitles.TryGetValue(id, out string? title))
                {
                    e.Model.Title = title;
                }
            }
            else
            {
                e.Cancel = true;
            }
        };

        using var reader = new StringReader(layout);
        serializer.Deserialize(reader);
        RemoveEmptyFloatingWindows(manager.Layout);

        HashSet<string> restored = manager.Layout.Descendents()
            .OfType<LayoutContent>()
            .Select(c => c.ContentId)
            .OfType<string>()
            .ToHashSet();
        string[] missing = [.. contents.Keys.Where(id => !restored.Contains(id))];
        if (missing.Length > 0 && !ReferenceEquals(layout, _defaultLayout) && !AddBesideExplorer(manager, missing, contents))
        {
            Apply(manager, _defaultLayout!);
        }
    }

    /// <summary>
    /// A panel added by a newer version (the Nodes window...) joins the pane of the Explorer, so
    /// that the layout arranged by the user is kept. False if one of them is not a side panel.
    /// </summary>
    private bool AddBesideExplorer(DockingManager manager, IReadOnlyList<string> missing, Dictionary<string, object> contents)
    {
        LayoutAnchorablePane? pane = manager.Layout.Descendents().OfType<LayoutAnchorable>().FirstOrDefault(a => a.ContentId == "explorer")?.Parent as LayoutAnchorablePane
                                     ?? manager.Layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault();
        if (pane is null || missing.Any(id => !_initialAnchorables.Contains(id)))
        {
            return false;
        }

        foreach (string id in missing)
        {
            pane.Children.Add(new LayoutAnchorable
            {
                ContentId = id,
                Title = _initialTitles.GetValueOrDefault(id),
                Content = contents[id],
                CanClose = false,
            });
        }

        return true;
    }

    private static Dictionary<string, object> Contents(DockingManager manager) =>
        manager.Layout.Descendents()
            .OfType<LayoutContent>()
            .Where(c => c.ContentId is not null)
            .ToDictionary(c => c.ContentId!, c => c.Content);

    /// <summary>
    /// Une fenêtre flottante vidée (ses panneaux masqués) reste dans la disposition
    /// enregistrée sans être affichée : on la retire pour que ses panneaux reviennent
    /// dans la fenêtre principale quand on les rouvre.
    /// </summary>
    private static void RemoveEmptyFloatingWindows(LayoutRoot root)
    {
        foreach (LayoutFloatingWindow window in root.FloatingWindows.ToList())
        {
            if (!window.Descendents().OfType<LayoutContent>().Any())
            {
                root.FloatingWindows.Remove(window);
            }
        }
    }
}
