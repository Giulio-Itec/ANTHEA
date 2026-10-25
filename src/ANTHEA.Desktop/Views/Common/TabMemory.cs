using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Mvvm;

namespace X.Desktop.Views.Common;

/// <summary>
/// SH49 reduced (F5.15, decision of 24/10): the inner tab chosen in a sheet stays when the tab of the shell comes back on screen and the
/// view is rebuilt from its ViewModel. The index lives with the nearest ViewModel that keeps the tabs of its views
/// (<see cref="IViewTabMemory"/>: the sheet, or a panel of the RC section), under the key of the TabControl; it is not written in the
/// document (rule T6). Only the TabControls of the sheet views whose selection is not already bound to the ViewModel have a key.
/// </summary>
public static class TabMemory
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(string), typeof(TabMemory),
        new PropertyMetadata(null, OnKeyChanged));

    public static string? GetKey(DependencyObject target) => (string?)target.GetValue(KeyProperty);

    public static void SetKey(DependencyObject target, string? value) => target.SetValue(KeyProperty, value);

    private static void OnKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not TabControl tabs || e.OldValue is not null) return;
        tabs.Loaded += Restore;
        tabs.SelectionChanged += Remember;
    }

    private static void Restore(object sender, RoutedEventArgs e)
    {
        var tabs = (TabControl)sender;
        if (GetKey(tabs) is { } key && Memory(tabs) is { } chosen && chosen.TryGetValue(key, out int index) && index >= 0 && index < tabs.Items.Count)
            tabs.SelectedIndex = index;
    }

    // Only the choices of a loaded view count: the selection made while the view is built is the default, not the user's.
    private static void Remember(object sender, SelectionChangedEventArgs e)
    {
        var tabs = (TabControl)sender;
        if (!ReferenceEquals(e.OriginalSource, tabs) || !tabs.IsLoaded || GetKey(tabs) is not { } key || Memory(tabs) is not { } chosen) return;
        chosen[key] = tabs.SelectedIndex;
    }

    private static IDictionary<string, int>? Memory(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is FrameworkElement { DataContext: IViewTabMemory owner }) return owner.SelectedTabs;
        return null;
    }
}
