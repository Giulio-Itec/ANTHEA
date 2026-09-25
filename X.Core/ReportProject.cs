using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Combines ANTHEA's generated reports, remapping image relationships and heading levels.</summary>
public static class ReportProject
{
    public sealed record SheetContent(byte[]? Docx = null, string? Error = null, IReadOnlyList<ProjectReportPlan.InputValue>? Derived = null);
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
        R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
        Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static void Write(string path, ProjectReportPlan plan, IReadOnlyDictionary<JsonObject, SheetContent> reports, IReadOnlyList<string> warnings, CancellationToken cancellation = default, bool materialSheet = false)
    {
        if (plan.Sheets.Length == 0) throw new ArgumentException("La sezione non contiene schede da esportare.");
        if (materialSheet && (plan.Sheets.Length != 1 || plan.Sheets[0].S("modulo_id") is not ("mat_calcestruzzo" or RebarMaterial.Module)))
            throw new ArgumentException("Il report materiale richiede una sola scheda materiali.");
        if (plan.Sheets.Any(s => !reports.ContainsKey(s))) throw new ArgumentException("Preparazione incompleta: manca una scheda del report.");
        var body = new XElement(W + "body");
        var relationships = new XElement(Rel + "Relationships");
        var media = new Dictionary<string, byte[]>(); int imageId = 0, bookmarkId = 0;
        var numbers = new Dictionary<JsonObject, string>(); var levels = new Dictionary<JsonObject, int>();
        void Number(JsonObject section, string number, int level)
        {
            numbers[section] = number; levels[section] = level; int i = 0;
            foreach (var sheet in section.Array("fogli").OfType<JsonObject>()) { numbers[sheet] = number + "." + ++i; levels[sheet] = level + 1; }
            foreach (var child in section.Array("strutture").OfType<JsonObject>()) Number(child, number + "." + ++i, level + 1);
        }
        Number(plan.Root, "1", 1);
        var anchors = numbers.Keys.Select((node, i) => (node, name: "section" + (i + 1))).ToDictionary(p => p.node, p => p.name);
        XElement Paragraph(string text, string style = "Normal") => new(W + "p", new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", style))),
            new XElement(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        void P(string text, string style = "Normal") => body.Add(Paragraph(text, style));
        void Heading(string text, int level) => P(text, "Heading" + Math.Clamp(level, 1, 9));
        string Text(JsonNode? value) => value is null ? "Non definito" : value is JsonValue v && v.TryGetValue<bool>(out bool b) ? b ? "Sì" : "No" :
            J.Number(value) is double n ? n.ToString(materialSheet ? "0.###" : "0.########", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) : value.ToString();
        void Table(IEnumerable<string[]> source, params string[] headers)
        {
            var rows = source.ToArray(); if (rows.Length == 0) return;
            int[] widths = headers.Length == 2 ? (materialSheet ? [4400, 4960] : [5300, 4060]) : Enumerable.Repeat(9360 / headers.Length, headers.Length).ToArray();
            var table = new XElement(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", 9360), new XAttribute(W + "type", "dxa")),
                new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
                new XElement(W + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(s => new XElement(W + s, new XAttribute(W + "w", materialSheet ? 60 : 75), new XAttribute(W + "type", "dxa")))),
                new XElement(W + "tblBorders", new[] { "top", "bottom", "insideH" }.Select(s => new XElement(W + s, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "D8E0E8"))))),
                new XElement(W + "tblGrid", widths.Select(n => new XElement(W + "gridCol", new XAttribute(W + "w", n)))));
            foreach (var (cells, i) in new[] { headers }.Concat(rows).Select((row, i) => (row, i)))
                table.Add(new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), i == 0 ? new XElement(W + "tblHeader") : null),
                    cells.Select((text, col) => new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", widths[col]), new XAttribute(W + "type", "dxa")),
                        i == 0 ? new XElement(W + "shd", new XAttribute(W + "fill", "E8EFF7")) : null), Paragraph(text, i == 0 ? "TableHeader" : "TableText")))));
            body.Add(table); P("");
        }
        IEnumerable<string[]> Values(IEnumerable<ProjectReportPlan.InputValue> values)
        {
            foreach (var item in values)
                if (item.Value is JsonArray or JsonObject)
                    foreach (var nested in ProjectReportPlan.Walk(item.Value, item.Label)) yield return [ProjectReportPlan.PathLabel(nested.Path), Text(nested.Value)];
                else yield return [item.Label, Text(item.Value)];
        }
        string SheetRef(JsonObject sheet) => numbers.TryGetValue(sheet, out var number) ? number + " " + sheet.S("nome") : ProjectSharedData.Location(sheet) + " › " + sheet.S("nome");
        P(materialSheet ? "Scheda materiale" : "Relazione di calcolo", "Title"); P(plan.Root.S("nome", "Sezione"), "Subtitle"); P("ANTHEA · " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        if (plan.Root["revisione"] is JsonObject revision)
        {
            P("Rev. " + revision.D("numero") + " · " + (DateTime.TryParse(revision.S("data"), out var date) ? date.ToString("dd/MM/yyyy HH:mm") : revision.S("data")), "Subtitle");
            if (revision.S("nota").Length > 0) P(revision.S("nota"));
        }
        if (materialSheet) P("Parametri impostati e proprietà ricavate dai dati correnti della scheda. Le indicazioni di applicabilità e gli eventuali dati da completare sono riportati nel documento.");
        else
        {
        P($"Il documento comprende {plan.Sheets.Length} schede, organizzate secondo la struttura della sezione e delle sue sottosezioni. I dati comuni compatibili e uguali sono raccolti al livello di riferimento; azioni, parametri specifici e risultati sono riportati per ciascun foglio.");
        P("I calcoli sono stati aggiornati sui dati acquisiti all'avvio dell'esportazione. Gli esiti mancanti o non determinati e i limiti dei singoli moduli restano esplicitamente indicati.");
        }
        var missing = reports.Where(r => r.Value.Error is not null).ToArray();
        if (plan.Conflicts.Count > 0 || missing.Length > 0) P("Attenzione: il report contiene " + plan.Conflicts.Select(c => c.Key).Distinct().Count() + " proprietà in conflitto e " + missing.Length + " schede senza risultati completi. Leggere le segnalazioni seguenti.", "Notice");
        if (!materialSheet)
        {
        Heading("Contenuti", 1);
        foreach (var (node, number) in numbers)
        {
            var p = new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 45)), new XElement(W + "ind", new XAttribute(W + "left", Math.Min(levels[node] - 1, 8) * 180))),
                new XElement(W + "hyperlink", new XAttribute(W + "anchor", anchors[node]), new XElement(W + "r", new XElement(W + "t", number + " " + node.S("nome", "Sezione")))));
            body.Add(p);
        }
        }
        if (plan.Conflicts.Count > 0)
        {
            Heading("Conflitti tra i fogli", 1); P("I valori discordanti sono conservati nei rispettivi fogli e non vengono unificati nel report.");
            foreach (var group in plan.Conflicts.GroupBy(c => c.Key))
            {
                Heading(ProjectReportPlan.Label(group.Key), 2);
                var participants = group.SelectMany(d => new[] { d.First, d.Second }).Distinct();
                Table(participants.Select(s => new[] { SheetRef(s), Text(ProjectSharedData.Fields(s)[group.Key].Value) }), "Foglio", "Valore adottato");
            }
        }
        if (warnings.Count > 0 || missing.Length > 0)
        {
            Heading("Avvisi", 1);
            foreach (string warning in warnings.Distinct()) P(warning);
            foreach (var (sheet, content) in missing) P(SheetRef(sheet) + ": " + content.Error, "Notice");
        }
        void AppendReport(byte[] bytes, JsonObject sheet)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            using var xml = zip.GetEntry("word/document.xml")!.Open();
            var imported = XDocument.Load(xml).Root!.Element(W + "body")!;
            var mapping = new Dictionary<string, string>();
            if (zip.GetEntry("word/_rels/document.xml.rels") is { } relEntry)
            {
                using var stream = relEntry.Open();
                foreach (var rel in XDocument.Load(stream).Root!.Elements())
                {
                    string type = (string)rel.Attribute("Type")!;
                    if (type.EndsWith("/styles")) continue;
                    if (!type.EndsWith("/image") || rel.Attribute("TargetMode") is not null) throw new InvalidDataException("Collegamento del report non supportato: " + type);
                    string target = (string)rel.Attribute("Target")!;
                    var entry = zip.GetEntry("word/" + target) ?? throw new InvalidDataException("Immagine assente nel report.");
                    if (!target.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Formato immagine del report non supportato.");
                    string id = "img" + ++imageId, name = "media/" + id + ".png";
                    using var image = new MemoryStream(); using (var input = entry.Open()) input.CopyTo(image); media["word/" + name] = image.ToArray();
                    mapping[(string)rel.Attribute("Id")!] = id;
                    relationships.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", name)));
                }
            }
            // These are our own generators: remove their standalone title block, retaining all report content.
            int titleParagraphs = sheet.S("modulo_id") == "str_palo" ? 3 : 2;
            foreach (var element in imported.Elements().Where(e => e.Name != W + "sectPr").Skip(titleParagraphs))
            {
                var copy = new XElement(element);
                foreach (var attribute in copy.DescendantsAndSelf().Attributes().Where(a => a.Name.Namespace == R))
                    if (mapping.TryGetValue(attribute.Value, out var id)) attribute.Value = id;
                    else throw new InvalidDataException("Relazione mancante nel report: " + attribute.Value);
                foreach (var drawing in copy.Descendants().Where(e => e.Name.LocalName is "docPr" or "cNvPr")) drawing.SetAttributeValue("id", ++imageId);
                foreach (var style in copy.Descendants(W + "pStyle"))
                    if (((string?)style.Attribute(W + "val")) is string name && name.StartsWith("Heading") && int.TryParse(name[7..], out int level))
                        style.SetAttributeValue(W + "val", "Heading" + Math.Min(9, levels[sheet] + level));
                body.Add(copy);
            }
        }
        void Chapter(JsonObject section)
        {
            if (!materialSheet) NodeHeading(section);
            var common = plan.Common.Where(v => ReferenceEquals(v.Section, section));
            foreach (var group in common.GroupBy(v => (v.Field.Group, Origin: ProjectSharedData.Location(v.Source), Members: string.Join("; ", v.Sheets.Select(SheetRef)))))
            {
                Heading(group.Key.Group switch { "Geometria" => "Geometria comune", "Armatura" => "Armature comuni", "Terreno" => "Dati comuni del terreno", _ => group.Key.Group + " comuni" }, levels[section] + 1);
                P("Riferimento: " + group.Key.Origin + ". Utilizzati da: " + group.Key.Members + ".");
                Table(Values(group.Select(v => new ProjectReportPlan.InputValue(v.Field.Group, ProjectReportPlan.Label(v.Field.Key), v.Field.Value))), "Proprietà", "Valore");
            }
            foreach (var sheet in section.Array("fogli").OfType<JsonObject>())
            {
                if (!materialSheet) NodeHeading(sheet);
                var refs = plan.Common.Where(v => v.Sheets.Contains(sheet)).Select(v => numbers[v.Section] + " " + v.Section.S("nome") + " — " + v.Field.Group).Distinct().ToArray();
                if (refs.Length > 0) P("Dati condivisi: vedere " + string.Join("; ", refs) + ".");
                foreach (var group in plan.LocalInputs(sheet).GroupBy(v => v.Group))
                { Heading(group.Key, materialSheet ? 1 : levels[sheet] + 1); Table(Values(group), "Proprietà", "Valore"); }
                var content = reports[sheet];
                if (content.Derived is { Count: > 0 }) { Heading("Proprietà di calcolo", materialSheet ? 1 : levels[sheet] + 1); Table(Values(content.Derived), "Proprietà", "Valore"); }
                if (content.Error is not null) P("Risultati non disponibili: " + content.Error, "Notice");
                if (content.Docx is not null) AppendReport(content.Docx, sheet);
            }
            if (section.Array("fogli").Count == 0 && section.Array("strutture").Count == 0) P("Sezione senza schede.");
            foreach (var child in section.Array("strutture").OfType<JsonObject>()) Chapter(child);
        }
        void NodeHeading(JsonObject node)
        {
            string revision = node["revisione"] is JsonObject rev ? " · Rev. " + (int)rev.D("numero") : "";
            var p = Paragraph(numbers[node] + " " + node.S("nome", "Sezione") + revision, "Heading" + Math.Min(9, levels[node]));
            int id = ++bookmarkId;
            p.Element(W + "pPr")!.AddAfterSelf(new XElement(W + "bookmarkStart", new XAttribute(W + "id", id), new XAttribute(W + "name", anchors[node])));
            p.Add(new XElement(W + "bookmarkEnd", new XAttribute(W + "id", id))); body.Add(p);
        }
        Chapter(plan.Root);
        relationships.Add(new XElement(Rel + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", R.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")),
            new XElement(Rel + "Relationship", new XAttribute("Id", "footer"), new XAttribute("Type", R.NamespaceName + "/footer"), new XAttribute("Target", "footer.xml")));
        body.Add(new XElement(W + "sectPr", new XElement(W + "footerReference", new XAttribute(W + "type", "default"), new XAttribute(R + "id", "footer")),
            new XElement(W + "pgSz", new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838)),
            new XElement(W + "pgMar", new XAttribute(W + "top", 1134), new XAttribute(W + "bottom", 1134), new XAttribute(W + "left", 1273), new XAttribute(W + "right", 1273), new XAttribute(W + "footer", 567))));
        var styles = new XElement(W + "styles");
        foreach (var (id, size, bold) in new[] { ("Normal", 21, false), ("Title", 36, true), ("Subtitle", 28, false), ("TableText", 19, false), ("TableHeader", 19, true), ("Caption", 20, false), ("Notice", 21, true) }
            .Concat(Enumerable.Range(1, 9).Select(n => ("Heading" + n, n == 1 ? 30 : n == 2 ? 26 : 23, true))))
        {
            bool heading = id.StartsWith("Heading");
            styles.Add(new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", id), new XElement(W + "name", new XAttribute(W + "val", id)),
                new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "before", heading ? 220 : 0), new XAttribute(W + "after", id.StartsWith("Table") ? 35 : 100)), new XElement(W + "widowControl"),
                    heading || id is "Title" or "Subtitle" or "Caption" or "TableHeader" ? new XElement(W + "keepNext") : null,
                    heading ? new XElement(W + "outlineLvl", new XAttribute(W + "val", int.Parse(id[7..]) - 1)) : null),
                new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri")), new XElement(W + "sz", new XAttribute(W + "val", size)), new XElement(W + "color", new XAttribute(W + "val", "000000")), bold ? new XElement(W + "b") : null)));
        }
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/word/footer.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/></Types>");
            Entry("_rels/.rels", new XElement(Rel + "Relationships", new XElement(Rel + "Relationship", new XAttribute("Id", "doc"), new XAttribute("Type", R.NamespaceName + "/officeDocument"), new XAttribute("Target", "word/document.xml"))).ToString());
            Entry("word/document.xml", new XDocument(new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body)).ToString());
            Entry("word/styles.xml", styles.ToString()); Entry("word/_rels/document.xml.rels", relationships.ToString());
            Entry("word/footer.xml", new XElement(W + "ftr", new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "jc", new XAttribute(W + "val", "right"))), new XElement(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), "ANTHEA · Pagina ")), new XElement(W + "fldSimple", new XAttribute(W + "instr", "PAGE"), new XElement(W + "r", new XElement(W + "t", "1"))))).ToString());
            foreach (var (name, bytes) in media) { using var stream = zip.CreateEntry(name).Open(); stream.Write(bytes); }
        }
        cancellation.ThrowIfCancellationRequested();
        Archivio.ScriviAtomico(path, memory.ToArray());
    }
}
