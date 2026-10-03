using System.Windows;
using System.Windows.Threading;
using MineEngine.Core.Logging;
using MineEngine.Editor;
using MineEngine.Editor.Views;

namespace MineEngine.App;

public partial class App : Application
{
    private CompositionRoot? _compositionRoot;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _compositionRoot = new CompositionRoot(this);
        var window = new MainWindow(_compositionRoot.MainViewModel, _compositionRoot.Preferences.Themes, _compositionRoot.DockLayout);
        MainWindow = window;
        window.Show();

        _compositionRoot.Log.Info($"Mine Engine {EditorInfo.VersionLabel} prêt. Créez ou ouvrez un projet pour commencer.");
        _compositionRoot.MdkLibrary.Refresh(_compositionRoot.Log);
        if (_compositionRoot.MdkLibrary.Installed.Count == 0)
        {
            _compositionRoot.Log.Warning(
                "Aucun MDK installé. Ouvrez Outils > Gestionnaire de MDK pour en télécharger un avant de créer un projet.");
        }
        else
        {
            _compositionRoot.Log.Info(
                "MDK installés : " + string.Join(", ", _compositionRoot.MdkLibrary.Installed.Select(m => m.DisplayName)));
        }
        if (e.Args.Length > 0)
        {
            _compositionRoot.MainViewModel.OpenProjectAtStartup(e.Args[0]);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _compositionRoot?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _compositionRoot?.Log.Error("Erreur inattendue : " + e.Exception);
        MessageBox.Show(
            "Une erreur inattendue s'est produite :\n\n" + e.Exception.Message + "\n\nLe détail est affiché dans la console Output.",
            "Mine Engine",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
