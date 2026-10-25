using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.Common;

/// <summary>
/// SH49 reduced (F5.15, decision of 25/10): the inner tab chosen in a sheet stays when the tab of the shell comes back on screen and the
/// view is rebuilt from its ViewModel. The index lives in memory with the DataContext of the TabControl (the ViewModel of the sheet or of
/// its panel), under the key of the TabControl; it is not written in the document (rule T6). Only the TabControls of the sheet views
/// whose selection is not already bound to the ViewModel have a key.
/// </summary>
public static class TabMemory
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached("Key", typeof(string), typeof(TabMemory),
        new PropertyMetadata(null, OnKeyChanged));

    public static string? GetKey(DependencyObject target) => (string?)target.GetValue(KeyProperty);

    public static void SetKey(DependencyObject target, string? value) => target.SetValue(KeyProperty, value);

    // The chosen indexes per owner, released with the owner.
    private static readonly ConditionalWeakTable<object, Dictionary<string, int>> Chosen = new();

    private static void OnKeyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not TabControl tabs || e.OldValue is not null) return;
        tabs.Loaded += Restore;
        tabs.SelectionChanged += Remember;
    }

    private static void Restore(object sender, RoutedEventArgs e)
    {
        var tabs = (TabControl)sender;
        if (tabs.DataContext is { } owner && GetKey(tabs) is { } key && Chosen.TryGetValue(owner, out var chosen)
            && chosen.TryGetValue(key, out int index) && index >= 0 && index < tabs.Items.Count)
            tabs.SelectedIndex = index;
    }

    // Only the choices of a loaded view count: the selection made while the view is built is the default, not the user's.
    private static void Remember(object sender, SelectionChangedEventArgs e)
    {
        var tabs = (TabControl)sender;
        if (!ReferenceEquals(e.OriginalSource, tabs) || !tabs.IsLoaded || tabs.DataContext is not { } owner || GetKey(tabs) is not { } key) return;
        Chosen.GetOrCreateValue(owner)[key] = tabs.SelectedIndex;
    }
}
