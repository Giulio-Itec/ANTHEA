using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

public static class ReportPileGroup
{
    public static byte[] Create(string title, JsonObject result)
    {
        if (result["input"] is not JsonObject input || result["confronto"] is not JsonArray rows) throw new ArgumentException("Risultati della palificata non disponibili.");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        void P(string text, bool bold = false) => body.Add(new XElement(w + "p", new XElement(w + "pPr", bold ? new XElement(w + "keepNext") : null), new XElement(w + "r", new XElement(w + "rPr", new XElement(w + "sz", new XAttribute(w + "val", bold ? 26 : 20)), bold ? new XElement(w + "b") : null), new XElement(w + "t", text))));
        string F(JsonNode? n) => J.Number(n)?.ToString("0.####", System.Globalization.CultureInfo.CurrentCulture) ?? "—";
        P(title, true); P("Efficienza orizzontale della palificata", true);
        P("Pali identici. Le grandezze confrontate hanno significato fisico diverso: Davisson riduce kh/nh, gli altri metodi forniscono p-multiplier. Non si calcola la capacità laterale del gruppo.");
        P($"Diametro D = {F(input["diametro"])} m; direzione H = {F(input["angolo"])}° antioraria da +X.");
        P($"Estrapolazioni: {input.B("estrapolazioni")}; estensione file proiettate: {input.B("file_proiettate")}; S∥ assegnato = {F(input["interasse_parallelo"])} m; S⊥ assegnato = {F(input["interasse_trasversale"])} m.");
        P("Coordinate dei pali", true);
        foreach (var p in input.Array("pali")) P($"{p.S("id")}: x = {F(p!["x"])} m; y = {F(p["y"])} m.");
        foreach (var row in rows.Select(r => r!))
        {
            int i = (int)row.D("Method");
            P(HorizontalPileGroup.MethodNames[i] + " · " + row.S("DirectionName") + (input.D("metodo") == i && input.S("direzione_selezionata") == row.S("DirectionId") ? " · selezionato" : ""), true);
            P($"Min {F(row["Minimum"])}; media {F(row["Factor"])}; max {F(row["Maximum"])}; riduzione media {F(row["ReductionPercent"])}%.");
            P(row.S("Quantity") + " = " + F(row["Factor"])); P(row.S("Source"));
            P($"S∥ = {F(row["ParallelSpacing"])} m; S⊥ = {F(row["TransverseSpacing"])} m.");
            if (row.S("Error") != "") P("Non disponibile: " + row.S("Error"));
            foreach (var warning in row.Array("Warnings")) P(warning!.ToString());
            if (J.Number(row["Factor"]).HasValue)
                foreach (var p in row.Array("Piles")) P($"{p.S("Id")} · fila {p.D("Row")} · {(i == 0 ? "Rg" : "Pm")} = {F(p!["Factor"])}" + (i >= 4 ? $" · β = {F(p["Beta"])} · α = {F(p["Alpha"])}" : ""));
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)), new XElement(w + "pgMar", new XAttribute(w + "top", 1134), new XAttribute(w + "bottom", 1134), new XAttribute(w + "left", 1134), new XAttribute(w + "right", 1134))));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Entry("word/document.xml", new XDocument(new XElement(w + "document", body)).ToString());
        }
        return memory.ToArray();
    }
}
