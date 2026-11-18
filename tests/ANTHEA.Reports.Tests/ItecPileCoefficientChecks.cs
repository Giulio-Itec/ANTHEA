using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Calculations;
using Anthea.Reports;
using X.Core;

/// <summary>
/// The coefficients of the shaft in the ITEC report of the vertical pile (18/11/2026): the tables of K, μ and α in the form of Viggiani, Fondazioni,
/// Tab. 13.2 and 13.3, with the rows of the pile highlighted, and K, μ, α of every layer as the engine used them. The expected values are
/// independent of the code: the printed tables of Viggiani (bored piles 0,5 loose and 0,4 dense as printed) and the hand values of the layers
/// (μ = tan φ′, α of the bored piles from cu).
/// </summary>
internal static class ItecPileCoefficientChecks
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    const string Highlight = "FFF2CC";

    internal static int Run(string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Coefficienti del palo: " + message); checks++; }
        string directory = Path.Combine(output, "palo-coefficienti"); Directory.CreateDirectory(directory);

        // A bored pile D 1,2 m, L 12 m, water table at 3 m: a dense granular layer (φ′ 32°) and a dense cohesive one (φ′ 24°, cu 80 kPa).
        JsonObject Sheet(string type = "Trivellato", string subtype = "", string head = "")
        {
            var data = Archivio.NuovoFoglio("geo_palo_verticale"); var g = data["generali"]!;
            g["tipo_palo"] = type; if (subtype != "") g["sottotipo_palo_battuto"] = subtype; if (head != "") g["profondita_testa"] = head;
            g["diametro"] = "1.2"; g["lunghezza"] = "12"; g["presenza_falda"] = true; g["profondita_falda"] = "3";
            g["azione_compressione"] = "1000"; g["azione_trazione"] = "200";
            var rows = data.Array("stratigrafie")[0]!.AsArray();
            var sand = rows[0]!;
            sand["spessore"] = "6"; sand["tipologia"] = "Granulare"; sand["addensamento"] = "Denso"; sand["peso_specifico"] = "18"; sand["peso_specifico_saturo"] = "20";
            sand["angolo_attrito"] = "32"; sand["coesione_efficace"] = "0"; sand["coesione_non_drenata"] = "0";
            var clay = sand.DeepClone(); clay["tipologia"] = "Coesivo"; clay["spessore"] = "8"; clay["angolo_attrito"] = "24"; clay["coesione_non_drenata"] = "80"; clay["nc"] = "9";
            rows.Add(clay);
            return data;
        }
        (XElement Body, string Text) Report(JsonObject data, HashSet<string>? options = null, string name = "")
        {
            var result = Calcolo.Calcola(data);
            Check(result.S("errore") == "", "scenario non calcolabile: " + result.S("errore"));
            var document = ReportWord.CreateDocument("Coefficienti del fusto", "geo_palo_verticale", data, result, options ?? PileReportContents.Defaults(data));
            if (name != "") File.WriteAllBytes(Path.Combine(directory, name + ".docx"), DocxWriter.Write(document));
            var body = document.GetXml("word/document.xml").Root!;
            return (body, string.Concat(body.Descendants(W + "t").Select(t => t.Value)));
        }
        static string Cell(XElement c) => string.Concat(c.Descendants(W + "t").Select(t => t.Value));
        static bool Lit(XElement c) => c.Element(W + "tcPr")?.Element(W + "shd")?.Attribute(W + "fill")?.Value == Highlight;
        XElement TableAfter(XElement body, string caption)
        {
            var title = body.Descendants(W + "p").FirstOrDefault(p => Cell(p).EndsWith(caption, StringComparison.Ordinal));
            Check(title is not null, "manca la tabella «" + caption + "»");
            return title!.ElementsAfterSelf(W + "tbl").First();
        }
        static string[][] Rows(XElement table) => table.Elements(W + "tr").Select(r => r.Elements(W + "tc").Select(Cell).ToArray()).ToArray();

        // The theory: K and μ of Viggiani Tab. 13.2, the bored pile highlighted.
        var (body, text) = Report(Sheet(), name: "trivellato");
        var shaft = TableAfter(body, "Coefficienti K e μ del fusto in condizioni drenate");
        var rows = Rows(shaft);
        string[][] expected = [["Battuto", "Profilato d'acciaio", "0,7", "1,0", "tan 20°"], ["", "Tubo d'acciaio chiuso", "1,0", "2,0", "tan 20°"],
            ["", "Calcestruzzo prefabbricato", "1,0", "2,0", "tan (¾ φ′)"], ["", "Calcestruzzo gettato in opera", "1,0", "3,0", "tan φ′"],
            ["Trivellato", "0,5", "0,4", "tan φ′"], ["Elica continua", "0,7", "0,9", "tan φ′"]];
        Check(rows.Length == 8 && rows.Skip(2).Zip(expected).All(p => p.First.SequenceEqual(p.Second)), "K e μ diversi da Viggiani Tab. 13.2: " + string.Join(" / ", rows.Select(r => string.Join("|", r))));
        Check(rows[0].SequenceEqual(["Tipo di palo", "K — stato di addensamento", "μ"]) && rows[1].SequenceEqual(["", "sciolto", "denso", ""]), "intestazione su due righe");
        var shaftRows = shaft.Elements(W + "tr").ToArray();
        Check(shaftRows[6].Elements(W + "tc").All(Lit) && shaftRows.Where((_, i) => i != 6).All(r => !r.Elements(W + "tc").Any(Lit)), "riga del trivellato non evidenziata da sola");
        Check(shaftRows[2].Elements(W + "tc").First().Element(W + "tcPr")!.Element(W + "vMerge")?.Attribute(W + "val")?.Value == "restart"
            && shaftRows[5].Elements(W + "tc").First().Element(W + "tcPr")!.Element(W + "vMerge") is { } merged && merged.Attribute(W + "val") is null, "«Battuto» non unito sulle quattro righe");

        // α of Viggiani Tab. 13.3: the constant values and the linear rule of the library (at cu = 40 kPa: 1 − 0,0111 · 15 = 0,8335; 0,7 − 0,008 · 15 = 0,58).
        var adhesion = Rows(TableAfter(body, "Coefficiente di aderenza α in condizioni non drenate"));
        string[][] alpha = [["Battuto", "cu ≤ 25", "1,0"], ["", "25 < cu < 70", "1 − 0,0111 (cu − 25)"], ["", "cu ≥ 70", "0,5"],
            ["Trivellato", "cu ≤ 25", "0,7"], ["", "25 < cu < 70", "0,7 − 0,008 (cu − 25)"], ["", "cu ≥ 70", "0,35"]];
        Check(adhesion.Length == 7 && adhesion.Skip(1).Zip(alpha).All(p => p.First.SequenceEqual(p.Second)), "α diverso da Viggiani Tab. 13.3: " + string.Join(" / ", adhesion.Select(r => string.Join("|", r))));
        Check(Math.Abs(Calcolo.CoefficienteAlfa(J.Obj(("tipo_palo", "Battuto")), 40) - 0.8335) < 1e-9 && Math.Abs(Calcolo.CoefficienteAlfa(J.Obj(("tipo_palo", "Trivellato")), 40) - 0.58) < 1e-9,
            "la regola lineare stampata non è quella della libreria");
        var adhesionRows = TableAfter(body, "Coefficiente di aderenza α in condizioni non drenate").Elements(W + "tr").ToArray();
        Check(adhesionRows.Skip(4).All(r => r.Elements(W + "tc").All(Lit)) && adhesionRows.Take(4).All(r => !r.Elements(W + "tc").Any(Lit)), "righe del trivellato non evidenziate");

        // The geotechnical model: K, μ, α of each layer (K dense bored 0,4; μ = tan 32° = 0,62, tan 24° = 0,45; α(80 kPa) bored 0,35 below the water table).
        var layers = Rows(TableAfter(body, "Coefficienti del fusto per strato"));
        Check(layers.Length == 3 && layers[0].SequenceEqual(["Strato", "Tipologia", "Stato", "φ′ [°]", "cu [kPa]", "K [−]", "μ [−]", "α [−]"])
            && layers[1].SequenceEqual(["1", "Granulare", "Denso", "32", "0", "0,40", "0,62", "—"]) && layers[2].SequenceEqual(["2", "Coesivo", "Denso", "24", "80", "0,40", "0,45", "0,35"]),
            "coefficienti per strato: " + string.Join(" / ", layers.Select(r => string.Join("|", r))));
        Check(text.Contains("K dalla Tabella 2.1 per la tecnologia «Trivellato»") && text.Contains("α dalla Tabella 2.2") && text.Contains("μ = tan φ′"), "nota della tabella per strato");
        Check(text.Contains("Tabella 2.1  Coefficienti K e μ") && text.Contains("Tabella 2.2  Coefficiente di aderenza") && text.Contains("Tabella 3.3  Coefficienti del fusto per strato"),
            "numerazione delle tabelle");

        // A driven precast pile: its row of K and μ and the rows «Battuto» of α highlighted; μ = tan (3/4 φ′): tan 24° = 0,45 and tan 18° = 0,32.
        var (precast, precastText) = Report(Sheet("Battuto", "Calcestruzzo prefabbricato"), name: "battuto-prefabbricato");
        var precastShaft = TableAfter(precast, "Coefficienti K e μ del fusto in condizioni drenate").Elements(W + "tr").ToArray();
        Check(precastShaft[4].Elements(W + "tc").Skip(1).All(Lit) && !precastShaft[6].Elements(W + "tc").Any(Lit), "riga del prefabbricato non evidenziata");
        var precastAdhesion = TableAfter(precast, "Coefficiente di aderenza α in condizioni non drenate").Elements(W + "tr").ToArray();
        Check(precastAdhesion.Skip(1).Take(3).All(r => r.Elements(W + "tc").All(Lit)) && precastAdhesion.Skip(4).All(r => !r.Elements(W + "tc").Any(Lit)), "righe del battuto non evidenziate");
        var precastLayers = Rows(TableAfter(precast, "Coefficienti del fusto per strato"));
        Check(precastLayers[1][5] == "2,00" && precastLayers[1][6] == "0,45" && precastLayers[2][6] == "0,32" && precastLayers[2][7] == "0,50",
            "coefficienti del prefabbricato (K denso 2, μ = tan 24° = 0,45 e tan 18° = 0,32, α(80) battuto 0,5): " + string.Join(" / ", precastLayers.Skip(1).Select(r => string.Join("|", r))));
        Check(precastText.Contains("μ = tan (¾ φ′)"), "regola di μ del prefabbricato nella nota");

        // The head 7 m below the ground: the granular layer (0–6 m) is above the head, without shaft.
        var (deep, _) = Report(Sheet(head: "7"), name: "testa-profonda");
        var deepLayers = Rows(TableAfter(deep, "Coefficienti del fusto per strato"));
        Check(deepLayers[1].Skip(5).All(v => v == "—") && deepLayers[2][5] == "0,40" && deepLayers[2][7] == "0,35", "strato sopra la testa senza coefficienti: " + string.Join(" / ", deepLayers.Skip(1).Select(r => string.Join("|", r))));

        // The contents: three new choices, on by default; without the tables of the theory the note of the layers names them in words.
        var data = Sheet();
        var keys = PileReportContents.Outline(data).SelectMany(c => c.Descendants()).Select(c => c.Key).ToHashSet();
        Check(new[] { "tab_k_mu", "tab_alfa", "tab_coefficienti_strati_0", "drenate_testo", "non_drenate_testo" }.All(keys.Contains)
            && new[] { "tab_k_mu", "tab_alfa", "tab_coefficienti_strati_0" }.All(PileReportContents.Defaults(data).Contains), "voci dei contenuti");
        var only = PileReportContents.Defaults(data); only.Remove("tab_k_mu"); only.Remove("tab_alfa");
        var (bare, bareText) = Report(data, only);
        Check(!bareText.Contains("Coefficienti K e μ del fusto in condizioni drenate") && !bareText.Contains("Coefficiente di aderenza α") && bareText.Contains("K dalla tabella dei coefficienti K e μ")
            && bareText.Contains("motore corrente considera c′"), "tabelle della teoria escluse");
        var legacy = PileReportContents.Normalize(data, ["criteri", "stratigrafia"]);
        Check(new[] { "drenate_testo", "tab_k_mu", "non_drenate_testo", "tab_alfa", "tab_coefficienti_strati_0" }.All(legacy.Contains), "selezioni storiche «criteri» e «stratigrafia»");
        return checks;
    }
}
