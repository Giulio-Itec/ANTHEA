using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Modules.VerticalPile;
using X.Desktop.Views.Common;

namespace X.Desktop.Views.VerticalPile;

/// <summary>
/// The layout of the cards of the vertical pile sheet (F5.6): LayoutCards of the historic view (SheetEditor), moved without changes into a
/// panel, which replaces its canvas. The seven children are the cards in the historic order: «Dati generali», «Efficienza», «Coefficienti
/// normativa», «Verifica», «Stratigrafia», «Profilo stratigrafico», «Grafici capacità portante». On the width of the viewport of the
/// scroll viewer that hosts the panel:
/// <list type="bullet">
/// <item>wide: the four upper cards on one row, the forms as wide as their labels on one line (UnwrappedSize of InputForm), the verification
/// in the rest; the three lower cards fill the height (at least 440) in the proportions 47/20/33, 16/65/19 with the profile extended;</item>
/// <item>compact (narrower than 1450 or than the forms): two columns (one under 1000) for the upper cards, then the lower cards on the
/// whole width, 340–500 high;</item>
/// <item>an extended card («Estendi»: <see cref="ExpandedCard"/>) alone on the whole viewport, the charts always, the profile in the compact
/// layout;</item>
/// <item>the extended stratigraphy with the profile at its right (<see cref="ExtendedStratigraphy"/>, 22/10/2026, as the historic view), alone
/// only when the table would be too narrow.</item>
/// </list>
/// Layout only: no data and no write of the document.
/// </summary>
public sealed class VerticalPileCardPanel : Panel
{
    /// <summary>The extended card (<see cref="VerticalPileSheetViewModel.ExpandedCard"/>), empty for none.</summary>
    public static readonly DependencyProperty ExpandedCardProperty = DependencyProperty.Register(nameof(ExpandedCard), typeof(string), typeof(VerticalPileCardPanel),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure));

    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    // The measures of LayoutCards, named (in DIP): the cards are placed on the viewport less ViewportInset; the forms are as wide as
    // their labels plus CardChrome; the verification needs VerificationMinimum; the upper cards are CardHeader higher than their forms
    // (EfficiencyNote more for the text under «Efficienza»); the upper row starts at CardTop; the lower cards are at least LowerMinimum
    // high and end BottomInset above the bottom; an extended card is inset by ExpandedLeft and ExpandedTop; the compact lower cards are
    // CompactInset less than the viewport (340–500).
    private const double ViewportInset = 4, CardChrome = 32, VerificationMinimum = 378, UpperGaps = 3, EfficiencyNote = 40, CardHeader = 68,
        CardTop = 14, LowerMinimum = 440, BottomInset = 24, ExpandedLeft = 16, ExpandedTop = 12, CompactInset = 28;

    /// <summary>The first of the three lower cards («Stratigrafia»).</summary>
    private const int FirstLower = 4;
    private ScrollViewer? scroll;
    private Rect[] places = [];

    public VerticalPileCardPanel()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    public string ExpandedCard
    {
        get => (string)GetValue(ExpandedCardProperty);
        set => SetValue(ExpandedCardProperty, value);
    }

    /// <summary>The index of the extended card in the historic order, −1 for none.</summary>
    private int Expanded => ExpandedCard switch
    {
        VerticalPileSheetViewModel.SurveysCard => 4,
        VerticalPileSheetViewModel.ProfileCard => 5,
        VerticalPileSheetViewModel.ChartsCard => 6,
        _ => -1
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        if (scroll is null) Attach();
        var cards = InternalChildren.Cast<UIElement>().ToArray();
        // The forms are built by their templates at the first measure: the cards are measured once without limits.
        for (int i = 0; i < Math.Min(3, cards.Length); i++) cards[i].Measure(Unbounded);
        var (size, rects, visible) = Layout(cards.Length, Viewport(availableSize), Expanded, i => Unwrapped(cards, i));
        places = rects;
        for (int i = 0; i < cards.Length; i++)
        {
            var visibility = visible[i] ? Visibility.Visible : Visibility.Collapsed;
            if (cards[i].Visibility != visibility) cards[i].Visibility = visibility;
            cards[i].Measure(visible[i] ? rects[i].Size : default);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cards = InternalChildren.Cast<UIElement>().ToArray();
        for (int i = 0; i < cards.Length; i++) cards[i].Arrange(i < places.Length && cards[i].Visibility == Visibility.Visible ? places[i] : default);
        return finalSize;
    }

    /// <summary>
    /// LayoutCards of the historic view for <paramref name="count"/> cards in a viewport of <paramref name="viewport"/>: the size of the panel, the
    /// place and the visibility of each card. <paramref name="unwrapped"/> gives the size of a form with its labels on one line.
    /// </summary>
    internal static (Size Size, Rect[] Places, bool[] Visible) Layout(int count, Size viewport, int expanded, Func<int, Size> unwrapped)
    {
        var places = new Rect[count];
        var visible = Enumerable.Repeat(true, count).ToArray();
        if (count < 7) return (default, places, visible);
        double w = Math.Max(320, viewport.Width - ViewportInset), h = Math.Max(240, viewport.Height - ViewportInset);
        double margin = w < 1000 ? 12 : 24, gap = 12;
        var generalSize = unwrapped(0);
        double generalWidth = Math.Ceiling(generalSize.Width) + CardChrome;
        double efficiencyWidth = Math.Ceiling(unwrapped(1).Width) + CardChrome;
        double normativeWidth = Math.Ceiling(unwrapped(2).Width) + CardChrome;
        bool compactLayout = w < Math.Max(1450, generalWidth + efficiencyWidth + normativeWidth + VerificationMinimum + 2 * margin + UpperGaps * gap);
        double pileTop = Math.Ceiling(Math.Max(generalSize.Height, Math.Max(unwrapped(2).Height, unwrapped(1).Height + EfficiencyNote))) + CardHeader;
        void Place(int i, double x, double y, double width, double height) => places[i] = new Rect(x, y, Math.Max(100, width), Math.Max(100, height));
        if (expanded == 4 && ExtendedStratigraphy.Split(w - 2 * ExpandedLeft, gap) is (double table, double side))
        {
            for (int i = 0; i < count; i++) visible[i] = i is 4 or 5;
            Place(4, ExpandedLeft, ExpandedTop, table, h - 2 * ExpandedTop); Place(5, ExpandedLeft + table + gap, ExpandedTop, side, h - 2 * ExpandedTop);
            return (new Size(w, h), places, visible);
        }
        if (expanded == 6 || expanded == 4 || compactLayout && expanded >= 0)
        {
            for (int i = 0; i < count; i++) visible[i] = i == expanded;
            Place(expanded, ExpandedLeft, ExpandedTop, w - 2 * ExpandedLeft, h - 2 * ExpandedTop);
            return (new Size(w, h), places, visible);
        }
        if (compactLayout)
        {
            int columns = w < 1000 ? 1 : 2;
            double cardWidth = (w - 2 * margin - (columns - 1) * gap) / columns, y = CardTop;
            for (int i = 0; i < 4; i += columns)
            {
                double rowHeight = pileTop;
                for (int col = 0; col < columns; col++) Place(i + col, margin + col * (cardWidth + gap), y, cardWidth, rowHeight);
                y += rowHeight + gap;
            }
            // Tables and plots need the full available width in the compact layout.
            for (int i = 4; i < count; i++)
            {
                double rowHeight = Math.Clamp(h - CompactInset, 340, 500);
                Place(i, margin, y, w - 2 * margin, rowHeight);
                y += rowHeight + gap;
            }
            return (new Size(w, y), places, visible);
        }
        double top = pileTop, x = margin, avail = w - 2 * margin - UpperGaps * gap;
        double[] widths = [generalWidth, efficiencyWidth, normativeWidth, avail - generalWidth - efficiencyWidth - normativeWidth];
        for (int i = 0; i < 4; i++) { double cw = widths[i]; Place(i, x, CardTop, cw, top); x += cw + gap; }
        double lowerTop = CardTop + top + gap; h = Math.Max(h, lowerTop + LowerMinimum); avail = w - 2 * margin - 2 * gap; x = margin;
        double[] lower = expanded == 5 ? [.16, .65, .19] : [.47, .20, .33];
        for (int i = 0; i < 3; i++) { double cw = avail * lower[i]; Place(FirstLower + i, x, lowerTop, cw, h - lowerTop - BottomInset); x += cw + gap; }
        return (new Size(w, h), places, visible);
    }

    /// <summary>The viewport of the hosting scroll viewer, or the size of the panel before the first layout (as the historic view).</summary>
    private Size Viewport(Size available)
    {
        static double Pick(double viewport, double available, double actual) =>
            double.IsFinite(viewport) && viewport > 0 ? viewport : double.IsFinite(available) ? available : actual;
        return new Size(Pick(scroll?.ViewportWidth ?? 0, available.Width, scroll?.ActualWidth ?? ActualWidth),
            Pick(scroll?.ViewportHeight ?? 0, available.Height, scroll?.ActualHeight ?? ActualHeight));
    }

    /// <summary>UnwrappedSize of InputForm: the rows of the form of card <paramref name="index"/> measured with their labels on one line.</summary>
    private static Size Unwrapped(UIElement[] cards, int index)
    {
        if (Descendants(cards[index]).OfType<FieldForm>().FirstOrDefault() is not { } form) return default;
        form.Table.Measure(Unbounded);
        return form.Table.DesiredSize;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    /// <summary>The scroll viewer that hosts the panel: a change of its viewport lays the cards out again, as the historic view.</summary>
    private void Attach()
    {
        if (scroll is not null) return;
        for (DependencyObject? node = VisualTreeHelper.GetParent(this); node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ScrollViewer found) { scroll = found; break; }
        if (scroll is null) return;
        scroll.ScrollChanged += OnScrollChanged;
        scroll.SizeChanged += OnScrollSizeChanged;
    }

    private void Detach()
    {
        if (scroll is null) return;
        scroll.ScrollChanged -= OnScrollChanged;
        scroll.SizeChanged -= OnScrollSizeChanged;
        scroll = null;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) InvalidateMeasure();
    }

    private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateMeasure();
}
