using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Anthea.Presentation.Shell.Projects;

namespace X.Desktop.Views.Shell;

/// <summary>
/// The mouse on the tree of the project in the new shell (F5.13 c, night of 22-23/10; ProjectHierarchy and ProjectSectionReorder of MainWindow), as
/// attached properties set by the templates and styles. The rules are those of <see cref="ProjectTreeViewModel"/> (PreviewDrop, Drop); this class
/// only moves the mouse into them:
/// <list type="bullet">
/// <item><c>DragRow</c> on the row of a node, <c>DragName</c> on its name (<see cref="ProjectNodeViewModel.CanDrag"/>): a sheet is dragged by its row,
/// a project or section by its name, as MainWindow; <c>DragRow</c> also on a card of «Schede da aggiungere» (a module, copied). Beyond the drag
/// distance of the system the drag starts, with the formats of MainWindow;</item>
/// <item><c>DropTarget</c> on a node: over it the place of the drop is computed on the height of its row, the hint goes to the header of the tree and the
/// indicator is drawn above the row, never changing its size (a line before or after it, the row marked when the drop goes inside); a section that can
/// take the drop expands, as today;</item>
/// <item><c>SectionClick</c> on the name of a node: a single click on a project or section shows its overview after the double click time of the
/// system (ScheduleProjectSectionClick), a model opens; cancelled by another press in the window (the double click renames), by the window losing the
/// focus, by a drag or by the rename.</item>
/// </list>
/// The state of a drag and of a click lives in the elements and in one <see cref="ClickTimer"/> per window: no static mutable state (P3).
/// </summary>
internal static class ProjectTreeInput
{
    /// <summary>The formats of the drag of MainWindow (ProjectHierarchy).</summary>
    internal const string ModuleFormat = "ANTHEA.ProjectModule", SheetFormat = "ANTHEA.ProjectSheet", SectionFormat = "ANTHEA.ProjectSection";

    public static readonly DependencyProperty DragRowProperty =
        DependencyProperty.RegisterAttached("DragRow", typeof(bool), typeof(ProjectTreeInput), new PropertyMetadata(false, OnDragSource));

    public static readonly DependencyProperty DragNameProperty =
        DependencyProperty.RegisterAttached("DragName", typeof(bool), typeof(ProjectTreeInput), new PropertyMetadata(false, OnDragSource));

    public static readonly DependencyProperty DropTargetProperty =
        DependencyProperty.RegisterAttached("DropTarget", typeof(bool), typeof(ProjectTreeInput), new PropertyMetadata(false, OnDropTarget));

    public static readonly DependencyProperty SectionClickProperty =
        DependencyProperty.RegisterAttached("SectionClick", typeof(bool), typeof(ProjectTreeInput), new PropertyMetadata(false, OnSectionClick));

    /// <summary>Where the press of a drag source started; null when no drag can start.</summary>
    private static readonly DependencyProperty StartProperty =
        DependencyProperty.RegisterAttached("Start", typeof(Point?), typeof(ProjectTreeInput), new PropertyMetadata(null));

    /// <summary>The last press on the element became a drag: its release is not a click.</summary>
    private static readonly DependencyProperty DraggedProperty =
        DependencyProperty.RegisterAttached("Dragged", typeof(bool), typeof(ProjectTreeInput), new PropertyMetadata(false));

    /// <summary>The indicator of a drop shown over a tree, kept on the tree.</summary>
    private static readonly DependencyProperty IndicatorProperty =
        DependencyProperty.RegisterAttached("Indicator", typeof(DropAdorner), typeof(ProjectTreeInput), new PropertyMetadata(null));

    /// <summary>The timer of the single click of a window, created when the first name of the tree of that window is clicked.</summary>
    private static readonly DependencyProperty ClickTimerProperty =
        DependencyProperty.RegisterAttached("ClickTimer", typeof(ClickTimer), typeof(ProjectTreeInput), new PropertyMetadata(null));

    public static bool GetDragRow(DependencyObject element) => (bool)element.GetValue(DragRowProperty);

    public static void SetDragRow(DependencyObject element, bool value) => element.SetValue(DragRowProperty, value);

    public static bool GetDragName(DependencyObject element) => (bool)element.GetValue(DragNameProperty);

    public static void SetDragName(DependencyObject element, bool value) => element.SetValue(DragNameProperty, value);

    public static bool GetDropTarget(DependencyObject element) => (bool)element.GetValue(DropTargetProperty);

    public static void SetDropTarget(DependencyObject element, bool value) => element.SetValue(DropTargetProperty, value);

    public static bool GetSectionClick(DependencyObject element) => (bool)element.GetValue(SectionClickProperty);

    public static void SetSectionClick(DependencyObject element, bool value) => element.SetValue(SectionClickProperty, value);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    // ---------------------------------------------------------------- drag

    /// <summary>
    /// The data of a drag from <paramref name="source"/>: the format of MainWindow and the payload of the ViewModel (a node, or a module id); a sheet
    /// from its row, a project or section from its name, a card of the catalogue from itself.
    /// </summary>
    internal static (string Format, object Payload)? DragData(FrameworkElement source) => source.DataContext switch
    {
        ProjectNodeViewModel { CanDrag: true, Node: { } node, IsSheet: true } when GetDragRow(source) => (SheetFormat, node),
        ProjectNodeViewModel { CanDrag: true, Node: { } node, IsSheet: false } when GetDragName(source) => (SectionFormat, node),
        ProjectCatalogCard card when GetDragRow(source) => (ModuleFormat, card.Module),
        _ => null
    };

    private static void OnDragSource(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement source) return;
        source.PreviewMouseLeftButtonDown -= OnPress; source.PreviewMouseLeftButtonUp -= OnRelease; source.MouseMove -= OnMove;
        if (!GetDragRow(source) && !GetDragName(source)) return;
        source.PreviewMouseLeftButtonDown += OnPress; source.PreviewMouseLeftButtonUp += OnRelease; source.MouseMove += OnMove;
    }

    private static void OnPress(object sender, MouseButtonEventArgs e)
    {
        var source = (FrameworkElement)sender;
        source.SetValue(DraggedProperty, false);
        // No drag from the box of the rename in place, nor from a button inside the row (the icon of a sheet).
        source.SetValue(StartProperty, e.OriginalSource is DependencyObject origin && (Ancestor<TextBoxBase>(origin, source) is not null || Ancestor<ButtonBase>(origin, source) is not null)
            ? null : e.GetPosition(source));
    }

    private static void OnRelease(object sender, MouseButtonEventArgs e) => ((FrameworkElement)sender).SetValue(StartProperty, null);

    private static void OnMove(object sender, MouseEventArgs e)
    {
        var source = (FrameworkElement)sender;
        if (e.LeftButton != MouseButtonState.Pressed) { source.SetValue(StartProperty, null); return; }
        if (source.GetValue(StartProperty) is not Point origin || DragData(source) is not { } data) return;
        var position = e.GetPosition(source);
        if (Math.Abs(position.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        source.SetValue(StartProperty, null);
        source.SetValue(DraggedProperty, true);
        e.Handled = true;
        Cancel(source);
        var tree = Ancestor<TreeView>(source, null) ?? (Window.GetWindow(source) is { } window ? Descendant<TreeView>(window) : null);
        try { DragDrop.DoDragDrop(source, new DataObject(data.Format, data.Payload), data.Format == ModuleFormat ? DragDropEffects.Copy : DragDropEffects.Move); }
        finally { if (tree is not null) Clear(tree); }
    }

    // ---------------------------------------------------------------- drop

    private static void OnDropTarget(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TreeViewItem item) return;
        item.DragOver -= OnDragOver; item.DragLeave -= OnDragLeave; item.Drop -= OnDrop;
        item.AllowDrop = e.NewValue is true;
        if (e.NewValue is not true) return;
        item.DragOver += OnDragOver; item.DragLeave += OnDragLeave; item.Drop += OnDrop;
    }

    private static object? Payload(IDataObject data) =>
        data.GetData(SheetFormat) as JsonObject ?? data.GetData(SectionFormat) as JsonObject ?? (object?)(data.GetData(ModuleFormat) as string);

    /// <summary>The innermost node handles the drag; its ancestors see it handled.</summary>
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var item = (TreeViewItem)sender;
        e.Effects = Payload(e.Data) is { } payload ? Over(item, payload, e.GetPosition(Row(item))) : DragDropEffects.None;
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (Ancestor<TreeView>((TreeViewItem)sender, null) is { } tree) Clear(tree);
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        e.Handled = true;
        var item = (TreeViewItem)sender;
        e.Effects = Payload(e.Data) is { } payload && Drop(item, payload, e.GetPosition(Row(item))) ? (e.Data.GetDataPresent(ModuleFormat) ? DragDropEffects.Copy : DragDropEffects.Move) : DragDropEffects.None;
    }

    /// <summary>
    /// A drag of <paramref name="payload"/> over <paramref name="item"/> at <paramref name="position"/> of its row (the checks call it without a mouse):
    /// the place and the hint from the ViewModel, the indicator over the row, a section that takes the drop expanded. The effect of the drag.
    /// </summary>
    internal static DragDropEffects Over(TreeViewItem item, object payload, Point position)
    {
        var tree = Ancestor<TreeView>(item, null);
        if (item.DataContext is not ProjectNodeViewModel node || tree?.DataContext is not ProjectTreeViewModel model) return DragDropEffects.None;
        var row = Row(item);
        var drop = model.DragOver(node, payload, position.Y / Math.Max(1, row.ActualHeight));
        if (drop.Place == ProjectDropPlace.None) { Clear(tree, keepHint: true); return DragDropEffects.None; }
        if (drop.Place == ProjectDropPlace.Inside && !node.IsSheet) node.IsExpanded = true;
        Show(tree, row, drop.Place);
        return drop.Copies ? DragDropEffects.Copy : DragDropEffects.Move;
    }

    /// <summary>The drop of <paramref name="payload"/> on <paramref name="item"/> at <paramref name="position"/> of its row: the indicator goes, the ViewModel drops.</summary>
    internal static bool Drop(TreeViewItem item, object payload, Point position)
    {
        var tree = Ancestor<TreeView>(item, null);
        if (tree is not null) Clear(tree);
        if (item.DataContext is not ProjectNodeViewModel node || tree?.DataContext is not ProjectTreeViewModel model) return false;
        return model.CompleteDrop(node, payload, position.Y / Math.Max(1, Row(item).ActualHeight));
    }

    /// <summary>The place of the indicator shown over <paramref name="tree"/>, null without one (for the checks).</summary>
    internal static ProjectDropPlace? Indicator(TreeView tree) => (tree.GetValue(IndicatorProperty) as DropAdorner)?.Place;

    /// <summary>The row of a node: the border of its header in the template of the shell, otherwise the node itself.</summary>
    internal static FrameworkElement Row(TreeViewItem item) => item.Template?.FindName("Row", item) as FrameworkElement ?? item;

    private static void Show(TreeView tree, FrameworkElement row, ProjectDropPlace place)
    {
        if (tree.GetValue(IndicatorProperty) is DropAdorner current && ReferenceEquals(current.AdornedElement, row) && current.Place == place) return;
        Clear(tree, keepHint: true);
        if (AdornerLayer.GetAdornerLayer(row) is not { } layer) return;
        var indicator = new DropAdorner(row, place, Brush(row, "Token.Accent"));
        layer.Add(indicator);
        tree.SetValue(IndicatorProperty, indicator);
    }

    private static void Clear(TreeView tree, bool keepHint = false)
    {
        if (!keepHint && tree.DataContext is ProjectTreeViewModel model) model.ClearDropHint();
        if (tree.GetValue(IndicatorProperty) is not DropAdorner indicator) return;
        tree.ClearValue(IndicatorProperty);
        AdornerLayer.GetAdornerLayer(indicator.AdornedElement)?.Remove(indicator);
    }

    private static Brush? Brush(FrameworkElement scope, string token) => scope.TryFindResource(token) as Brush;

    /// <summary>
    /// The indicator of SectionDropAdorner: a line of two pixels with a dot at its start, above or below the row; for a drop inside, the row outlined in
    /// the same colour (MainWindow colours the header). Drawn above the row: the row never changes its size under the pointer.
    /// </summary>
    private sealed class DropAdorner : Adorner
    {
        private readonly Brush? line;

        internal DropAdorner(UIElement row, ProjectDropPlace place, Brush? line) : base(row)
        {
            Place = place; this.line = line;
            IsHitTestVisible = false;
        }

        internal ProjectDropPlace Place { get; }

        protected override void OnRender(DrawingContext context)
        {
            var size = AdornedElement.RenderSize;
            if (Place == ProjectDropPlace.Inside)
            {
                // The outline of the row in the colour of the line: a fill above the row would cover its name.
                context.DrawRoundedRectangle(null, new Pen(line, 2), new Rect(1, 1, Math.Max(0, size.Width - 2), Math.Max(0, size.Height - 2)), 4, 4);
                return;
            }
            double y = Place == ProjectDropPlace.After ? size.Height : 0;
            context.DrawLine(new Pen(line, 2), new Point(0, y), new Point(size.Width, y));
            context.DrawEllipse(line, null, new Point(0, y), 3, 3);
        }
    }

    // ---------------------------------------------------------------- single click on a section

    private static void OnSectionClick(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement label) return;
        label.MouseLeftButtonUp -= OnLabelUp;
        if (e.NewValue is true) label.MouseLeftButtonUp += OnLabelUp;
    }

    private static void OnLabelUp(object sender, MouseButtonEventArgs e)
    {
        var label = (FrameworkElement)sender;
        // Projects and sections show their overview; a model opens (in MainWindow its node is a button); a sheet only gets selected.
        if (label.DataContext is not ProjectNodeViewModel { IsSheet: false, IsRenaming: false } node) return;
        if (label.GetValue(DraggedProperty) is true) { label.SetValue(DraggedProperty, false); return; }
        if (Window.GetWindow(label) is not { } window) return;
        e.Handled = true;
        Timer(window).Schedule(label, node);
    }

    /// <summary>The click that would follow, cancelled (a drag started from the window).</summary>
    private static void Cancel(FrameworkElement element)
    {
        if (Window.GetWindow(element)?.GetValue(ClickTimerProperty) is ClickTimer timer) timer.Cancel();
    }

    /// <summary>The timer of the single click of <paramref name="window"/>, created once (the checks read it to wait for the click).</summary>
    internal static ClickTimer Timer(Window window)
    {
        if (window.GetValue(ClickTimerProperty) is ClickTimer timer) return timer;
        timer = new ClickTimer(window, TimeSpan.FromMilliseconds(GetDoubleClickTime()));
        window.SetValue(ClickTimerProperty, timer);
        return timer;
    }

    /// <summary>
    /// The single click of the names of the tree of one window (MainWindow.ScheduleProjectSectionClick): after the double click time the node opens
    /// (<see cref="ProjectNodeViewModel.OpenCommand"/>: the overview of the section) if its name is still on screen; any press in the window, the
    /// window losing the focus and a new click cancel it first. A double click therefore renames, as today.
    /// </summary>
    internal sealed class ClickTimer
    {
        private (FrameworkElement Label, ProjectNodeViewModel Node, CancellationTokenSource Wait)? pending;

        /// <summary>
        /// The wait is a delay whose end comes back to the thread of the window (no timer of the dispatcher: their number in the desktop may only go
        /// down, Architecture.Tests).
        /// </summary>
        internal ClickTimer(Window window, TimeSpan delay)
        {
            Delay = delay;
            window.PreviewMouseDown += (_, _) => Cancel();
            window.Deactivated += (_, _) => Cancel();
            window.Closed += (_, _) => Cancel();
        }

        /// <summary>The time of the double click of the system.</summary>
        internal TimeSpan Delay { get; }

        /// <summary>A click is waiting.</summary>
        internal bool IsPending => pending is not null;

        internal void Schedule(FrameworkElement label, ProjectNodeViewModel node)
        {
            Cancel();
            var wait = new CancellationTokenSource();
            pending = (label, node, wait);
            _ = Task.Delay(Delay, wait.Token).ContinueWith(done => { if (!done.IsCanceled) Fire(wait); }, CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }

        internal void Cancel()
        {
            if (pending is { Wait: var wait }) { wait.Cancel(); wait.Dispose(); }
            pending = null;
        }

        private void Fire(CancellationTokenSource wait)
        {
            if (pending is not { } waiting || !ReferenceEquals(waiting.Wait, wait)) return;
            Cancel();
            var (label, node, _) = waiting;
            if (!label.IsLoaded || !label.IsVisible || node.IsRenaming) return;
            if (node.OpenCommand.CanExecute(null)) node.OpenCommand.Execute(null);
        }
    }

    // ---------------------------------------------------------------- tree helpers

    private static T? Ancestor<T>(DependencyObject element, DependencyObject? stop) where T : DependencyObject
    {
        for (var current = element; current is not null && !ReferenceEquals(current, stop); current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private static T? Descendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (Descendant<T>(child) is { } inner) return inner;
        }
        return null;
    }
}
