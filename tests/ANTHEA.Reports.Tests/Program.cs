using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Reports;
using Anthea.Testing.Normalization;

int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
void Reject<T>(Action action, string message) where T : Exception
{
    try { action(); } catch (T) { checks++; return; }
    throw new InvalidOperationException(message);
}
try
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory!.FullName;
    string output = args.Length == 0 ? Path.Combine(root, "supporto/artefatti/refactoring/f3/reports") : Path.GetFullPath(args[0]);
    Directory.CreateDirectory(output);
    XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
    DocxDocument Chapter(string name, byte[] png)
    {
        var doc = new DocxDocument();
        doc.SetXml("[Content_Types].xml", "<Types />");
        doc.SetXml("word/document.xml", new XElement(w + "document", new XElement(w + "body",
            new XElement(w + "p", new XElement(w + "r", new XElement(w + "t", "Titolo autonomo"))),
            new XElement(w + "p", new XElement(w + "r", new XElement(w + "t", "Sottotitolo"))),
            new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", "Heading1"))),
                new XElement(w + "r", new XElement(w + "t", name))),
            new XElement(w + "p", new XElement(w + "drawing", new XElement(a + "blip", new XAttribute(r + "embed", "sameId")))),
            new XElement(w + "sectPr"))).ToString());
        doc.SetXml("word/_rels/document.xml.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship",
            new XAttribute("Id", "sameId"), new XAttribute("Type", r.NamespaceName + "/image"), new XAttribute("Target", "media/image.png"))).ToString());
        doc.SetBytes("word/media/image.png", png);
        return doc;
    }
    var image = new byte[] { 1, 2, 3 };
    var first = Chapter("Primo – φ, γ", image); image[0] = 9;
    Check(first.GetBytes("word/media/image.png")[0] == 1, "Il documento conserva il buffer esterno modificabile");
    var copy = first.GetBytes("word/media/image.png"); copy[0] = 8;
    Check(first.GetBytes("word/media/image.png")[0] == 1, "GetBytes espone lo stato interno");
    Reject<ArgumentException>(() => first.SetXml("../document.xml", "<x />"), "Nome di parte non valido accettato");
    Reject<InvalidDataException>(() => DocxWriter.Write(new()), "Pacchetto incompleto accettato");
    var roundtrip = DocxWriter.Read(DocxWriter.Write(first));
    Check(XNode.DeepEquals(first.GetXml("word/document.xml"), roundtrip.GetXml("word/document.xml")), "Serializzazione perde testo o XML");
    Check(first.GetBytes("word/media/image.png").SequenceEqual(roundtrip.GetBytes("word/media/image.png")), "Serializzazione altera immagini");
    var sheet1 = JsonNode.Parse("{\"nome\":\"Foglio A\",\"modulo_id\":\"geo_palo_verticale\"}")!.AsObject();
    var sheet2 = (JsonObject)sheet1.DeepClone(); sheet2["nome"] = "Foglio B";
    var section = new JsonObject { ["nome"] = "Progetto", ["fogli"] = new JsonArray(sheet1, sheet2), ["strutture"] = new JsonArray() };
    var plan = new ProjectReportData(section, [sheet1, sheet2], [], [],
        new Dictionary<JsonObject, IReadOnlyList<ProjectReportData.InputValue>> { [sheet1] = [], [sheet2] = [] },
        new Dictionary<JsonObject, string> { [sheet1] = "Progetto", [sheet2] = "Progetto" });
    var contents = new Dictionary<JsonObject, ProjectReportData.SheetContent> {
        [sheet1] = new(first), [sheet2] = new(Chapter("Secondo", [4, 5, 6])) };
    string source = first.GetXml("word/document.xml").ToString();
    var composed = ProjectReportComposer.Compose(plan, contents, []);
    var body = composed.GetXml("word/document.xml");
    Check(!body.Descendants(w + "t").Any(t => t.Value == "Titolo autonomo"), "Blocco titolo autonomo duplicato");
    Check(body.Descendants(w + "pStyle").Count(s => (string?)s.Attribute(w + "val") == "Heading3") == 2, "Livelli di capitolo non adattati");
    var ids = body.Descendants(a + "blip").Select(e => (string)e.Attribute(r + "embed")!).ToArray();
    Check(ids.Length == 2 && ids.Distinct().Count() == 2, "Collisione fra relazioni immagine dei capitoli");
    var imageRelations = composed.GetXml("word/_rels/document.xml.rels").Root!.Elements().Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/image") == true).ToArray();
    Check(imageRelations.Length == 2 && imageRelations.Select(e => (string)e.Attribute("Id")!).SequenceEqual(ids), "Relazioni dei capitoli mancanti");
    Check(imageRelations.Select(e => composed.GetBytes("word/" + (string)e.Attribute("Target")!)[0]).SequenceEqual(new byte[] { 1, 4 }), "Immagini dei capitoli scambiate");
    Check(source == first.GetXml("word/document.xml").ToString(), "Composizione modifica il capitolo sorgente");
    Reject<OperationCanceledException>(() => ProjectReportComposer.Compose(plan, contents, [], new CancellationToken(true)), "Annullamento ignorato");
    var invalid = Chapter("Esterno", [1]);
    var relationships = invalid.GetXml("word/_rels/document.xml.rels"); relationships.Root!.Elements().First().SetAttributeValue("TargetMode", "External");
    invalid.SetXml("word/_rels/document.xml.rels", relationships.ToString()); contents[sheet1] = new(invalid);
    Reject<InvalidDataException>(() => ProjectReportComposer.Compose(plan, contents, []), "Relazione esterna importata");

    if (args.Length == 4 && args[1] == "--compare")
    {
        string before = Path.GetFullPath(args[2]), after = Path.GetFullPath(args[3]);
        var normalizer = new TextNormalizer();
        int documents = 0, xmlParts = 0, mediaParts = 0;
        var differences = new List<string>();
        foreach (string file in Directory.GetFiles(before, "*.docx", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(before, file), other = Path.Combine(after, relative);
            if (!File.Exists(other)) { differences.Add(relative + ": documento mancante"); continue; }
            using var za = ZipFile.OpenRead(file); using var zb = ZipFile.OpenRead(other); documents++;
            if (!za.Entries.Select(e => e.FullName).Order().SequenceEqual(zb.Entries.Select(e => e.FullName).Order()))
                differences.Add(relative + ": parti diverse");
            foreach (var entry in za.Entries)
            {
                var right = zb.GetEntry(entry.FullName); if (right is null) continue;
                using var sa = entry.Open(); using var sb = right.Open();
                if (entry.FullName.EndsWith(".xml") || entry.FullName.EndsWith(".rels"))
                {
                    using var ra = new StreamReader(sa); using var rb = new StreamReader(sb);
                    string leftText = normalizer.Normalize(ra.ReadToEnd(), new GuidMap());
                    string rightText = normalizer.Normalize(rb.ReadToEnd(), new GuidMap());
                    if (leftText != rightText) differences.Add(relative + ": " + entry.FullName);
                    xmlParts++;
                }
                else
                {
                    using var ma = new MemoryStream(); using var mb = new MemoryStream(); sa.CopyTo(ma); sb.CopyTo(mb);
                    if (!ma.ToArray().SequenceEqual(mb.ToArray())) differences.Add(relative + ": " + entry.FullName);
                    mediaParts++;
                }
            }
        }
        File.WriteAllText(Path.Combine(output, "parts-comparison.json"), JsonSerializer.Serialize(new { documents, xmlParts, mediaParts, differences }, new JsonSerializerOptions { WriteIndented = true }));
        Check(documents > 0 && differences.Count == 0, "Differenze fra parti DOCX: " + string.Join("; ", differences.Take(10)));
    }
    string result = $"PASS {checks} controlli Reports";
    File.WriteAllText(Path.Combine(output, "esito.txt"), result); Console.WriteLine(result); return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
