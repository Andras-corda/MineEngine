using System.Windows.Input;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Creation;

/// <summary>What the creation window asks to make: the choice, the name and the template (null without one).</summary>
public sealed record NewElementRequest(NewElementEntry Entry, string Name, NewElementTemplate? Template);

/// <summary>
/// Creation window (V0.7.14): everything that can be created, by category, with the name typed
/// directly and the templates of the choice. <c>create</c> makes the element and returns why it
/// could not (the window then stays open with the message), or null.
/// </summary>
public sealed class NewElementViewModel : ObservableObject
{
    private readonly Func<NewElementRequest, string?> _create;
    private NewElementCategory _selectedCategory;
    private NewElementEntry? _selectedEntry;
    private NewElementTemplate? _selectedTemplate;
    private string _name = string.Empty;
    private string? _error;

    public NewElementViewModel(Func<NewElementRequest, string?> create, NewElementEntry? initial = null, string? javaPackage = null)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
        JavaPackage = javaPackage ?? string.Empty;
        _selectedCategory = Categories.FirstOrDefault(c => initial is not null && c.Entries.Contains(initial)) ?? Categories[0];
        SelectedEntry = initial is { IsAvailable: true } ? initial : _selectedCategory.Entries.FirstOrDefault(e => e.IsAvailable);
        ConfirmCommand = new RelayCommand(Confirm, () => SelectedEntry is { IsAvailable: true } && Name.Trim().Length > 0);
    }

    public event EventHandler<bool>? CloseRequested;

    public IReadOnlyList<NewElementCategory> Categories => NewElementCatalog.Categories;

    public string JavaPackage { get; }

    public NewElementCategory SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is not null && SetProperty(ref _selectedCategory, value))
            {
                SelectedEntry = value.Entries.FirstOrDefault(e => e.IsAvailable);
            }
        }
    }

    /// <summary>The chosen element; a "coming later" one can be looked at, not created.</summary>
    public NewElementEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            NewElementEntry? previous = _selectedEntry;
            if (!SetProperty(ref _selectedEntry, value))
            {
                return;
            }

            // The name follows the choice until the user types one.
            if (value is { IsAvailable: true } && (Name.Length == 0 || Name == previous?.DefaultName))
            {
                Name = value.DefaultName;
            }

            SelectedTemplate = value?.Templates.FirstOrDefault();
            Error = null;
            OnPropertyChanged(nameof(Templates));
            OnPropertyChanged(nameof(HasTemplates));
            OnPropertyChanged(nameof(NameHint));
            OnPropertyChanged(nameof(IsComing));
        }
    }

    public IReadOnlyList<NewElementTemplate> Templates => SelectedEntry?.Templates ?? [];

    public bool HasTemplates => Templates.Count > 0;

    public bool IsComing => SelectedEntry is { IsAvailable: false };

    public NewElementTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                Error = null;
            }
        }
    }

    /// <summary>What the name becomes: a class of a package, a folder, or the name shown in the game.</summary>
    public string NameHint => SelectedEntry?.Action switch
    {
        NewElementAction.JavaClass => Tr.F("Class name (package {0})", JavaPackage.Length == 0 ? Tr.T("default") : JavaPackage),
        NewElementAction.Folder => Tr.T("Folder name"),
        NewElementAction.Coming => Tr.T("This element is planned for a later version."),
        _ => Tr.T("Name (its identifier is made from it, you can change both later)"),
    };

    /// <summary>Why the last creation failed, shown under the name.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _error is not null;

    public ICommand ConfirmCommand { get; }

    private void Confirm()
    {
        if (SelectedEntry is not { IsAvailable: true } entry)
        {
            return;
        }

        Error = _create(new NewElementRequest(entry, Name.Trim(), SelectedTemplate));
        if (Error is null)
        {
            CloseRequested?.Invoke(this, true);
        }
    }
}
