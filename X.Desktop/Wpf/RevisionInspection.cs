using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace X.Desktop;

// Protect inputs, not the whole sheet: tabs, scrolling, selection and result views remain usable.
internal static class RevisionInspection
{
    private static readonly DependencyProperty AllowedProperty = DependencyProperty.RegisterAttached("Allowed", typeof(bool), typeof(RevisionInspection), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    internal static T Allow<T>(T view) where T : DependencyObject { view.SetValue(AllowedProperty, true); return view; }
    private static bool Allowed(DependencyObject view) => (bool)view.GetValue(AllowedProperty);
    private static DependencyObject? Parent(DependencyObject view) => view is Visual ? VisualTreeHelper.GetParent(view) : LogicalTreeHelper.GetParent(view);

    internal static Action Protect(FrameworkElement root)
    {
        void Apply(DependencyObject node, HashSet<DependencyObject> seen, bool logical)
        {
            if (!seen.Add(node)) return;
            if (!Allowed(node))
            {
                if (node is TextBox { IsReadOnly: false } text) text.IsReadOnly = true;
                if (node is DataGrid grid)
                {
                    if (!grid.IsReadOnly) grid.IsReadOnly = true;
                    if (grid.CanUserAddRows) grid.CanUserAddRows = false;
                    if (grid.CanUserDeleteRows) grid.CanUserDeleteRows = false;
                }
                if (node is ComboBox or CheckBox or RadioButton or Button or Slider && ((UIElement)node).IsEnabled) ((UIElement)node).IsEnabled = false;
                if (node is FrameworkElement { ContextMenu: not null } element) element.ContextMenu = null;
            }
            // Do not treat a control's template buttons as document editing commands.
            if (node is TextBox or ComboBox or ButtonBase or Slider) return;
            if (logical) foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) Apply(child, seen, true);
            if (node is Visual)
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Apply(VisualTreeHelper.GetChild(node, i), seen, logical);
        }
        void Refresh(object? sender, EventArgs e) => Apply(root, [], false);
        Apply(root, [], true);
        // Tabs and calculated results materialize controls after the editor is constructed.
        root.LayoutUpdated += Refresh;
        // Block custom paste/delete handlers as well as direct control edits.
        root.PreviewKeyDown += (_, e) =>
        {
            if (e.OriginalSource is not DependencyObject source || Allowed(source)) return;
            if (source is TextBox text) text.IsReadOnly = true;
            if (e.Key is Key.Delete or Key.Back or Key.F2 ||
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.V or Key.X or Key.Z or Key.Y ||
                Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && e.Key == Key.Insert) e.Handled = true;
        };
        root.PreviewTextInput += (_, e) => { if (e.OriginalSource is DependencyObject source && !Allowed(source)) e.Handled = true; };
        CommandManager.AddPreviewExecutedHandler(root, (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && !Allowed(source) &&
                (e.Command == ApplicationCommands.Paste || e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Delete ||
                 e.Command == ApplicationCommands.Undo || e.Command == ApplicationCommands.Redo)) e.Handled = true;
        });
        root.PreviewMouseDown += (_, e) =>
        {
            for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, root); node = Parent(node))
                if (!Allowed(node) && node is ComboBox or CheckBox or RadioButton or Button or Slider) { e.Handled = true; return; }
        };
        return () => root.LayoutUpdated -= Refresh;
    }
}
