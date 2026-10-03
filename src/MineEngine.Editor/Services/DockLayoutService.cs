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
            _log.Warning("Disposition des panneaux illisible, disposition par défaut utilisée : " + exception.Message);
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
            _log.Warning("Impossible d'enregistrer la disposition des panneaux : " + exception.Message);
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
        Dictionary<string, object> contents = manager.Layout.Descendents()
            .OfType<LayoutContent>()
            .Where(c => c.ContentId is not null)
            .ToDictionary(c => c.ContentId!, c => c.Content);

        var serializer = new XmlLayoutSerializer(manager);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            if (e.Model.ContentId is { } id && contents.TryGetValue(id, out object? content))
            {
                e.Content = content;
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
        if (!contents.Keys.All(restored.Contains) && !ReferenceEquals(layout, _defaultLayout))
        {
            Apply(manager, _defaultLayout!);
        }
    }

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
