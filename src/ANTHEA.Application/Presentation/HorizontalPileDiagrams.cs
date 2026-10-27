using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

/// <summary>A segment of a curve, between two rows of the diagram: values (already multiplied) and depths; dashed or solid.</summary>
public sealed record HorizontalDiagramSegment(double FromValue, double FromDepth, double ToValue, double ToDepth, bool Dashed);

/// <summary>
/// A curve of a panel of the diagrams: legend (empty: none, the mirrored limit), colour (#RRGGBB), with sign (the reactions and the
/// actions of the pile, drawn thicker) or not, dashed in the legend, and its segments.
/// </summary>
public sealed record HorizontalDiagramCurve(string Label, string Color, bool Signed, bool Dashed, IReadOnlyList<HorizontalDiagramSegment> Segments);

/// <summary>A point of a panel: value on the axis of the panel and depth.</summary>
public sealed record HorizontalDiagramPoint(double Value, double Depth);

/// <summary>
/// A panel of the diagrams: title, curves, the range of the axis (symmetric when a value is negative), the five labels of the axis,
/// the resisting moment (±My lines, moment panel), the capacity line with the depth of zero shear (panel of Q of the pile view), the
/// hinges on the moment, the message shown when the stresses are not available.
/// </summary>
public sealed record HorizontalDiagramPanel(string Title, IReadOnlyList<HorizontalDiagramCurve> Curves, double Minimum, double Maximum, IReadOnlyList<string> Ticks,
    double? MomentLimit, HorizontalDiagramPoint? Capacity, IReadOnlyList<HorizontalDiagramPoint> Hinges, string? Message);

/// <summary>A layer drawn on the left: from <see cref="Top"/> to <see cref="End"/> (cut at the length), its index (colour) and labels.</summary>
public sealed record HorizontalDiagramLayer(int Index, double Top, double End, string Label, string TopLabel);

/// <summary>A depth line under the panels: depth, label with the value, colour and slot of the label (0–3, from the left).</summary>
public sealed record HorizontalDiagramDepth(double Depth, string Label, string Color, int Slot);

/// <summary>
/// The diagrams of one survey of the capacity of the horizontal pile («Diagrammi e dettagli» and the figures of the report), as plain
/// data (F5.7): soil view (stresses, equivalent pressure, linear reaction) or pile view (reaction, shear, moment, limit resultant).
/// HorizontalDiagrams (X.Desktop) draws it; the ViewModel exposes it. Depths in m from the ground, values in the units of the panels.
/// </summary>
public sealed record HorizontalDiagram(bool SoilView, string Title, string Subtitle, string Basis, double Length, string LengthLabel,
    IReadOnlyList<HorizontalDiagramLayer> Layers, double? Hinge, double? ShearDepth, string? ShearLabel, IReadOnlyList<HorizontalDiagramPanel> Panels,
    IReadOnlyList<HorizontalDiagramDepth> Depths, string Note, string HingeNote, string? BromsNote);

/// <summary>
/// Derivations of the diagrams of the horizontal pile, moved without changes from the historic view (HorizontalDiagrams.Render and
/// StratigraphyDrawing in X.Desktop, F5.7): which rows the reaction draws, the curves and their segments, the ranges and labels of the
/// axes, the layers, the depth lines and the notes.
/// </summary>
public static class HorizontalPileDiagrams
{
    private sealed record Curve(string Label, string Key, string Color, bool Signed = false, bool Dashed = false, double Multiplier = 1);

    /// <summary>
    /// Only the above-hinge branch is determined by the long-pile Broms mechanism; the engine's below-hinge equilibrium completion is
    /// not a physical reaction diagram: the depth of the first internal hinge of a long pile, otherwise null.
    /// </summary>
    public static double? ReactionLimit(JsonNode result) => result.S("meccanismo") == "Lungo"
        ? result.Array("cerniere_m").Select(J.Number).Where(z => z > 0).Min() : null;

    /// <summary>The rows of the reaction above the internal hinge (the whole diagram without it).</summary>
    public static JsonNode?[] PhysicalDiagram(JsonNode result)
    {
        double? limit = ReactionLimit(result);
        return result.Array("diagrammi").Where(r => limit is null || r.D("z") < limit ||
            (r.D("z") == limit && r.S("lato") != "dopo")).ToArray();
    }

    /// <summary>The diagrams of the survey <paramref name="surveyIndex"/> of <paramref name="result"/>, soil or pile view.</summary>
    public static HorizontalDiagram Build(JsonObject result, int surveyIndex, bool soilView)
    {
        var s = result.Array("sondaggi")[surveyIndex]!; var g = result["input"]!["generali"]!;
        double length = g.D("lunghezza"), diameter = g.D("diametro");
        var ground = s.Array("diagramma_terreno"); var actual = s.Array("diagrammi");
        double? hinge = ReactionLimit(s);
        static string F(double n) => Math.Abs(n) >= 10000 ? n.ToString("0.##E+0") : n.ToString("0.##");
        string title = soilView ? "Terreno · dalle tensioni verticali alla reazione laterale" : "Palo · equilibrio delle reazioni e sollecitazioni";
        string subtitle = $"Sondaggio {surveyIndex + 1} · {s.S("meccanismo")} · Hu = {s.D("capacita_kn"):N2} kN · D = {diameter:0.###} m · {result.S("metodo_calcolo")}";
        // Testa sotto il piano campagna (1.14.3, 0 altrimenti): le quote partono dalla testa, gli strati sopra la testa (ricoprimento) non sono
        // disegnati e il taglio nei coesivi è a 1,5D dal piano campagna, cioè 1,5D − d dalla testa.
        double head = Calcolo.ProfonditaTestaMostrata(result);
        var layers = result["input"].Array("stratigrafie")[surveyIndex]!.AsArray(); double layerTop = -head; int cover = 0;
        var drawnLayers = new List<HorizontalDiagramLayer>();
        for (int i = 0; i < layers.Count && layerTop < length; i++)
        {
            double end = Math.Min(length, layerTop + layers[i].D("spessore"));
            if (head > 0 && end <= 0) { cover++; layerTop = end; continue; }
            double top = Math.Max(layerTop, 0);
            drawnLayers.Add(new(i, top, end, $"{i + 1} · {(layers[i].S("tipologia") == "Coesivo" ? "Coesivo" : "Granulare")}", F(top)));
            layerTop = end;
        }
        double? shear = null; string? shearLabel = null; double neglected = 1.5 * diameter - head;
        if (layers.Skip(cover).Any(l => l.S("tipologia") == "Coesivo") && neglected > 0 && neglected < length)
        { shear = neglected; shearLabel = head > 0 ? $"1,5D − d = {neglected:0.###} m\nTaglio nei coesivi" : $"1,5D = {neglected:0.###} m\nTaglio nei coesivi"; }
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
        var drawnPanels = new List<HorizontalDiagramPanel>();
        for (int j = 0; j < panels.Length; j++)
        {
            var (panelTitle, curves) = panels[j];
            var values = curves.SelectMany(c => (c.Signed ? actual : ground).Where(p => J.Number(p![c.Key]) is not null).Select(p => p.D(c.Key) * c.Multiplier)).ToList();
            double min = Math.Min(0, values.DefaultIfEmpty(0).Min()), max = Math.Max(0, values.DefaultIfEmpty(0).Max());
            bool moment = curves.Any(c => c.Key == "m_knm");
            if (moment) { max = Math.Max(max, result.D("momento_resistente_knm")); min = Math.Min(min, -result.D("momento_resistente_knm")); }
            if (min < 0) { max = Math.Max(max, -min); min = -max; }
            if (max <= min) max = min + 1;
            var ticks = Enumerable.Range(0, 5).Select(tick => F(min + (max - min) * tick / 4)).ToArray();
            var drawn = curves.Select(c =>
            {
                var rows = (c.Signed ? actual : ground).Where(p => J.Number(p![c.Key]) is not null).ToArray();
                var segments = new List<HorizontalDiagramSegment>();
                for (int k = 1; k < rows.Length; k++)
                {
                    var a = rows[k - 1]; var b = rows[k];
                    bool completion = c.Signed && hinge is double h && (a.D("z") + b.D("z")) / 2 > h;
                    segments.Add(new(a.D(c.Key) * c.Multiplier, a.D("z"), b.D(c.Key) * c.Multiplier, b.D("z"), c.Dashed || completion));
                }
                return new HorizontalDiagramCurve(c.Label, c.Color, c.Signed, c.Dashed, segments);
            }).ToArray();
            HorizontalDiagramPoint? capacity = !soilView && j == 3 ? new(s.D("capacita_kn"), s.D("quota_taglio_nullo_m")) : null;
            var hinges = !soilView && moment
                ? s.Array("cerniere_m").Select(v => J.Number(v) ?? 0).Select(z => new HorizontalDiagramPoint(actual.First(p => Math.Abs(p.D("z") - z) < 1e-9).D("m_knm"), z)).ToArray()
                : [];
            string? message = soilView && j == 0 && !s.B("tensioni_disponibili") ? "σv / σ′v non disponibili\nCompletare γ e γsat" : null;
            drawnPanels.Add(new(panelTitle, drawn, min, max, ticks, moment ? result.D("momento_resistente_knm") : null, capacity, hinges, message));
        }
        var depths = new List<HorizontalDiagramDepth>();
        void DepthLine(double z, string label, string color, int slot)
        {
            if (z < 0 || z > length) return;
            depths.Add(new(z, $"{label} = {z:0.###} m", color, slot));
        }
        if (g.B("presenza_falda") && g.D("profondita_falda") - head <= length) DepthLine(g.D("profondita_falda") - head, "Falda", "#0284C7", 0);
        DepthLine(s.D("quota_taglio_nullo_m"), "z_f · V = 0", "#7E22CE", 1);
        if (s["inversione_reazioni_m"] is not null) DepthLine(s.D("inversione_reazioni_m"), "b · inversione di p", "#15803D", 2);
        DepthLine(s.D("fine_reazioni_m"), "t · fine reazioni", "#64748B", 3);
        string note = soilView ? s.S("nota_tensioni") : "dV/dz = −p; dM/dz = V. Q è l'integrale del limite positivo, distinto dall'integrale della reazione con segno. z_f e b sono quote diverse.";
        string hingeNote = hinge is null ? "p positiva opposta a Hu. Le discontinuità di p alle interfacce sono mantenute; V e M sono continui in assenza di forze/coppie concentrate."
            : "Fascia grigia sotto la cerniera interna: completamento idealizzato; p, V e M tratteggiati. Nessuna deformata o rotazione viene calcolata.";
        string? broms = Math.Abs(s.D("risultante_concentrata_kn")) > 1e-8
            ? $"Broms: F = {s.D("risultante_concentrata_kn"):0.###} kN a z = {s.D("quota_risultante_m"):0.###} m, nel verso di Hu; è una forza concentrata, separata da p e q." : null;
        string basis = head > 0 ? $"Stato limite alla capacità Hu. z dalla testa, a d = {head:0.###} m sotto il piano campagna, positiva verso il basso."
            : "Stato limite alla capacità Hu. z dal piano campagna, positiva verso il basso.";
        return new(soilView, title, subtitle, basis, length, F(length), drawnLayers, hinge, shear, shearLabel,
            drawnPanels, depths, note, hingeNote, broms);
    }
}
