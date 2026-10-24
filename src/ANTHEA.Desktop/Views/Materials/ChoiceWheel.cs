using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace X.Desktop.Views.Materials;

/// <summary>
/// The mouse wheel over a closed choice scrolls the page instead of changing the choice, as Ui.Choice of the historic views
/// (the type of the steel): attached behaviour, presentation only.
/// </summary>
public static class ChoiceWheel
{
    public static readonly DependencyProperty ForwardWhenClosedProperty = DependencyProperty.RegisterAttached("ForwardWhenClosed", typeof(bool), typeof(ChoiceWheel),
        new PropertyMetadata(false, (target, e) =>
        {
            if (target is not ComboBox choice) return;
            choice.PreviewMouseWheel -= OnWheel;
            if (e.NewValue is true) choice.PreviewMouseWheel += OnWheel;
        }));

    public static bool GetForwardWhenClosed(DependencyObject target) => (bool)target.GetValue(ForwardWhenClosedProperty);

    public static void SetForwardWhenClosed(DependencyObject target, bool value) => target.SetValue(ForwardWhenClosedProperty, value);

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox choice || choice.IsDropDownOpen) return;
        ChainedScrollViewer.ForwardWheel(choice, e);
        e.Handled = true;
    }
}
