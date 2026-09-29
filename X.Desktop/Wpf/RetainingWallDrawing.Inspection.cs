using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallDrawing
{
    internal bool ShowLoads = true, ShowLabels = true, ShowRebar = true, ShowPressures = true, CombinedLoads;
    internal string Mode = "Geometria e carichi", Member = "Fusto";
    internal string? SelectedAction;
    internal int SelectedLayer = -1, SelectedPressure = -1;
    internal event Action<string>? Inspected;
    internal event Action<string>? ActionSelected;
    internal static readonly string[] LayerColors = ["#DBCDAE", "#B9D6C6", "#D2C2DC", "#D9C3A7", "#B9CFDC", "#DAD6A8"];
    private readonly List<(Rect Area, string Text, string? Id)> hits = [];
    private readonly List<(Rect Area, Func<Point,string> Read)> plots = [];
    internal int InspectionCount => hits.Count + plots.Count;
    internal RetainingWallDrawing()
    {
        ClipToBounds = true;
        MouseMove += (_, e) =>
        {
            var p = e.GetPosition(this);
            foreach (var plot in plots) if (plot.Area.Contains(p)) { ToolTip = plot.Read(p); Inspected?.Invoke((string)ToolTip); return; }
            foreach (var hit in hits.AsEnumerable().Reverse()) if (hit.Area.Contains(p)) { ToolTip = hit.Text; Inspected?.Invoke(hit.Text); Cursor = hit.Id is null ? Cursors.Cross : Cursors.Hand; return; }
            ToolTip = null; Cursor = Cursors.Arrow;
        };
        MouseLeftButtonDown += (_, e) => { var p = e.GetPosition(this); foreach (var hit in hits.AsEnumerable().Reverse()) if (hit.Id is not null && hit.Area.Contains(p)) { ActionSelected?.Invoke(hit.Id); return; } };
    }
    private static void WrappedText(DrawingContext dc, string text, double x, double y, double width)
    {
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Ui.Navy, 1) { MaxTextWidth = Math.Max(20, width) }; dc.DrawText(ft, new Point(x, y));
    }
    private static void Arrow(DrawingContext dc, Point from, Point to, Brush brush, double thickness = 1.6)
    {
        var pen = new Pen(brush, thickness); dc.DrawLine(pen, from, to); var v = from - to; if (v.Length < .1) return; v.Normalize(); var n = new Vector(-v.Y, v.X); dc.DrawLine(pen, to, to + v * 7 + n * 3); dc.DrawLine(pen, to, to + v * 7 - n * 3);
    }
    private void DrawRebarData(DrawingContext dc, Func<double,double,Point> p, double h, double t, double a, double back, double cover)
    {
        var d = Data!; bool two = d["reinforcement"].B("two_zones"); double lower = two ? d["reinforcement"].D("lower_height") : h;
        foreach (var (key, y0, y1) in new[] { ("stem", 0d, lower), ("stem_upper", lower, h) })
        {
            if (y1 <= y0) continue; var color = key == "stem" ? Ui.Brush("#B24F43") : Ui.Brush("#715BA6");
            if (key == "stem_upper")
            {
                double x0 = back - d["geometry"].D("stem_top") - (d["geometry"].D("stem_base") - d["geometry"].D("stem_top")) * (1 - lower / h) + cover;
                dc.DrawLine(new Pen(color, 3), p(back - cover, t + lower), p(back - cover, t + h - cover));
                dc.DrawLine(new Pen(color, 3), p(x0, t + lower), p(back - d["geometry"].D("stem_top") + cover, t + h - cover));
                dc.DrawLine(new Pen(color, 1) { DashStyle = DashStyles.Dash }, p(a - .2, t + lower), p(back + .15, t + lower));
            }
            string label = RetainingWallWorkspace.RebarLabel(d, key); var mid = p(a, t + (y0 + y1) / 2);
            if (ShowLabels) Text(dc, $"{d["reinforcement"]![key].D("count"):0} Ø{d["reinforcement"]![key].D("diameter"):0}/m\nper faccia", Math.Max(2, mid.X - 60), mid.Y, color, 10);
            var rect = new Rect(p(a, t + y1), p(back, t + y0)); hits.Add((rect, label + $" · zona {y0:0.##}–{y1:0.##} m dal piede", null));
        }
        if (ShowLabels) Text(dc, $"Valle {d["reinforcement"]!["toe"].D("count"):0}Ø{d["reinforcement"]!["toe"].D("diameter"):0} · Monte {d["reinforcement"]!["heel"].D("count"):0}Ø{d["reinforcement"]!["heel"].D("diameter"):0} /m/faccia · c={d["materials"].D("cover"):0} mm", 10, ActualHeight - 62, Ui.Brush("#B24F43"), 10);
    }
    private void DrawUtilization(DrawingContext dc, Func<double,double,Point> p, double back, double ht, double right)
    {
        var stations = Case!.Sections.Where(f => f.Name == "Fusto").OrderBy(f => f.Position).ToArray();
        for (int j = 1; j < stations.Length; j++)
        {
            var f = stations[j]; var previous = stations[j - 1]; string location = "Fusto z=" + f.Position.ToString("0.00", CultureInfo.InvariantCulture) + " m";
            var local = Calculation!.Structural.Where(c => c.Name.StartsWith(location) && (c.Combination == Case.Name || c.Combination == "Dettagli")).ToArray();
            double ratio = local.Select(c => c.Status.StartsWith("Non soddisfatta") && c.Ratio is null ? double.PositiveInfinity : c.Ratio ?? 0).DefaultIfEmpty(0).Max(); bool missing = local.Length == 0 || local.Any(c => c.Ratio is null);
            var color = UtilizationPalette.Brush(ratio > 1 ? 2 : missing ? null : ratio);
            Polygon(dc, [p(back - previous.Thickness, ht - previous.Position), p(back, ht - previous.Position), p(back, ht - f.Position), p(back - f.Thickness, ht - f.Position)], color);
            hits.Add((new Rect(p(back - f.Thickness, ht - previous.Position), p(back, ht - f.Position)), location + $" · ηmax={ratio:0.000}" + (missing ? " · controlli da completare" : "") + "\n" + string.Join("; ", local.Select(c => c.Name.Split('·').Last() + ": " + (c.Ratio is double r ? r.ToString("0.000") : c.Status))), null));
        }
        double toe = Data!["geometry"].D("toe"), width = back + Data["geometry"].D("heel"), slab = Data["geometry"].D("slab");
        foreach (var (member, left, end) in new[] { ("Valle", 0d, toe), ("Monte", back, width) })
        {
            if (end <= left) continue;
            var local = Calculation!.Structural.Where(c => c.Name.StartsWith(member) && (c.Combination == Case.Name || c.Combination == "Dettagli")).ToArray();
            double ratio = local.Select(c => c.Status.StartsWith("Non soddisfatta") && c.Ratio is null ? double.PositiveInfinity : c.Ratio ?? 0).DefaultIfEmpty(0).Max(); bool missing = local.Length == 0 || local.Any(c => c.Ratio is null);
            var rect = new Rect(p(left, slab), p(end, 0)); var color = UtilizationPalette.Brush(ratio > 1 ? 2 : missing ? null : ratio); dc.DrawRectangle(color, null, rect);
            hits.Add((rect, $"{member}: ηmax={ratio:0.000}" + (missing ? " · verifiche da completare" : "") + "\n" + string.Join("; ", local.OrderByDescending(c => c.Ratio ?? double.PositiveInfinity).Take(3).Select(c => c.Name + " " + c.Status)), null));
        }
        Text(dc, "Tassi di lavoro · η = Ed/Rd", right + 5, 28, size: 11);
        var ranges = new (double? Value, string Label)[] { (.5, "0–0,50"), (.7, "0,50–0,70"), (.9, "0,70–0,90"), (1, "0,90–1,00"), (2, "> 1,00"), (null, "Controlli incompleti") };
        for (int i = 0; i < ranges.Length; i++)
        {
            double y = 50 + i * 18; dc.DrawRectangle(UtilizationPalette.Brush(ranges[i].Value), null, new Rect(right + 5, y, 14, 12));
            Text(dc, ranges[i].Label, right + 25, y - 1, size: 11);
        }
        hits.Add((new Rect(right + 5, 28, Math.Max(1, ActualWidth - right - 5), 132), UtilizationPalette.Legend + "\nGrigio: esito incompleto o non disponibile", null));
        WrappedText(dc, string.Join("\n", Calculation!.Checks.Where(c => c.Combination == Case.Name).Select(c => c.Name + "\n" + (c.Ratio is double r ? r.ToString("0.000") : c.Status))), right + 5, 172, ActualWidth - right - 10);
    }
    private void DrawResistance(DrawingContext dc, Func<double,double,Point> p, double width, double right)
    {
        var c = Case!; var color = Ui.Brush("#33836A"); Arrow(dc, p(.1, 0), p(width / 2, 0), color, 3); Arrow(dc, p(width / 2, -.5), p(width / 2, 0), color, 3);
        WrappedText(dc, $"Rd scorr.\n{c.SlidingResistance:0.00} kN/m\n\nMRd ribalt.\n{c.OverturningResistance:0.00} kNm/m\n\nRd portanza\n{(c.BearingResistance is double br ? br.ToString("0.00") + " kN/m" : "da verificare")}", right - 5, 30, ActualWidth - right);
        hits.Add((new Rect(0, ActualHeight - 110, ActualWidth, 100), $"V′tanδd/γR={c.SlidingResistance:0.000} kN/m; Mstab/γR={c.OverturningResistance:0.000} kNm/m; U={c.Uplift:0.000} kN/m; B′={c.EffectiveWidth:0.000} m; e={c.Eccentricity:0.000} m", null));
    }
    private void DrawLoads(DrawingContext dc, Func<double,double,Point> p, double back, double far, double ht)
    {
        if (Data?["actions"] is not JsonArray actions) return; int index = 0, uniform = 0;
        foreach (var a in actions)
        {
            index++; double factor = Case?.Actions.FirstOrDefault(x => x.Id == a.S("id"))?.Factor ?? 1;
            if (!a.B("visible") || !a.B("enabled") || a.D("value") == 0 || (CombinedLoads && (Case is null || factor == 0))) continue;
            string type = a.S("type"); var color = a.S("id") == SelectedAction ? Ui.Blue : a.S("category") == "A" ? Ui.Brush("#C24843") : Ui.Brush("#566991"); double thick = a.S("id") == SelectedAction ? 2.8 : 1.6;
            Point point = p(back, a.D("z")); Rect area;
            if (type == "Sovraccarico uniforme")
            {
                double shift = uniform++ * 17; var left = p(back + .05, ht); var right = p(far, ht);
                for (double xx = left.X + 8; xx < right.X; xx += Math.Max(16, (right.X - left.X) / 7)) Arrow(dc, new(xx, left.Y - 16 - shift), new(xx, left.Y - shift), color, thick);
                point = new(left.X, left.Y - 29 - shift); area = new Rect(left.X, point.Y, right.X - left.X, 30);
            }
            else if (type == "Pressione laterale")
            { var bottom = p(back, a.D("z0")); for (double yy = point.Y; yy <= bottom.Y; yy += 22) Arrow(dc, new(point.X + 33, yy), new(point.X, yy), color, thick); area = new Rect(point.X, point.Y - 14, 110, Math.Max(28, bottom.Y - point.Y)); point = new(point.X + 38, point.Y - 10); }
            else if (type == "Forza verticale")
            { point = p(a.D("x"), a.D("z")); Arrow(dc, new(point.X, point.Y - 35), point, color, thick); area = new Rect(point.X - 10, point.Y - 50, 105, 52); point = new(point.X + 6, point.Y - 35); }
            else if (type == "Momento")
            { dc.DrawEllipse(null, new Pen(color, thick), point, 13, 13); Arrow(dc, new(point.X + 9, point.Y - 11), new(point.X + 13, point.Y), color, thick); area = new Rect(point.X - 17, point.Y - 19, 115, 38); point = new(point.X + 17, point.Y - 15); }
            else { Arrow(dc, new(point.X + 42, point.Y), point, color, thick); area = new Rect(point.X, point.Y - 22, 145, 28); point = new(point.X + 44, point.Y - 17); }
            if (ShowLabels) Text(dc, $"{index}: {a.D("value") * (CombinedLoads ? factor : 1):0.##} {RetainingWall.ActionUnit(type)}", point.X, point.Y, color, 10);
            hits.Add((area, $"{index}. {a.S("name")} · {type} · {a.S("category")}\nValore={a.D("value"):0.###} {RetainingWall.ActionUnit(type)}; z={a.D("z"):0.###} m; x={a.D("x"):0.###} m; fattore combinazione={factor:0.###}; valore combinato={factor * a.D("value"):0.###}", a.S("id")));
        }
    }
}

