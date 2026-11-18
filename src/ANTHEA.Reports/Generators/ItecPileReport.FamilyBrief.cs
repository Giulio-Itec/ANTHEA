using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

namespace Anthea.Reports;

internal sealed partial class ItecPileReport
{
    void BuildFamilyBrief(string title, string module, JsonObject data, JsonObject result,
        IReadOnlyList<ImmagineReport> images, Dictionary<string, List<XElement>> blocks)
    {
        body.RemoveNodes();
        BriefText(FamilyTitle(module, result), 14, 100);
        BriefText("Report breve · " + title, 10, 160);
        bool micro = module == "geo_micropalo_verticale", elastic = result.S("tipo_risultato") == "palo_elastico";
        if (micro) MicroBrief(data, result, blocks, images);
        else if (elastic) ElasticBrief(result, images);
        else HorizontalBrief(result, images);
        FinishFamily();
    }
    void SmallTable(string[] headers, List<string[]> rows, double[]? widths = null) => BriefTable(headers, rows, widths ?? Enumerable.Repeat(1d, headers.Length).ToArray(), preservePrecision: true);
    // Scientific notation retains small rotations and displacements that would round to zero with 2 decimal places.
    static string Precise(JsonNode? value) => J.Number(value) is double n ? n.ToString("G4", CultureInfo.GetCultureInfo("it-IT")) : Value(value);
    void BriefFigure(IReadOnlyList<ImmagineReport> images)
    {
        if (images.Count > 0) { Image(images[0]); body.Elements().Last().ReplaceWith(P("Figura 1  " + images[0].Titolo, "Didascalia")); }
        else BriefText("Grafico non disponibile; i valori del risultato sono riportati nelle tabelle.");
    }
    void MicroBrief(JsonObject data, JsonObject result, Dictionary<string, List<XElement>> blocks, IReadOnlyList<ImmagineReport> images)
    {
        var g = data["generali"]!;
        BriefText("Verifica assiale a compressione e trazione con metodo Bustamante–Doix. " + (result.B("copertura_completa") ? "Copertura stratigrafica completa." : "VERIFICA INCOMPLETA: stratigrafia insufficiente."));
        BriefHeading("1  Oggetto e dati generali");
        SmallTable(["Db [m]", "L [m]", "θ [°]", "Testa [m]", "Inizio aderenza [m]", "Iniezione"],
            [[Value(g["diametro"]), Value(g["lunghezza"]), Value(g["inclinazione"]), Value(g["profondita_testa"] ?? JsonValue.Create(0)), Value(g["inizio_aderenza"]), g.S("tipo_iniezione")]]);
        BriefText($"Pressione di iniezione pi = {Value(g["pressione_iniezione"])} MPa; γp = {Value(g["peso_specifico_palo"])} kN/m³. Coordinate s lungo asse dalla testa; z verticale. Peso proprio proiettato sull’asse.");
        BriefHeading("2  Modello geotecnico e metodo");
        var layers = data.Array("stratigrafie").SelectMany(s => s!.AsArray()).ToArray();
        var rows = layers.GroupBy(s => s.S("terreno")).Select(group => new[] { group.Key, group.Count().ToString(), BriefRange(group, "alpha"),
            group.All(s => s.B("laterale_attiva", true)) ? "Attiva" : group.All(s => !s.B("laterale_attiva", true)) ? "Esclusa" : "Attiva / esclusa" }).ToList();
        SmallTable(["Terreno", "N. strati", "α", "Aderenza"], rows, [3, 1, 1, 1.8]);
        BriefText($"{data.Array("stratigrafie").Count} verticali: sintesi di tutti gli strati; sequenze e spessori nel report completo. L’aderenza deriva dagli abachi del metodo, con pi e terreno assegnati; α maggiora il diametro.", 10);
        BriefText(g.B("considera_punta") ? $"Punta inclusa: Rb = {Value(g["percentuale_punta"])}% di Rs." : "Resistenza di punta esclusa.");
        BriefHeading("3  Coefficienti e azioni assegnate");
        var table = blocks["coefficienti"].Last(b => b.Name == W + "tbl");
        var coeff = table.Elements(W + "tr").Skip(1).Select(r => r.Elements(W + "tc").Select(Text).ToArray()).ToList();
        SmallTable(coeff.Select(r => r[0]).ToArray(), [coeff.Select(r => r[1]).ToArray()]);
        BriefText($"Gruppo: {data["efficienza"].S("metodo")}; ηc = {Value(result["efficienza"]?["eta_compressione"])}; ηt = {Value(result["efficienza"]?["eta_trazione"])}.");
        SmallTable(["Compressione in testa [kN]", "Trazione in testa [kN]", "Peso finale [kN]"],
            [[Value(g["azione_compressione"]), Value(g["azione_trazione"]), Value(result.Array("dettagli").LastOrDefault()?["peso"])]]);
        BriefHeading("4  Azioni e verifica alla quota finale", true);
        var verification = blocks["verifiche"].Single(b => b.Name == W + "tbl");
        var verificationRows = verification.Elements(W + "tr").Skip(1).Select(r => r.Elements(W + "tc").Select(Text).ToArray()).ToList();
        SmallTable(["Verifica", "s [m]", "Rd [kN]", "Ed [kN]", "Esito"], verificationRows, [1.6, .8, 1, 1, 2.5]);
        BriefText("Ed comprende il peso proprio secondo i coefficienti del foglio. Le azioni in testa sono già di progetto; i valori sono quelli del motore.", 10);
        BriefWarnings(result);
        BriefHeading("5  Capacità portante lungo il micropalo");
        BriefFigure(images.Where(i => i.Categoria == "grafico_capacita").ToArray());
    }
    void HorizontalBrief(JsonObject result, IReadOnlyList<ImmagineReport> images)
    {
        var data = result["input"]!; var g = data["generali"]!;
        BriefText("Metodo: " + result.S("metodo_calcolo") + ".");
        BriefHeading("1  Oggetto e dati generali");
        SmallTable(["D [m]", "L [m]", "e [m]", "Testa", "HEd [kN]", "N [kN]"],
            [[Value(g["diametro"]), Value(g["lunghezza"]), Value(g["eccentricita"]), g.S("vincolo"), Value(g["azione_orizzontale"]), Value(g["azione_assiale"])]]);
        BriefText($"My adottato = {Value(result["momento_resistente_knm"])} kNm. Origine: {g.S("origine_momento")}. " + g.S("provenienza_momento"));
        BriefHeading("2  Modello geotecnico");
        var layers = data.Array("stratigrafie").SelectMany(s => s!.AsArray()).ToArray();
        SmallTable(["Terreno", "N. strati", "γ / γsat [kN/m³]", "φ′ [°]", "cu [kPa]"], layers.GroupBy(s => s.S("tipologia")).Select(gr => new[] { gr.Key, gr.Count().ToString(), BriefRange(gr, "peso_specifico") + " / " + BriefRange(gr, "peso_specifico_saturo"), BriefRange(gr, "angolo_attrito"), BriefRange(gr, "coesione_non_drenata") }).ToList(), [1.6, .9, 2, 1, 1]);
        BriefText($"{data.Array("stratigrafie").Count} verticali; intervalli su tutti gli strati, sequenze nel report completo. " + (g.B("presenza_falda") ? $"Falda a {Value(g["profondita_falda"])} m." : "Falda assente."), 10);
        BriefText("Coesivo non drenato o granulare drenato, senza modello c–φ. La capacità Hu deriva dal confronto dei meccanismi e dall’equilibrio del palo. " + (result.B("sperimentale") || result.S("metodo_calcolo").Contains("Stratificato", StringComparison.OrdinalIgnoreCase) ? "Le formule sono state riadattate per tenere conto della presenza di più strati, valutando le reazioni con le proprietà di ciascuno strato." : "Metodo omogeneo di Broms."));
        BriefHeading("3  Coefficienti ed efficienza");
        SmallTable(["Verticali indagate", "ξ₃", "ξ₄", "γR", "η"], [[Value(result["verticali_indagate"]), Value(result["xi3"]), Value(result["xi4"]), Value(result["gamma_r"]), Value(result["efficienza"]?["eta"])]]);
        BriefText("Efficienza: " + result["efficienza"].S("metodo") + ".", after: 0);
        body.Add(BriefParagraph("Rk = min(Hu,media/ξ₃; Hu,min/ξ₄);     Rd = η Rk/γR", align: "center"));
        BriefHeading("4  Resistenza e verifica", true);
        BriefText($"Hu = {Value(result["capacita_kn"])} kN; verticale governante {Value(result["sondaggio_governante"])}; meccanismo: {result.S("meccanismo").ToLowerInvariant()}.");
        body.Add(BriefParagraph("Condizione di verifica: HEd ≤ Rd; rapporto di utilizzo HEd/Rd ≤ 1.", align: "center"));
        var verification = HorizontalPileVerificationReport.Table(result);
        SmallTable(verification.Colonne, verification.Righe, [1, 1, 1, 1, 2]);
        if (!HorizontalPileVerificationReport.Available(result)) BriefText(HorizontalPileVerificationReport.MissingResistance);
        BriefWarnings(result);
        BriefHeading("5  Sollecitazioni alla capacità ultima");
        BriefText("Diagrammi della verticale governante alla Hu, non al carico HEd. Il tratto sotto la cerniera è un completamento idealizzato; eventuali forze concentrate restano distinte. Spostamenti e rotazioni richiedono l’analisi elastica.", 10);
        BriefFigure(images);
    }
    void ElasticBrief(JsonObject result, IReadOnlyList<ImmagineReport> images)
    {
        var d = result["input"]!; var r = result["risposta"]!;
        BriefText("Risposta al carico assegnato. Il modello elastico non determina la capacità ultima né un esito di verifica geotecnica.");
        BriefHeading("1  Geometria e azioni");
        SmallTable(["D [m]", "L [m]", "L libero [m]", "H [kN]", "C [kNm]", "N [kN]"], [[Value(d["diametro"]), Value(d["lunghezza"]), Value(d["libero"]), Value(d["H"]), Value(d["C"]), Value(d["N"])]]);
        BriefText($"Testa: {d.S("vincolo")}; punta: {d.S("punta")}. EI = {Precise(d["EI"])} kNm²; origine: {d.S("fonte_EI")}. Peso lineare = {Value(d["peso_lineare"])} kN/m.");
        BriefHeading("2  Modello geotecnico e rigidezze");
        var parameters = result.Array("parametri_terreno");
        SmallTable(["Legge", "N. tratti", "Valore adottato", "Unità"], parameters.GroupBy(p => (Law: p.S("Law"), Unit: p.S("Unit"))).Select(gr => new[] { ElasticHorizontalPile.ParameterLabel(gr.Key.Law), gr.Count().ToString(), BriefRange(gr, "AdoptedValue"), gr.Key.Unit }).ToList(), [2, 1, 2, 1]);
        BriefText("Intervalli su tutti i tratti; sequenza, fonti e condizioni di applicabilità nel report completo. " + (d.B("falda") ? $"Falda a {Value(d["z_falda"])} m." : "Falda assente."), 10);
        foreach (var p in parameters.Where(p => p.B("Overridden"))) BriefText($"Strato {Value(p?["Layer"])}: override — {p.S("OverrideReason")}", 10);
        BriefHeading("3  Criteri e controllo del modello");
        BriefText("FEM Euler–Bernoulli con matrice di fondazione consistente; rigidezza EI assegnata. N positivo a compressione, senza trasferimento assiale al terreno, galleggiamento automatico o effetti del secondo ordine.");
        BriefText($"Mesh: {Value(r["Elements"])} elementi; confronto {Value(r["ComparisonElements"])}. Convergenza: " + (r.B("MeshConverged") ? "soddisfatta." : "NON soddisfatta: raffinare.") + $" Residui: F = {Precise(r["ForceResidual"])} kN, M = {Precise(r["MomentResidual"])} kNm.");
        BriefHeading("4  Risposta e stato delle verifiche", true);
        SmallTable(["Grandezza", "Estremo assoluto", "x dalla testa [m]"], new[] { ("y", "y [m]"), ("theta", "θ [rad]"), ("N", "N [kN]"), ("V", "V [kN]"), ("M", "M [kNm]") }.Select(k => new[] { k.Item2, Precise(r["Extrema"]?[k.Item1]?["AbsoluteMaximum"]), Value(r["Extrema"]?[k.Item1]?["AbsoluteMaximumDepth"]) }).ToList(), [1.8, 2, 2]);
        BriefText(ReinforcementState(result), 10);
        BriefHeading("5  Spostamenti e sollecitazioni");
        BriefText("Risultati sotto le azioni assegnate; profondità dalla testa. L’analisi elastica non fornisce automaticamente verifiche di resistenza o di esercizio.", 10);
        BriefFigure(images);
    }
    void BriefWarnings(JsonObject result)
    {
        var warnings = result.Array("avvisi").Select(w => w?.ToString()).Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => result["sezione"] is JsonObject section && section.S("tipo") != "CHS" && w == section.S("modello") ? HorizontalPileResistanceReport.ConcreteBrief : w).ToArray();
        if (warnings.Length > 0) BriefText("Avvisi: " + HorizontalPileWording.Display(string.Join(" · ", warnings)), 10);
    }
}
