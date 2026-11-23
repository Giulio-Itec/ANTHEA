using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

namespace Anthea.Reports;

internal sealed partial class ItecPileReport
{
    internal static DocxDocument CreateFamily(string title, string module, JsonObject data, JsonObject result,
        IReadOnlySet<string> options, IReadOnlyList<ImmagineReport> images, DocxDocument source)
    {
        var report = new ItecPileReport();
        var blocks = FamilyBlocks(module, result, source);
        if (options.Contains(PileReportContents.Brief)) report.BuildFamilyBrief(title, module, data, result, images, blocks);
        else report.BuildFamily(title, module, data, result, options, images, blocks);
        return report.document;
    }

    // Reuse the existing report's input/result projections: this layer only groups and formats them.
    static Dictionary<string, List<XElement>> FamilyBlocks(string module, JsonObject result, DocxDocument source)
    {
        var groups = new Dictionary<string, List<XElement>>();
        string current = "oggetto";
        bool micro = module == "geo_micropalo_verticale", elastic = result.S("tipo_risultato") == "palo_elastico";
        int index = 0;
        foreach (var block in source.GetXml("word/document.xml").Root!.Element(W + "body")!.Elements())
        {
            if (block.Name != W + "p" && block.Name != W + "tbl") continue;
            string text = Text(block);
            if (index++ == 0 || string.IsNullOrWhiteSpace(text)) continue;
            if (micro)
            {
                if (text.StartsWith("Dati generali —")) current = "geometria";
                else if (text.StartsWith("Efficienza —") || text == "Coefficienti applicati") current = "coefficienti";
                else if (text.StartsWith("Sondaggio ") && text.Contains(" — strato ")) current = "terreni";
                else if (text == "Metodo") current = "metodo";
                else if (text == "Criteri e unità") current = "criteri";
                else if (text == "Resistenze di progetto") current = "verifiche";
                else if (text == "Avvisi del calcolo") current = "avvisi";
                else if (text == "Dettagli ogni 0,50 m e quota finale") current = "dettagli";
            }
            else if (elastic)
            {
                if (block.Name == W + "tbl")
                {
                    string first = Text(block.Elements(W + "tr").First().Elements(W + "tc").First());
                    current = first switch { "Parametro" => "geometria", "Strato" => "terreni", "Grandezza" => "resistenze", _ => current };
                }
                else if (text.StartsWith("EJ da sezione:")) current = "coefficienti";
                else if (text == "Determinazione dei parametri del terreno per tratto") current = "profili";
                else if (text.StartsWith("Mesh:")) current = "verifiche";
                else if (text.StartsWith("FEM Hermite")) current = "criteri";
                else if (text.StartsWith("Fonte FEM consultata:")) current = "fonti";
                else if (text.StartsWith("Valori lungo il palo:")) current = "dettagli";
                else if (text == "Armature e verifiche per tratto") current = "armature";
                else if (current == "oggetto") current = "metodo";
            }
            else
            {
                if (text is "Modello e fonti" or "Modello di calcolo per terreno stratificato" or "Metodo omogeneo di Broms") current = "metodo";
                else if (text == "Lettura dei diagrammi") current = "criteri";
                else if (text == "Dati di ingresso") current = "geometria";
                else if (text.StartsWith("Coefficienti:")) current = "coefficienti";
                else if (text.StartsWith("Stratigrafia ")) current = "terreni";
                else if (text == "Risultati") current = "resistenze";
                else if (text == HorizontalPileVerificationReport.Title) current = "verifiche";
                else if (text == "Avvisi e limiti") current = "avvisi";
                else if (text.StartsWith("Sondaggio ") || text == HorizontalPileResistanceReport.DiagnosticsTitle) current = "dettagli";
                else if (text == "Riferimenti bibliografici e attribuzione delle ipotesi") current = "fonti";
            }
            if (!groups.TryGetValue(current, out var list)) groups[current] = list = [];
            list.Add(block);
        }
        return groups;
    }

    void BuildFamily(string title, string module, JsonObject data, JsonObject result, IReadOnlySet<string> options,
        IReadOnlyList<ImmagineReport> images, Dictionary<string, List<XElement>> blocks)
    {
        bool header = options.Contains("intestazione"), micro = module == "geo_micropalo_verticale";
        if (!header) body.RemoveNodes();
        else body.Descendants(W + "br").Where(e => (string?)e.Attribute(W + "type") == "page").Remove();
        int cover = body.Elements().Count();
        var outline = PileFamilyReport.Outline(module, data, result);
        foreach (var content in outline.Where(c => c.Key is not ("indice" or "intestazione")))
        {
            if (!content.Descendants().Any(c => options.Contains(c.Key))) continue;
            Heading(content.Label);
            foreach (var part in content.Children.Count > 0 ? content.Children : new[] { content })
            {
                if (!options.Contains(part.Key)) continue;
                if (content.Children.Count > 0) Subheading(part.Label);
                if (part.Key == "oggetto") Paragraph(micro
                    ? "La presente relazione riguarda la verifica geotecnica della capacità portante assiale del micropalo soggetto ad azioni di compressione e trazione. Le resistenze sono valutate con il metodo di Bustamante e Doix, in funzione della stratigrafia e dei parametri di iniezione assegnati, e confrontate con le azioni di progetto."
                    : HorizontalIntroduction(module, result));
                if (micro && part.Key == "coefficienti")
                {
                    MicroGroupTable(data, result);
                    FamilySource(blocks["coefficienti"].SkipWhile(b => Text(b) != "Coefficienti applicati"), result);
                }
                else if (!micro && result.S("tipo_risultato") != "palo_elastico" && part.Key == "metodo")
                    HorizontalTheory(result);
                else if (!micro && result.S("tipo_risultato") != "palo_elastico" && part.Key == "criteri")
                    HorizontalDiagramCriteria();
                else if (!micro && result.S("tipo_risultato") != "palo_elastico" && part.Key == "verifiche")
                {
                    Paragraph(HorizontalPileVerificationReport.Introduction);
                    HEq("R_d = ", HFrac("η R_k", "γ_R"), "     ;     H_Ed ≤ R_d     ;     ", HFrac("H_Ed", "R_d"), " ≤ 1");
                    var verification = HorizontalPileVerificationReport.Table(result);
                    Table(verification.Titolo, verification.Colonne, verification.Righe, [1, 1, 1, 1, 2], preservePrecision: true);
                    if (!HorizontalPileVerificationReport.Available(result)) Paragraph(HorizontalPileVerificationReport.MissingResistance);
                }
                else if (part.Key != "oggetto" && blocks.TryGetValue(part.Key, out var source))
                    FamilySource(source, result, micro && part.Key == "geometria"
                        ? new HashSet<string> { "sicurezza_laterale_compressione", "sicurezza_laterale_trazione", "sicurezza_base", "peso_palo_sfavorevole", "peso_palo_favorevole" } : null);
                if (micro && part.Key == "resistenze") foreach (var table in Tabelle.CapacitaPalo(result, true)) FamilyTable(table);
                if (!micro && part.Key == "geometria") foreach (var image in images.Where(i => i.Categoria == "geometria_palo")) Image(image);
                if (part.Key == "profili") foreach (var image in images.Where(i => i.Categoria == "stratigrafia")) Image(image);
                if (micro && part.Key == "resistenze") foreach (var image in images.Where(i => i.Categoria == "grafico_nq")) Image(image);
                if (part.Key == "grafici") foreach (var image in images.Where(i => i.Categoria == (micro ? "grafico_capacita" : "grafici"))) Image(image);
                if (part.Key == "verifiche" && result.S("tipo_risultato") == "palo_elastico")
                    Paragraph("La risposta elastica non costituisce una verifica di capacità portante. " + ReinforcementState(result));
                if (micro && part.Key == "fonti") Paragraph(BustamanteDoix.Fonte);
                if (!micro && result.S("tipo_risultato") != "palo_elastico" && part.Key == "profili")
                    Paragraph("Le proprietà sono assegnate per strato e per verticale. Le tensioni efficaci tengono conto degli strati sovrastanti e della falda impostata; i diagrammi del terreno sono riportati con le sollecitazioni.");
                if (part.Key == "avvisi" && !blocks.ContainsKey("avvisi")) Paragraph("Nessun avviso del calcolo.");
            }
        }
        FinishFamily(cover, header, options.Contains("indice"));
    }

    void MicroGroupTable(JsonObject data, JsonObject result)
    {
        var input = data["efficienza"];
        var adopted = result["efficienza"]!;
        string method = adopted.S("metodo", "Nessuna riduzione");
        var rows = new List<string[]> { new[] { "Metodo", method } };
        if (method is "Converse-Labarre" or "Feld")
        {
            foreach (string key in new[] { "numero_pali_x", "numero_pali_y" }) rows.Add([FamilyLabel(key), input?[key]?.ToString() ?? "—"]);
            rows.Add(["Numero totale di pali", adopted["numero_pali"]?.ToString() ?? "—"]);
        }
        if (method == "Converse-Labarre")
            foreach (string key in new[] { "interasse_x", "interasse_y" }) rows.Add([FamilyLabel(key), input?[key]?.ToString() ?? "—"]);
        foreach (string key in new[] { "eta_compressione", "eta_trazione" }) rows.Add([FamilyLabel(key), adopted[key]?.ToString() ?? "—"]);
        FamilyTable(new("Efficienza del gruppo adottata", ["Parametro", "Valore"], rows));
    }

    void FamilySource(IEnumerable<XElement> blocks, JsonObject result, IReadOnlySet<string>? omittedParameters = null)
    {
        string caption = "Dati e risultati";
        foreach (var block in blocks)
        {
            if (block.Name == W + "tbl")
            {
                var rows = block.Elements(W + "tr").Select(r => r.Elements(W + "tc").Select(Text).ToArray()).ToList();
                if (caption == "Efficienza — risultati" && result["efficienza"] is JsonObject efficiency) FamilyTable(Tabelle.Parametri(caption, efficiency, "efficienza"));
                else FamilyTable(new(caption, rows[0], rows.Skip(1).Where(r => omittedParameters is null || !omittedParameters.Contains(r[0])).ToList()));
            }
            else
            {
                string text = Text(block).Replace(" I valori arrotondati sono solo di presentazione.", "");
                if (text is "Metodo" or "Criteri e unità" or "Dati di ingresso" or "Modello e fonti" or "Lettura dei diagrammi" or "Risultati" or "Avvisi e limiti" or "Avvisi del calcolo" or "Determinazione dei parametri del terreno per tratto") continue;
                bool tableTitle = block.Descendants(W + "b").Any() && text.Length < 160;
                caption = tableTitle ? text : "Dati e risultati";
                if (tableTitle && block.NextNode is XElement next && next.Name == W + "tbl") continue;
                if (text.StartsWith("Rd,c =") || text.StartsWith("Rs ="))
                {
                    XNamespace m = "http://schemas.openxmlformats.org/officeDocument/2006/math";
                    var p = P(""); p.Elements(W + "r").Remove();
                    p.Element(W + "pPr")!.Add(new XElement(W + "spacing", new XAttribute(W + "before", 200), new XAttribute(W + "after", 220)));
                    // Separate sentences avoid an equation wider than the text area.
                    foreach (var line in text.Split(". ", StringSplitOptions.RemoveEmptyEntries))
                    { var equation = new XElement(p); equation.Add(new XElement(m + "oMath", new XElement(m + "r", new XElement(m + "t", line)))); body.Add(CenteredEquation(equation)); }
                }
                else
                {
                    Paragraph(text);
                    if (block.Descendants(W + "b").Any()) body.Elements().Last().Element(W + "pPr")!.Element(W + "spacing")!.AddBeforeSelf(new XElement(W + "keepNext"));
                }
            }
        }
    }
    void FamilyTable(Tabella table)
    {
        if (HorizontalPileResistanceReport.IsTable(table.Titolo))
        {
            Table(table.Titolo, table.Colonne, table.Righe, [3, 1.4, .6], preservePrecision: true);
            return;
        }
        string Format(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("it-IT"), out double n) || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return n.ToString("G5", CultureInfo.GetCultureInfo("it-IT"));
            return value is "true" ? "Sì" : value is "false" ? "No" : value;
        }
        bool parameter = table.Colonne[0] == "Parametro";
        table = table with { Righe = table.Righe.Where(r => !parameter || r[0] is not ("errore" or "metodo_micropalo"))
            .Select(r => r.Select((v, i) => parameter && i == 0 ? FamilyLabel(v) : Format(v)).ToArray()).ToList() };
        // Keep identification columns when a wide detail table is continued on a second table.
        if (table.Colonne.Length <= 7) { Table(table.Titolo, table.Colonne, table.Righe, preservePrecision: true); return; }
        for (int start = 2; start < table.Colonne.Length; start += 5)
        {
            var cols = new[] { 0, 1 }.Concat(Enumerable.Range(start, Math.Min(5, table.Colonne.Length - start))).ToArray();
            Table(table.Titolo + (start > 2 ? " — segue" : ""), cols.Select(i => table.Colonne[i]).ToArray(), table.Righe.Select(r => cols.Select(i => r[i]).ToArray()).ToList(), preservePrecision: true);
        }
    }
    static string FamilyLabel(string key) => key switch
    {
        "inizio_aderenza" => "Inizio aderenza lungo asse [m]", "inclinazione" => "Inclinazione dalla verticale [°]",
        "pressione_iniezione" => "Pressione di iniezione [MPa]", "spessore" => "Spessore verticale [m]",
        "azione_compressione" => "Compressione in testa [kN]", "azione_trazione" => "Trazione in testa [kN]",
        "interasse_x" => "Interasse x [m]", "interasse_y" => "Interasse y [m]", "percentuale_punta" => "Contributo di punta [% di Rs]",
        "peso_palo_sfavorevole" => "γG sfavorevole", "peso_palo_favorevole" => "γG favorevole",
        "sicurezza_laterale_compressione" => "γs,c", "sicurezza_laterale_trazione" => "γs,t", "sicurezza_base" => "γb",
        "tipo_iniezione" => "Tipo di iniezione", "profilo_chs" => "Profilo tubolare di armatura",
        "considera_punta" => "Contributo di punta incluso", "terreno" => "Terreno", "laterale_attiva" => "Aderenza laterale attiva",
        "numero_pali_x" => "Numero di pali in direzione x", "numero_pali_y" => "Numero di pali in direzione y",
        "alpha" => "Maggiorazione del diametro α", _ => Label(key)
    };
    static string HorizontalIntroduction(string module, JsonObject result)
    {
        string element = module == "geo_micropalo_orizzontale" ? "micropalo" : "palo";
        return result.S("tipo_risultato") == "palo_elastico"
            ? $"La presente relazione analizza la risposta del {element} alle azioni orizzontali assegnate mediante un modello elastico di interazione con il terreno. L’analisi considera la geometria, i vincoli, la rigidezza della sezione e le proprietà degli strati attraversati e determina gli spostamenti, le rotazioni e le sollecitazioni lungo il {element}."
            : $"La presente relazione riguarda la valutazione della capacità portante del {element} soggetto ad azioni orizzontali. L’analisi considera la geometria e il vincolo in testa, la stratigrafia e la resistenza flessionale della sezione. Sono riportate la resistenza orizzontale, le sollecitazioni lungo il {element} e il confronto con l’azione di progetto assegnata.";
    }

    static string FamilyTitle(string module, JsonObject result) => result.S("tipo_risultato") == "palo_elastico"
        ? (module == "geo_micropalo_orizzontale" ? "Micropalo" : "Palo") + " — risposta elastica orizzontale"
        : module == "geo_micropalo_verticale" ? "Micropalo — capacità portante verticale"
        : (module == "geo_micropalo_orizzontale" ? "Micropalo" : "Palo") + " — capacità portante orizzontale";
    static string ReinforcementState(JsonObject result) => result["armature"] is JsonNode reinforcement
        ? "Armature: " + reinforcement.S("stato") + ". " + string.Join("; ", reinforcement.Array("tratti").Select(t => t.S("id") + ": " + (t?["riepilogo"]?.S("stato") ?? t.S("stato"))))
        : "Verifiche delle armature non incluse nel risultato.";

    void FinishFamily(int cover = 0, bool header = false, bool index = false)
    {
        var generated = body.Elements().Skip(cover).ToArray(); foreach (var item in generated) item.Remove();
        if (index)
        {
            body.Add(P("INDICE", "Titolo1"));
            foreach (var (anchor, text, level) in contents)
            { var p = P("", level switch { 1 => "Sommario1", 2 => "Sommario2", _ => "Sommario3" }); p.Element(W + "r")!.ReplaceWith(new XElement(W + "hyperlink", new XAttribute(W + "anchor", anchor), Run(text))); body.Add(p); }
        }
        var section = new XElement(document.GetXml("anthea/body-section.xml").Root!);
        section.Elements(W + "type").Remove(); section.Elements(W + "pgNumType").Remove();
        if (!header) { section.Elements(W + "headerReference").Remove(); section.Elements(W + "footerReference").Remove(); }
        var first = section.Elements().First(e => e.Name != W + "headerReference" && e.Name != W + "footerReference");
        first.AddBeforeSelf(new XElement(W + "type", new XAttribute(W + "val", "nextPage")));
        section.Element(W + "pgMar")!.SetAttributeValue(W + "bottom", 1701);
        if (index) body.Add(new XElement(W + "p", new XElement(W + "pPr", new XElement(section))));
        body.Add(generated); body.Add(section);
        document.SetXml("anthea/layout.xml", new XElement("layout", new XAttribute("coverBlocks", cover)).ToString());
        document.SetXml("word/document.xml", body.Document!.ToString()); document.SetXml("word/_rels/document.xml.rels", relations.ToString());
        var settings = document.GetXml("word/settings.xml");
        foreach (var key in new[] { "updateFields", "footnotePr", "endnotePr", "attachedTemplate", "linkStyles" }) settings.Root!.Elements(W + key).Remove();
        document.SetXml("word/settings.xml", settings.ToString());
    }
}
