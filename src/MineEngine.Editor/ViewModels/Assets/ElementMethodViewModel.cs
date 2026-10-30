using System.Windows.Input;
using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>
/// A place of a method in the class of an element (V0.7.13): empty until the method is created in
/// Java or in a graph of the element, then opened (code or graph) or removed. Every change is one
/// step that can be undone; removing a nodal method also removes its graph.
/// </summary>
public sealed class ElementMethodViewModel : ObservableObject
{
    private readonly Asset _element;
    private readonly AssetEditingContext _context;
    private readonly Func<IReadOnlyList<ElementMethod>> _getMethods;
    private readonly Action<IReadOnlyList<ElementMethod>> _setMethods;

    public ElementMethodViewModel(
        Asset element, AssetEditingContext context, MethodSlot slot,
        Func<IReadOnlyList<ElementMethod>> getMethods, Action<IReadOnlyList<ElementMethod>> setMethods)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
        _getMethods = getMethods;
        _setMethods = setMethods;
        CreateJavaCommand = new RelayCommand(CreateJava, () => Method is null);
        CreateNodalCommand = new RelayCommand(CreateNodal, () => Method is null);
        OpenCommand = new RelayCommand(Open, () => Method is not null);
        RemoveCommand = new RelayCommand(Remove, () => Method is not null);
    }

    public MethodSlot Slot { get; }

    public ElementMethod? Method => _getMethods().FirstOrDefault(m => m.Slot == Slot.Key);

    public bool Exists => Method is not null;

    public bool IsMissing => Method is null;

    /// <summary>"No method", "Java" or "Graph", next to the name of the method.</summary>
    public string State => Method?.Language switch
    {
        MethodLanguage.Java => Tr.F("{0}: Java", Slot.JavaName),
        MethodLanguage.Nodal => Tr.F("{0}: graph", Slot.JavaName),
        _ => Tr.T("No method"),
    };

    public ICommand CreateJavaCommand { get; }

    public ICommand CreateNodalCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand RemoveCommand { get; }

    /// <summary>Called when the methods of the element or the project change (undo, graph removed).</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Method));
        OnPropertyChanged(nameof(Exists));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(State));
        CommandManager.InvalidateRequerySuggested();
    }

    private PropertyChangeCommand<IReadOnlyList<ElementMethod>> SetMethods(string description, IReadOnlyList<ElementMethod> methods) =>
        new(description, _element, "Methods", _getMethods, _setMethods, methods);

    private void CreateJava()
    {
        _context.History.Execute(SetMethods(Tr.F("Create the method {0}", Slot.JavaName),
            [.. _getMethods(), ElementMethod.Java(Slot.Key, Slot.DefaultJavaBody)]));
        _context.History.Seal();
        _context.Navigation.OpenMethod(_element, Slot.Key);
    }

    private void CreateNodal()
    {
        ResourceId id = _context.Project.Assets.CreateUniqueResourceId($"{_element.ResourceId.Value}_{Slot.JavaName.ToLowerInvariant()}", AssetType.Graph);
        GraphAsset graph = Slot.CreateGraph(id, _element.Id);
        graph.Folder = _element.Folder;
        string description = Tr.F("Create the method {0}", Slot.JavaName);
        _context.History.Execute(new CompositeCommand(description,
        [
            new AddAssetCommand(_context.Project.Assets, graph, Tr.T("Graph")),
            SetMethods(description, [.. _getMethods(), ElementMethod.Nodal(Slot.Key, graph.Id)]),
        ]));
        _context.History.Seal();
        _context.Navigation.Open(graph);
    }

    private void Open()
    {
        if (Method is not { } method)
        {
            return;
        }

        if (method.Language == MethodLanguage.Java)
        {
            _context.Navigation.OpenMethod(_element, Slot.Key);
        }
        else if (method.GraphId is { } graphId && _context.Project.Assets.Find(graphId) is { } graph)
        {
            _context.Navigation.Open(graph);
        }
    }

    private void Remove()
    {
        if (Method is not { } method)
        {
            return;
        }

        string description = Tr.F("Remove the method {0}", Slot.JavaName);
        var commands = new List<IUndoableCommand> { SetMethods(description, [.. _getMethods().Where(m => m.Slot != Slot.Key)]) };
        if (method.GraphId is { } graphId && _context.Project.Assets.Find(graphId) is { } graph)
        {
            commands.Add(new RemoveAssetCommand(_context.Project.Assets, graph, Tr.T("Graph")));
        }

        _context.History.Execute(new CompositeCommand(description, commands));
        _context.History.Seal();
    }
}
