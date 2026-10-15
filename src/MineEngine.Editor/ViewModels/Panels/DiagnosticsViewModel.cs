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
        DiagnosticSeverity.Error => Tr.T("Erreur"),
        DiagnosticSeverity.Warning => Tr.T("Avertissement"),
        _ => Tr.T("Info"),
    };

    public string Message => Diagnostic.Message;

    public string Source => Diagnostic.Source ?? string.Empty;

    /// <summary>"Validation" ou "Build".</summary>
    public string Origin { get; }

    /// <summary>Origine affichée (Validation, Build) dans la langue de l'interface.</summary>
    public string OriginLabel => Tr.T(Origin);

    public bool CanNavigate => Diagnostic.AssetId is not null || Diagnostic.FilePath is not null;
}

/// <summary>
/// Panneau Diagnostics : problèmes de la dernière validation (mise à jour à chaque
/// modification) et du dernier build. Un double-clic mène à l'élément concerné, ou au fichier
/// Java à la ligne de l'erreur.
/// </summary>
public sealed class DiagnosticsViewModel : ObservableObject
{
    private const string ValidationOrigin = "Validation";
    private const string BuildOrigin = "Build";

    private readonly Action<Diagnostic> _navigate;
    private DiagnosticItemViewModel? _selectedItem;

    public DiagnosticsViewModel(Action<Diagnostic> navigate)
    {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
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
            return (errors > 1 ? Tr.F("{0} erreurs", errors) : Tr.F("{0} erreur", errors))
                + " · "
                + (warnings > 1 ? Tr.F("{0} avertissements", warnings) : Tr.F("{0} avertissement", warnings));
        }
    }

    public string Summary => Items.Count == 0
        ? Tr.T("Aucun problème")
        : Tr.F("{0} erreur(s), {1} avertissement(s)", ErrorCount, WarningCount);

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
        if (_selectedItem is { CanNavigate: true } item)
        {
            _navigate(item.Diagnostic);
        }
    }
}
