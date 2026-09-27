using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>View-only plots of the engine's actual trials; no calculation rules live here.</summary>
internal sealed class BridgeOptimizationPlot : FrameworkElement
{
    internal bool Scatter, ParetoOnly;
    internal int Variable, SelectedRank;
    internal string? FamilyFilter;
    internal event Action<int>? Selected;
    private BridgeConcept.OptimizationTrial[] trials = [];
    private BridgeConcept.OptimizationSolution[] solutions = [];
    private BridgeConcept.Result? baseline;
    private readonly List<(Point Point, BridgeConcept.OptimizationTrial Trial, int Rank)> hits = new();
    private static readonly Brush[] Palette = [Ui.Brush("#0B5CAD"), Ui.Brush("#168B94"), Ui.Brush("#8462AB"), Ui.Brush("#C26817"), Ui.Brush("#337E46"), Ui.Brush("#B44866"), Ui.Brush("#526680"), Ui.Brush("#98682D"), Ui.Brush("#557C19"), Ui.Brush("#0082C2"), Ui.Brush("#BF3737"), Ui.Brush("#5724A3"), Ui.Brush("#BD277A"), Ui.Brush("#1D5951")];
    private static readonly string[] FamilyLabels = ["Soletta", "Travi T", "Travi I c.a.p.", "Travi U", "Cassone c.a.p.", "Conci FCM", "Travi I acc.", "Cassone acc.", "Incorporate", "Ortotropa", "Arco", "Strallato", "Sospeso", "Reticolare"];
    internal static Brush FamilyBrush(string id) => Palette[Math.Max(0, Array.FindIndex(BridgeConcept.Families, f => f.Id == id)) % Palette.Length];
    internal int PointCount => hits.Count;
    internal IReadOnlyList<(Point Point, BridgeConcept.OptimizationTrial Trial, int Rank)> Points => hits;
    internal void SetData(BridgeConcept.OptimizationTrial[] attempted, BridgeConcept.OptimizationSolution[] admitted, BridgeConcept.Result? reference)
    { trials = attempted; solutions = admitted; baseline = reference; SelectedRank = 0; InvalidateVisual(); }
    internal BridgeOptimizationPlot()
    {
        Cursor = Cursors.Cross; ToolTipService.SetInitialShowDelay(this, 100); ToolTipService.SetShowDuration(this, 20000);
        MouseMove += (_, e) =>
        {
            var hit = Nearest(e.GetPosition(this));
            ToolTip = hit is { } p ? $"{(p.Rank > 0 ? "Soluzione #" + p.Rank : "Tentativo " + p.Trial.Iteration)} · {BridgeConcept.Families.Single(f => f.Id == p.Trial.Family).Name}\n" +
                $"{p.Trial.Spans} campate · d = {Value(p.Trial.Depth)} m · pali {Value(p.Trial.PileLength)} m\n{Value(p.Trial.Cost, "N0")} € · {Value(p.Trial.Carbon, "N1")} tCO₂e\n{p.Trial.Pier} · {p.Trial.Foundation} · {(p.Trial.Continuous ? "Continuo" : "Indipendente")}\n" +
                (p.Trial.Admissible ? "Ammessa dai filtri" : string.Join("; ", p.Trial.Exclusions)) : null;
        };
        MouseLeftButtonDown += (_, e) => SelectAt(e.GetPosition(this));
    }
    internal void SelectAt(Point location)
    { var hit = Nearest(location); if (Scatter && hit is { Rank: > 0 } p) Selected?.Invoke(p.Rank); }
    private (Point Point, BridgeConcept.OptimizationTrial Trial, int Rank)? Nearest(Point p)
    {
        if (hits.Count == 0) return null;
        var nearest = hits.OrderBy(h => (h.Point - p).LengthSquared).ThenBy(h => h.Rank).First();
        return (nearest.Point - p).Length <= 12 ? nearest : null;
    }
    private static string Value(double? value, string format = "N2") => value?.ToString(format, CultureInfo.GetCultureInfo("it-IT")) ?? "—";
    private double? Y(BridgeConcept.OptimizationTrial t) => Scatter ? t.Carbon : Variable switch {
        0 => t.Cost / 1e6, 1 => t.Carbon, 2 => t.Depth, 3 => t.Spans, 4 => t.PileLength,
        5 => Array.FindIndex(BridgeConcept.Families, f => f.Id == t.Family), 6 => t.Pier.StartsWith("Antenna") ? 4 : Array.IndexOf(BridgeConcept.Piers, t.Pier),
        7 => Array.IndexOf(BridgeConcept.Foundations, t.Foundation), _ => t.Continuous ? 1 : 0
    };
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); hits.Clear();
        double w = ActualWidth, h = ActualHeight; if (w < 180 || h < 150) return;
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
        bool categorical = !Scatter && Variable >= 5;
        double left = categorical ? 110 : 70, right = w - 25, top = 24, bottom = h - 46;
        var rankByTrial = solutions.ToDictionary(s => s.Trial.Iteration);
        var shown = (Scatter && solutions.Length > 0 ? solutions.Select(s => s.Trial) : trials.AsEnumerable())
            .Where(t => !Scatter || (t.Admissible && (FamilyFilter is null || t.Family == FamilyFilter)
                && (!ParetoOnly || rankByTrial.TryGetValue(t.Iteration, out var s) && s.Pareto)))
            .Where(t => Y(t) is double y && double.IsFinite(y) && (!Scatter || t.Cost is double cost && double.IsFinite(cost))).ToArray();
        if (shown.Length == 0) { Text(dc, trials.Length == 0 ? "Avvia la ricerca per popolare il grafico" : "Nessun punto con questi filtri", 18, h / 2, 12, Ui.Muted); return; }
        double X(BridgeConcept.OptimizationTrial t) => Scatter ? t.Cost!.Value / 1e6 : t.Iteration;
        double xmin = shown.Min(X), xmax = shown.Max(X), ymin = shown.Min(t => Y(t)!.Value), ymax = shown.Max(t => Y(t)!.Value);
        if (Scatter && baseline is not null) { xmin = Math.Min(xmin, baseline.TotalCost / 1e6); xmax = Math.Max(xmax, baseline.TotalCost / 1e6); ymin = Math.Min(ymin, baseline.Carbon); ymax = Math.Max(ymax, baseline.Carbon); }
        if (!Scatter) { xmin = 1; xmax = Math.Max(2, trials.LastOrDefault()?.Iteration ?? 2); }
        static void Expand(ref double min, ref double max)
        { double pad = max > min ? (max - min) * .06 : Math.Max(.1, Math.Abs(min) * .05); min -= pad; max += pad; }
        if (Scatter) Expand(ref xmin, ref xmax); Expand(ref ymin, ref ymax);
        Point P(double x, double y) => new(left + (x - xmin) / (xmax - xmin) * (right - left), bottom - (y - ymin) / (ymax - ymin) * (bottom - top));
        var grid = new Pen(Ui.Brush("#E3EAF1"), 1);
        string[] labels = Variable == 5 ? FamilyLabels : Variable == 6 ? ["Telaio", "Circolare", "Setto", "Martello", "Antenna"] : Variable == 7 ? ["Auto", "Diretta", "Pali Ø1,0", "Pali Ø1,5"] : ["Indipendente", "Continua"];
        if (categorical)
        {
            for (int k = (int)Math.Ceiling(ymin); k <= Math.Floor(ymax); k++)
            {
                double y = P(xmin, k).Y; dc.DrawLine(grid, new(left, y), new(right, y));
                Text(dc, k >= 0 && k < labels.Length ? labels[k] : k.ToString(), 2, y - 7, 10, Ui.Muted);
            }
        }
        else for (int k = 0; k <= 4; k++)
        {
            double v = ymin + (ymax - ymin) * k / 4, y = P(xmin, v).Y;
            dc.DrawLine(grid, new(left, y), new(right, y));
            Text(dc, Value(v, Math.Abs(ymax) >= 100 ? "N0" : "N2"), 2, y - 7, 10, Ui.Muted);
        }
        for (int k = 0; k <= 4; k++)
        {
            double v = xmin + (xmax - xmin) * k / 4, x = P(v, ymin).X;
            dc.DrawLine(grid, new(x, top), new(x, bottom));
            Text(dc, Value(v, Scatter ? "N2" : "N0"), x - 16, bottom + 7, 10, Ui.Muted);
        }
        Text(dc, Scatter ? "CO₂ [t]" : new[] { "Costo [M€]", "CO₂ [t]", "d [m]", "Campate [n.]", "L pali [m]", "Tipologia", "Pila", "Fondazione", "Continuità" }[Math.Clamp(Variable, 0, 8)], left, 2, 11, Ui.Navy);
        Text(dc, Scatter ? "Costo [M€]" : "Tentativo", Math.Max(left, right - 90), h - 18, 11, Ui.Navy);
        dc.PushClip(new RectangleGeometry(new Rect(left - 10, top - 10, right - left + 20, bottom - top + 20)));
        if (Scatter)
        {
            Point? prev = null;
            foreach (var t in shown.Where(t => rankByTrial.TryGetValue(t.Iteration, out var s) && s.Pareto).OrderBy(X))
            { var p = P(X(t), Y(t)!.Value); if (prev is { } q) dc.DrawLine(new Pen(Ui.Brush("#879AA9"), 1), q, p); prev = p; }
        }
        foreach (var t in shown.OrderBy(t => t.Admissible))
        {
            var p = P(X(t), Y(t)!.Value); var s = rankByTrial.GetValueOrDefault(t.Iteration);
            dc.DrawEllipse(t.Admissible ? FamilyBrush(t.Family) : Ui.Brush("#CCD3DD"), null, p, Scatter ? 3.1 : 2, Scatter ? 3.1 : 2);
            if (Scatter && s?.Pareto == true) dc.DrawEllipse(null, new Pen(FamilyBrush(t.Family), 1.2), p, 5.5, 5.5);
            hits.Add((p, t, s?.Rank ?? 0));
        }
        if (!Scatter && Variable is 0 or 1)
        {
            // Running minimum uses a fixed physical unit, so it remains comparable throughout the search.
            double best = double.PositiveInfinity; Point? prev = null;
            foreach (var t in shown.OrderBy(t => t.Iteration))
            {
                if (t.Admissible) best = Math.Min(best, Y(t)!.Value);
                if (!double.IsFinite(best)) continue;
                var p = P(X(t), best); if (prev is { } q) { dc.DrawLine(new Pen(Ui.Navy, 1.5), q, new(p.X, q.Y)); dc.DrawLine(new Pen(Ui.Navy, 1.5), new(p.X, q.Y), p); } prev = p;
            }
        }
        if (Scatter && baseline is not null)
        {
            var p = P(baseline.TotalCost / 1e6, baseline.Carbon); var pen = new Pen(Ui.Navy, 2);
            dc.DrawLine(pen, new(p.X - 5, p.Y - 5), new(p.X + 5, p.Y + 5)); dc.DrawLine(pen, new(p.X - 5, p.Y + 5), new(p.X + 5, p.Y - 5));
        }
        foreach (var hit in hits.Where(p => Scatter && (p.Rank == 1 || p.Rank == SelectedRank)))
        {
            if (hit.Rank == 1) Star(dc, hit.Point);
            if (hit.Rank == SelectedRank) dc.DrawEllipse(null, new Pen(Ui.Navy, 2), hit.Point, 9, 9);
        }
        dc.Pop();
    }
    private static void Star(DrawingContext dc, Point p)
    {
        var shape = new StreamGeometry(); using (var c = shape.Open())
        {
            for (int k = 0; k < 10; k++)
            {
                double a = k * Math.PI / 5 - Math.PI / 2, r = k % 2 == 0 ? 8 : 3.5;
                var q = new Point(p.X + Math.Cos(a) * r, p.Y + Math.Sin(a) * r);
                if (k == 0) c.BeginFigure(q, true, true); else c.LineTo(q, true, false);
            }
        }
        dc.DrawGeometry(Ui.Brush("#F3BE4A"), new Pen(Ui.Navy, 1), shape);
    }
    private static void Text(DrawingContext dc, string text, double x, double y, double size, Brush brush) =>
        dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1), new Point(x, y));
}
