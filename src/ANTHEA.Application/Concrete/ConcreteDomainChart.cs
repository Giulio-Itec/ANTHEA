using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
namespace Anthea.Application.Concrete;

public sealed record DomainChartGeometry((double[] A, double[] B)[] Segments, string? Title, string? XLabel, string? YLabel, string? Note, string EmptyMessage);
public sealed record DomainChartActions(List<PlotMarkerData> Markers, List<(double[] A, double[] B)> Lines);
public static class ConcreteDomainChart
{
    public static ConcreteResults.Action ReadNamedAction(JsonObject row)
    {
        if (string.IsNullOrWhiteSpace(row.S("nome"))) throw new ArgumentException("Assegnare un nome alla combinazione.");
        return new(SectionWorkspace.ReadAction(row));
    }
    public static bool Includes(JsonObject options, ConcreteResults.Check? check) => options.S("filtro") switch
    {
        "Entro il dominio" => check?.Utilization is <= 1, "Fuori dominio" => check?.Utilization is > 1,
        "Da controllare" => check?.Utilization is null, _ => true
    };
    public static DomainChartGeometry Geometry(string key, JsonObject options, ConcreteResults.Domain2D? section2d, string? error = null)
    {
        if (section2d is null) return new([], null, null, null, null, error ?? "Dominio in attesa di aggiornamento automatico");
        try
        {
            bool nm = options.S("tipo") == "N–M";
            double value = SectionWorkspace.Number(options.S(nm ? "theta" : "N"), nm ? "θ" : "N") * (nm ? Math.PI / 180 : 1);
            return new(section2d.Segments.Select(s => (section2d.Project(s.A), section2d.Project(s.B))).ToArray(),
                (options.ContainsKey("modello_resistente") ? key + " · " + options.S("modello_resistente") : SectionWorkspace.Label(key)) + " · " + (nm ? $"N–M, θ = {value * 180 / Math.PI:0.00}°" : $"Mx–My, N = {value:0.00} kN"),
                nm ? "Mθ [kNm]" : "Mx [kNm]", nm ? "N [kN]" : "My [kNm]",
                "Sezione calcolata da GPC Engine · azioni fuori piano escluse salvo proiezione esplicita",
                "Nessuna intersezione del dominio con il piano selezionato");
        }
        catch (ArgumentException ex) { return new([], null, null, null, null, ex.Message); }
    }
    public static List<(string Id, ConcreteResults.Action Force, bool Pass)> VisibleActions(JsonObject options, IEnumerable<JsonObject> rows, string? selected,
        IReadOnlyDictionary<string, ConcreteResults.Check>? checks, ConcreteResults.Domain3D? three = null, ConcreteResults.Domain2D? two = null, bool report3D = false)
    {
        var visible = new List<(string Id, ConcreteResults.Action Force, bool Pass)>();
        foreach (var item in rows)
        {
            if (!item.B("visible", true) || options.B("solo_selezionata") && (report3D || !options.B("tutte_rd")) && item.S("id") != selected) continue;
            try
            {
                var force = ReadNamedAction(item);
                if (three is not null) force = three.LocalAction(force);
                if (two is not null) force = two.LocalAction(force);
                visible.Add((item.S("id"), force, checks?.GetValueOrDefault(item.S("id"))?.Utilization is <= 1));
            }
            catch (ArgumentException) { }
        }
        return visible;
    }
    private static double[] Project(ConcreteResults.Action p, bool nm, double value) => nm ? [p.Mx * Math.Cos(value) + p.My * Math.Sin(value), p.N] : [p.Mx, p.My];
    public static DomainChartActions Actions(JsonObject options, ConcreteResults.Domain2D? domain, IReadOnlyList<JsonObject> rows, string? selected,
        IReadOnlyDictionary<string, ConcreteResults.Check>? checks)
    {
        var markers = new List<PlotMarkerData>(); var lines = new List<(double[], double[])>();
        var visible = VisibleActions(options, rows, selected, checks, two: domain);
        try
        {
            bool nm = options.S("tipo") == "N–M"; double value = SectionWorkspace.Number(options.S(nm ? "theta" : "N"), "Piano") * (nm ? Math.PI / 180 : 1);
            foreach (var action in visible)
            {
                // Il punto verificato: con «proietta» l'azione fuori piano proiettata che la verifica ha usato (seguito di SLU-1, 16/10/2026).
                var p = domain is not null ? domain.Project(checks?.GetValueOrDefault(action.Id)?.ProjectedAction ?? action.Force) : Project(action.Force, nm, value);
                if (options.B("mostra_ed", true) && (!options.B("solo_selezionata") || action.Id == selected))
                    markers.Add(new(p[0], p[1], action.Id == selected ? "Ed" : "", options.B("colora_eta") ? UtilizationColors.Color(checks?.GetValueOrDefault(action.Id)?.Utilization) : action.Id == selected ? "#E09620" : "#0B5CAD", Radius: options.D("dimensione_ed", 5)));
                if ((action.Id == selected || options.B("tutte_rd")) && options.B("mostra_linee", true))
                {
                    lines.Add((new double[] { 0, 0 }, p));
                    if (checks?.GetValueOrDefault(action.Id)?.Resistance is ConcreteResults.Action resistance)
                        lines.Add((p, domain is not null ? domain.Project(resistance) : Project(resistance, nm, value)));
                }
            }
            if (options.B("mostra_rd", true))
                foreach (var item in rows.Where(r => r.B("visible", true) && (options.B("tutte_rd") || r.S("id") == selected)))
                    if (checks?.GetValueOrDefault(item.S("id")) is { Resistance: ConcreteResults.Action r } check)
                    { var p = domain is not null ? domain.Project(r) : Project(r, nm, value); markers.Add(new(p[0], p[1], item.S("id") == selected ? "Rd" : "", UtilizationColors.Color(check.Utilization), Radius: options.D("dimensione_rd", 5))); }
        }
        catch (ArgumentException) { }
        return new(markers, lines);
    }

    // ---------------------------------------------------------------- the domain panels (F5.10 S3): read once for the historic view and the ViewModel

    /// <summary>The size of the points of the actions («dimensione_ed») or of the resistances («dimensione_rd») of a domain panel; 5 by default.</summary>
    public static double PointSize(JsonObject options, string key) => options.D(key, 5);

    /// <summary>The transparency of the 3D domain, in percent; 35 by default.</summary>
    public static double Transparency(JsonObject options) => options.D("trasparenza", 35);

    /// <summary>A factor of the axes of a domain («scala_x», «scala_y», «scala_n», «scala_mx», «scala_my»); 1 by default.</summary>
    public static double AxisScale(JsonObject options, string key) => options.D(key, 1);

    /// <summary>The factors of the axes of the window «Scala…»: Mx, N, My for the 3D domain, x and y for the 2D one.</summary>
    public static IReadOnlyList<string> ScaleKeys(bool threeD) => threeD ? ["scala_mx", "scala_n", "scala_my"] : ["scala_x", "scala_y"];

    /// <summary>
    /// «Applica» of the window «Scala…»: the factors of the axes (0.25–4) and the zoom (the text in percent, 10–2000 %, as a factor) read from
    /// the draft of the window; a text that is not a number, or a value out of its range, is refused with its message.
    /// </summary>
    public static (Dictionary<string, double> Values, double Zoom) ReadScale(JsonObject draft, IReadOnlyList<string> keys)
    {
        var values = keys.ToDictionary(k => k, k => ConcreteEditor.Number(draft.S(k), k));
        double zoom = ConcreteEditor.Number(draft.S("zoom"), "Zoom") / 100;
        if (values.Values.Any(v => v < .25 || v > 4) || zoom < .1 || zoom > 20) throw new ArgumentException("Fattori ammessi 0,25–4; zoom 10–2000%.");
        return (values, zoom);
    }

    /// <summary>The triangles of a mesh of the 3D domain (none without a mesh).</summary>
    public static int TriangleCount(ConcreteResults.Mesh? mesh) => mesh is null ? 0 : mesh.Triangles.Count / 3;

    /// <summary>
    /// «Sezione sull’azione selezionata» of the 2D domain: the field of the plane through the action and its text, N for the section Mx–My, θ
    /// in degrees (from Mx and My) for the section N–M.
    /// </summary>
    public static (string Key, string Value) PlaneThrough(JsonObject options, ConcreteResults.Action action) => options.S("tipo") == "Mx–My"
        ? ("N", action.N.ToString("G12", System.Globalization.CultureInfo.InvariantCulture))
        : ("theta", (Math.Atan2(action.My, action.Mx) * 180 / Math.PI).ToString("G12", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// «Dettagli combinazione» of a domain panel for the selected row: limit state and method, axes, forces of the row, outcome, resistance with
    /// the state at the resistant point, extremes of the mesh (3D) or of the curve (2D), the error of the check. Numbers in the current culture,
    /// as the historic text; a row that cannot be read gives its message.
    /// </summary>
    public static string Detail(string label, bool threeD, JsonObject options, JsonObject row, ConcreteResults.Check? check,
        IEnumerable<ConcreteResults.Action>? extrema, string? error)
    {
        try
        {
            var a = ReadNamedAction(row);
            string text = $"{row.S("nome")}\n\nNEd = {a.N:0.00} kN\nMx,Ed = {a.Mx:0.00} kNm\nMy,Ed = {a.My:0.00} kNm\n\n"
                + (check is null ? "Da calcolare" : $"η = {check.Utilization?.ToString("0.00") ?? "—"}\n{check.Status}");
            text = label + " · " + (threeD ? options.S("criterio") + " / " + options.S("strategia") : options.S("tipo") + (options.S("proietta") == "Sì" ? " · proiezione attiva" : " · senza proiezione"))
                + "\n\nAZIONI · " + options.S("assi", "Locali") + "\n" + text;
            if (check?.Resistance is ConcreteResults.Action r)
                text += $"\n\nRESISTENZA · assi locali\nNRd = {r.N:0.00} kN\nMx,Rd = {r.Mx:0.00} kNm\nMy,Rd = {r.My:0.00} kNm\n"
                    + ConcreteResponsePresentation.ResponseSummary(check.Response, "STATO AL PUNTO RESISTENTE");
            if (extrema?.ToArray() is { Length: > 0 } points)
                text += $"\n\nESTREMI {(threeD ? "MESH" : "CURVA")}\nN: {points.Min(p => p.N):0.00} … {points.Max(p => p.N):0.00} kN\nMx: {points.Min(p => p.Mx):0.00} … {points.Max(p => p.Mx):0.00} kNm\nMy: {points.Min(p => p.My):0.00} … {points.Max(p => p.My):0.00} kNm\nEstremi campionati; non sono resistenze al N della riga.";
            if (error is not null) text += "\n\nDa correggere: " + error;
            return text;
        }
        catch (ArgumentException failure) { return failure.Message; }
    }
}
