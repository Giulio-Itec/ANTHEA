using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Reports;

/// <summary>Shared outline for the pile report and its content selector. Only leaves are exported.</summary>
public sealed record PileReportContent(string Key, string Label, bool DefaultSelected = true, bool IsTable = false,
    IReadOnlyList<PileReportContent>? Items = null)
{
    public IReadOnlyList<PileReportContent> Children => Items ?? [];
    public IEnumerable<PileReportContent> Descendants() => new[] { this }.Concat(Children.SelectMany(c => c.Descendants()));
}

public static class PileReportContents
{
    public const string Version = "palo_contenuti_v2";
    public const string Brief = "palo_report_breve";
    static PileReportContent Table(string key, string label, bool selected = true) => new(key, label, selected, true);
    static PileReportContent Group(string key, string label, params PileReportContent[] children) => new(key, label, Items: children);
    public static IReadOnlyList<PileReportContent> Outline(JsonObject data) => [
        new("intestazione", "Intestazione e copertina", false), new("indice", "Indice", false),
        Group("c1", "Oggetto e dati generali", new("oggetto", "Oggetto e unità di misura"), Table("tab_geometria", "Geometria e inquadramento"),
            Group("riferimenti", "Riferimenti e coefficienti adottati", new("normativa", "Riferimenti normativi"), Table("tab_coefficienti", "Coefficienti applicati"))),
        Group("c2", "Teoria di calcolo", new("resistenza", "Resistenza a compressione e trazione"),
            Group("drenate", "Condizioni drenate", new("drenate_testo", "Equazioni del ramo drenato"), Table("tab_k_mu", "Coefficienti K e μ (Viggiani)")),
            Group("non_drenate", "Condizioni non drenate", new("non_drenate_testo", "Equazioni del ramo non drenato"), Table("tab_alfa", "Coefficiente di aderenza α (Viggiani)")),
            new("resistenze_progetto", "Resistenze caratteristiche e di progetto"),
            Group("gruppo", "Efficienza del gruppo", new("efficienza_testo", "Metodo ed efficienze adottate"), Table("tab_efficienza", "Dati e risultati dell’efficienza"))),
        Group("c3", "Modello geotecnico", new[] { new PileReportContent("inquadramento", "Falda e quota della testa") }.Concat(
            Enumerable.Range(0, data.Array("stratigrafie").Count).Select(i => Group("verticale_" + i, $"Verticale di indagine {i + 1}",
                Table("tab_strati_" + i, "Strati e condizioni del modello"), Table("tab_terreni_" + i, "Parametri geotecnici"),
                Table("tab_coefficienti_strati_" + i, "Coefficienti del fusto per strato (K, μ, α)"), new("profilo_" + i, "Profilo stratigrafico")))).ToArray()),
        Group("c4", "Calcolo della capacità portante", Group("fattore_nq", "Fattore di capacità portante Nq",
            Table("tab_punta", "Parametri alla punta"), new("grafico_nq", "Curve Nq")),
            Group("contributi", "Contributi resistenti alla quota finale", new("contributi_testo", "Ramo determinante"), Table("tab_contributi", "Resistenze e ramo determinante"))),
        Group("c5", "Azioni e verifica", Group("azioni", "Azioni assegnate e peso proprio", new("azioni_testo", "Azioni, peso e sottospinta"), Table("tab_azioni", "Azioni assegnate")),
            Group("riepilogo", "Riepilogo delle verifiche", new("verifica_testo", "Criterio di verifica"), Table("tab_verifiche", "Esiti alla quota finale")),
            new("grafico_capacita", "Grafici della capacità portante")),
        Group("c6", "Parametri e tensioni lungo il palo", Table("tab_parametri", "Parametri alla punta e fattori Nq", false), Table("tab_integrazione", "Tensioni e integrazione per strato", false)),
        Group("c7", "Contributi e resistenze lungo il palo", Table("tab_dettagli_resistenze", "Contributi e resistenze a ogni quota", false), Table("tab_dettagli_verifiche", "Verifiche a ogni quota", false)),
        new("avvisi", "Avvisi del calcolo", false)
    ];
    public static HashSet<string> Defaults(JsonObject data) => Outline(data).SelectMany(c => c.Descendants())
        .Where(c => c.Children.Count == 0 && c.DefaultSelected).Select(c => c.Key).Append(Version).ToHashSet();
    public static HashSet<string> All(JsonObject data) => Outline(data).SelectMany(c => c.Descendants())
        .Where(c => c.Children.Count == 0).Select(c => c.Key).Append(Version).ToHashSet();
    public static HashSet<string> Normalize(JsonObject data, HashSet<string>? options)
    {
        if (options is null) return Defaults(data);
        if (options.Contains(Version)) return options.ToHashSet();
        var mapped = new HashSet<string> { Version };
        var outline = Outline(data).SelectMany(c => c.Descendants()).ToArray();
        void Add(params string[] keys) { foreach (var key in keys) foreach (var item in outline.Where(c => c.Key == key).SelectMany(c => c.Descendants()).Where(c => c.Children.Count == 0)) mapped.Add(item.Key); }
        foreach (var key in options) switch (key)
        {
            case "generali": Add("oggetto", "tab_geometria"); break;
            case "coefficienti": Add("riferimenti"); break;
            case "criteri": Add("resistenza", "drenate", "non_drenate", "resistenze_progetto"); break;
            case "efficienza": Add("gruppo"); break;
            case "stratigrafia": Add("c3"); break;
            case "nq": Add("tab_punta"); break;
            case "risultati": Add("contributi", "azioni", "riepilogo"); break;
            case "dettagli": Add("c6", "c7"); break;
            case "grafico_profilo": foreach (var c in outline.Where(c => c.Key.StartsWith("profilo_"))) Add(c.Key); break;
            default: Add(key); break;
        }
        return mapped;
    }
}
