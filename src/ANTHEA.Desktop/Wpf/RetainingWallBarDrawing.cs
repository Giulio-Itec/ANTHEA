using Anthea.Application.Walls;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The reinforcement drawing of the cantilever wall (1.16.0): page 1 the section with every position, its call-out «pos. N Ø/passo»
/// and the totals per diameter; then the bar schedule with the shapes quoted in cm; last the criteria and the warnings. The geometry,
/// the shapes and the quantities are those of GPC.Design (WallBarScheduling); here they are only drawn.
/// </summary>
internal sealed class RetainingWallBarDrawing : DrawingView
{
    internal static readonly Size SheetSize = new(1120, 790);
    internal JsonObject Data { get; }
    internal WallModel.BarSchedule Schedule { get; }
    internal int Page { get; set; }
    // The pagination is the plan of the figures of the report (RetainingWallPresentation, F5.9), shared with the ViewModel.
    int ShapePages => Anthea.Application.Presentation.RetainingWallPresentation.BarShapePages(Schedule);
    internal int PageCount => Anthea.Application.Presentation.RetainingWallPresentation.BarSchedulePages(Schedule);
    int RowsPerPage => Anthea.Application.Presentation.RetainingWallPresentation.BarRowsPerPage;
    static readonly Brush Ink = Brushes.Black, Grey = Ui.Brush("#5E6670");
    static readonly Pen Thin = new(Brushes.Gray, .7), Outline = new(Ink, 1.3), Rule = new(Ui.Brush("#9AA3AD"), .6);
    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    static string N(double v, string format = "0.###") => v.ToString(format, It);
    static string Cm(double metres) => (metres * 100).ToString("0", It);
    internal RetainingWallBarDrawing(JsonObject data, WallModel.BarSchedule schedule)
    { Data = data; Schedule = schedule; Width = SheetSize.Width; Height = SheetSize.Height; }
    internal void RenderPage(DrawingContext dc, Size size) => Render(dc, size);

    /// <summary>Colour of a position: stem bars by face, foundation bars, U-bars, ties, longitudinal bars.</summary>
    internal static Brush Colour(WallModel.ScheduledBar bar) => bar.Kind switch
    {
        "Principale" when bar.Zone is "stem" or "stem_upper" => Ui.Brush(bar.Face == "monte" ? "#165DA0" : "#AA3E26"),
        "Principale" => Ui.Brush(bar.Face == "inferiore" ? "#1F7A4D" : "#7A5A12"),
        "Forcella" => Ui.Brush("#7B3FA0"),
        "Spillo" => Ui.Brush("#B5651D"),
        _ => Ink
    };
    /// <summary>Call-out of a position as on a drawing: Ø and spacing in cm, or the number of longitudinal bars.</summary>
    internal static string Callout(WallModel.ScheduledBar bar) => bar.Kind switch
    {
        "Longitudinale" => $"pos. {bar.Mark}  {bar.Lines} Ø{N(bar.Diameter)}",
        "Spillo" => $"pos. {bar.Mark}  Ø{N(bar.Diameter)} spilli",
        _ => $"pos. {bar.Mark}  Ø{N(bar.Diameter)}/{N(bar.Spacing / 10, "0.#")}"
    };

    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new(0, 0, size.Width, size.Height));
        dc.PushTransform(new ScaleTransform(size.Width / SheetSize.Width, size.Height / SheetSize.Height));
        dc.DrawRectangle(null, Thin, new(18, 18, 1084, 754));
        Text(dc, "ANTHEA   /   MURO A MENSOLA   /   TAVOLA DELLE ARMATURE", 35, 30, 15, Ink, bold: true);
        Text(dc, Page == 0 ? "Sezione trasversale armata e riepilogo" : Page > ShapePages ? "Criteri della distinta e avvisi" : "Distinta ferri", 35, 53, 23, Ink, bold: true);
        var m = Data["materials"]!;
        Text(dc, $"Tratto L = {N(Schedule.PanelLength)} m  ·  {m.S("materiale_cls_nome")}  ·  {m.S("materiale_acciaio_nome")}  ·  cnom {m.S("cover")} mm  ·  c estremità {N(Schedule.EndCover)} mm  ·  barra commerciale {N(Schedule.StockLength)} m", 35, 89, 12, Ink);
        dc.DrawLine(Thin, new(35, 113), new(1085, 113));
        if (Page == 0) { Section(dc); Summary(dc); }
        else if (Page > ShapePages) Notes(dc);
        else Table(dc, Schedule.Bars.Skip((Page - 1) * RowsPerPage).Take(RowsPerPage).ToArray());
        dc.DrawLine(Thin, new(35, 724), new(1085, 724));
        Text(dc, "Quote delle sagome in cm all’asse delle barre, archi sviluppati  ·  Peso netto senza sfridi  ·  Disegni non in scala", 35, 739, 11, Ink);
        Text(dc, $"{Page + 1} / {PageCount}", 1030, 739, 12, Ink);
        dc.Pop();
    }

    void Section(DrawingContext dc)
    {
        var g = Data["geometry"]!; double a = g.D("toe"), s = g.D("stem_base"), st = g.D("stem_top"), heel = g.D("heel"), t = g.D("slab"), h = g.D("height"), b = a + s + heel;
        double scale = Math.Min(330 / b, 470 / (t + h)), left = 195 + (330 - b * scale) / 2, bottom = 632;
        Point P(double x, double y) => new(left + x * scale, bottom - y * scale);
        Point Q(WallModel.BarPoint p) => P(p.X, p.Y);
        Text(dc, "SEZIONE TRASVERSALE", 40, 124, 13, Ink, bold: true);
        Text(dc, "VALLE", 40, 146, 11, Grey); Text(dc, "MONTE", 560, 146, 11, Grey);
        dc.DrawGeometry(Ui.Brush("#F2F4F6"), Outline, Path(new[] { P(0, 0), P(b, 0), P(b, t), P(a + s, t), P(a + s, t + h), P(a + s - st, t + h), P(a, t), P(0, t) }, true));
        // Bars of the section plane, then the longitudinal bars as dots in their true position.
        foreach (var bar in Schedule.Bars.Where(x => x.Kind != "Longitudinale"))
        {
            var pen = new Pen(Colour(bar), bar.Kind == "Principale" ? 1.8 : 1.3);
            if (bar.Kind == "Spillo")
            {
                double y0 = bar.Shape.Average(p => p.Y);
                foreach (var row in bar.SectionPoints)
                    dc.DrawGeometry(null, pen, Path(bar.Shape.Select(p => P(p.X, p.Y - y0 + row.Y)), false));
            }
            else dc.DrawGeometry(null, pen, Path(bar.Shape.Select(Q), false));
        }
        foreach (var bar in Schedule.Bars.Where(x => x.Kind == "Longitudinale"))
            foreach (var p in bar.SectionPoints) dc.DrawEllipse(Ink, null, Q(p), 2.1, 2.1);
        // Call-outs: one per position, on the side of the wall where it lies, ordered by height so that the leaders do not cross.
        double middle = a + s / 2;
        var anchors = Schedule.Bars.Select(bar => (bar, at: Anchor(bar))).ToArray();
        foreach (var side in new[] { true, false })
        {
            // Each label as close as possible to the height of its point, at least 18 px apart, within the drawing area.
            var group = anchors.Where(x => x.at.X < middle == side).Select(x => (x.bar, target: P(x.at.X, x.at.Y))).OrderBy(x => x.target.Y).ToArray();
            double x = side ? 38 : 578, gap = 18, high = 168, low = 676;
            var ys = group.Select(g => g.target.Y - 8).ToArray();
            for (int i = 0; i < ys.Length; i++) ys[i] = Math.Max(i == 0 ? high : ys[i - 1] + gap, ys[i]);
            for (int i = ys.Length - 1; i >= 0; i--) ys[i] = Math.Min(i == ys.Length - 1 ? low : ys[i + 1] - gap, ys[i]);
            for (int i = 0; i < group.Length; i++)
            {
                var (bar, target) = group[i]; var colour = Colour(bar); double y = ys[i];
                var start = new Point(side ? x + 150 : x - 4, y + 8);
                dc.DrawLine(new Pen(colour, .7), start, target); dc.DrawEllipse(colour, null, target, 1.8, 1.8);
                Text(dc, Callout(bar), x, y, 11, colour, 150, true);
            }
        }
        Dimension(dc, P(0, 0) + new Vector(0, 22), P(b, 0) + new Vector(0, 22), Cm(b));
        Dimension(dc, P(0, 0) + new Vector(0, 44), P(a, 0) + new Vector(0, 44), Cm(a));
        Dimension(dc, P(a, 0) + new Vector(0, 44), P(a + s, 0) + new Vector(0, 44), Cm(s));
        Dimension(dc, P(a + s, 0) + new Vector(0, 44), P(b, 0) + new Vector(0, 44), Cm(heel));
        Dimension(dc, P(a + s - st, t + h) + new Vector(0, -16), P(a + s, t + h) + new Vector(0, -16), Cm(st));
        Dimension(dc, P(b, 0) + new Vector(26, 0), P(b, t) + new Vector(26, 0), Cm(t), true);
        Dimension(dc, P(b, t) + new Vector(26, 0), P(b, t + h) + new Vector(26, 0), Cm(h), true);
        if (Data["reinforcement"].B("two_zones"))
        {
            double split = Data["reinforcement"].D("lower_height"), lap = Schedule.Bars.Where(x => x.Kind == "Principale").Max(x => x.Lap) / 1000;
            var p = P(a, t + split + lap / 2); var q = P(a + s, t + split - lap / 2);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(28, 232, 139, 29)), null, new(p, q));
            Text(dc, "l₀ " + Cm(lap), P(a + s, 0).X + 4, (p.Y + q.Y) / 2 - 7, 10, Grey, 60);
        }
        Text(dc, "Quote in cm. Punti neri: ferri longitudinali nella posizione vera, con una barra in ogni angolo di forcelle e risvolti.", 40, 690, 11, Grey, 540);
    }
    /// <summary>The point of a position where its leader ends: the middle of its longest straight leg, or the middle bar of a layer.</summary>
    static (double X, double Y) Anchor(WallModel.ScheduledBar bar)
    {
        if (bar.Kind == "Longitudinale" && bar.SectionPoints.Count > 0) { var p = bar.SectionPoints[bar.SectionPoints.Count / 2]; return (p.X, p.Y); }
        if (bar.Kind == "Spillo" && bar.SectionPoints.Count > 0)
        { var row = bar.SectionPoints[0]; return (bar.Shape.Min(p => p.X) + .3 * (bar.Shape.Max(p => p.X) - bar.Shape.Min(p => p.X)), row.Y); }
        var leg = bar.Legs.Where(l => !l.Arc && l.From is not null && l.To is not null).OrderByDescending(l => l.Length).FirstOrDefault();
        if (leg is null) { var p = bar.Shape[bar.Shape.Count / 2]; return (p.X, p.Y); }
        // Two thirds along the leg, so that call-outs of parallel bars do not meet at the same point.
        return (leg.From!.X + (leg.To!.X - leg.From.X) * .62, leg.From.Y + (leg.To.Y - leg.From.Y) * .62);
    }

    void Summary(DrawingContext dc)
    {
        double x0 = 760;
        Text(dc, "RIEPILOGO PER DIAMETRO", x0, 124, 13, Ink, bold: true);
        double[] x = [x0, x0 + 70, x0 + 160, x0 + 230];
        string[] headers = ["Ø [mm]", "L totale [m]", "kg/m", "Peso [kg]"];
        for (int i = 0; i < headers.Length; i++) Text(dc, headers[i], x[i], 150, 11, Ink, bold: true);
        double y = 172;
        foreach (var d in Schedule.Diameters)
        {
            string[] values = [N(d.Diameter), N(d.Length, "0.00"), N(d.KgPerMetre, "0.000"), N(d.Weight, "0.0")];
            for (int i = 0; i < values.Length; i++) Text(dc, values[i], x[i], y, 11, Ink);
            dc.DrawLine(Rule, new(x0, y + 19), new(1080, y + 19)); y += 23;
        }
        Text(dc, $"Peso totale   {N(Schedule.KnownWeight, "0.0")} kg", x0, y + 8, 14, Ink, 320, true);
        Text(dc, $"Calcestruzzo del tratto   {N(Schedule.ConcreteVolume, "0.00")} m³", x0, y + 32, 12, Ink, 320);
        Text(dc, $"Incidenza   {N(Schedule.Incidence, "0")} kg/m³", x0, y + 52, 14, Ink, 320, true);
        y += 90;
        Text(dc, "POSIZIONI", x0, y, 13, Ink, bold: true); y += 22;
        foreach (var bar in Schedule.Bars)
        {
            Text(dc, $"pos. {bar.Mark}", x0, y, 10, Colour(bar), 50, true);
            Text(dc, Describe(bar), x0 + 48, y, 10, Ink, 275); y += 15;
            if (y > 560) break;
        }
        var warnings = Schedule.Warnings.Where(w => w.StartsWith("Risultato del muro", StringComparison.Ordinal)).ToArray();
        if (warnings.Length > 0) Text(dc, warnings[0], x0, 640, 11, Ui.Brush("#AA3E26"), 320, true);
    }
    internal static string Describe(WallModel.ScheduledBar bar) => WallModel.BarPositionName(bar);

    void Table(DrawingContext dc, IReadOnlyList<WallModel.ScheduledBar> rows)
    {
        double[] x = [35, 95, 515, 575, 640, 745, 850, 950];
        string[] headers = ["Pos.", "Sagoma (quote in cm)", "Ø [mm]", "n°", "L unit. [cm]", "L totale [m]", "Peso [kg]", "Note"];
        for (int i = 0; i < headers.Length; i++) Text(dc, headers[i], x[i] + 4, 124, 11, Ink, i == 1 ? 400 : i == 7 ? 130 : 110, true);
        dc.DrawLine(Thin, new(35, 144), new(1085, 144));
        double rowHeight = 95, y = 146;
        foreach (var bar in rows)
        {
            var colour = Colour(bar);
            Text(dc, bar.Mark, x[0] + 14, y + 36, 15, colour, bold: true);
            Sketch(dc, bar, new Rect(x[1] + 6, y + 6, x[2] - x[1] - 16, rowHeight - 14), colour);
            string[] values = [N(bar.Diameter), bar.Quantity.ToString(It), bar.CutLength is double l ? Cm(l) : "—", bar.TotalLength is double total ? N(total, "0.00") : "—", bar.Weight is double kg ? N(kg, "0.0") : "—"];
            for (int i = 0; i < values.Length; i++) Text(dc, values[i], x[i + 2] + 4, y + 38, 12, Ink);
            Text(dc, bar.Note, x[7] + 4, y + 6, 9.5, Grey, 132);
            dc.DrawLine(Rule, new(35, y + rowHeight), new(1085, y + rowHeight)); y += rowHeight;
        }
        foreach (double v in x.Skip(1)) dc.DrawLine(Rule, new(v, 144), new(v, y));
    }
    /// <summary>Shape not to scale: the straight legs in their directions (arcs as corners), lengths compressed when they are very
    /// different so that short legs stay readable, each with its length in cm; a bend without radius shows its angle. Tall shapes (stem
    /// bars) are drawn lying down, as usual in a bar schedule.</summary>
    static void Sketch(DrawingContext dc, WallModel.ScheduledBar bar, Rect box, Brush colour)
    {
        var legs = bar.Legs.Where(l => !l.Arc && l.From is not null && l.To is not null && l.Length > 0).ToList();
        if (legs.Count == 0) return;
        double longest = legs.Max(l => l.Length), shortest = legs.Min(l => l.Length), power = longest > 5 * shortest ? .5 : 1;
        var directions = legs.Select(l => { var d = new Vector(l.To!.X - l.From!.X, -(l.To.Y - l.From.Y)); d.Normalize(); return d; }).ToList();
        double spanX = Math.Abs(legs.Zip(directions, (l, d) => d.X * l.Length).Sum()), spanY = legs.Zip(directions, (l, d) => Math.Abs(d.Y) * l.Length).Sum();
        if (spanY > 2.5 * Math.Max(spanX, legs.Zip(directions, (l, d) => Math.Abs(d.X) * l.Length).Max())) directions = directions.Select(d => new Vector(-d.Y, d.X)).ToList();
        var points = new List<Point> { new(0, 0) }; var labels = new List<(Point At, Vector Normal, string Text)>();
        for (int i = 0; i < legs.Count; i++)
        {
            var leg = legs[i]; var d = directions[i];
            double drawn = Math.Max(12, 110 * Math.Pow(leg.Length / longest, power));
            var start = points[^1]; var end = start + d * drawn; points.Add(end);
            labels.Add((start + (end - start) / 2, new Vector(d.Y, -d.X), Cm(leg.Length) + (leg.Angle > .05 && leg.Angle < 179 ? $" ({N(leg.Angle, "0.#")}°)" : "")));
        }
        double minX = points.Min(p => p.X), maxX = points.Max(p => p.X), minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
        double f = Math.Min(2.6, Math.Min((box.Width - 90) / Math.Max(1, maxX - minX), (box.Height - 36) / Math.Max(1, maxY - minY)));
        var origin = new Point(box.X + 45 + (box.Width - 90 - (maxX - minX) * f) / 2 - minX * f, box.Y + 18 + (box.Height - 36 - (maxY - minY) * f) / 2 - minY * f);
        Point T(Point p) => new(origin.X + p.X * f, origin.Y + p.Y * f);
        var centre = T(new((minX + maxX) / 2, (minY + maxY) / 2));
        dc.DrawGeometry(null, new Pen(colour, 2), Path(points.Select(T), false));
        foreach (var (at, normal, text) in labels)
        {
            // Label on the outer side of the leg (away from the centre of the shape).
            var p = T(at); var n = normal; if ((p - centre) * n < 0) n = -n;
            var q = p + n * 11; double width = text.Length * 6.2;
            Text(dc, text, q.X - width / 2 + n.X * width / 2, q.Y - 8 + n.Y * 4, 10.5, Ink);
        }
        if (bar.Kind == "Longitudinale" && bar.Pieces > 1) Text(dc, $"× {bar.Pieces} pezzi per filo, l₀ = {Cm(bar.Lap / 1000)}", box.X + 45, box.Bottom - 14, 9.5, Grey);
    }
    void Notes(DrawingContext dc)
    {
        Text(dc, "Il calcolo del muro e le verifiche delle sezioni sono riferiti a una striscia di 1 m; la lunghezza del tratto serve alla distinta. Le barre principali hanno passo 1000/n: nel tratto il numero dei pezzi è arrotondato per eccesso con una barra a ogni estremità, così che il passo non superi quello verificato.", 40, 130, 13, Ink, 1025);
        Text(dc, "Piedi del fusto incrociati in fondazione (nodo in apertura, EN 1992-1-1 Allegato J): le barre di monte piegano verso valle, quelle di valle verso monte, appoggiate sulla rete inferiore. Barre di fondazione uniche fra le due mensole quando hanno lo stesso Ø e numero e stanno nella barra commerciale, con risvolti ai bordi liberi; forcelle in testa al fusto con gambe pari a due volte lo spessore in testa (EN 1992-1-1 §9.3.1.4, fig. 9.8). Spilli della giunzione con ganci a 135° prolungati di 10Ø (NTC 2018 §7.4.6.2.2).", 40, 192, 13, Ink, 1025);
        Text(dc, "Ferri longitudinali nello strato interno, una barra in ogni angolo; oltre la barra commerciale in pezzi uguali con sovrapposizione l₀ calcolata come per le verticali. Quote all’asse con gli archi sviluppati al raggio (mandrino + Ø)/2, arrotondate al cm; L unitaria = somma dei tratti. Peso netto, senza sfridi.", 40, 288, 13, Ink, 1025);
        double y = 360;
        foreach (string warning in Schedule.Warnings.Skip((Page - ShapePages - 1) * 8).Take(8))
        {
            var text = new FormattedText("• " + warning, It, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Ink, 1) { MaxTextWidth = 1025 };
            dc.DrawText(text, new(40, y)); y += text.Height + 10;
        }
    }
    static void Dimension(DrawingContext dc, Point p, Point q, string label, bool vertical = false)
    {
        dc.DrawLine(Thin, p, q); var v = vertical ? new Vector(4, 0) : new Vector(0, 4);
        dc.DrawLine(Thin, p - v, p + v); dc.DrawLine(Thin, q - v, q + v);
        if (vertical) { dc.PushTransform(new RotateTransform(-90, p.X + 4, (p.Y + q.Y) / 2)); Text(dc, label, p.X + 4 - 10, (p.Y + q.Y) / 2 - 2, 11, Ink); dc.Pop(); }
        else Text(dc, label, (p.X + q.X) / 2 - 12, p.Y - 16, 11, Ink);
    }
}
