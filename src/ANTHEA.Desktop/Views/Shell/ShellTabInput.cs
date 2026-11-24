using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Text;
using Anthea.Presentation.Shell.Tabs;

namespace X.Desktop.Views.Shell;

/// <summary>Pointer gestures and insertion indicator for tab headers. The ViewModel owns the order and validates each move.</summary>
public static class ShellTabInput
{
    private const string Format = "ANTHEA.ShellTab";
    internal const string SectionFormat = "ANTHEA.QuickSection.v1";
    private const string ModuleFormat = "ANTHEA.QuickSection.Module";
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ShellTabInput), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty StartProperty = DependencyProperty.RegisterAttached("Start", typeof(Point?), typeof(ShellTabInput));
    private static readonly DependencyProperty IndicatorProperty = DependencyProperty.RegisterAttached("Indicator", typeof(InsertionLine), typeof(ShellTabInput));
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void Changed(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not FrameworkElement header) return;
        header.PreviewMouseLeftButtonDown -= Press; header.PreviewMouseLeftButtonUp -= Release; header.PreviewMouseMove -= Move;
        header.PreviewDragOver -= DragOver; header.PreviewDragLeave -= DragLeave; header.PreviewDrop -= Drop;
        header.AllowDrop = args.NewValue is true;
        if (!header.AllowDrop) return;
        header.PreviewMouseLeftButtonDown += Press; header.PreviewMouseLeftButtonUp += Release; header.PreviewMouseMove += Move;
        header.PreviewDragOver += DragOver; header.PreviewDragLeave += DragLeave; header.PreviewDrop += Drop;
    }

    private static void Press(object sender, MouseButtonEventArgs args)
    {
        var header = (FrameworkElement)sender;
        // Only the title starts a drag. The separate close button keeps its normal click behaviour.
        bool title = header.Tag is ShellWindowsViewModel model && args.OriginalSource is DependencyObject origin
            && Ancestor<ButtonBase>(origin)?.Command == model.SelectCommand;
        header.SetValue(StartProperty, title ? args.GetPosition(header) : null);
    }

    private static void Release(object sender, MouseButtonEventArgs args) => ((FrameworkElement)sender).ClearValue(StartProperty);

    private static void Move(object sender, MouseEventArgs args)
    {
        var header = (FrameworkElement)sender;
        if (args.LeftButton != MouseButtonState.Pressed) { header.ClearValue(StartProperty); return; }
        if (header.GetValue(StartProperty) is not Point start || header.DataContext is not ShellTabViewModel { IsDisposed: false } tab) return;
        Point point = args.GetPosition(header);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        header.ClearValue(StartProperty); args.Handled = true;
        var strip = Ancestor<ItemsControl>(header);
        // Releasing the title's capture also cancels its pending click: a drag never changes selection on mouse-up.
        if (Mouse.Captured is UIElement captured && header.IsAncestorOf(captured)) captured.ReleaseMouseCapture();
        if (header.Tag is not ShellWindowsViewModel model) return;
        var drag = model.BeginTabDrag(tab);
        var data = CreateDragData(drag);
        try
        {
            var result = DragDrop.DoDragDrop(header, data, DragDropEffects.Move);
            if (result == DragDropEffects.Move && !drag.CompletedLocally && drag.Payload is { } transferred)
                model.CompleteSectionDrag(tab, transferred);
        }
        finally { model.EndTabDrag(drag); Clear(strip); }
    }

    private static void DragOver(object sender, DragEventArgs args)
    {
        args.Handled = true;
        var header = (FrameworkElement)sender;
        var local = Local(header, args.Data);
        args.Effects = Over(header, local?.Tab, args.GetPosition(header).X, local is null ? Text(args.Data, ModuleFormat) : null);
    }

    private static void DragLeave(object sender, DragEventArgs args) { args.Handled = true; Clear(Ancestor<ItemsControl>((FrameworkElement)sender)); }

    private static void Drop(object sender, DragEventArgs args)
    {
        args.Handled = true;
        var header = (FrameworkElement)sender;
        var local = Local(header, args.Data);
        bool done = local is not null
            ? CompleteDrop(header, local.Tab, args.GetPosition(header).X)
            : CompleteExternalDrop(header, ReadSection(args.Data), args.GetPosition(header).X);
        if (done && local is not null && header.Tag is ShellWindowsViewModel model) model.MarkTabDragCompleted(local);
        args.Effects = done ? DragDropEffects.Move : DragDropEffects.None;
    }

    internal static DragDropEffects Over(FrameworkElement header, ShellTabViewModel? moving, double x, string? externalModule = null)
    {
        var strip = Ancestor<ItemsControl>(header); Clear(strip);
        bool after = x >= header.ActualWidth / 2;
        if (header.Tag is not ShellWindowsViewModel model || header.DataContext is not ShellTabViewModel target
            || !(moving is not null ? model.CanDropTab(moving, target, after) : externalModule is not null && model.CanReceiveSection(externalModule, target))) return DragDropEffects.None;
        if (strip is not null && AdornerLayer.GetAdornerLayer(header) is { } layer)
        {
            var indicator = new InsertionLine(header, after, header.TryFindResource("Token.Accent") as Brush);
            layer.Add(indicator); strip.SetValue(IndicatorProperty, indicator);
        }
        return DragDropEffects.Move;
    }

    internal static bool CompleteDrop(FrameworkElement header, ShellTabViewModel? moving, double x)
    {
        Clear(Ancestor<ItemsControl>(header));
        return header.Tag is ShellWindowsViewModel model && header.DataContext is ShellTabViewModel target
            && model.DropTab(moving, target, x >= header.ActualWidth / 2);
    }

    internal static bool CompleteExternalDrop(FrameworkElement header, string? payload, double x)
    {
        Clear(Ancestor<ItemsControl>(header));
        return payload is not null && header.Tag is ShellWindowsViewModel model && header.DataContext is ShellTabViewModel target
            && model.DropExternalSection(payload, target, x >= header.ActualWidth / 2);
    }

    internal static string? ReadSection(IDataObject data) => Text(data, SectionFormat);

    internal static DataObject SectionData(string payload, string module)
    {
        var data = new DataObject();
        // Streams cross the native OLE boundary as UTF-8 bytes, without CLR object serialization.
        SetText(data, SectionFormat, payload);
        SetText(data, ModuleFormat, module);
        return data;
    }

    private static DataObject CreateDragData(TabDragSession drag)
    {
        var data = drag.Payload is { } payload && drag.Tab is SheetTabViewModel sheet ? SectionData(payload, sheet.Module) : new DataObject();
        SetText(data, Format, drag.Token);
        return data;
    }

    private static void SetText(DataObject data, string format, string text) => data.SetData(format, new MemoryStream(Encoding.UTF8.GetBytes(text), false), false);

    private static string? Text(IDataObject data, string format)
    {
        try
        {
            if (!data.GetDataPresent(format, false) || data.GetData(format, false) is not Stream stream) return null;
            long position = stream.Position;
            try
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                return reader.ReadToEnd();
            }
            finally { stream.Position = position; }
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or IOException
            or NotSupportedException or InvalidOperationException or System.Runtime.Serialization.SerializationException) { return null; }
    }

    private static TabDragSession? Local(FrameworkElement header, IDataObject data) => (header.Tag as ShellWindowsViewModel)?.FindTabDrag(Text(data, Format));

    private static void Clear(ItemsControl? strip)
    {
        if (strip?.GetValue(IndicatorProperty) is not InsertionLine indicator) return;
        AdornerLayer.GetAdornerLayer(indicator.AdornedElement)?.Remove(indicator);
        strip.ClearValue(IndicatorProperty);
    }

    private sealed class InsertionLine : Adorner
    {
        private readonly bool after;
        private readonly Brush? brush;
        internal InsertionLine(UIElement header, bool after, Brush? brush) : base(header)
        { this.after = after; this.brush = brush; IsHitTestVisible = false; }

        protected override void OnRender(DrawingContext drawing)
        {
            double x = after ? AdornedElement.RenderSize.Width - 1 : 1;
            drawing.DrawLine(new Pen(brush, 2), new Point(x, 1), new Point(x, Math.Max(1, AdornedElement.RenderSize.Height - 1)));
        }
    }

    private static T? Ancestor<T>(DependencyObject? element) where T : DependencyObject
    {
        for (var current = element; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is T found) return found;
        return null;
    }
}
