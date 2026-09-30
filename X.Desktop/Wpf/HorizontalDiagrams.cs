using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>Coordinated depth plots. Dashed lower branches are equilibrium completions.</summary>
internal sealed class HorizontalDiagrams(JsonObject result, int surveyIndex, bool soilView) : DrawingView
{
    private sealed record Curve(string Label, string Key, string Color, bool Signed = false, bool Dashed = false, double Multiplier = 1);
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        if (size.Width < 700 || size.Height < 450) return;
        var s = result.Array("sondaggi")[surveyIndex]!; var g = result["input"]!["generali"]!;
        double length = g.D("lunghezza"), diameter = g.D("diametro");
        var ground = s.Array("diagramma_terreno"); var actual = s.Array("diagrammi");
        double? hinge = StratigraphyDrawing.ReactionLimit(s);
        const double top = 159, left = 142;
        double bottom = size.Height - 152, right = size.Width - 18;
        double Y(double z) => top + z / length * (bottom - top);
        string F(double n) => Math.Abs(n) >= 10000 ? n.ToString("0.##E+0") : n.ToString("0.##");
        Text(dc, soilView ? "Terreno · dalle tensioni verticali alla reazione laterale" : "Palo · equilibrio delle reazioni e sollecitazioni", 16, 10, 18, Ui.Navy, size.Width - 32, true);
        Text(dc, $"Sondaggio {surveyIndex + 1} · {s.S("meccanismo")} · Hu = {s.D("capacita_kn"):N2} kN · D = {diameter:0.###} m · {result.S("metodo_calcolo")}", 16, 39, 12, width: size.Width - 32);
        Text(dc, "Stato limite alla capacità Hu. z dal piano campagna, positiva verso il basso.", 16, 60, 11, width: size.Width - 32);
        Text(dc, "Strati / z [m]", 16, 96, 12, Ui.Navy, 120, true);
        var layers = result["input"].Array("stratigrafie")[surveyIndex]!.AsArray(); double layerTop = 0;
        for (int i = 0; i < layers.Count && layerTop < length; i++)
        {
            double end = Math.Min(length, layerTop + layers[i].D("spessore"));
            var color = Ui.Brush(StratigraphyDrawing.LayerColors[i % StratigraphyDrawing.LayerColors.Length].ToString()!);
            dc.DrawRectangle(color, null, new Rect(45, Y(layerTop), 25, Y(end) - Y(layerTop)));
            var wash = color.Clone(); wash.Opacity = .065;
            dc.DrawRectangle(wash, null, new Rect(left, Y(layerTop), right - left, Y(end) - Y(layerTop)));
            dc.DrawLine(new Pen(Ui.Brush("#CBD5E1"), .8), new Point(left, Y(layerTop)), new Point(right, Y(layerTop)));
            Text(dc, F(layerTop), 8, Y(layerTop) - 7, 10, width: 35);
            if (Y(end) - Y(layerTop) >= 27)
            {
                Text(dc, $"{i + 1} · {(layers[i].S("tipologia") == "Coesivo" ? "Coesivo" : "Granulare")}", 75, (Y(layerTop) + Y(end)) / 2 - 13, 10, Ui.Navy, 65);
            }
            layerTop = end;
        }
        Text(dc, F(length), 8, bottom - 7, 10, width: 35);
        if (hinge is double zh)
        {
            var shade = Ui.Brush("#C4B5D4").Clone(); shade.Opacity = .16;
            dc.DrawRectangle(shade, null, new Rect(left, Y(zh), right - left, bottom - Y(zh)));
        }
        if (layers.Any(l => l.S("tipologia") == "Coesivo") && 1.5 * diameter < length)
        {
            Text(dc, $"1,5D = {1.5 * diameter:0.###} m\nTaglio nei coesivi", 16, 115, 10, Ui.Brush("#B45309"), 123);
            dc.DrawLine(new Pen(Ui.Brush("#B45309"), .7) { DashStyle = DashStyles.Dot }, new Point(42, Y(1.5 * diameter)), new Point(right, Y(1.5 * diameter)));
        }
        var panels = soilView
            ? new[] {
                ("Tensioni verticali [kPa]", new[] { new Curve("σv totale", "sigma_v_kpa", "#64748B"), new Curve("u idrostatica", "u_kpa", "#0284C7"), new Curve("σ′v efficace", "sigma_eff_kpa", "#B45309") }),
                ("Pressione equivalente [kPa]", new[] { new Curve("± q_lim = ± p_lim / D", "q_lim_kpa", "#94A3B8", Dashed:true), new Curve("", "q_lim_kpa", "#94A3B8", Dashed:true, Multiplier:-1), new Curve("q = p / D alla Hu", "q_kpa", "#15803D", Signed:true) }),
                ("Reazione lineare [kN/m]", new[] { new Curve("± p_lim disponibile", "p_lim_kn_m", "#94A3B8", Dashed:true), new Curve("", "p_lim_kn_m", "#94A3B8", Dashed:true, Multiplier:-1), new Curve("p adottata alla Hu", "p_kn_m", "#15803D", Signed:true) }) }
            : new[] {
                ("Reazione p [kN/m]", new[] { new Curve("± p_lim", "p_lim_kn_m", "#94A3B8", Dashed:true), new Curve("", "p_lim_kn_m", "#94A3B8", Dashed:true, Multiplier:-1), new Curve("p alla Hu", "p_kn_m", "#15803D", Signed:true) }),
                ("Taglio V [kN]", new[] { new Curve("V = Hu − ∫p dz", "v_kn", "#0284C7", Signed:true) }),
                ("Momento M [kNm]", new[] { new Curve("M; linee grigie ±My", "m_knm", "#7E22CE", Signed:true) }),
                ("Risultante limite Q [kN]", new[] { new Curve("Q = ∫p_lim dz", "q_integrale_kn", "#B45309") }) };
        double column = (right - left) / panels.Length;
        for (int j = 0; j < panels.Length; j++)
        {
            var (title, curves) = panels[j]; double x0 = left + j * column + 12, width = column - 30;
            Text(dc, title, x0, 87, 12, Ui.Navy, width + 8, true);
            int legend = 0;
            foreach (var c in curves.Where(c => c.Label != ""))
            {
                double ly = 111 + 15 * legend++; var pen = new Pen(Ui.Brush(c.Color), 2) { DashStyle = c.Dashed ? DashStyles.Dash : DashStyles.Solid };
                dc.DrawLine(pen, new Point(x0, ly + 6), new Point(x0 + 15, ly + 6));
                Text(dc, c.Label, x0 + 19, ly, 10, width: width - 14);
            }
            var values = curves.SelectMany(c => (c.Signed ? actual : ground).Where(p => J.Number(p![c.Key]) is not null).Select(p => p.D(c.Key) * c.Multiplier)).ToList();
            double min = Math.Min(0, values.DefaultIfEmpty(0).Min()), max = Math.Max(0, values.DefaultIfEmpty(0).Max());
            bool moment = curves.Any(c => c.Key == "m_knm");
            if (moment) { max = Math.Max(max, result.D("momento_resistente_knm")); min = Math.Min(min, -result.D("momento_resistente_knm")); }
            if (min < 0) { max = Math.Max(max, -min); min = -max; }
            if (max <= min) max = min + 1;
            double X(double v) => x0 + (v - min) / (max - min) * width;
            var area = new Rect(x0, top, width, bottom - top);
            for (int tick = 0; tick <= 4; tick++)
            {
                double xx = x0 + width * tick / 4;
                dc.DrawLine(new Pen(Ui.Brush("#E2E8F0"), .6), new Point(xx, top), new Point(xx, bottom));
                if (width >= 230 || tick % 2 == 0)
                {
                    var number = new FormattedText(F(min + (max - min) * tick / 4), System.Globalization.CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Ui.Muted, 1);
                    dc.DrawText(number, new Point(Math.Clamp(xx - number.Width / 2, x0, Math.Max(x0, x0 + width - number.Width)), bottom + 5));
                }
            }
            dc.DrawRectangle(null, new Pen(Ui.Brush("#CBD5E1"), 1), area);
            dc.DrawLine(new Pen(Ui.Brush("#64748B"), .8), new Point(X(0), top), new Point(X(0), bottom));
            dc.PushClip(new RectangleGeometry(area));
            if (moment) foreach (int sign in new[] { -1, 1 })
                dc.DrawLine(new Pen(Ui.Brush("#64748B"), 1) { DashStyle = DashStyles.Dot }, new Point(X(sign * result.D("momento_resistente_knm")), top), new Point(X(sign * result.D("momento_resistente_knm")), bottom));
            foreach (var c in curves)
            {
                var rows = (c.Signed ? actual : ground).Where(p => J.Number(p![c.Key]) is not null).ToArray();
                for (int k = 1; k < rows.Length; k++)
                {
                    var a = rows[k - 1]; var b = rows[k];
                    bool completion = c.Signed && hinge is double h && (a.D("z") + b.D("z")) / 2 > h;
                    var pen = new Pen(Ui.Brush(c.Color), c.Signed ? 2.2 : 1.6) { DashStyle = c.Dashed || completion ? DashStyles.Dash : DashStyles.Solid };
                    dc.DrawLine(pen, new Point(X(a.D(c.Key) * c.Multiplier), Y(a.D("z"))), new Point(X(b.D(c.Key) * c.Multiplier), Y(b.D("z"))));
                }
            }
            if (!soilView && j == 3)
            {
                double h = s.D("capacita_kn"), zf = s.D("quota_taglio_nullo_m");
                dc.DrawLine(new Pen(Ui.Brush("#7E22CE"), 1) { DashStyle = DashStyles.Dot }, new Point(X(h), top), new Point(X(h), bottom));
                dc.DrawEllipse(Ui.Brush("#7E22CE"), null, new Point(X(h), Y(zf)), 4, 4);
                Text(dc, "Q(z_f) = Hu", x0 + 5, top + 4, 10, Ui.Navy, width - 10);
            }
            if (!soilView && moment)
                foreach (var z in s.Array("cerniere_m").Select(v => J.Number(v) ?? 0))
                {
                    var row = actual.First(p => Math.Abs(p.D("z") - z) < 1e-9);
                    dc.DrawEllipse(Brushes.White, new Pen(Ui.Brush("#7E22CE"), 2), new Point(X(row.D("m_knm")), Y(z)), 5, 5);
                }
            dc.Pop();
            if (soilView && j == 0 && !s.B("tensioni_disponibili"))
                Text(dc, "σv / σ′v non disponibili\nCompletare γ e γsat", x0 + 5, top + 30, 11, Ui.Navy, width - 10);
        }
        void DepthLine(double z, string label, string color, double offset)
        {
            if (z < 0 || z > length) return;
            var pen = new Pen(Ui.Brush(color), 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(pen, new Point(42, Y(z)), new Point(right, Y(z)));
            // Labels live below the plots to avoid overlap at close interfaces.
            Text(dc, $"{label} = {z:0.###} m", 16 + offset, bottom + 32, 11, Ui.Brush(color), 235);
        }
        if (g.B("presenza_falda") && g.D("profondita_falda") <= length) DepthLine(g.D("profondita_falda"), "Falda", "#0284C7", 0);
        DepthLine(s.D("quota_taglio_nullo_m"), "z_f · V = 0", "#7E22CE", 240);
        if (s["inversione_reazioni_m"] is not null) DepthLine(s.D("inversione_reazioni_m"), "b · inversione di p", "#15803D", 480);
        DepthLine(s.D("fine_reazioni_m"), "t · fine reazioni", "#64748B", 720);
        string note = soilView ? s.S("nota_tensioni") : "dV/dz = −p; dM/dz = V. Q è l'integrale del limite positivo, distinto dall'integrale della reazione con segno. z_f e b sono quote diverse.";
        Text(dc, note, 16, bottom + 57, 11, width: size.Width - 32);
        Text(dc, hinge is null ? "p positiva opposta a Hu. Le discontinuità di p alle interfacce sono mantenute; V e M sono continui in assenza di forze/coppie concentrate."
            : "Fascia grigia sotto la cerniera interna: completamento idealizzato; p, V e M tratteggiati. Nessuna deformata o rotazione viene calcolata.", 16, bottom + 89, 11, width: size.Width - 32);
        if (Math.Abs(s.D("risultante_concentrata_kn")) > 1e-8)
            Text(dc, $"Broms: F = {s.D("risultante_concentrata_kn"):0.###} kN a z = {s.D("quota_risultante_m"):0.###} m, nel verso di Hu; è una forza concentrata, separata da p e q.", 16, bottom + 121, 11, Brushes.DarkOrange, size.Width - 32);
    }
}

internal sealed partial class HorizontalWorkspace
{
    private static FrameworkElement ReferencesPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(Ui.Text("Riferimenti e ruolo nel modello", 18, true));
        foreach (var r in PaloOrizzontale.References())
        {
            var block = Ui.Stack(Ui.Text(r.S("titolo"), 13, true), Ui.Text(r.S("dettaglio"), 11), Ui.Text(r.S("uso"), 12));
            if (r.S("url") is string url && url != "") block.Children.Add(Ui.Button("Apri riferimento", () => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })));
            block.Margin = new Thickness(0, 18, 0, 4); panel.Children.Add(block);
        }
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    internal IReadOnlyList<ReportOrizzontale.Figure> ReportFigures()
    {
        if (Result is null) return [];
        return Enumerable.Range(0, Result.Array("sondaggi").Count).SelectMany(i => new[] { true, false }.Select(soil =>
            new ReportOrizzontale.Figure(i + 1, soil ? "Tensioni e resistenze del terreno" : "Equilibrio e sollecitazioni alla capacità ultima",
                new HorizontalDiagrams(Result, i, soil).Png(1000, 660), 1000d / 660))).ToArray();
    }
}
