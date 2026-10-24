using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Reports;

namespace X.Desktop.Views.EmbeddedWalls;

/// <summary>Mesh and nodal values from the frozen hydraulic result. All operations below are display coordinates and color bins.</summary>
public sealed class SeepageDiagram : FrameworkElement
{
    private const double MarginLeft = 65, MarginRight = 25, MarginTop = 50, MarginBottom = 65, LabelGap = 20, AxisLabelGap = 5;
    private const double NodeRadius = 2.5, LegendSwatch = 12, LegendSlot = 100;
    private const int TickCount = 4;
    public static readonly DependencyProperty FieldProperty = DependencyProperty.Register(nameof(Field), typeof(EmbeddedWallHydraulicData), typeof(SeepageDiagram), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty QuantityProperty = DependencyProperty.Register(nameof(Quantity), typeof(string), typeof(SeepageDiagram), new FrameworkPropertyMetadata("Carico idraulico [m]", FrameworkPropertyMetadataOptions.AffectsRender));
    public EmbeddedWallHydraulicData? Field { get => (EmbeddedWallHydraulicData?)GetValue(FieldProperty); set => SetValue(FieldProperty, value); }
    public string Quantity { get => (string)GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var ink = (Brush)FindResource("Token.Ink"); var paper = (Brush)FindResource("Token.Paper");
        dc.DrawRectangle(paper, null, new Rect(RenderSize));
        void Text(string text, double x, double y) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        if (Field is null || !Field.Complete || Field.Nodes.Count == 0) { Text("Calcolare la filtrazione e selezionare una fase convergente.", 15, 15); return; }
        Text(Quantity + " · valori nodali; mesh in scala geometrica", 15, 10);
        var nodes = Field.Nodes; var byId = nodes.ToDictionary(n => n.Index);
        double minX = nodes.Min(n => n.X), maxX = nodes.Max(n => n.X), minZ = nodes.Min(n => n.Z), maxZ = nodes.Max(n => n.Z);
        double scale = Math.Min(Math.Max(1, ActualWidth - MarginLeft - MarginRight) / (maxX - minX), Math.Max(1, ActualHeight - MarginTop - MarginBottom) / (maxZ - minZ));
        Point Pixel(EmbeddedWallHydraulicNode n) => new(MarginLeft + (n.X - minX) * scale, MarginTop + (maxZ - n.Z) * scale);
        double Value(EmbeddedWallHydraulicNode n) => Quantity == "Pressione interstiziale [kPa]" ? n.Pressure : n.Head;
        double low = nodes.Min(Value), high = nodes.Max(Value), range = high - low;
        Brush[] palette = [ (Brush)FindResource("Token.Series.Blue"), (Brush)FindResource("Token.Series.Green"), (Brush)FindResource("Token.Series.Orange"), (Brush)FindResource("Token.Series.Red") ];
        dc.PushOpacity(.22);
        var mesh = new StreamGeometry(); using (var context = mesh.Open()) foreach (var e in Field.Elements)
        { context.BeginFigure(Pixel(byId[e.A]), false, true); context.LineTo(Pixel(byId[e.B]), true, false); context.LineTo(Pixel(byId[e.C]), true, false); }
        mesh.Freeze(); dc.DrawGeometry(null, new Pen(ink, .5), mesh); dc.Pop();
        foreach (var n in nodes)
        {
            int bin = range <= 1e-12 ? 0 : Math.Min(palette.Length - 1, (int)((Value(n) - low) / range * palette.Length));
            dc.DrawEllipse(palette[bin], null, Pixel(n), NodeRadius, NodeRadius);
        }
        double plotBottom = MarginTop + (maxZ - minZ) * scale;
        for (int i = 0; i <= TickCount; i++)
        {
            double fraction = (double)i / TickCount;
            Text((maxZ - (maxZ - minZ) * fraction).ToString("G4") + " m", 0, MarginTop + (maxZ - minZ) * fraction * scale);
            Text((minX + (maxX - minX) * fraction).ToString("G4") + " m", MarginLeft + (maxX - minX) * fraction * scale, plotBottom + AxisLabelGap);
        }
        for (int i = 0; i < palette.Length; i++)
        {
            double x = MarginLeft + i * LegendSlot, y = plotBottom + LabelGap + LabelGap;
            dc.DrawRectangle(palette[i], null, new Rect(x, y, LegendSwatch, LegendSwatch));
            Text((low + range * i / palette.Length).ToString("G4"), x + LabelGap, y);
        }
        Text("max " + high.ToString("G4"), MarginLeft + palette.Length * LegendSlot, plotBottom + LabelGap + LabelGap);
    }
}
