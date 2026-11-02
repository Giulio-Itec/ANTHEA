using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.Common;

/// <summary>
/// Two children one above the other (review of the Actions sheets, 2/11/2026): the first takes its own height, but not more than
/// <see cref="FirstMaximumFraction"/> of the panel when the second needs the room; the second takes the rest. On the minimum sheet of the
/// shell (1120 × 600) the assistance of the Actions sheets and the diagram of a case shrink, and the tabs and the table of the case keep a
/// usable height; with room enough the first child keeps its full height. In an unbounded height (a scroll viewer) both take their own
/// height. Layout only.
/// </summary>
public sealed class CappedSplitPanel : Panel
{
    public static readonly DependencyProperty FirstMaximumFractionProperty = DependencyProperty.Register(nameof(FirstMaximumFraction), typeof(double),
        typeof(CappedSplitPanel), new FrameworkPropertyMetadata(0.45, FrameworkPropertyMetadataOptions.AffectsMeasure), v => v is double d && d > 0 && d < 1);

    /// <summary>Largest share of the height of the panel given to the first child (0.45 by default).</summary>
    public double FirstMaximumFraction { get => (double)GetValue(FirstMaximumFractionProperty); set => SetValue(FirstMaximumFractionProperty, value); }

    protected override Size MeasureOverride(Size available)
    {
        UIElement? first = InternalChildren.Count > 0 ? InternalChildren[0] : null, second = InternalChildren.Count > 1 ? InternalChildren[1] : null;
        double height = available.Height, limit = height;
        if (!double.IsInfinity(height))
        {
            // The share of the first child grows over the fraction with the room that the second one does not use (a short table).
            second?.Measure(new Size(available.Width, double.PositiveInfinity));
            limit = Math.Max(height * FirstMaximumFraction, height - (second?.DesiredSize.Height ?? 0));
        }
        first?.Measure(new Size(available.Width, limit));
        double top = first?.DesiredSize.Height ?? 0;
        second?.Measure(new Size(available.Width, double.IsInfinity(height) ? height : Math.Max(0, height - top)));
        double width = Math.Max(first?.DesiredSize.Width ?? 0, second?.DesiredSize.Width ?? 0);
        return new Size(double.IsInfinity(available.Width) ? width : Math.Min(width, available.Width), top + (second?.DesiredSize.Height ?? 0));
    }

    protected override Size ArrangeOverride(Size final)
    {
        UIElement? first = InternalChildren.Count > 0 ? InternalChildren[0] : null, second = InternalChildren.Count > 1 ? InternalChildren[1] : null;
        // The measure has already capped the first child (its desired height); unbounded, it keeps its whole height.
        double top = Math.Min(first?.DesiredSize.Height ?? 0, final.Height);
        first?.Arrange(new Rect(0, 0, final.Width, top));
        second?.Arrange(new Rect(0, top, final.Width, Math.Max(0, final.Height - top)));
        for (int i = 2; i < InternalChildren.Count; i++) InternalChildren[i].Arrange(new Rect());
        return final;
    }
}
