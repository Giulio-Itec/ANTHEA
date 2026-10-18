using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Modules.HorizontalPile;
using X.Desktop.Views.Common;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// The layout of the cards of the tab of the capacity of the horizontal pile (F5.7): LayoutCards of the historic view
/// (HorizontalPresentation.cs), moved without changes into a panel, which replaces its canvas. The seven children are the cards in the
/// historic order: «Dati generali», «Efficienza», «Coefficienti normativa», «Verifica», «Stratigrafia», «Profilo stratigrafico» and the
/// section. On the viewport of the scroll viewer that hosts the panel (less 4, at least 320 × 280), margins and gaps of 12:
/// <list type="bullet">
/// <item>an extended card («Estendi»: <see cref="ExpandedCard"/>) alone on the whole width, at least 350 high (540 the section);</item>
/// <item>wide (1350 or more): the four upper cards on one row (31/20/18/31 % of the width), as high as the general form plus 80 (at least
/// 395) and the advanced options when they are open; the three lower cards below (38/16/46 %), at least 360 high;</item>
/// <item>compact: one or two columns (two from 1050) for the upper cards (heights: the general form plus 96, at least 340, then 340, 270,
/// 340), then the three lower cards on the whole width (350–500 high, 540 the section).</item>
/// </list>
/// The height of the general form is the one of its rows with the labels on one line (UnwrappedSize of InputForm), as the open advanced
/// options (the expander tagged «opzioni-avanzate» in the first card, with its form). Layout only: no data and no write of the document.
/// </summary>
public sealed class HorizontalPileCardPanel : Panel
{
    /// <summary>The extended card (<see cref="HorizontalPileSheetViewModel.ExpandedCard"/>), empty for none.</summary>
    public static readonly DependencyProperty ExpandedCardProperty = DependencyProperty.Register(nameof(ExpandedCard), typeof(string), typeof(HorizontalPileCardPanel),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Tag of the expander «Opzioni avanzate» of the first card.</summary>
    public const string AdvancedTag = "opzioni-avanzate";

    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    // The measures of LayoutCards, named (in DIP): margin and gap of 12; the viewport less ViewportInset, at least MinimumWidth × MinimumHeight;
    // the upper cards CompactChrome (compact) or WideChrome (wide) higher than the general form, at least CompactTop or WideTop; the wide
    // layout from WideLayout, the lower cards at least WideLower; two columns from TwoColumns; the compact lower cards CompactInset less than
    // the viewport, between ExpandedHeight and CompactLower, the section SectionHeight; an extended card at least ExpandedHeight high.
    private const double Margin = 12, Gap = 12, UpperGaps = 3, ViewportInset = 4, MinimumWidth = 320, MinimumHeight = 280, CompactChrome = 96, CompactTop = 340,
        WideChrome = 80, WideTop = 395, WideLayout = 1350, WideLower = 360, TwoColumns = 1050, CompactInset = 24, CompactLower = 500, ExpandedHeight = 350,
        SectionHeight = 540;

    /// <summary>The cards, the first of the three lower cards («Stratigrafia») and the card of the section.</summary>
    private const int Cards = 7, FirstLower = 4, SectionIndex = 6;
    private ScrollViewer? scroll;
    private Rect[] places = [];

    public HorizontalPileCardPanel()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        // The advanced options of the first card change the height of the upper row (LayoutCards at Expanded and Collapsed).
        AddHandler(Expander.ExpandedEvent, new RoutedEventHandler((_, _) => InvalidateMeasure()));
        AddHandler(Expander.CollapsedEvent, new RoutedEventHandler((_, _) => InvalidateMeasure()));
    }

    public string ExpandedCard
    {
        get => (string)GetValue(ExpandedCardProperty);
        set => SetValue(ExpandedCardProperty, value);
    }

    /// <summary>The index of the extended card in the historic order, −1 for none.</summary>
    private int Expanded => ExpandedCard switch
    {
        HorizontalPileSheetViewModel.SurveysCard => 4,
        HorizontalPileSheetViewModel.ProfileCard => 5,
        HorizontalPileSheetViewModel.SectionCard => 6,
        _ => -1
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        if (scroll is null) Attach();
        var cards = InternalChildren.Cast<UIElement>().ToArray();
        // The forms are built by their templates at the first measure: the first card is measured once without limits.
        if (cards.Length > 0) cards[0].Measure(Unbounded);
        var (general, model, advanced) = Forms(cards);
        var (size, rects, visible) = Layout(cards.Length, Viewport(availableSize), Expanded, general, advanced ? model : null);
        places = rects;
        for (int i = 0; i < cards.Length; i++)
        {
            var visibility = visible[i] ? Visibility.Visible : Visibility.Collapsed;
            if (cards[i].Visibility != visibility) cards[i].Visibility = visibility;
            cards[i].Measure(visible[i] ? rects[i].Size : default);
        }
        return size;
    }

    /// <summary>
    /// The cards in their places. The historic canvas, as wide (and high) as the layout, is centred in the viewport when it is smaller: the
    /// places move by half of the difference, as there.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var cards = InternalChildren.Cast<UIElement>().ToArray();
        var offset = new Vector(Math.Max(0, (finalSize.Width - DesiredSize.Width) / 2), Math.Max(0, (finalSize.Height - DesiredSize.Height) / 2));
        for (int i = 0; i < cards.Length; i++)
            cards[i].Arrange(i < places.Length && cards[i].Visibility == Visibility.Visible ? Rect.Offset(places[i], offset) : default);
        return finalSize;
    }

    /// <summary>
    /// LayoutCards of the historic view for <paramref name="count"/> cards in a viewport of <paramref name="viewport"/>: the size of the panel, the
    /// place and the visibility of each card. <paramref name="general"/> is the general form with its labels on one line, <paramref name="model"/>
    /// the form of the advanced options when they are open (null when closed).
    /// </summary>
    internal static (Size Size, Rect[] Places, bool[] Visible) Layout(int count, Size viewport, int expanded, Size general, Size? model)
    {
        var places = new Rect[count];
        var visible = Enumerable.Repeat(true, count).ToArray();
        if (count != Cards) return (default, places, visible);
        double w = Math.Max(MinimumWidth, viewport.Width - ViewportInset), h = Math.Max(MinimumHeight, viewport.Height - ViewportInset);
        double advanced = model?.Height ?? 0;
        double top = Math.Max(CompactTop, general.Height + CompactChrome) + advanced;
        void Place(int i, double x, double y, double width, double height) => places[i] = new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
        if (expanded >= 0)
        {
            for (int i = 0; i < count; i++) visible[i] = i == expanded;
            double expandedHeight = Math.Max(h - 2 * Margin, expanded == SectionIndex ? SectionHeight : ExpandedHeight);
            Place(expanded, Margin, Margin, w - 2 * Margin, expandedHeight);
            return (new Size(w, expandedHeight + 2 * Margin), places, visible);
        }
        if (w >= WideLayout)
        {
            double availableTop = w - 2 * Margin - UpperGaps * Gap;
            double[] shares = [.31, .20, .18, .31];
            top = Math.Max(WideTop, general.Height + WideChrome) + advanced;
            double x = Margin;
            for (int i = 0; i < FirstLower; i++) { double cw = availableTop * shares[i]; Place(i, x, Margin, cw, top); x += cw + Gap; }
            double y = Margin + top + Gap, lower = Math.Max(WideLower, h - y - Margin), available = w - 2 * Margin - 2 * Gap;
            x = Margin; double[] proportions = [.38, .16, .46];
            for (int i = 0; i < proportions.Length; i++) { double cw = available * proportions[i]; Place(FirstLower + i, x, y, cw, lower); x += cw + Gap; }
            return (new Size(w, y + lower + Margin), places, visible);
        }
        int columns = w >= TwoColumns ? 2 : 1;
        double cardWidth = (w - 2 * Margin - (columns - 1) * Gap) / columns, row = Margin;
        double[] heights = [top, 340, 270, 340];
        for (int i = 0; i < FirstLower; i += columns)
        {
            double rowHeight = heights.Skip(i).Take(columns).Max();
            int inRow = Math.Min(columns, FirstLower - i);
            for (int c = 0; c < inRow; c++) Place(i + c, Margin + c * (cardWidth + Gap), row, inRow == 1 ? w - 2 * Margin : cardWidth, rowHeight);
            row += rowHeight + Gap;
        }
        for (int i = FirstLower; i < count; i++)
        {
            double ch = i == SectionIndex ? SectionHeight : Math.Clamp(h - CompactInset, ExpandedHeight, CompactLower);
            Place(i, Margin, row, w - 2 * Margin, ch); row += ch + Gap;
        }
        return (new Size(w, row), places, visible);
    }

    /// <summary>The viewport of the hosting scroll viewer, or the size of the panel before the first layout (as the historic view).</summary>
    private Size Viewport(Size available)
    {
        static double Pick(double viewport, double available, double actual) =>
            double.IsFinite(viewport) && viewport > 0 ? viewport : double.IsFinite(available) ? available : actual;
        return new Size(Pick(scroll?.ViewportWidth ?? 0, available.Width, scroll?.ActualWidth ?? ActualWidth),
            Pick(scroll?.ViewportHeight ?? 0, available.Height, scroll?.ActualHeight ?? ActualHeight));
    }

    /// <summary>
    /// UnwrappedSize of the general form and of the form of the advanced options of the first card, and whether the advanced options are open:
    /// the rows of the table of each FieldForm measured with their labels on one line.
    /// </summary>
    private static (Size General, Size Model, bool Advanced) Forms(UIElement[] cards)
    {
        if (cards.Length == 0) return (default, default, false);
        var expander = Descendants(cards[0]).OfType<Expander>().FirstOrDefault(e => Equals(e.Tag, AdvancedTag));
        var forms = Descendants(cards[0]).OfType<FieldForm>().ToList();
        var general = forms.FirstOrDefault(f => expander is null || !IsInside(f, expander));
        var model = expander is null ? null : forms.FirstOrDefault(f => IsInside(f, expander));
        return (Unwrapped(general), Unwrapped(model), expander?.IsExpanded == true);
    }

    private static Size Unwrapped(FieldForm? form)
    {
        if (form is null) return default;
        form.Table.Measure(Unbounded);
        return form.Table.DesiredSize;
    }

    private static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (var node = VisualTreeHelper.GetParent(element); node is not null; node = VisualTreeHelper.GetParent(node))
            if (ReferenceEquals(node, ancestor)) return true;
        return false;
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
