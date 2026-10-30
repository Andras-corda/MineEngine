using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MineEngine.Editor.Views;

/// <summary>
/// Opens the context menu of a button under it when the button is clicked (the "+" of a method of
/// an element, V0.7.13: Java or graph). Set with views:MenuOnClick.IsEnabled="True".
/// </summary>
public static class MenuOnClick
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(MenuOnClick), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button)
        {
            return;
        }

        button.Click -= OnClick;
        if ((bool)e.NewValue)
        {
            button.Click += OnClick;
        }
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.DataContext = button.DataContext;
            menu.IsOpen = true;
        }
    }
}
