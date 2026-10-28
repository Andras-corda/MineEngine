using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MineEngine.Editor.ViewModels.Sparkle;

namespace MineEngine.Editor.Views.Sparkle;

/// <summary>
/// Detached preview of a Sparkle system: a window of its own (it can go to another screen) with
/// the square preview and the number of particles. It plays the same timeline as the tab; closing
/// it docks the preview again next to the canvas.
/// </summary>
public sealed class SparklePreviewWindow : Window
{
    private readonly SparkleDocumentViewModel _document;

    public SparklePreviewWindow(SparkleDocumentViewModel document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        DataContext = document;
        Width = 520;
        Height = 560;
        MinWidth = 220;
        MinHeight = 240;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "HubBackgroundBrush");
        UpdateTitle();
        document.PropertyChanged += OnDocumentPropertyChanged;
        Closed += (_, _) => document.PropertyChanged -= OnDocumentPropertyChanged;

        var count = new TextBlock { Margin = new Thickness(0, 6, 0, 0), FontSize = 12 };
        count.SetResourceReference(TextBlock.ForegroundProperty, "HintBrush");
        count.SetBinding(TextBlock.TextProperty, new Binding($"{nameof(SparkleDocumentViewModel.Player)}.{nameof(SparklePlayer.ParticleText)}"));
        var time = new TextBlock { Margin = new Thickness(0, 6, 0, 0), FontSize = 12, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        time.SetResourceReference(TextBlock.ForegroundProperty, "StrongTextBrush");
        time.SetBinding(TextBlock.TextProperty, new Binding($"{nameof(SparkleDocumentViewModel.Player)}.{nameof(SparklePlayer.TimeText)}"));
        DockPanel.SetDock(time, Dock.Right);
        var status = new DockPanel { Children = { time, count } };
        DockPanel.SetDock(status, Dock.Bottom);

        Content = new DockPanel
        {
            Margin = new Thickness(10),
            Children = { status, new SquarePanel { Child = SparkleView.CreatePreview() } },
        };
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SparkleDocumentViewModel.FileName):
                UpdateTitle();
                break;
            case nameof(SparkleDocumentViewModel.IsPreviewDetached) when !_document.IsPreviewDetached:
                Close();
                break;
        }
    }

    private void UpdateTitle() => Title = Tr.F("Preview – {0}", _document.FileName);
}
