using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

namespace Anthea.Reports;

internal sealed partial class ItecPileReport
{
    internal static DocxDocument CreateBrief(string title, JsonObject data, JsonObject result,
        IReadOnlyList<ImmagineReport> images, DocxDocument summary)
    {
        var report = new ItecPileReport();
        report.BuildBrief(title, data, result, images, summary);
        return report.document;
    }

    void BuildBrief(string title, JsonObject data, JsonObject result, IReadOnlyList<ImmagineReport> images, DocxDocument summary)
    {
        body.RemoveNodes();
        var sourceTables = summary.GetXml("word/document.xml").Descendants(W + "tbl").ToArray();
        List<string[]> ReadTable(string prefix) => sourceTables.Single(t => t.PreviousNode is XElement p && Text(p).StartsWith(prefix, StringComparison.Ordinal))
            .Elements(W + "tr").Skip(1).Select(r => r.Elements(W + "tc").Select(Text).ToArray()).ToList();
        var coefficients = ReadTable("Coefficienti applicati").ToDictionary(row => row[0], row => row[1]);
        var verification = ReadTable("Resistenze di progetto");
        var g = data["generali"]!;
        var last = result.Array("dettagli").LastOrDefault();
        bool complete = result.B("copertura_completa");
        string outcome = !complete ? "Verifica incompleta: stratigrafia insufficiente."
            : verification.Any(r => r[^1] == "Verifica non soddisfatta") ? "Una o più verifiche non sono soddisfatte alla quota finale."
            : verification.Any(r => r[^1] != "Verifica soddisfatta") ? "Azione non inserita: una o più verifiche non sono determinabili."
            : "Verifiche soddisfatte alla quota finale per le azioni assegnate.";

        var titleParagraph = BriefParagraph("Capacità portante verticale del palo", size: 14, after: 100);
        titleParagraph.Element(W + "r")!.Element(W + "rPr")!.AddFirst(new XElement(W + "rFonts", new XAttribute(W + "ascii", "Manrope ExtraBold"), new XAttribute(W + "hAnsi", "Manrope ExtraBold")));
        body.Add(titleParagraph);
        BriefText("Report breve · " + title, 10, 160);
        BriefText("Sintesi della verifica geotecnica a compressione e trazione. " + outcome, 12, 140);

        BriefHeading("1  Oggetto e dati generali");
        BriefTable(["Palo", "D [m]", "L [m]", "Testa [m]", "γp [kN/m³]", "Verticali"],
            [[g.S("tipo_palo"), Value(g["diametro"]), Value(g["lunghezza"]), Value(g["profondita_testa"] ?? JsonValue.Create(0)), Value(g["peso_specifico_palo"]), data.Array("stratigrafie").Count.ToString()]], [2.4, 1, 1, 1.3, 1.7, 1.3]);
        BriefText("Quote stratigrafiche dal piano campagna; profondità z dalla testa del palo. " +
            (g.S("tipo_palo") == "Battuto" ? "Tipo: " + g.S("sottotipo_palo_battuto") + "." : ""));

        BriefHeading("2  Modello geotecnico");
        var surveys = data.Array("stratigrafie");
        var layers = surveys.SelectMany(s => s!.AsArray()).ToArray();
        BriefText($"Verticali di indagine: {surveys.Count}. " + (g.B("presenza_falda") ? $"Falda a {Value(g["profondita_falda"])} m dal piano campagna. " : "Falda assente. ")
            + (complete ? "Copertura stratigrafica completa." : "Copertura stratigrafica insufficiente."));
        bool detailed = surveys.Count == 1 && layers.Length <= 3;
        var soilRows = new List<string[]>();
        if (detailed)
        {
            double top = 0;
            foreach (var (layer, index) in layers.Select((s, i) => (s, i)))
            {
                double thickness = J.Number(layer?["spessore"]) ?? throw new InvalidDataException("Spessore dello strato assente nel report breve.");
                double bottom = top + thickness;
                soilRows.Add([$"{index + 1}  {layer.S("tipologia")}", BriefNumber(top) + "–" + BriefNumber(bottom),
                    Value(layer?["peso_specifico"]) + " / " + Value(layer?["peso_specifico_saturo"]), Value(layer?["angolo_attrito"]),
                    Value(layer?["coesione_efficace"]) + " / " + Value(layer?["coesione_non_drenata"]), layer.B("laterale_attiva", true) ? "Attiva" : "Esclusa"]);
                top = bottom;
            }
        }
        else
        {
            foreach (var group in layers.GroupBy(s => (Type: s.S("tipologia"), Active: s.B("laterale_attiva", true))))
                soilRows.Add([group.Key.Type, group.Count().ToString(), BriefRange(group, "peso_specifico") + " / " + BriefRange(group, "peso_specifico_saturo"),
                    BriefRange(group, "angolo_attrito"), BriefRange(group, "coesione_efficace") + " / " + BriefRange(group, "coesione_non_drenata"), group.Key.Active ? "Attiva" : "Esclusa"]);
        }
        BriefTable([detailed ? "Strato e terreno" : "Terreno", detailed ? "Quota [m]" : "N. strati", "γ / γsat\n[kN/m³]", "φ′ [°]", "c′ / cu\n[kPa]", "Laterale"], soilRows, [2.8, 1.5, 1.8, 1.1, 1.7, 1.5]);
        if (!detailed) BriefText("Intervalli dei parametri su tutti gli strati e tutte le verticali; la sequenza degli strati è riportata nel report completo.", 10);
        var cohesive = layers.Where(s => s.S("tipologia") == "Coesivo").ToArray();
        if (cohesive.Length > 0) BriefText("Nc nei terreni coesivi: " + BriefRange(cohesive, "nc") + ". Nel ramo non drenato, il tratto sopra falda è trattato in condizioni drenate.");

        BriefHeading("3  Criteri coefficienti e azioni");
        BriefText("NTC 2018, § 6.4.3.1.1, tabelle 6.4.II e 6.4.IV; azioni sui ponti: tabella 5.1.V. Approccio 2: A1 + M1 + R3.");
        string[] keys = ["ξ3", "ξ4", "γb", "γs,c", "γs,t", "γG sfavorevole", "γG favorevole"];
        BriefTable(["ξ3", "ξ4", "γb", "γs,c", "γs,t", "γG sfav.", "γG fav."], [keys.Select(k => coefficients[k]).ToArray()], [1, 1, 1, 1, 1, 1.6, 1.6]);
        BriefText($"Efficienza del gruppo: ηc = {Value(result["efficienza"]?["eta_compressione"])}, ηt = {Value(result["efficienza"]?["eta_trazione"])}. Azioni in testa già di progetto, prima del contributo del peso del palo.");
        BriefTable(["Compressione in testa [kN]", "Trazione in testa [kN]", "Peso alla quota disponibile [kN]"],
            [[Value(g["azione_compressione"]), Value(g["azione_trazione"]), Value(last?["peso"])]], [1, 1, 1]);
        BriefText("Le azioni Ed comprendono il peso e il trattamento della sottospinta previsto dal modello per ciascuna condizione.", after: 0);

        BriefHeading(complete ? "4  Resistenze e verifiche alla punta" : "4  Risultati alla quota disponibile", page: true);
        BriefText($"Quota disponibile z = {Value(last?["z"])} m dalla testa.", after: 0);
        body.Add(BriefParagraph("Ed ≤ Rd;     I = Ed/Rd ≤ 1", align: "center"));
        var details = Tabelle.Crea(result, false);
        var rows = result["curve"]!.AsObject().Select((kv, i) =>
        {
            var end = kv.Value!.Array("progetto")[^1]!;
            var action = Calcolo.AzioniCurva(result, kv.Key)?.LastOrDefault();
            string index = details.Single(t => t.Titolo == kv.Key.Replace('_', ' ') + " — verifica a ogni quota").Righe[^1][5];
            return new[] { Condition(kv.Key), Value(end[1]), Value(action?[1]), index,
                verification[i][^1].Replace("Verifica non soddisfatta", "Non verificata").Replace("Verifica soddisfatta", "Verificata")
                    .Replace("Verifica incompleta: stratigrafia insufficiente", "Incompleta").Replace("Azione non inserita", "Azione assente") };
        }).ToList();
        BriefTable(["Verifica", "Rd [kN]", "Ed [kN]", "I", "Esito"], rows, [3.2, 1.5, 1.5, .8, 2.2]);
        BriefText(outcome);
        var warnings = result.Array("avvisi").Select(v => v?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
        var limitations = new List<string>();
        if (warnings.Any(s => s.StartsWith("ATTENZIONE Nq:", StringComparison.Ordinal))) limitations.Add("φ fuori dal campo dell’abaco: adottato il bordo");
        if (warnings.Any(s => s.Contains("z/D < 4", StringComparison.Ordinal))) limitations.Add("per z/D < 4 adottata la curva di bordo");
        if (limitations.Count > 0) BriefText("Avviso Nq: " + string.Join("; ", limitations) + ".", 10);
        foreach (var warning in warnings.Where(s => !s.StartsWith("Nq:", StringComparison.Ordinal) && !s.StartsWith("ATTENZIONE Nq:", StringComparison.Ordinal) && !s.StartsWith("Ipotesi progettuale", StringComparison.Ordinal)))
            BriefText(warning, 10);
        if (J.Number(g["peso_palo_sfavorevole"]) != 1.35 || J.Number(g["peso_palo_favorevole"]) != 1)
            BriefText("Attenzione: coefficienti del peso diversi da 1,35 sfavorevole e 1,00 favorevole previsti per questa relazione sui ponti.", 10);
        BriefHeading("5  Andamento della capacità portante");
        foreach (var image in images.Where(im => im.Categoria == "grafico_capacita").Take(1))
        {
            Image(image);
            var caption = body.Elements().Last();
            caption.ReplaceWith(BriefParagraph("Resistenze di progetto e azioni lungo il palo", 10, 0, "Didascalia", "center"));
            var picture = body.Elements().Last().PreviousNode as XElement;
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            var extent = picture!.Descendants(wp + "extent").Single();
            const long width = 5760000;
            long height = (long)extent.Attribute("cy")! * width / (long)extent.Attribute("cx")!;
            extent.SetAttributeValue("cx", width); extent.SetAttributeValue("cy", height);
            foreach (var size in picture.Descendants(a + "xfrm").Elements(a + "ext")) { size.SetAttributeValue("cx", width); size.SetAttributeValue("cy", height); }
            picture.Element(W + "pPr")!.Add(new XElement(W + "jc", new XAttribute(W + "val", "center")));
        }
        var section = new XElement(document.GetXml("anthea/body-section.xml").Root!);
        foreach (var name in new[] { "headerReference", "footerReference", "pgNumType", "type" }) section.Elements(W + name).Remove();
        section.AddFirst(new XElement(W + "type", new XAttribute(W + "val", "nextPage")));
        section.Element(W + "pgMar")!.SetAttributeValue(W + "bottom", 1701);
        body.Add(section);
        document.SetXml("word/document.xml", body.Document!.ToString());
        document.SetXml("word/_rels/document.xml.rels", relations.ToString());
        document.SetXml("anthea/layout.xml", "<layout coverBlocks=\"0\" />");
        var settings = document.GetXml("word/settings.xml");
        foreach (var name in new[] { "updateFields", "footnotePr", "endnotePr", "attachedTemplate", "linkStyles" }) settings.Root!.Elements(W + name).Remove();
        document.SetXml("word/settings.xml", settings.ToString());
    }

    static string BriefNumber(double value) => value.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
    static string BriefRange(IEnumerable<JsonNode?> layers, string key)
    {
        var values = layers.Select(s => J.Number(s?[key])).ToArray();
        if (values.Any(v => !v.HasValue || !double.IsFinite(v.Value))) return "—";
        var distinct = values.Select(v => v!.Value).Distinct().Order().ToArray();
        return distinct.Length == 0 ? "—" : distinct.Length == 1 ? BriefNumber(distinct[0]) : BriefNumber(distinct[0]) + "–" + BriefNumber(distinct[^1]);
    }
    static XElement BriefParagraph(string text, int size = 11, int after = 100, string style = "Normale", string align = "left")
    {
        var p = P(text, style);
        p.Element(W + "pPr")!.Add(new XElement(W + "keepLines"), new XElement(W + "spacing", new XAttribute(W + "after", after), new XAttribute(W + "line", 240), new XAttribute(W + "lineRule", "auto")), new XElement(W + "jc", new XAttribute(W + "val", align)));
        p.Element(W + "r")!.AddFirst(new XElement(W + "rPr", new XElement(W + "color", new XAttribute(W + "val", "000000")), new XElement(W + "sz", new XAttribute(W + "val", size * 2))));
        return p;
    }
    void BriefText(string text, int size = 11, int after = 100) => body.Add(BriefParagraph(text, size, after));
    void BriefHeading(string title, bool page = false)
    {
        var p = BriefParagraph(title, 12, 120, "Titolo1");
        var properties = p.Element(W + "pPr")!;
        properties.Element(W + "spacing")!.SetAttributeValue(W + "before", page ? 0 : 200);
        properties.Element(W + "pStyle")!.AddAfterSelf(new XElement(W + "keepNext"));
        if (page) properties.Element(W + "keepLines")!.AddAfterSelf(new XElement(W + "pageBreakBefore"));
        body.Add(p);
    }
    void BriefTable(string[] headers, List<string[]> rows, double[] weights, bool preservePrecision = false)
    {
        int[] widths = weights.Select(v => (int)Math.Round(TableWidthTwips * v / weights.Sum())).ToArray(); widths[^1] += TableWidthTwips - widths.Sum();
        var table = new XElement(W + "tbl", new XElement(W + "tblPr",
            new XElement(W + "tblW", new XAttribute(W + "w", TableWidthTwips), new XAttribute(W + "type", "dxa")),
            new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(s => new XElement(W + s, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "808080")))),
            new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
            new XElement(W + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(s => new XElement(W + s, new XAttribute(W + "w", s is "top" or "bottom" ? 70 : 85), new XAttribute(W + "type", "dxa"))))),
            new XElement(W + "tblGrid", widths.Select(v => new XElement(W + "gridCol", new XAttribute(W + "w", v)))));
        foreach (var (values, i) in new[] { headers }.Concat(rows).Select((r, i) => (r, i)))
        {
            var row = new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), i == 0 ? new XElement(W + "tblHeader") : null));
            foreach (var (value, j) in values.Select((v, j) => (v, j)))
            {
                bool numeric = double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("it-IT"), out double number);
                string shown = i > 0 && numeric && !preservePrecision ? BriefNumber(number) : value;
                var p = BriefParagraph(shown, 10, 0, align: i > 0 && j == 0 && !numeric ? "left" : "center");
                if (i == 0) p.Element(W + "r")!.Element(W + "rPr")!.AddFirst(new XElement(W + "b"));
                if (shown.Contains('\n'))
                {
                    var r = p.Element(W + "r")!; r.Elements(W + "t").Remove();
                    foreach (var (line, n) in shown.Split('\n').Select((t, n) => (t, n))) { if (n > 0) r.Add(new XElement(W + "br")); r.Add(new XElement(W + "t", line)); }
                }
                row.Add(new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", widths[j]), new XAttribute(W + "type", "dxa")),
                    i == 0 ? new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "fill", "E7E6E6")) : null, new XElement(W + "vAlign", new XAttribute(W + "val", "center"))), p));
            }
            table.Add(row);
        }
        body.Add(table); BriefText("", 2, 60);
    }
}
