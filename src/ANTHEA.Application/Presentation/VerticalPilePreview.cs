using System.Globalization;
using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

/// <summary>Catalogs shared by pile editors. Each caller owns its arrays.</summary>
public static class PileInputCatalog
{
    public static string[] DrivenPileTypes => Calcolo.Parametri.Keys.Where(k => k is not ("Trivellato" or "Elica continua")).ToArray();
    public static string MicropileMethod => BustamanteDoix.Versione;
    public static string[] MicropileSoils => BustamanteDoix.Terreni.Keys.ToArray();
    public static string[] ChsProfiles => Chs.Catalogo.Keys.ToArray();
    public static string[] InvestigatedProfiles => Calcolo.Verticali.Keys.ToArray();
    public static IReadOnlyDictionary<string, string> CorrelationFields(JsonNode? general, string defaultCount = "")
    {
        var fields = new Dictionary<string, string>();
        if (Calcolo.Verticali.TryGetValue(general.S("verticali_indagate", defaultCount), out var xi))
        { fields["__xi3"] = xi.Xi3.ToString("F2"); fields["__xi4"] = xi.Xi4.ToString("F2"); }
        return fields;
    }
}
public sealed record VerticalPilePreviewData(string Weight, string Efficiency, IReadOnlyDictionary<string, string> Correlation);

/// <summary>Values shown before the full calculation. Invalid inputs preserve the historical
/// missing-value markers; engineering operations remain in the same calculation library.</summary>
public static class VerticalPilePreview
{
    public static string NormativeBase(JsonNode general, bool micro) => Calcolo.SicurezzaBaseNormativa(general, micro).ToString("0.00", CultureInfo.InvariantCulture);
    public static string? InitialAlpha(string soil, string injection) => BustamanteDoix.Terreni.ContainsKey(soil) && injection is "IGU" or "IRS"
        ? BustamanteDoix.IntervalloAlpha(soil, injection)[0].ToString(CultureInfo.InvariantCulture) : null;
    public static void ResetAlphas(JsonObject data)
    {
        foreach (var rows in data.Array("stratigrafie")) foreach (var row in rows!.AsArray())
            if (InitialAlpha(row.S("terreno"), data["generali"].S("tipo_iniezione")) is { } alpha) row!["alpha"] = alpha;
    }
    public static VerticalPilePreviewData Read(JsonObject data, bool micro)
    {
        var g = data["generali"]!; string weight = "—";
        if (micro)
        {
            try { weight = Chs.Peso(g.S("profilo_chs"), g.D("diametro"), g.Required("peso_specifico_palo", strict: true)).D("q_totale").ToString("0.0", CultureInfo.GetCultureInfo("it-IT")); }
            catch (ArgumentException) { }
        }
        var efficiency = Calcolo.Efficienza(data);
        return new(weight, efficiency.S("errore") != "" ? efficiency.S("errore") : $"ηg,c = {efficiency.D("eta_compressione"):F3}\nηg,t = {efficiency.D("eta_trazione"):F3}", PileInputCatalog.CorrelationFields(g));
    }
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> LayerFields(JsonNode g, IEnumerable<JsonObject> rows, bool micro)
    {
        double diameter = g.D("diametro"), depth = 0; bool valid = true; int index = 0;
        var result = new List<IReadOnlyDictionary<string, string>>();
        foreach (var d in rows)
        {
            var fields = new Dictionary<string, string>(); result.Add(fields);
            void Field(string key, string value) => fields[key] = value;
            Field("__strato", d.S("strato", ((char)('A' + index++ % 26)).ToString()));
            if (micro)
            {
                string tau = "—";
                if (!d.B("laterale_attiva", true)) tau = "0,0";
                else
                {
                    try
                    {
                        var parameter = BustamanteDoix.Parametro(d.S("terreno"), g.S("tipo_iniezione"), g.Required("pressione_iniezione", strict: true), d.Required("alpha", strict: true));
                        tau = parameter.D("s").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
                    }
                    catch (ArgumentException) { /* Dati incompleti o fuori abaco: nessun valore applicabile. */ }
                }
                Field("__tau", tau);
                continue;
            }
            var (k, mu) = Calcolo.CoefficientiLaterali(g, d); double? cu = J.Number(d["coesione_non_drenata"]), thickness = J.Number(d["spessore"]), phi = J.Number(d["angolo_attrito"]);
            Field("__k", k?.ToString("0.###") ?? "—"); Field("__mu", mu?.ToString("0.###") ?? "—"); Field("__alfa", d.S("tipologia") == "Coesivo" && cu >= 0 ? Calcolo.CoefficienteAlfa(g, cu.Value).ToString("0.###") : "—");
            valid &= thickness > 0; if (valid) depth += thickness!.Value;
            Field("__nq", valid && double.IsFinite(depth) && diameter > 0 && phi is not null ? Nq.Dettaglio(phi.Value, depth / diameter, diameter > .8).D("nq").ToString("0.###") : "—");
        }
        return result;
    }
}
