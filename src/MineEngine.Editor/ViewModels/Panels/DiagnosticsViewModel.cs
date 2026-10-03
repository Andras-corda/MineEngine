using System.Collections.ObjectModel;
using System.Windows.Input;
using MineEngine.Core.Diagnostics;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Panels;

/// <summary>Une ligne du panneau Diagnostics.</summary>
public sealed class DiagnosticItemViewModel
{
    public DiagnosticItemViewModel(Diagnostic diagnostic, string origin)
    {
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
        Origin = origin;
    }

    public Diagnostic Diagnostic { get; }

    public DiagnosticSeverity Severity => Diagnostic.Severity;

    public string SeverityLabel => Severity switch
    {
        DiagnosticSeverity.Error => "Erreur",
        DiagnosticSeverity.Warning => "Avertissement",
        _ => "Info",
    };

    public string Message => Diagnostic.Message;

    public string Source => Diagnostic.Source ?? string.Empty;

    /// <summary>"Validation" ou "Build".</summary>
    public string Origin { get; }

    public bool CanNavigate => Diagnostic.AssetId is not null;
}

/// <summary>
/// Panneau Diagnostics : problèmes de la dernière validation (mise à jour à chaque
/// modification) et du dernier build. Un double-clic mène à l'asset concerné.
/// </summary>
public sealed class DiagnosticsViewModel : ObservableObject
{
    private const string ValidationOrigin = "Validation";
    private const string BuildOrigin = "Build";

    private readonly Action<Guid> _navigateToAsset;
    private DiagnosticItemViewModel? _selectedItem;

    public DiagnosticsViewModel(Action<Guid> navigateToAsset)
    {
        _navigateToAsset = navigateToAsset ?? throw new ArgumentNullException(nameof(navigateToAsset));
        NavigateCommand = new RelayCommand(Navigate, () => _selectedItem?.CanNavigate == true);
    }

    public ObservableCollection<DiagnosticItemViewModel> Items { get; } = [];

    public DiagnosticItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public ICommand NavigateCommand { get; }

    public int ErrorCount => Items.Count(i => i.Severity == DiagnosticSeverity.Error);

    public int WarningCount => Items.Count(i => i.Severity == DiagnosticSeverity.Warning);

    /// <summary>Résumé court pour la barre de statut ("0 erreur · 2 avertissements").</summary>
    public string ProblemsLabel
    {
        get
        {
            int errors = ErrorCount;
            int warnings = WarningCount;
            return $"{errors} erreur{(errors > 1 ? "s" : string.Empty)} · {warnings} avertissement{(warnings > 1 ? "s" : string.Empty)}";
        }
    }

    public string Summary => Items.Count == 0
        ? "Aucun problème"
        : $"{ErrorCount} erreur(s), {WarningCount} avertissement(s)";

    /// <summary>Remplace les résultats de validation (les résultats du dernier build sont conservés).</summary>
    public void SetValidationResults(IEnumerable<Diagnostic> diagnostics) => Replace(ValidationOrigin, diagnostics);

    /// <summary>Remplace les résultats du dernier build.</summary>
    public void SetBuildResults(IEnumerable<Diagnostic> diagnostics) => Replace(BuildOrigin, diagnostics);

    public void Clear()
    {
        Items.Clear();
        RaiseCounts();
    }

    private void Replace(string origin, IEnumerable<Diagnostic> diagnostics)
    {
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].Origin == origin)
            {
                Items.RemoveAt(i);
            }
        }

        foreach (Diagnostic diagnostic in diagnostics.OrderByDescending(d => d.Severity))
        {
            Items.Add(new DiagnosticItemViewModel(diagnostic, origin));
        }

        RaiseCounts();
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ProblemsLabel));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
    }

    private void Navigate()
    {
        if (_selectedItem?.Diagnostic.AssetId is { } id)
        {
            _navigateToAsset(id);
        }
    }
}
