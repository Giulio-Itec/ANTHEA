using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Reports;

public static class RheologyReport
{
    private const int WidthTwips = 9600, MaximumTableRows = 36;
    public static DocxDocument Create(string title, JsonObject input, JsonObject result)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        XElement P(string text, bool heading = false, bool cell = false) => new(w + "p",
            new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", cell ? 50 : 100)), heading ? new XElement(w + "keepNext") : null),
            new XElement(w + "r", new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", "Arial"), new XAttribute(w + "hAnsi", "Arial")),
                new XElement(w + "sz", new XAttribute(w + "val", cell ? heading ? 20 : 18 : heading ? 25 : 20)), heading ? new XElement(w + "b") : null), new XElement(w + "t", text)));
        string Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var number) ? number.ToString("G10", CultureInfo.GetCultureInfo("it-IT")) : n?.ToString() ?? "";
        void Table(string[] headers, IEnumerable<string[]> rows)
        {
            int width = WidthTwips / headers.Length;
            XElement Row(string[] cells, bool header = false) => new(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), header ? new XElement(w + "tblHeader") : null),
                cells.Select(s => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", width), new XAttribute(w + "type", "dxa")),
                    new XElement(w + "vAlign", new XAttribute(w + "val", "center")), header ? new XElement(w + "shd", new XAttribute(w + "fill", "E8EDF2")) : null), P(s, header, true))));
            body.Add(new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", WidthTwips), new XAttribute(w + "type", "dxa")),
                    new XElement(w + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side => new XElement(w + side, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", 4), new XAttribute(w + "color", "D9D9D9")))),
                    new XElement(w + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(side => new XElement(w + side, new XAttribute(w + "w", 80), new XAttribute(w + "type", "dxa"))))),
                new XElement(w + "tblGrid", headers.Select(_ => new XElement(w + "gridCol", new XAttribute(w + "w", width)))), Row(headers, true), rows.Select(row => Row(row))), P(""));
        }
        body.Add(P(title, true), P("ANTHEA · Viscosità e maturazione"), P("EN 1992-1-1:2004 · allegato B e §3.1.3"), P("Calcestruzzo ordinario a 20 °C, umidità costante e viscosità lineare. Ecm(t) è il modulo per maturazione. Ritiro e storia termica esclusi."), P("Dati", true));
        Table(["Parametro", "Valore"], new[] { ("fck [MPa]", "Fck"), ("Cemento", "Cement"), ("RH [%]", "Humidity"), ("h0 [mm]", "NotionalSize"), ("Età al carico [giorni]", "LoadingAge"), ("Età iniziale [giorni]", "StartAge"), ("Età finale [giorni]", "EndAge"), ("Campionamento", "Samples") }.Select(p => new[] { p.Item1, p.Item2 == "Cement" ? Text(input[p.Item2]).Replace("Class", "") : Text(input[p.Item2]) }));
        body.Add(P("Parametri", true));
        Table(["Grandezza", "Valore"], new[] { ("φ(∞, t0)", "CreepInfinity"), ("Ecm,28 [MPa]", "Ecm28Mpa"), ("Ec di riferimento per φ [MPa]", "CreepReferenceModulusMpa") }.Select(p => new[] { p.Item1, Text(result[p.Item2]) }));
        string[] Headers() => ["Età [giorni]", "φ(t, t0)", "Ecm(t) [MPa]", "Ecm(t) / Ecm,28"];
        string[] Values(JsonNode p) => [Text(p["AgeDays"]), Text(p["CreepCoefficient"]), Text(p["EcmMpa"]), Text(p["ModulusFactor"])];
        body.Add(P("Lettura puntuale", true));
        if (result["QueryPoint"] is JsonObject query) Table(Headers(), [Values(query)]); else body.Add(P(Text(result["QueryError"])));
        var points = result["Points"]!.AsArray(); int stride = Math.Max(1, (points.Count + MaximumTableRows - 1) / MaximumTableRows);
        body.Add(P("Tabella sintetica delle curve", true), P("Righe selezionate dal campionamento calcolato, comprese le estremità. Valori esposti con dieci cifre significative; righe identiche alla precisione esposta riportate una volta. La tabella interattiva contiene tutti i punti."));
        Table(Headers(), points.Where((_, index) => index % stride == 0 || index == points.Count - 1).Select(p => Values(p!)).DistinctBy(row => string.Join("|", row)));
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)), new XElement(w + "pgMar", new XAttribute(w + "top", 1000), new XAttribute(w + "bottom", 1000), new XAttribute(w + "left", 1100), new XAttribute(w + "right", 1100))));
        var package = new DocxDocument();
        package.SetXml("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        package.SetXml("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        package.SetXml("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString());
        return package;
    }
}
