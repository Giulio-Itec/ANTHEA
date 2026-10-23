using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.Shell;

/// <summary>
/// The scrolling of small screens of MainWindow (DisplayAdaptation.Viewport) for the templates of the new shell (F5.13 c, night of 22-23/10): its content
/// is as large as the visible area, never smaller than <see cref="MinimumContentWidth"/> × <see cref="MinimumContentHeight"/> (its margins apart); in a
/// smaller window the content keeps that size and scrolls. A finite size even inside a scroll viewer, so that star grids and the scroll bars of the
/// views keep working. The sizes come from <c>SheetLayout</c> (Presentation); no state of its own beyond the element.
/// </summary>
internal sealed class MinimumSizeViewport : ScrollViewer
{
    public static readonly DependencyProperty MinimumContentWidthProperty =
        DependencyProperty.Register(nameof(MinimumContentWidth), typeof(double), typeof(MinimumSizeViewport), new PropertyMetadata(0d, OnMinimum));

    public static readonly DependencyProperty MinimumContentHeightProperty =
        DependencyProperty.Register(nameof(MinimumContentHeight), typeof(double), typeof(MinimumSizeViewport), new PropertyMetadata(0d, OnMinimum));

    public MinimumSizeViewport()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Focusable = false;
        Loaded += (_, _) => Resize();
        ScrollChanged += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, this) && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) Resize();
        };
    }

    public double MinimumContentWidth { get => (double)GetValue(MinimumContentWidthProperty); set => SetValue(MinimumContentWidthProperty, value); }

    public double MinimumContentHeight { get => (double)GetValue(MinimumContentHeightProperty); set => SetValue(MinimumContentHeightProperty, value); }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        Resize();
    }

    private static void OnMinimum(DependencyObject element, DependencyPropertyChangedEventArgs e) => ((MinimumSizeViewport)element).Resize();

    /// <summary>The content as large as the visible area, at least the minimum; before the first layout the minimum (DisplayAdaptation.Viewport).</summary>
    private void Resize()
    {
        if (Content is not FrameworkElement content) return;
        var margin = content.Margin;
        content.Width = Math.Max(MinimumContentWidth, ViewportWidth - margin.Left - margin.Right);
        content.Height = Math.Max(MinimumContentHeight, ViewportHeight - margin.Top - margin.Bottom);
    }
}
