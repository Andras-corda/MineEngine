using System.Windows;
using System.Windows.Threading;
using MineEngine.Core.Logging;
using MineEngine.Editor;
using MineEngine.Editor.Themes;
using MineEngine.Editor.Views;

namespace MineEngine.App;

public partial class App : Application
{
    private CompositionRoot? _compositionRoot;

    /// <summary>
    /// Initializes the application, sets up logging, applies editor fonts, and opens the main window.
    /// It also handles unhandled exceptions and manages the startup process, including opening a project if specified in the command line arguments or reopening the last project if no arguments are provided.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        EditorFonts.Apply(this); // Fonts de l'éditeur 
        _compositionRoot = new CompositionRoot(this); // Relie les dépendances et initialise les services de l'application
        
        // Affiche la fenêtre principale de l'application avec le ViewModel principal
        // (thèmes et la disposition du dock)
        var window = new MainWindow(
            _compositionRoot.MainViewModel, 
            _compositionRoot.Preferences.Themes, 
            _compositionRoot.DockLayout
            );
        MainWindow = window;
        window.Show();

        // Vérifie les MDK installés et affiche un message d'avertissement si aucun n'est trouvé
        _compositionRoot.Log.Info(
            Tr.F("Mine Engine {0} is ready. Create or open a project to get started.", 
            EditorInfo.VersionLabel)
            );

        _compositionRoot.MdkLibrary.Refresh(_compositionRoot.Log);

        // Vérifie si des MDK sont installés et affiche un message approprié
        if (_compositionRoot.MdkLibrary.Installed.Count == 0)
        {
            _compositionRoot.Log.Warning(
                Tr.T("No MDK is installed. Open Tools > MDK Manager to download one before creating a project."));
        }
        else
        {
            _compositionRoot.Log.Info(
                Tr.T("Installed MDKs: ") + string.Join(", ", _compositionRoot.MdkLibrary.Installed.Select(m => m.DisplayName)));
        }
        if (e.Args.Length > 0)
        {
            _compositionRoot.MainViewModel.OpenProjectAtStartup(e.Args[0]);
        }
        else
        {
            _compositionRoot.MainViewModel.ReopenLastProjectAtStartup();
        }
    }

    /// <summary>
    /// Cleans up resources and disposes of the composition root when the application exits.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnExit(ExitEventArgs e)
    {
        _compositionRoot?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Handles unhandled exceptions that occur in the application, logs the error, and displays a message box to inform the user about the unexpected error.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _compositionRoot?.Log.Error(Tr.T("Unexpected error : ") + e.Exception);
        MessageBox.Show(
            Tr.T("An unexpected error has occurred : \n\n") + e.Exception.Message + Tr.T("\n\nThe details are displayed in the Output console."),
            "Mine Engine",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
