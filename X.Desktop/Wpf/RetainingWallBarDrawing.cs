using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed class RetainingWallBarDrawing : DrawingView
{
    internal static readonly Size SheetSize = new(1120, 790);
    internal JsonObject Data { get; }
    internal RetainingWall.BarSchedule Schedule { get; }
    internal int Page { get; set; }
    int ShapePages => (int)Math.Ceiling(Schedule.Bars.Count / 4d);
    internal int PageCount => 1 + ShapePages + Math.Max(1, (int)Math.Ceiling(Schedule.Warnings.Count / 8d));
    static readonly Brush Ink = Brushes.Black, Blue = Ui.Brush("#165DA0"), Red = Ui.Brush("#AA3E26");
    static readonly Pen Thin = new(Brushes.Gray, .7), Outline = new(Ink, 1.3);
    static string N(double v) => v.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"));
    internal RetainingWallBarDrawing(JsonObject data, RetainingWall.BarSchedule schedule)
    { Data = data; Schedule = schedule; Width = SheetSize.Width; Height = SheetSize.Height; }
    internal void RenderPage(DrawingContext dc, Size size) => Render(dc, size);
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new(0, 0, size.Width, size.Height));
        dc.PushTransform(new ScaleTransform(size.Width / SheetSize.Width, size.Height / SheetSize.Height));
        dc.DrawRectangle(null, Thin, new(18, 18, 1084, 754));
        Text(dc, "ANTHEA   /   MURO A MENSOLA", 35, 30, 15, Ink, bold: true);
        Text(dc, Page == 0 ? "Sezione armata e distinta ferri" : Page > ShapePages ? "Criteri della distinta e controlli da completare" : "Sagome delle barre e sviluppi", 35, 53, 23, Ink, bold: true);
        var m = Data["materials"]!;
        Text(dc, $"Tratto {N(Schedule.PanelLength)} m  ·  {m.S("materiale_cls_nome")}  ·  {m.S("materiale_acciaio_nome")}  ·  cnom {m.S("cover")} mm  ·  c estremità {N(Schedule.EndCover)} mm", 35, 89, 12, Ink);
        dc.DrawLine(Thin, new(35, 113), new(1085, 113));
        if (Page == 0) { Section(dc); Inventory(dc); }
        else if (Page > ShapePages) Notes(dc);
        else
        {
            var slice = Schedule.Bars.Skip((Page - 1) * 4).Take(4).ToArray();
            for (int i = 0; i < slice.Length; i++) Card(dc, slice[i], new(35 + (i % 2) * 532, 130 + (i / 2) * 293, 518, 279));
        }
        dc.DrawLine(Thin, new(35, 724), new(1085, 724));
        Text(dc, "DISTINTA PRELIMINARE  ·  Quote geometriche in mm all’asse delle barre  ·  Sviluppi in m  ·  Disegni non in scala", 35, 739, 11, Ink);
        Text(dc, $"{Page + 1} / {PageCount}", 1030, 739, 12, Ink);
        dc.Pop();
    }
    void Section(DrawingContext dc)
    {
        var g = Data["geometry"]!; double a = g.D("toe"), s = g.D("stem_base"), st = g.D("stem_top"), heel = g.D("heel"), t = g.D("slab"), h = g.D("height"), b = a + s + heel;
        double scale = Math.Min(350 / b, 390 / (t + h)), left = 98, bottom = 562;
        Point P(double x, double y) => new(left + x * scale, bottom - y * scale);
        Text(dc, "SEZIONE TRASVERSALE", 40, 128, 13, Ink, bold: true);
        Text(dc, "VALLE", left, 157, 11, Ink); Text(dc, "MONTE", left + b * scale - 50, 157, 11, Ink);
        dc.DrawGeometry(Ui.Brush("#F2F4F6"), Outline, Path(new[] { P(0, 0), P(b, 0), P(b, t), P(a + s, t), P(a + s, t + h), P(a + s - st, t + h), P(a, t), P(0, t) }, true));
        var main = Schedule.Bars.Where(v => v.Kind == "Principale").ToArray();
        int valley = 0, mountain = 0;
        foreach (var bar in main)
        {
            var color = bar.Face is "valle" or "superiore" ? Red : Blue;
            dc.DrawGeometry(null, new Pen(color, 1.8), Path(bar.Shape.Select(q => P(q.X, q.Y)), false));
            bool lhs = bar.Face is "valle" or "inferiore";
            int order = lhs ? valley++ : mountain++;
            double y = 221 + order * 66, x = lhs ? 38 : 428;
            var point = P((bar.Shape[0].X + bar.Shape[1].X) / 2, (bar.Shape[0].Y + bar.Shape[1].Y) / 2);
            dc.DrawLine(new Pen(color, .8), point, new(lhs ? x + 47 : x - 6, y + 9));
            Text(dc, bar.Mark + " Ø" + N(bar.Diameter), x, y, 12, color, 85, true);
        }
        if (Data["reinforcement"].B("two_zones"))
        {
            double split = Data["reinforcement"].D("lower_height"), lap = main.Max(x => x.Lap) / 1000;
            var p = P(a, t + split + lap / 2); var q = P(a + s, t + split - lap / 2);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(28, 232, 139, 29)), null, new(p, q));
            Text(dc, "l₀ " + N(lap * 1000), left + b * scale + 4, p.Y - 18, 11, Red, 100);
        }
        Dimension(dc, P(0, 0) + new Vector(0, 28), P(b, 0) + new Vector(0, 28), N(b * 1000));
        Dimension(dc, P(0, 0) + new Vector(0, 51), P(a, 0) + new Vector(0, 51), N(a * 1000));
        Dimension(dc, P(a, 0) + new Vector(0, 51), P(a + s, 0) + new Vector(0, 51), N(s * 1000));
        Dimension(dc, P(a + s, 0) + new Vector(0, 51), P(b, 0) + new Vector(0, 51), N(heel * 1000));
        Dimension(dc, P(a + s - st, t + h) + new Vector(0, -20), P(a + s, t + h) + new Vector(0, -20), N(st * 1000));
        Dimension(dc, new(77, bottom), new(77, bottom - t * scale), N(t * 1000), true);
        Dimension(dc, new(58, bottom - t * scale), new(58, bottom - (t + h) * scale), N(h * 1000), true);
        Text(dc, "P: principali in sezione. S: secondarie lungo muro, fuori piano. C: collegamenti. Sagome e marche nelle pagine successive.", 40, 643, 12, Ink, 470);
    }
    void Inventory(DrawingContext dc)
    {
        Text(dc, "QUANTITÀ DEL TRATTO", 560, 128, 13, Ink, bold: true);
        double[] x = [560, 620, 670, 730, 815, 902, 1008];
        string[] headers = ["Marca", "Ø", "Pezzi", "L / pezzo", "L totale", "Peso kg", "Tipo"];
        for (int i = 0; i < headers.Length; i++) Text(dc, headers[i], x[i], 157, 11, Ink, bold: true);
        double y = 184;
        foreach (var bar in Schedule.Bars)
        {
            string[] values = [bar.Mark, N(bar.Diameter), bar.Quantity.ToString(), bar.CutLength is double l ? N(l) : "da definire", bar.TotalLength is double total ? N(total) : "—", bar.Weight is double kg ? N(kg) : "—", bar.Kind == "Principale" ? "P" : bar.Kind == "Secondaria" ? "S" : "C"];
            for (int i = 0; i < values.Length; i++) Text(dc, values[i], x[i], y, 11, Ink);
            dc.DrawLine(Thin, new(560, y + 20), new(1080, y + 20)); y += 25;
        }
        Text(dc, $"{(Schedule.CompleteQuantities ? "Peso delle barre definite" : "Peso parziale delle barre definite")}: {N(Schedule.KnownWeight)} kg", 560, y + 16, 14, Ink, 515, true);
        Text(dc, $"Barra commerciale {N(Schedule.StockLength)} m. Quantità nette, sfridi esclusi. I valori del tratto includono i pezzi alle estremità e non coincidono necessariamente con la stima continua kg/m del calcolo.", 560, y + 46, 12, Ink, 515);
    }
    void Card(DrawingContext dc, RetainingWall.ScheduledBar b, Rect box)
    {
        dc.DrawRectangle(null, Thin, box); double x = box.X + 13, y = box.Y + 10;
        Text(dc, $"{b.Mark}   Ø{N(b.Diameter)}   {b.Quantity} pezzi", x, y, 16, Blue, bold: true);
        Text(dc, RetainingWall.RebarZoneName(b.Zone) + " · " + b.Face + " · " + b.Kind, x, y + 25, 12, Ink, box.Width - 24);
        if (b.Shape.Count > 1)
        {
            double minX = b.Shape.Min(p => p.X), maxX = b.Shape.Max(p => p.X), minY = b.Shape.Min(p => p.Y), maxY = b.Shape.Max(p => p.Y);
            double scale = Math.Min(275 / Math.Max(.01, maxX - minX), 104 / Math.Max(.01, maxY - minY));
            Point P(RetainingWall.BarPoint p) => new(x + 48 + (p.X - minX) * scale, y + 161 - (p.Y - minY) * scale);
            dc.DrawGeometry(null, new Pen(Blue, 2), Path(b.Shape.Select(P), false));
            if (b.Mandrel > 0)
            {
                foreach (var (index, label) in new[] { (0, "A"), (1, "B"), (b.Shape.Count - 2, "C") })
                { var p = P(b.Shape[index]); var q = P(b.Shape[index + 1]); Text(dc, label, (p.X + q.X) / 2 + 5, (p.Y + q.Y) / 2 - 15, 12, Ink); }
            }
            else Text(dc, "A = " + N(b.CutLength!.Value * 1000), x + 130, y + 115, 12, Ink);
        }
        else Text(dc, "Sagoma e ganci da definire", x + 70, y + 100, 16, Red);
        string legs = string.Join("   ·   ", b.Legs.Select(l => l.Name + "=" + N(l.Length * 1000)));
        Text(dc, legs, x, y + 176, 11, Ink, box.Width - 24);
        Text(dc, $"L={(b.CutLength is double cut ? N(cut) + " m" : "da definire")}   ·   passo {N(b.Spacing)} mm" + (b.Mandrel > 0 ? "   ·   mandrino " + N(b.Mandrel) + " mm" : "") + (b.Lap > 0 ? "   ·   l₀ " + N(b.Lap) + " mm" : ""), x, y + 198, 12, Ink, box.Width - 24);
        Text(dc, b.Note, x, y + 228, 10, Ink, box.Width - 24);
    }
    void Notes(DrawingContext dc)
    {
        Text(dc, "Il calcolo del muro e le verifiche delle sezioni rimangono riferiti a una striscia di 1 m. La lunghezza del tratto serve soltanto alla distinta. Il numero delle principali viene arrotondato per eccesso in modo che il passo nel tratto non superi quello verificato.", 40, 137, 15, Ink, 1025);
        Text(dc, "Secondarie: barre rettilinee lungo il muro su entrambe le facce. Al cambio di zona e al centro del fusto in fondazione la barra di confine viene attribuita a una sola zona. Le quantità dei collegamenti dipendono dal numero di file nella sovrapposizione e dai collegamenti per metro assegnati.", 40, 221, 15, Ink, 1025);
        double y = 314;
        foreach (string warning in Schedule.Warnings.Skip((Page - ShapePages - 1) * 8).Take(8))
        {
            var text = new FormattedText("• " + warning, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Ink, 1) { MaxTextWidth = 1025 };
            dc.DrawText(text, new(40, y)); y += text.Height + 10;
        }
    }
    static void Dimension(DrawingContext dc, Point p, Point q, string label, bool vertical = false)
    {
        dc.DrawLine(Thin, p, q); var v = vertical ? new Vector(4, 0) : new Vector(0, 4);
        dc.DrawLine(Thin, p - v, p + v); dc.DrawLine(Thin, q - v, q + v);
        if (vertical) { dc.PushTransform(new RotateTransform(-90, p.X - 16, (p.Y + q.Y) / 2)); Text(dc, label, p.X - 16, (p.Y + q.Y) / 2, 11, Ink); dc.Pop(); }
        else Text(dc, label, (p.X + q.X) / 2 - 14, p.Y - 17, 11, Ink);
    }
}
