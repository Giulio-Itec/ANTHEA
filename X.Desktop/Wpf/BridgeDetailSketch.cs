using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>Local elevation, intentionally schematic; transverse plate dimensions are drawn in the section above.</summary>
internal sealed class BridgeDetailSketch : FrameworkElement
{
    internal JsonObject? Input { get; set; }
    internal bool AtSupport { get; set; }
    internal List<string> Labels { get; } = [];
    internal BridgeDetailSketch() { Height = 165; MinWidth = 280; ClipToBounds = true; }
    protected override void OnRender(DrawingContext dc)
    {
        Labels.Clear(); if (Input is not {} d || ActualWidth < 100) return;
        double w = ActualWidth, x0 = 22, x1 = w - 22, mid = w / 2, top = 30, bottom = 92;
        var ink = Ui.Brush("#436581"); var pen = new Pen(ink, 1.4); var accent = Ui.Brush("#127A83");
        void Text(string text, double x, double y, Brush? brush = null, double size = 10, bool centered = false)
        {
            Labels.Add(text);
            var f = new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, brush ?? Ui.Navy, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            f.MaxTextWidth = Math.Max(50, centered ? w - 12 : w - x - 6);
            dc.DrawText(f, new Point(centered ? Math.Max(3, x - f.Width / 2) : x, y));
        }
        void Line(double x, bool selected = false)
        { dc.DrawLine(new Pen(selected ? accent : ink, selected ? 5 : 2), new Point(x, top), new Point(x, bottom)); }
        void Dim(double a, double b, double y, string label)
        {
            var p = new Pen(Ui.Muted, .8); dc.DrawLine(p, new(a, y), new(b, y));
            foreach (double x in new[] { a, b }) dc.DrawLine(p, new(x, y - 3), new(x, y + 3));
            Text(label, (a + b) / 2, y + 3, centered: true);
        }
        dc.DrawRoundedRectangle(Ui.Brush("#F1F6FA"), null, new Rect(0, 0, w, Height), 6, 6);
        Text("PROSPETTO · schema non in scala", 10, 5, Ui.Muted, 9);
        bool enabled = d.B(AtSupport ? "appoggio" : "irrigidimenti");
        if (!enabled) { Text(AtSupport ? "Appoggio non inserito" : "Intermedi assenti · pannello lungo", 15, 64, Ui.Muted, 12); return; }
        bool endLeft = AtSupport && d.S("pos_app") == BridgeSection.SupportLocations[1];
        bool endRight = AtSupport && d.S("pos_app") == BridgeSection.SupportLocations[2];
        if (endLeft) mid = w * .26; if (endRight) mid = w * .74;
        dc.DrawRectangle(Ui.Brush("#DCE7EF"), pen, new Rect(x0, top, x1 - x0, bottom - top));
        dc.DrawLine(new Pen(ink, 3), new(x0, top), new(x1, top)); dc.DrawLine(new Pen(ink, 3), new(x0, bottom), new(x1, bottom));
        if (!endLeft) Line(x0 + 5); if (!endRight) Line(x1 - 5); Line(mid, true);
        string F(double v) => EngineeringFormat.Number(v);
        if (AtSupport)
        {
            var triangle = new StreamGeometry(); using (var ctx = triangle.Open())
            { ctx.BeginFigure(new(mid, bottom + 3), true, true); ctx.LineTo(new(mid - 10, bottom + 20), true, false); ctx.LineTo(new(mid + 10, bottom + 20), true, false); }
            dc.DrawGeometry(Brushes.White, new Pen(accent, 1.3), triangle);
            dc.DrawLine(new Pen(accent, 1.3), new(mid - 15, bottom + 23), new(mid + 15, bottom + 23));
            Text("R = " + (J.Number(d["R_app"]) is {} r ? F(r) + " kN" : "da assegnare"), mid, 140, accent, 10, true);
            if (endLeft || endRight)
            {
                double tip = endLeft ? x0 : x1, far = endLeft ? x1 : x0;
                Dim(Math.Min(tip, mid), Math.Max(tip, mid), 112, "c=" + F(d.D("c_app")));
                Dim(Math.Min(mid, far), Math.Max(mid, far), 112, "a=" + F(d.D(endLeft ? "a_app_dx" : "a_app_sx")));
                if (d.B("terminale_rigido"))
                {
                    double second = mid + (endLeft ? 1 : -1) * (x1 - x0) * .18; Line(second);
                    Dim(Math.Min(mid, second), Math.Max(mid, second), 48, "e=" + F(d.D("e_term")));
                    Text("2 coppie", (mid + second) / 2, 72, size: 9, centered: true);
                }
            }
            else
            { Dim(x0, mid - 17, 118, "aL=" + F(d.D("a_app_sx"))); Dim(mid + 17, x1, 118, "aR=" + F(d.D("a_app_dx"))); }
        }
        else
        {
            Dim(x0, mid, 108, "aL=" + F(d.D("a_irr")));
            Dim(mid, x1, 108, "aR=" + F(d.B("pannelli_uguali") ? d.D("a_irr") : d.D("a_irr_dx")));
            Text(d.S("lati_irr") + " · dimensioni in mm", mid, 142, Ui.Muted, 10, true);
        }
    }
}
