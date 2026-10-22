using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace X.Desktop;

/// <summary>
/// Moving among the input boxes with the keyboard (Tab, Shift+Tab) selects the whole content of the box reached, in every window (user request
/// of 22/10/2026): typing then replaces the value. A click keeps the caret where it falls. Left out: multi-line boxes, and the editing box of a
/// DataGrid text column, which the DataGrid already selects when the edit starts from the keyboard and fills with the typed character.
/// </summary>
internal static class TabSelection
{
    /// <summary>Once per application (App.OnStartup), before the first window.</summary>
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(TextBox), UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnFocus));
    }

    private static void OnFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box || !ReferenceEquals(e.NewFocus, box) || box.AcceptsReturn) return;
        if (InputManager.Current.MostRecentInputDevice is not KeyboardDevice || InTextColumn(box)) return;
        // After the handlers of the box: the forms show the raw number when the box gets the focus, and that resets the selection.
        box.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (box.IsKeyboardFocused) box.SelectAll(); });
    }

    private static bool InTextColumn(DependencyObject element)
    {
        for (var node = VisualTreeHelper.GetParent(element); node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is DataGridCell cell) return cell.Column is DataGridBoundColumn;
        return false;
    }
}
