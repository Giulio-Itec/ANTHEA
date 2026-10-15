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
}
