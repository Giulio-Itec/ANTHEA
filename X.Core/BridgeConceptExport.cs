using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Exports consume the same typed calculation as the UI. They do not introduce calculation rules.</summary>
public static class BridgeConceptExport
{
    private static string F(double n, string fmt = "0.###") => n.ToString(fmt, CultureInfo.GetCultureInfo("it-IT"));
    public static string Csv(BridgeConcept.Result r)
    {
        static string Cell(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var rows = new List<string[]> { new[] { "Parte", "Voce", "Quantità", "Unità", "Prezzo unitario EUR", "Costo EUR", "CO2 materiali t" } };
        rows.AddRange(r.Quantities.Select(q => new[] { q.Group, q.Item, F(q.Amount, "G17"), q.Unit, F(q.Rate, "G17"), F(q.Cost, "G17"), F(q.Carbon, "G17") }));
        rows.Add(["Totali", "Costo diretto", "", "", "", F(r.DirectCost, "G17"), ""]);
        rows.Add(["Totali", "Costo con oneri e imprevisti, IVA esclusa", "", "", "", F(r.TotalCost, "G17"), ""]);
        rows.Add(["Totali", "CO2 materiali + maggiorazione cantiere", "", "", "", "", F(r.Carbon, "G17")]);
        rows.Add(["Ambito", BridgeConcept.Scope, "", "", "", "", ""]);
        return string.Join("\r\n", rows.Select(row => string.Join(";", row.Select(Cell)))) + "\r\n";
    }
    public static byte[] Report(string title, JsonObject data, BridgeConcept.Result result, byte[]? elevation = null, byte[]? section = null)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", rel = "http://schemas.openxmlformats.org/package/2006/relationships",
            rns = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var body = new XElement(w + "body"); var images = new List<byte[]>();
        XElement Paragraph(string text, string style = "Normal") => new(w + "p", new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", style))),
            new XElement(w + "r", new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        void P(string text, string style = "Normal") => body.Add(Paragraph(text, style));
        void Table(string[] headers, IEnumerable<string[]> rows, int[]? widths = null)
        {
            widths ??= Enumerable.Repeat(9360 / headers.Length, headers.Length).ToArray();
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", 9360), new XAttribute(w + "type", "dxa")),
                new XElement(w + "tblLayout", new XAttribute(w + "type", "fixed")),
                new XElement(w + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(k => new XElement(w + k, new XAttribute(w + "w", 75), new XAttribute(w + "type", "dxa")))),
                new XElement(w + "tblBorders", new[] { "top", "bottom", "insideH" }.Select(k => new XElement(w + k, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", 4), new XAttribute(w + "color", "D8E0EB"))))),
                new XElement(w + "tblGrid", widths.Select(v => new XElement(w + "gridCol", new XAttribute(w + "w", v)))));
            foreach (var (row, index) in new[] { headers }.Concat(rows).Select((v, k) => (v, k)))
                table.Add(new XElement(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), index == 0 ? new XElement(w + "tblHeader") : null),
                    row.Select((cell, k) => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", widths[k]), new XAttribute(w + "type", "dxa")), index == 0 ? new XElement(w + "shd", new XAttribute(w + "fill", "E8EFF7")) : null), Paragraph(cell, index == 0 ? "TableHeader" : "TableText")))));
            body.Add(table); P("");
        }
        void Picture(byte[]? bytes, string label)
        {
            if (bytes is null) return; images.Add(bytes); int id = images.Count;
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
            P(label, "Heading2");
            long cx = 5943600, cy = 2377440;
            body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "drawing", new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                new XElement(wp + "docPr", new XAttribute("id", id), new XAttribute("name", label)),
                new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic",
                    new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", id), new XAttribute("name", label)), new XElement(pic + "cNvPicPr")),
                    new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(rns + "embed", "img" + id)), new XElement(a + "stretch", new XElement(a + "fillRect"))),
                    new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                        new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")))))))))));
        }
        P("ANTHEA · " + title, "Title"); P("Bridge Design · " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Subtitle");
        P("Ambito e limiti", "Heading1"); P(BridgeConcept.Scope);
        P("Modello parametrico ANTHEA v1. Riferimento funzionale: thebridgeeng.com/design, esplorato il 26/09/2026. Regole proprie esplicitate; risultati non equivalenti al suo Detailed check AASHTO.");
        P("Esclusi: inviluppi di traffico mobile, fasi costruttive, sisma, vento, urti, fatica, instabilità, precompressione nelle deformazioni, viscosità, fessurazione, collegamenti e verifiche geotecniche complete.");
        P("Riepilogo", "Heading1");
        Table(["Indicatore", "Valore"], [
            ["Tipologia", result.Family.Name], ["Lunghezza / larghezza", F(result.Length) + " / " + F(result.Width) + " m"],
            ["Campate", string.Join(" + ", result.Spans.Select(v => F(v))) + " m"], ["Altezza in campata / sulle pile", F(result.Depth) + " / " + F(result.PierDepth) + " m"],
            ["Costo totale, IVA esclusa", F(result.TotalCost, "N0") + " €"], ["Intervallo parametrico", F(result.CostLow, "N0") + "–" + F(result.CostHigh, "N0") + " €"],
            ["CO₂ materiali + cantiere", F(result.Carbon, "N0") + " tCO₂e"], ["Durata indicativa ±25%", F(result.Duration, "0") + " mesi"]
        ]);
        Picture(elevation, "Prospetto schematico"); Picture(section, "Sezione schematica");
        P("Quantità e prezzi", "Heading1");
        foreach (var group in result.Quantities.GroupBy(q => q.Group))
        {
            P(group.Key, "Heading2"); Table(["Voce", "Quantità", "Unità", "Prezzo €", "Importo €"], group.Select(q => new[] { q.Item, F(q.Amount, "N1"), q.Unit, F(q.Rate, "N1"), F(q.Cost, "N0") }), [3500, 1300, 800, 1500, 2260]);
        }
        P($"Diretto {F(result.DirectCost, "N0")} € × (1 + {F(data["assumptions"].D("prelims"))}% oneri) × (1 + {F(data["assumptions"].D("contingency"))}% imprevisti) = {F(result.TotalCost, "N0")} €.");
        P("Regole e risultati intermedi", "Heading1");
        Table(["Grandezza", "Valore", "Regola"], result.Details.Select(d => new[] { d.Name, F(d.Value) + " " + d.Unit, d.Rule }), [2800, 1700, 4860]);
        P("Appoggi e reazioni", "Heading1"); P("Reazioni G+Q non fattorizzate sull'intero impalcato. Le fondazioni sono dimensionate con carico assiale centrato e valori convenzionali.");
        Table(["Appoggio / tipo", "x [m]", "R [kN]", "H [m]", "Pali [n.]"], result.Supports.Select(s => new[] { (s.Index + 1) + " · " + s.Type, F(s.X), F(s.Reaction), F(s.PierHeight), s.Piles.ToString() }));
        P("Avvisi del modello", "Heading1"); foreach (string warning in result.Warnings) P(warning);
        P("Input e coefficienti conservati", "Heading1");
        foreach (var (group, label, fields) in new[] {
            ("input", "Geometria e materiali (incluse opzioni delle altre famiglie)", BridgeConcept.Site.Concat(BridgeConcept.Layout).Concat(BridgeConcept.Section).Concat(BridgeConcept.Substructure)),
            ("rates", "Listino unitario EUR", BridgeConcept.Rates.AsEnumerable()), ("assumptions", "Ipotesi e coefficienti", BridgeConcept.Assumptions.AsEnumerable()) })
        { P(label, "Heading2"); Table(["Parametro", "Valore", "Unità"], fields.Select(p => new[] { p.Label, data[group]!.S(p.Key), p.Unit }), [5900, 1860, 1600]); }
        Table(["Scelta", "Valore"], new[] { ("obstacle", "Ostacolo"), ("soil", "Terreno"), ("pier", "Pila"), ("foundation", "Fondazione richiesta"), ("continuous", "Continuità"), ("start_pier", "Inizio su pila"), ("end_pier", "Fine su pila"), ("low_carbon", "Cls ridotta CO₂"), ("recycled", "Acciaio riciclato") }.Select(p => new[] { p.Item2, data["input"]!.S(p.Item1) }));
        if (data["alternative_a"] is JsonObject baseline)
        {
            P("Confronto con alternativa A", "Heading1");
            try
            {
                var old = BridgeConcept.Calculate(baseline);
                Table(["Indicatore", "A", "B corrente", "Δ B − A"], new[] { ("Costo €", old.TotalCost, result.TotalCost), ("CO₂ t", old.Carbon, result.Carbon), ("Durata mesi", old.Duration, result.Duration) }.Select(v => new[] { v.Item1, F(v.Item2), F(v.Item3), F(v.Item3 - v.Item2) }));
            }
            catch (ArgumentException ex) { P("Alternativa A non calcolabile: " + ex.Message); }
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)),
            new XElement(w + "pgMar", new XAttribute(w + "top", 1050), new XAttribute(w + "bottom", 1050), new XAttribute(w + "left", 1273), new XAttribute(w + "right", 1273))));
        var relationships = new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", rns.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
        // Namespace URIs already end in relationships; the relationship types append a slash.
        var styles = new XElement(w + "styles", new XElement(w + "docDefaults", new XElement(w + "rPrDefault", new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", "Calibri"), new XAttribute(w + "hAnsi", "Calibri")), new XElement(w + "sz", new XAttribute(w + "val", 20))))));
        foreach (var (name, size, bold) in new[] { ("Normal", 20, false), ("Title", 34, true), ("Subtitle", 23, false), ("Heading1", 26, true), ("Heading2", 23, true), ("TableText", 18, false), ("TableHeader", 18, true) })
            styles.Add(new XElement(w + "style", new XAttribute(w + "type", "paragraph"), new XAttribute(w + "styleId", name), new XElement(w + "name", new XAttribute(w + "val", name)),
                new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", name.StartsWith("Heading") ? 140 : 70)), name.StartsWith("Heading") ? new XElement(w + "keepNext") : null),
                new XElement(w + "rPr", new XElement(w + "sz", new XAttribute(w + "val", size)), bold ? new XElement(w + "b") : null, new XElement(w + "color", new XAttribute(w + "val", "0B2A4A")))));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string content) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/></Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            for (int k = 0; k < images.Count; k++)
            { string path = $"media/image{k + 1}.png"; relationships.Add(new XElement(rel + "Relationship", new XAttribute("Id", "img" + (k + 1)), new XAttribute("Type", rns.NamespaceName + "/image"), new XAttribute("Target", path))); using var stream = zip.CreateEntry("word/" + path).Open(); stream.Write(images[k]); }
            Entry("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString());
            Entry("word/styles.xml", new XDocument(styles).ToString()); Entry("word/_rels/document.xml.rels", new XDocument(relationships).ToString());
        }
        return memory.ToArray();
    }
}
