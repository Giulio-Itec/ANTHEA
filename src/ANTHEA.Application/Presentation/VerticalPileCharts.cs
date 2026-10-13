using System.Text.Json.Nodes;
using Anthea.Calculations;
namespace Anthea.Application.Presentation;

public sealed record PlotSeriesData(string Name, List<double[]> Points, string Color, bool Dashed = false, bool Highlighted = false, string? DashPattern = null);
public sealed record PlotMarkerData(double X, double Y, string Label, string Color, bool ProjectToAxes = false, string? XCaption = null, string? YCaption = null, double Radius = 5);
public sealed class PlotData
{
    public string Title { get; set; } = "";
    public string XLabel { get; set; } = "";
    public string YLabel { get; set; } = "";
    public string Note { get; set; } = "";
    public double? XMinimum { get; set; }
    public List<PlotSeriesData> Series { get; set; } = [];
    public List<PlotMarkerData> Markers { get; set; } = [];
}
public sealed record VerticalPileCurve(string Key, PlotSeriesData Series, bool Visible);

/// <summary>Existing chart preparation, shared by the sheet and standalone report; calculations remain in their engines.</summary>
public static class VerticalPileCharts
{
    public static IReadOnlyList<VerticalPileCurve> Capacity(JsonObject data, JsonObject result, bool micro)
    {
        var allSeries = new List<VerticalPileCurve>();
        foreach (var (name, curve) in result!["curve"]!.AsObject())
        {
            foreach (string branch in new[] { "progetto", "media", "minima" })
            {
                string key = branch + "_" + name, label = name.Replace("non_drenante", "Non dren.").Replace("drenante", "Dren.").Replace("compressione", "C").Replace("trazione", "T").Replace('_', ' ') + " · " + branch;
                string color = micro ? name.EndsWith("trazione") ? "#9B59B6" : "#16703B" : name.Contains("non_drenante")
                    ? name.EndsWith("trazione") ? "#B779D6" : "#6A1B9A"
                    : name.EndsWith("trazione") ? "#73B84C" : "#16703B";
                var series = new PlotSeriesData(label, curve!.Array(branch).Select(p => new[] { p![1]!.GetValue<double>(), p[0]!.GetValue<double>() }).ToList(), color, branch != "progetto", DashPattern: branch == "media" ? "Dash" : branch == "minima" ? "Dot" : null);
                allSeries.Add(new(key, series, data["visibilita_grafici"].B(key, branch == "progetto")));
            }
        }
        foreach (var (name, points) in result["azioni"]!.AsObject()) if (points!.AsArray().Count > 0)
        { string key = "azione_" + name; allSeries.Add(new(key, new PlotSeriesData("Ed " + name, points.AsArray().Select(p => new[] { p![1]!.GetValue<double>(), p[0]!.GetValue<double>() }).ToList(), name == "compressione" ? "#D32F2F" : "#29A9E0", true), data["visibilita_grafici"].B(key, true))); }
        return allSeries;
    }
    public static bool CapacityIncludes(string key, bool micro, int selected)
    {
        if (selected <= 0) return key.StartsWith("progetto_") || key.StartsWith("azione_");
        string condition = micro ? selected == 1 ? "compressione" : "trazione" : new[] { "", "drenante_compressione", "drenante_trazione", "non_drenante_compressione", "non_drenante_trazione" }[selected];
        return key.StartsWith("azione_") ? key.EndsWith(condition.EndsWith("trazione") ? "trazione" : "compressione") : key[(key.IndexOf('_') + 1)..] == condition;
    }
    public static PlotData Reference(JsonObject data, JsonObject? result, bool micro, int selectedSurvey)
    {
        var reference = new PlotData();
        var series = new List<PlotSeriesData>(); string[] colors = ["#0B5CAD", "#FF2E8B57", "#FFFF8C00", "#FF800080", "#FFA52A2A", "#FF008080", "#FFDC143C", "#FF808080"]; int i = 0;
        reference.Title = micro ? "Abachi Bustamante–Doix" : "Nq parametrizzato"; reference.XLabel = micro ? "p_l [MPa]" : "φ [°]"; reference.YLabel = micro ? "s [kPa]" : "Nq / Nq*"; reference.Markers = []; reference.Note = "";
        if (micro)
        {
            int selected = selectedSurvey;
            var layers = selected >= 0 && selected < data.Array("stratigrafie").Count ? data.Array("stratigrafie")[selected]!.AsArray() : new JsonArray();
            string injection = data["generali"].S("tipo_iniezione");
            var codes = layers.Where(l => l.B("laterale_attiva", true) && BustamanteDoix.Terreni.ContainsKey(l.S("terreno")))
                .Select(l => BustamanteDoix.Terreni[l.S("terreno")].Family + (injection == "IGU" ? "2" : "1")).ToHashSet();
            foreach (var (name, pts) in BustamanteDoix.Curve.Where(c => codes.Count == 0 || codes.Contains(c.Key)))
                series.Add(new(name, pts.Select(p => new[] { p.X, 1000 * p.Y }).ToList(), colors[i++ % colors.Length]));
            var segments = result?.Array("dettagli").LastOrDefault()?.Array("sondaggi");
            if (segments is not null && selected >= 0 && selected < segments.Count)
                foreach (var segment in segments[selected]!.Array("tratti").Where(t => t.B("laterale_attiva", true) && t.S("curva") != "").DistinctBy(t => t.S("curva")))
                {
                    string code = segment.S("curva"); var color = series.First(s => s.Name == code).Color;
                    reference.Markers.Add(new(segment.D("pl"), segment.D("s"), code, color, ProjectToAxes: true,
                        XCaption: $"pl={segment.D("pl"):0.#}", YCaption: $"s\n{segment.D("s"):0.#}"));
                }
            reference.Note = $"Stratigrafia {selected + 1} · {injection} · {string.Join(", ", series.Select(s => s.Name))} · p_l = p_i" + (reference.Markers.Count == 0 ? " · punti disponibili dopo il calcolo" : "");
        }
        else
        {
            bool big = data["generali"].D("diametro", 1) > .8; reference.XMinimum = 25;
            foreach (double ratio in big ? Nq.RapportiGrande : Nq.RapportiMedio)
            { double low = big ? 26 : 25, high = big ? 42 : ratio == 5 ? 38.8 : ratio == 10 ? 40 : ratio == 20 ? 41 : 41.6; series.Add(new("z/D=" + ratio, Enumerable.Range(0, 161).Select(k => { double phi = low + (high - low) * k / 160; return new[] { phi, Nq.Dettaglio(phi, ratio, big).D("nq") }; }).ToList(), colors[i++ % colors.Length])); }
            double length = data["generali"].D("lunghezza"), diameter = data["generali"].D("diametro");
            if (length > 0 && diameter > 0 && double.IsFinite(length / diameter))
            {
                double ratio = length / diameter, low = 25, high = big ? 42 : 41.6; int selected = selectedSurvey;
                var surveys = result?.B("copertura_completa") == true ? result.Array("dettagli").LastOrDefault(d => Math.Abs(d.D("z") - length) < 1e-8)?.Array("sondaggi") : null;
                if (selected >= 0 && surveys is not null && selected < surveys.Count && surveys[selected]?["dettaglio_nq"] is { } detail)
                {
                    double phi = detail.D("phi"), nq = detail.D("nq"); low = Math.Min(low, phi); high = Math.Max(high, phi);
                    reference.Markers.Add(new(phi, nq, $"S{selected + 1}: φ={phi:0.##}° · {(big ? "Nq*" : "Nq")}={nq:0.###}", "#FFDC143C", ProjectToAxes: true));
                    reference.Note = $"Stratigrafia {selected + 1} · punta L={length:0.###} m · L/D={ratio:0.###}" + (detail.B("limite_phi") || detail.B("limite_rapporto") ? " · valore limitato al bordo dell'abaco" : "");
                }
                else reference.Note = $"L/D effettivo = {ratio:0.###} · punto disponibile con dati completi fino alla punta";
                reference.XMinimum = low; series.Add(new($"L/D effettivo = {ratio:0.###}", Enumerable.Range(0, 201).Select(k => { double phi = low + (high - low) * k / 200; return new[] { phi, Nq.Dettaglio(phi, ratio, big).D("nq") }; }).ToList(), "#FFDC143C", Highlighted: true));
            }
        }
        reference.Series = series;
        return reference;
    }
}
