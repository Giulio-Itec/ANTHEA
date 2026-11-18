using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Reports;

/// <summary>Contents and routing of the standalone ITEC reports for the other pile modules.</summary>
public static class PileFamilyReport
{
    public static bool Supports(string module) => module is "geo_palo_verticale" or "geo_micropalo_verticale" or "geo_palo_orizzontale" or "geo_micropalo_orizzontale";
    public static bool IsElastic(JsonObject data, JsonObject? result = null) => result is null ? data.S("vista_orizzontale") == "elastico" : result.S("tipo_risultato") == "palo_elastico";
    public static IReadOnlyList<PileReportContent> Outline(string module, JsonObject data, JsonObject? result = null)
    {
        if (module == "geo_palo_verticale") return PileReportContents.Outline(data);
        bool micro = module == "geo_micropalo_verticale", elastic = !micro && IsElastic(data, result);
        PileReportContent Group(string key, string title, params PileReportContent[] children) => new(key, title, Items: children);
        return [new("intestazione", "Intestazione e copertina", false), new("indice", "Indice", false),
            Group("c1", "Oggetto e dati generali", new("oggetto", "Oggetto e ambito"), new("geometria", "Geometria, azioni e sezione")),
            Group("c2", "Teoria di calcolo", new("metodo", elastic ? "Modello elastico e ipotesi" : micro ? "Metodo Bustamante–Doix" : "Modello di capacità portante"),
                new("criteri", "Criteri e interpretazione dei risultati")),
            Group("c3", "Modello geotecnico", new("terreni", "Stratigrafia e parametri adottati"), new("profili", elastic ? "Determinazione delle rigidezze" : "Profili e condizioni del terreno")),
            Group("c4", elastic ? "Calcolo della risposta elastica" : "Calcolo della capacità portante", new("coefficienti", elastic ? "Rigidezza della sezione" : "Coefficienti ed efficienza del gruppo"), new("resistenze", elastic ? "Estremi, reazioni ed equilibrio" : "Resistenze e meccanismi")),
            Group("c5", "Azioni e verifica", new("verifiche", elastic ? "Convergenza e stato delle verifiche" : micro ? "Riepilogo della verifica" : HorizontalPileVerificationReport.Title), new("grafici", elastic ? "Spostamenti e sollecitazioni" : "Diagrammi della capacità portante")),
            Group("c6", "Dettagli del calcolo", new PileReportContent[] { new("dettagli", elastic ? "Valori lungo il palo e discretizzazione" : "Valori, contributi e verifiche di dettaglio", false), new("armature", "Armature e verifiche per tratto", false) }.Where(c => elastic || c.Key != "armature").ToArray()),
            Group("c7", "Riferimenti", new PileReportContent("fonti", "Riferimenti del metodo", false)), new("avvisi", "Avvisi del calcolo", false)];
    }
    public static HashSet<string> Defaults(string module, JsonObject data, JsonObject? result = null) => Outline(module, data, result)
        .SelectMany(c => c.Descendants()).Where(c => c.Children.Count == 0 && c.DefaultSelected).Select(c => c.Key).Append(PileReportContents.Version).ToHashSet();
    public static DocxDocument Create(string title, string module, JsonObject data, JsonObject result, IReadOnlySet<string> options, IReadOnlyList<ImmagineReport>? images = null)
    {
        if (!Supports(module) || module == "geo_palo_verticale") throw new ArgumentException("Modulo non previsto.", nameof(module));
        if (result.S("errore") != "") throw new ArgumentException("Calcolo non disponibile: " + result.S("errore"));
        bool micro = module == "geo_micropalo_verticale";
        var source = micro ? ReportWord.CreateDocument(title, module, data, result, ReportWord.Sezioni.Select(s => s.Key).ToHashSet())
            : IsElastic(data, result) ? ReportElasticPile.CreateDocument(title, result) : ReportOrizzontale.CreateDocument(title, result);
        return ItecPileReport.CreateFamily(title, module, data, result, options, images ?? [], source);
    }
}
