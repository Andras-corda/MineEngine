using System.Windows.Input;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels;

/// <summary>Une commande dans l'onglet Raccourcis des paramètres : son raccourci, modifiable.</summary>
public sealed class ShortcutRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private string _gesture;
    private string? _conflict;

    public ShortcutRowViewModel(ShortcutDefinition definition, string gesture, Action changed)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _gesture = gesture ?? string.Empty;
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        ResetCommand = new RelayCommand(() => Gesture = Definition.DefaultGesture, () => !IsDefault);
        ClearCommand = new RelayCommand(() => Gesture = string.Empty, () => _gesture.Length > 0);
    }

    public ShortcutDefinition Definition { get; }

    public string Category => Definition.Category;

    public string Label => Definition.Label;

    /// <summary>Raccourci au format invariant ("Ctrl+Shift+B") ; vide : aucun.</summary>
    public string Gesture
    {
        get => _gesture;
        set
        {
            if (SetProperty(ref _gesture, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DisplayGesture));
                OnPropertyChanged(nameof(IsDefault));
                _changed();
            }
        }
    }

    public string DisplayGesture => _gesture.Length == 0 ? Tr.T("None") : ShortcutCatalog.Display(_gesture);

    public bool IsDefault => _gesture == Definition.DefaultGesture;

    /// <summary>Autre commande qui a le même raccourci, ou null.</summary>
    public string? Conflict
    {
        get => _conflict;
        set
        {
            if (SetProperty(ref _conflict, value))
            {
                OnPropertyChanged(nameof(HasConflict));
            }
        }
    }

    public bool HasConflict => _conflict is not null;

    public ICommand ResetCommand { get; }

    public ICommand ClearCommand { get; }
}
