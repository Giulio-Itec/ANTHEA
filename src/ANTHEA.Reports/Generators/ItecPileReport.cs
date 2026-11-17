using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

namespace Anthea.Reports;

/// <summary>Relazione autonoma del palo: modello ITEC, dati e risultati del motore, nessun ricalcolo geotecnico.</summary>
internal sealed partial class ItecPileReport
{
    const int BookmarkIdStart = 10000, DrawingIdStart = 20000;
    const int TableWidthTwips = 9638;
    const double FigureWidthEmu = 6120000, FigureHeightEmu = 5400000;
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    readonly DocxDocument document;
    readonly XElement body, relations;
    readonly List<(string Anchor, string Text, int Level)> contents = [];
    int chapter, subchapter, tableNumber, figureNumber;

    ItecPileReport()
    {
        using var stream = typeof(ItecPileReport).Assembly.GetManifestResourceStream("Anthea.ItecPile.docx")
            ?? throw new InvalidDataException("Modello ITEC non disponibile.");
        using var memory = new MemoryStream(); stream.CopyTo(memory);
        document = DocxWriter.Read(memory.ToArray());
        body = document.GetXml("word/document.xml").Root!.Element(W + "body")!;
        relations = document.GetXml("word/_rels/document.xml.rels").Root!;
    }

    internal static DocxDocument Create(string title, JsonObject data, JsonObject result, HashSet<string> options,
        IReadOnlyList<ImmagineReport>? images, DocxDocument legacy)
    {
        var report = new ItecPileReport();
        report.Build(title, data, result, options, images ?? [], legacy);
        return report.document;
    }

    void Build(string title, JsonObject data, JsonObject result, HashSet<string> options, IReadOnlyList<ImmagineReport> images, DocxDocument legacy)
    {
        bool withHeader = options.Contains("intestazione");
        if (!withHeader) body.RemoveNodes();
        else body.Descendants(W + "br").Where(e => (string?)e.Attribute(W + "type") == "page").Remove();
        int coverCount = body.Elements().Count();
        document.SetXml("anthea/layout.xml", new XElement("layout", new XAttribute("coverBlocks", coverCount)).ToString());
        var generated = new List<XElement>();
        var g = data["generali"]!;
        var last = result.Array("dettagli").LastOrDefault();
        var outline = PileReportContents.Outline(data).SelectMany(c => c.Descendants()).ToDictionary(c => c.Key);
        bool Selected(string key) => outline[key].Descendants().Any(c => options.Contains(c.Key));
        var legacyTables = legacy.GetXml("word/document.xml").Descendants(W + "tbl").ToArray();
        Tabella? LegacyTable(string titlePrefix)
        {
            var t = legacyTables.FirstOrDefault(t => t.PreviousNode is XElement p && Text(p).StartsWith(titlePrefix, StringComparison.Ordinal));
            if (t is null) return null;
            var rows = t.Elements(W + "tr").Select(r => r.Elements(W + "tc").Select(Text).ToArray()).ToList();
            return new(titlePrefix, rows[0], rows.Skip(1).ToList());
        }

        if (Selected("c1")) Heading(outline["c1"].Label);
        if (Selected("oggetto"))
        {
        Subheading(outline["oggetto"].Label);
        Paragraph("Verifica geotecnica della capacità portante verticale del palo soggetto a compressione e trazione.");
        Paragraph("Lunghezze in m, azioni e resistenze in kN, tensioni in kPa, pesi dell’unità di volume in kN/m³. Profondità z positiva verso il basso dalla testa del palo; la stratigrafia è riferita al piano campagna.");
        }
        if (Selected("tab_geometria"))
        {
            string[] keys = ["tipo_palo", "sottotipo_palo_battuto", "diametro", "lunghezza", "profondita_testa", "peso_specifico_palo", "verticali_indagate"];
            Table("Geometria e inquadramento", ["Parametro", "Valore"], keys.Where(k => g[k] is not null && (k != "sottotipo_palo_battuto" || g.S("tipo_palo") == "Battuto"))
                .Select(k => new[] { Label(k), Value(g[k]) }).ToList(), [3, 2]);
        }
        if (Selected("riferimenti"))
        {
            Subheading("Riferimenti e coefficienti adottati");
            if (Selected("normativa")) Paragraph("D.M. 17 gennaio 2018, NTC: § 6.4.3.1.1 e tabelle 6.4.II e 6.4.IV per la resistenza assiale dei pali; tabella 5.1.V per le azioni sui ponti. Approccio 2: A1 + M1 + R3.");
            if (Selected("tab_coefficienti") && LegacyTable("Coefficienti applicati") is { } coefficients) Table(coefficients);
            if (J.Number(g["peso_palo_sfavorevole"]) != 1.35 || J.Number(g["peso_palo_favorevole"]) != 1)
                Paragraph("Attenzione: i coefficienti del peso proprio impostati nel foglio differiscono da 1,35 sfavorevole e 1,00 favorevole richiesti per questa relazione sui ponti. Verificare i dati del foglio prima di usare il report.");
        }

        if (Selected("c2"))
        {
            Heading("Teoria di calcolo");
            if (Selected("resistenza"))
            {
                Subheading(outline["resistenza"].Label);
                Paragraph("La resistenza a compressione comprende punta e fusto; in trazione si considera il contributo laterale.");
                Equation(0); Equation(1);
                Paragraph("D e L sono diametro e lunghezza; qb è la resistenza unitaria di punta e τ quella tangenziale lungo il fusto. I contributi Rb e Rs sono espressi in kN.");
            }
            if (Selected("drenate"))
            {
                Subheading("Condizioni drenate"); Equation(2);
                Paragraph("Il motore corrente considera c′ nella resistenza laterale dei terreni coesivi; nei terreni granulari il termine è nullo. k e μ sono i coefficienti del modello adottato. Nq dipende dall’angolo di attrito e dal rapporto fra profondità della punta dal piano campagna e diametro.");
            }
            if (Selected("non_drenate"))
            {
                Subheading("Condizioni non drenate"); Equation(3);
                Paragraph("La resistenza di punta è lorda: σv è la tensione verticale totale alla punta. Il ramo non drenato si applica ai terreni coesivi sotto falda; negli altri tratti si usa il ramo drenato. I valori di Nc, cu e α adottati sono riportati nei dati e nei dettagli del calcolo.");
            }
            if (Selected("resistenze_progetto"))
            {
                Subheading("Resistenze caratteristiche e di progetto");
                Paragraph("Per ciascun contributo di punta e laterale si valutano separatamente media/ξ₃ e minimo/ξ₄ sulle verticali. Nei due rami, i contributi caratteristici sono divisi per i rispettivi coefficienti di resistenza; si assume il minore dei valori di progetto.");
                Equation(4); Equation(5);
                Paragraph("ηc ed ηt sono le efficienze del gruppo. Le tabelle distinguono le resistenze calcolate, caratteristiche e di progetto e indicano il ramo determinante.");
            }
            if (Selected("gruppo"))
            {
                Subheading("Efficienza del gruppo");
                if (Selected("efficienza_testo")) Paragraph($"Metodo: {data["efficienza"].S("metodo", "Nessuna riduzione")}. Efficienza in compressione ηc = {Value(result["efficienza"]?["eta_compressione"])}; in trazione ηt = {Value(result["efficienza"]?["eta_trazione"])}.");
                var efficiencyTables = data["efficienza"].S("metodo", "Nessuna riduzione") == "Nessuna riduzione"
                    ? new[] { "Efficienza — risultati" } : new[] { "Efficienza — dati", "Efficienza — risultati" };
                if (Selected("tab_efficienza")) foreach (var prefix in efficiencyTables)
                    if (LegacyTable(prefix) is { } table) Table(table with { Righe = table.Righe.Where(r => r[0] != "errore").Select(r => new[] { Label(r[0]), r[1] }).ToList() });
            }
        }

        if (Selected("c3"))
        {
            Heading("Modello geotecnico");
            if (Selected("inquadramento"))
            {
            Subheading(outline["inquadramento"].Label);
            Paragraph(g.B("presenza_falda") ? $"Falda a {Value(g["profondita_falda"])} m sotto il piano campagna." : "Falda assente nel modello.");
            Paragraph($"Testa del palo a {Value(g["profondita_testa"] ?? JsonValue.Create(0))} m sotto il piano campagna. Gli strati sopra la testa contribuiscono alle tensioni verticali, senza resistenza laterale lungo il palo.");
            }
            for (int i = 0; i < data.Array("stratigrafie").Count; i++)
            {
                if (!Selected("verticale_" + i)) continue;
                Subheading($"Verticale di indagine {i + 1}");
                var layers = data.Array("stratigrafie")[i]!.AsArray();
                if (Selected("tab_strati_" + i)) Table("Strati e condizioni del modello", ["Strato", "Tipologia", "Spessore [m]", "Stato", "Laterale"], layers.Select((s, j) => new[] {
                    (j+1).ToString(), s.S("tipologia"), Value(s?["spessore"]), s.S("addensamento"), s.B("laterale_attiva", true) ? "Attiva" : "Esclusa" }).ToList(), [1, 2, 1.3, 1.5, 1.3]);
                if (Selected("tab_terreni_" + i)) Table("Parametri geotecnici", ["Strato", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]", "c′ [kPa]", "cu [kPa]", "Nc"], layers.Select((s, j) => new[] { (j+1).ToString() }
                    .Concat(new[] { "peso_specifico", "peso_specifico_saturo", "angolo_attrito", "coesione_efficace", "coesione_non_drenata", "nc" }.Select(k => Value(s?[k]))).ToArray()).ToList());
                if (Selected("profilo_" + i)) foreach (var image in images.Where(im => im.Categoria == "stratigrafia" && im.SurveyIndex == i)) Image(image);
            }
        }
        if (Selected("c4"))
        {
            Heading("Calcolo della capacità portante");
            Paragraph($"Quota finale disponibile: z = {Value(last?["z"])} m dalla testa. Lunghezza del palo: {Value(g["lunghezza"])} m.");
            if (Selected("fattore_nq"))
            {
                Subheading("Fattore di capacità portante Nq");
                if (Selected("tab_punta") && last is not null) Table("Parametri alla punta", ["Verticale", "σ′v [kPa]", "σv [kPa]", "φ′ [°]", "Nq", "Nc"], last.Array("sondaggi").Select((s, i) => new[] { (i+1).ToString() }
                    .Concat(new[] { "sigma_punta", "sigma_totale_punta", "phi_punta", "nq", "nc" }.Select(k => Value(s?[k]))).ToArray()).ToList());
                if (options.Contains("grafico_nq")) foreach (var image in images.Where(im => im.Categoria == "grafico_nq")) Image(image);
            }
            if (Selected("contributi") && last is not null)
            {
                Subheading("Contributi resistenti alla quota finale");
                if (Selected("tab_contributi"))
                {
                Table("Resistenze calcolate per verticale", ["Verticale", "Condizione", "Rb,cal [kN]", "Rs,cal [kN]"], last.Array("sondaggi").SelectMany((s, i) => new[] { "drenante", "non_drenante" }
                    .Select(c => new[] { (i+1).ToString(), Condition(c), Value(s?[c]?["base"]), Value(s?[c]?["laterale"]) })).ToList());
                foreach (var table in Tabelle.CapacitaPalo(result)) Table(table);
                var rows = result["curve"]!.AsObject().Select(kv => new[] { Condition(kv.Key),
                    kv.Value!.Array("media")[^1]![1]!.GetValue<double>() <= kv.Value.Array("minima")[^1]![1]!.GetValue<double>() ? "Media / ξ₃" : "Minimo / ξ₄" }).ToList();
                Table("Ramo determinante alla quota finale", ["Verifica", "Ramo"], rows, [3, 2]);
                }
                if (Selected("contributi_testo")) Paragraph("Le componenti caratteristiche e di progetto si riferiscono al ramo determinante; i fattori di efficienza sono inclusi nelle resistenze di progetto finali.");
            }
        }

        if (Selected("c5"))
        {
            Heading("Azioni e verifica");
            if (Selected("azioni"))
            {
                Subheading("Azioni assegnate e peso proprio");
                if (Selected("azioni_testo"))
                {
                Paragraph("Le azioni in testa sono già di progetto e non includono il peso del palo. Peso e sottospinta sono gestiti dal motore una sola volta; la tabella degli esiti riporta l’azione specifica di ciascuna condizione.");
                Equation(6);
                Paragraph(Calcolo.RegoleSottospinta);
                }
                if (Selected("tab_azioni")) Table("Azioni assegnate e peso proprio", ["Grandezza", "Valore"], [
                    ["Compressione in testa [kN]", Value(g["azione_compressione"])], ["Trazione in testa [kN]", Value(g["azione_trazione"])],
                    ["Peso proprio alla quota finale [kN]", Value(last?["peso"])], ["γG sfavorevole", Value(g["peso_palo_sfavorevole"])],
                    ["γG favorevole", Value(g["peso_palo_favorevole"])], ["Sottospinta", g.B("considera_sottospinta") ? "Considerata" : "Non considerata"]], [3, 2]);
            }
            if (Selected("riepilogo"))
            {
                Subheading("Riepilogo delle verifiche");
                if (Selected("verifica_testo")) Equation(7);
                if (Selected("tab_verifiche") && LegacyTable("Resistenze di progetto") is { } verification)
                {
                    var details = Tabelle.Crea(result, false);
                    var rows = result["curve"]!.AsObject().Select((kv, i) =>
                    {
                        var end = kv.Value!.Array("progetto")[^1]!;
                        var action = Calcolo.AzioniCurva(result, kv.Key)?.LastOrDefault();
                        var detail = details.Single(t => t.Titolo == kv.Key.Replace('_', ' ') + " — verifica a ogni quota").Righe[^1];
                        return new[] { Condition(kv.Key), Value(end[0]), Value(end[1]), Value(action?[1]), detail[5], verification.Righe[i][^1] };
                    }).ToList();
                    Table("Riepilogo alla quota finale", ["Verifica", "z [m]", "Rd [kN]", "Ed [kN]", "I = Ed/Rd", "Esito"], rows, [2.1, 1, 1.2, 1.2, 1, 2.4]);
                }
            }
            if (Selected("grafico_capacita"))
            {
                Subheading(outline["grafico_capacita"].Label);
                foreach (var image in images.Where(im => im.Categoria == "grafico_capacita")) Image(image);
            }
        }
        if (Selected("c6") || Selected("c7"))
        {
            var tables = Tabelle.Crea(result, false);
            bool Parameters(Tabella t) => t.Titolo.Contains("Nq —") || t.Titolo.Contains("parametri alla punta") || t.Titolo.Contains("integrazione per strato");
            if (Selected("c6"))
            {
            Heading(outline["c6"].Label);
            Paragraph("Profondità dalla testa del palo. Dettagli ogni 0,50 m e quota finale disponibile. Valori arrotondati ai soli fini della presentazione.");
            foreach (var key in new[] { "tab_parametri", "tab_integrazione" }) if (Selected(key))
            {
                Subheading(outline[key].Label);
                foreach (var table in tables.Where(t => Parameters(t) && t.Titolo.Contains("integrazione per strato") == (key == "tab_integrazione"))) Table(table);
            }
            }
            if (Selected("c7"))
            {
            Heading(outline["c7"].Label);
            Paragraph("Contributi per verticale e inviluppo delle verticali. Le azioni delle verifiche sono quelle della corrispondente condizione di calcolo.");
            foreach (var key in new[] { "tab_dettagli_resistenze", "tab_dettagli_verifiche" }) if (Selected(key))
            {
                Subheading(outline[key].Label);
                foreach (var table in tables.Where(t => !Parameters(t) && t.Titolo.Contains("verifica a ogni quota") == (key == "tab_dettagli_verifiche"))) Table(table);
            }
            }
        }
        if (Selected("avvisi"))
        {
            Heading("Avvisi del calcolo");
            if (result["avvisi"] is JsonArray warnings && warnings.Count > 0)
                foreach (var warning in warnings) { if (warning is not null) Paragraph(warning.ToString()); }
            else Paragraph("Nessun avviso del calcolo.");
        }

        // Internal chapter links need no automatic field update or external file access.
        generated.AddRange(body.Elements().Skip(coverCount).ToArray());
        foreach (var e in generated) e.Remove();
        if (Selected("indice"))
        {
            var indexTitle = P("INDICE", "Titolo1");
            body.Add(indexTitle);
            foreach (var (anchor, text, level) in contents)
            {
                var p = P("", level == 1 ? "Sommario1" : "Sommario2");
                p.Element(W + "r")!.ReplaceWith(new XElement(W + "hyperlink", new XAttribute(W + "anchor", anchor), Run(text)));
                body.Add(p);
            }
        }
        var section = new XElement(document.GetXml("anthea/body-section.xml").Root!);
        // Use a new-page body section and reserve the complete footer.
        section.Elements(W + "type").Remove();
        var references = section.Elements().Where(e => e.Name == W + "headerReference" || e.Name == W + "footerReference").ToArray();
        if (!withHeader) foreach (var reference in references) reference.Remove();
        var firstSetting = section.Elements().FirstOrDefault(e => e.Name != W + "headerReference" && e.Name != W + "footerReference");
        firstSetting?.AddBeforeSelf(new XElement(W + "type", new XAttribute(W + "val", "nextPage")));
        section.Element(W + "pgMar")!.SetAttributeValue(W + "bottom", 1701);
        section.Elements(W + "pgNumType").Remove();
        if (Selected("indice")) body.Add(new XElement(W + "p", new XElement(W + "pPr", new XElement(section))));
        body.Add(generated); body.Add(new XElement(section));
        var first = generated.FirstOrDefault(); first?.Element(W + "pPr")?.Elements(W + "pageBreakBefore").Remove();
        var footer = new XElement(W + "ftr", P(title, "Pidipagina"), P("Pagina ", "Pidipagina"));
        footer.Elements().Last().Add(new XElement(W + "fldSimple", new XAttribute(W + "instr", "PAGE"), Run("1")));
        foreach (var p in footer.Elements()) p.Element(W + "pPr")!.Add(new XElement(W + "jc", new XAttribute(W + "val", "right")));
        var footerTitle = footer.Elements().First().Element(W + "pPr")!;
        footerTitle.Element(W + "jc")!.SetAttributeValue(W + "val", "center");
        footerTitle.Element(W + "jc")!.AddBeforeSelf(new XElement(W + "pBdr", new XElement(W + "top", new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "808080"))));
        document.SetXml("word/footer4.xml", footer.ToString());
        document.SetXml("word/document.xml", body.Document!.ToString());
        document.SetXml("word/_rels/document.xml.rels", relations.ToString());
        var settings = document.GetXml("word/settings.xml");
        foreach (var name in new[] { "updateFields", "footnotePr", "endnotePr", "attachedTemplate", "linkStyles" }) settings.Root!.Elements(W + name).Remove();
        document.SetXml("word/settings.xml", settings.ToString());
    }

    void Heading(string text)
    {
        chapter++; subchapter = tableNumber = figureNumber = 0;
        AddHeading(chapter + "  " + text, 1);
    }
    void Subheading(string text) => AddHeading($"{chapter}.{++subchapter}  {text}", 2);
    void AddHeading(string text, int level)
    {
        string anchor = "itec" + contents.Count;
        var p = P(text, level == 1 ? "Titolo1" : "Titolo2");
        p.Element(W + "pPr")!.Add(new XElement(W + "keepNext"), new XElement(W + "keepLines"));
        p.Element(W + "pPr")!.Add(new XElement(W + "spacing", new XAttribute(W + "before", level == 1 ? 360 : 300), new XAttribute(W + "after", 180)));
        p.Element(W + "pPr")!.AddAfterSelf(new XElement(W + "bookmarkStart", new XAttribute(W + "id", BookmarkIdStart + contents.Count), new XAttribute(W + "name", anchor)));
        p.Add(new XElement(W + "bookmarkEnd", new XAttribute(W + "id", BookmarkIdStart + contents.Count)));
        contents.Add((anchor, text, level)); body.Add(p);
    }
    static XElement Run(string text) => new(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
    static XElement P(string text, string style = "Normale") => new(W + "p", new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", style))), Run(text));
    void Paragraph(string text)
    {
        var p = P(text);
        p.Element(W + "pPr")!.Add(new XElement(W + "spacing", new XAttribute(W + "after", 160)));
        body.Add(p);
    }
    void Equation(int id)
    {
        var p = new XElement(document.GetXml("anthea/equations.xml").Root!.Elements().Single(e => (int)e.Attribute("id")! == id).Element(W + "p")!);
        p.Element(W + "pPr")?.Remove();
        p.AddFirst(new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "before", 200), new XAttribute(W + "after", 220))));
        body.Add(CenteredEquation(p));
    }
    static XElement CenteredEquation(XElement paragraph)
    {
        XNamespace math = "http://schemas.openxmlformats.org/officeDocument/2006/math";
        var properties = paragraph.Element(W + "pPr")!;
        properties.Elements(W + "jc").Remove();
        properties.Add(new XElement(W + "jc", new XAttribute(W + "val", "center")));
        foreach (var inline in paragraph.Elements(math + "oMath").ToArray())
            inline.ReplaceWith(new XElement(math + "oMathPara", new XElement(inline)));
        foreach (var display in paragraph.Elements(math + "oMathPara"))
        {
            var alignment = display.Element(math + "oMathParaPr");
            if (alignment is null) { alignment = new XElement(math + "oMathParaPr"); display.AddFirst(alignment); }
            alignment.Elements(math + "jc").Remove();
            alignment.Add(new XElement(math + "jc", new XAttribute(math + "val", "center")));
        }
        return paragraph;
    }
    static string Text(XElement element) => string.Concat(element.Descendants(W + "t").Select(t => t.Value));
    static string Value(JsonNode? node) => node is null || string.IsNullOrWhiteSpace(node.ToString()) ? "—" : J.Number(node) is double number
        ? number.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) : node.ToString();
    static string Condition(string text) => text.Replace("non_drenante", "Non drenata").Replace("drenante", "Drenata").Replace('_', ' ');
    static string Label(string key) => key switch
    {
        "tipo_palo" => "Tipologia del palo", "sottotipo_palo_battuto" => "Tipo di palo battuto", "diametro" => "Diametro D [m]", "lunghezza" => "Lunghezza L [m]",
        "profondita_testa" => "Profondità della testa d [m]", "peso_specifico_palo" => "Peso dell’unità di volume γp [kN/m³]", "verticali_indagate" => "Verticali indagate n",
        "eta_compressione" => "Efficienza in compressione ηc", "eta_trazione" => "Efficienza in trazione ηt", "numero_pali" => "Numero di pali", _ => key.Replace('_', ' ')
    };
    void Table(Tabella table) => Table(table.Titolo, table.Colonne, table.Righe);
    void Table(string title, string[] columns, List<string[]> rows, double[]? weights = null, bool preservePrecision = false)
    {
        if (rows.Count == 0) return;
        var caption = P($"Tabella {chapter}.{++tableNumber}  {Condition(title)}", "Didascalia");
        caption.Element(W + "pPr")!.Add(new XElement(W + "keepNext")); body.Add(caption);
        weights ??= Enumerable.Repeat(1d, columns.Length).ToArray();
        int[] widths = weights.Select(v => (int)Math.Round(TableWidthTwips * v / weights.Sum())).ToArray(); widths[^1] += TableWidthTwips - widths.Sum();
        var table = new XElement(W + "tbl", new XElement(W + "tblPr",
            new XElement(W + "tblW", new XAttribute(W + "w", TableWidthTwips), new XAttribute(W + "type", "dxa")),
            new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(s => new XElement(W + s, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "808080")))),
            new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
            new XElement(W + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(s => new XElement(W + s, new XAttribute(W + "w", s is "top" or "bottom" ? 40 : 65), new XAttribute(W + "type", "dxa"))))),
            new XElement(W + "tblGrid", widths.Select(v => new XElement(W + "gridCol", new XAttribute(W + "w", v)))));
        foreach (var (values, i) in new[] { columns }.Concat(rows).Select((r, i) => (r, i)))
        {
            var row = new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), i == 0 ? new XElement(W + "tblHeader") : null));
            foreach (var (value, j) in values.Select((v, j) => (v, j)))
            {
                string shown = !preservePrecision && i > 0 && double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("it-IT"), out double numeric)
                    ? numeric.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) : value;
                var p = P(shown); p.Element(W + "pPr")!.Add(new XElement(W + "spacing", new XAttribute(W + "before", 0), new XAttribute(W + "after", 0), new XAttribute(W + "line", 240), new XAttribute(W + "lineRule", "auto")), new XElement(W + "jc", new XAttribute(W + "val", "center")));
                if (preservePrecision && rows.Count <= 8 && i < rows.Count) p.Element(W + "pPr")!.Element(W + "pStyle")!.AddAfterSelf(new XElement(W + "keepNext"));
                p.Element(W + "r")!.AddFirst(new XElement(W + "rPr", i == 0 ? new XElement(W + "b") : null, new XElement(W + "sz", new XAttribute(W + "val", 20))));
                row.Add(new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", widths[j]), new XAttribute(W + "type", "dxa")), i == 0 ? new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "fill", "E7E6E6")) : null, new XElement(W + "vAlign", new XAttribute(W + "val", "center"))), p));
            }
            table.Add(row);
        }
        body.Add(table); body.Add(P(""));
    }
    void Image(ImmagineReport image)
    {
        string id = "itecFigure" + (++figureNumber) + "c" + chapter;
        string part = "media/" + id + ".png";
        document.SetBytes("word/" + part, image.Png);
        relations.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/image"), new XAttribute("Target", part)));
        int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(image.Png.AsSpan(16, 4));
        int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(image.Png.AsSpan(20, 4));
        double scale = Math.Min(FigureWidthEmu / width, FigureHeightEmu / height);
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        long cx = (long)(width * scale), cy = (long)(height * scale);
        var drawing = new XElement(W + "drawing", new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
            new XElement(wp + "docPr", new XAttribute("id", DrawingIdStart + chapter * 100 + figureNumber), new XAttribute("name", image.Titolo)),
            new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic",
                new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", id)), new XElement(pic + "cNvPicPr")),
                new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(R + "embed", id)), new XElement(a + "stretch", new XElement(a + "fillRect"))),
                new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst"))))))));
        var p = P(""); p.Element(W + "r")!.ReplaceWith(new XElement(W + "r", drawing)); p.Element(W + "pPr")!.Add(new XElement(W + "keepNext")); body.Add(p);
        body.Add(P($"Figura {chapter}.{figureNumber}  {image.Titolo}", "Didascalia"));
    }
}
