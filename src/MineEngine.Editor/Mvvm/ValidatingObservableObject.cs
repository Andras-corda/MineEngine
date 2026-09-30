using System.Collections;
using System.ComponentModel;

namespace MineEngine.Editor.Mvvm;

/// <summary>
/// ViewModel capable de signaler des erreurs de saisie par propriété ;
/// WPF les affiche automatiquement autour des champs concernés.
/// </summary>
public abstract class ValidatingObservableObject : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, string[]> _errors = [];

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out string[]? errors) ? errors : [];

    protected void SetError(string propertyName, string error)
    {
        _errors[propertyName] = [error];
        RaiseErrorsChanged(propertyName);
    }

    protected void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            RaiseErrorsChanged(propertyName);
        }
    }

    private void RaiseErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
    }
}
