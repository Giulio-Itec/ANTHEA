using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>Typography only: never imports the reference's body, fields, headers, relationships or page setup.</summary>
public sealed class ReportStyleTemplate
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private readonly XElement styles;
    private const int MaximumStyleCharacters = 8_388_608;
    public string Name { get; }
    internal string StylesXml => styles.ToString();

    private ReportStyleTemplate(string name, XElement styles) { Name = name; this.styles = new(styles); }

    public static ReportStyleTemplate Read(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not (".docx" or ".dotx"))
            throw new ArgumentException("Scegliere un documento Word .docx o un modello .dotx.");
        using var stream = File.OpenRead(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        XElement? ReadPart(string part)
        {
            if (zip.GetEntry(part) is not { } entry) return null;
            if (entry.Length > MaximumStyleCharacters) throw new InvalidDataException("Parte degli stili del template troppo grande.");
            using var input = entry.Open();
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumStyleCharacters });
            return XElement.Load(reader);
        }
        var source = ReadPart("word/styles.xml") ?? throw new InvalidDataException("Il template non contiene stili Word.");
        var theme = ReadPart("word/theme/theme1.xml");
        var all = source.Elements(W + "style").Where(s => (string?)s.Attribute(W + "type") == "paragraph")
            .ToDictionary(s => (string?)s.Attribute(W + "styleId") ?? throw new InvalidDataException("Stile senza identificatore."));
        if (all.Count == 0) throw new InvalidDataException("Il template non contiene stili di paragrafo.");
        string Key(string? text) => (text ?? "").Replace(" ", "").ToLowerInvariant();
        XElement? Find(string role) => all.Values.FirstOrDefault(s => Key((string?)s.Element(W + "name")?.Attribute(W + "val")) == Key(role))
            ?? all.GetValueOrDefault(role);
        var normal = Find("Normal") ?? all.Values.FirstOrDefault(s => (string?)s.Attribute(W + "default") == "1")
            ?? throw new InvalidDataException("Il template non contiene uno stile Normale.");
        var output = new XElement(W + "styles", new XAttribute(XNamespace.Xmlns + "w", W));
        foreach (string role in new[] { "Normal", "Title", "Subtitle", "Caption", "TableText", "TableHeader", "Notice" }
            .Concat(Enumerable.Range(1, 9).Select(i => "Heading" + i)))
        {
            var match = Find(role);
            if (match is null && role.StartsWith("Heading"))
                match = all.Values.FirstOrDefault(s => (string?)s.Element(W + "pPr")?.Element(W + "outlineLvl")?.Attribute(W + "val") == (int.Parse(role[7..]) - 1).ToString());
            match ??= role == "Title" ? Find("heading 1") ?? normal : normal;
            var pPr = new XElement(W + "pPr"); var rPr = new XElement(W + "rPr");
            Merge(pPr, source.Element(W + "docDefaults")?.Element(W + "pPrDefault")?.Element(W + "pPr"));
            Merge(rPr, source.Element(W + "docDefaults")?.Element(W + "rPrDefault")?.Element(W + "rPr"));
            var chain = new List<XElement>(); var seen = new HashSet<string>();
            for (XElement? s = match; s is not null; s = all.GetValueOrDefault((string?)s.Element(W + "basedOn")?.Attribute(W + "val") ?? ""))
            {
                if (!seen.Add((string)s.Attribute(W + "styleId")!)) throw new InvalidDataException("Ereditarietà ciclica negli stili del template.");
                chain.Add(s);
            }
            foreach (var s in chain.AsEnumerable().Reverse()) { Merge(pPr, s.Element(W + "pPr")); Merge(rPr, s.Element(W + "rPr")); }
            // The report owns numbering and layout. Keep only self-contained typographic properties.
            Keep(pPr, "spacing", "jc", "ind", "keepNext", "keepLines", "widowControl", "contextualSpacing", "outlineLvl");
            Keep(rPr, "rFonts", "sz", "szCs", "b", "bCs", "i", "iCs", "color", "caps", "smallCaps", "u", "lang");
            if (rPr.Element(W + "rFonts") is { } fonts)
            {
                foreach (string field in new[] { "ascii", "hAnsi", "eastAsia", "cs" })
                {
                    string? themed = (string?)fonts.Attribute(W + (field + "Theme"));
                    if (themed is not null)
                    {
                        string? family = (string?)theme?.Descendants(A + (themed.StartsWith("major") ? "majorFont" : "minorFont")).FirstOrDefault()?.Element(A + "latin")?.Attribute("typeface");
                        if (!string.IsNullOrWhiteSpace(family)) fonts.SetAttributeValue(W + field, family);
                    }
                }
                fonts.Attributes().Where(a => a.Name.LocalName.EndsWith("Theme", StringComparison.OrdinalIgnoreCase)).Remove();
            }
            rPr.Element(W + "color")?.Attributes().Where(a => a.Name.LocalName.StartsWith("theme")).Remove();
            if (role.StartsWith("Heading") || role == "Title")
            {
                pPr.SetElementValue(W + "keepNext", "");
                if (role == "Title") pPr.Elements(W + "outlineLvl").Remove();
            }
            if (role is "TableText" or "TableHeader")
            {
                // A template rarely defines table paragraphs; use its family while keeping engineering tables readable.
                if (Find(role) is null)
                {
                    pPr.SetElementValue(W + "spacing", null);
                    pPr.Add(new XElement(W + "spacing", new XAttribute(W + "before", 0), new XAttribute(W + "after", 40)));
                    pPr.SetElementValue(W + "jc", null);
                    pPr.Add(new XElement(W + "jc", new XAttribute(W + "val", "left")));
                    if ((int?)rPr.Element(W + "sz")?.Attribute(W + "val") is int size && size > 20) rPr.Element(W + "sz")!.SetAttributeValue(W + "val", 20);
                }
                if (role == "TableHeader") rPr.SetElementValue(W + "b", "");
            }
            output.Add(new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", role),
                role == "Normal" ? new XAttribute(W + "default", 1) : null,
                new XElement(W + "name", new XAttribute(W + "val", role)), pPr, rPr));
        }
        return new(Path.GetFileName(path), output);
    }

    internal static ReportStyleTemplate Restore(string name, string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumStyleCharacters });
        var root = XElement.Load(reader);
        if (root.Name != W + "styles" || !root.Elements(W + "style").Any()) throw new InvalidDataException("Template salvato non valido.");
        return new(name, root);
    }

    public DocxDocument Apply(DocxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new DocxDocument();
        foreach (var part in document.Parts) result.SetBytes(part.Key, part.Value);
        var xml = result.GetXml("word/document.xml");
        var body = xml.Root!.Element(W + "body") ?? throw new InvalidDataException("Report senza corpo.");
        var first = body.Elements(W + "p").FirstOrDefault();
        foreach (var p in body.Descendants(W + "p"))
        {
            var properties = p.Element(W + "pPr");
            if (properties is null) { properties = new(W + "pPr"); p.AddFirst(properties); }
            string? old = (string?)properties.Element(W + "pStyle")?.Attribute(W + "val");
            string role = old is not null && styles.Elements(W + "style").Any(s => (string?)s.Attribute(W + "styleId") == old) ? old : "Normal";
            bool table = p.Ancestors(W + "tc").Any();
            bool heading = !table && p.Descendants(W + "rPr").Any(r => r.Element(W + "b") is { } b && (string?)b.Attribute(W + "val") is not ("0" or "false"));
            if (old is null) role = table ? (p.Ancestors(W + "tr").First().Element(W + "trPr")?.Element(W + "tblHeader") is not null ? "TableHeader" : "TableText")
                : p == first ? "Title" : heading ? "Heading1" : "Normal";
            properties.Elements(W + "pStyle").Remove(); properties.AddFirst(new XElement(W + "pStyle", new XAttribute(W + "val", role)));
            // Keep alignment/indentation of tables and technical graphics, and keep all pagination constraints.
            properties.Elements(W + "spacing").Remove();
            if (!table && !p.Descendants(W + "drawing").Any()) properties.Elements(W + "jc").Remove();
            foreach (var rPr in p.Descendants(W + "rPr"))
            {
                foreach (string key in new[] { "rFonts", "sz", "szCs", "rStyle" }) rPr.Elements(W + key).Remove();
                if (role == "Title" || role.StartsWith("Heading"))
                    foreach (string key in new[] { "b", "bCs", "i", "iCs", "color", "caps", "smallCaps", "u" }) rPr.Elements(W + key).Remove();
            }
        }
        result.SetXml("word/document.xml", xml.ToString());
        var merged = document.Contains("word/styles.xml") ? document.GetXml("word/styles.xml").Root! : new XElement(W + "styles");
        merged.Elements(W + "docDefaults").Remove();
        foreach (var style in merged.Elements(W + "style")) style.Attribute(W + "default")?.Remove();
        foreach (var style in styles.Elements(W + "style"))
        {
            merged.Elements(W + "style").Where(s => (string?)s.Attribute(W + "styleId") == (string?)style.Attribute(W + "styleId")).Remove();
            merged.Add(new XElement(style));
        }
        result.SetXml("word/styles.xml", merged.ToString());
        var rels = result.Contains("word/_rels/document.xml.rels") ? result.GetXml("word/_rels/document.xml.rels").Root! : new XElement(R + "Relationships");
        const string type = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
        rels.Elements().Where(e => (string?)e.Attribute("Type") == type).Remove();
        string id = "reportStyles";
        while (rels.Elements().Any(e => (string?)e.Attribute("Id") == id)) id += "_";
        rels.Add(new XElement(R + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", "styles.xml")));
        result.SetXml("word/_rels/document.xml.rels", rels.ToString());
        var types = result.GetXml("[Content_Types].xml");
        XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        if (!types.Root!.Elements().Any(e => (string?)e.Attribute("PartName") == "/word/styles.xml"))
            types.Root.Add(new XElement(ct + "Override", new XAttribute("PartName", "/word/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml")));
        result.SetXml("[Content_Types].xml", types.ToString());
        return result;
    }

    private static void Keep(XElement element, params string[] names) => element.Elements().Where(e => e.Name.Namespace != W || !names.Contains(e.Name.LocalName)).Remove();
    private static void Merge(XElement target, XElement? source)
    {
        if (source is null) return;
        foreach (var property in source.Elements())
        {
            var current = target.Element(property.Name);
            if (current is null) target.Add(new XElement(property));
            else if (property.Name.LocalName is not ("spacing" or "rFonts" or "ind" or "lang")) current.ReplaceWith(new XElement(property));
            else
            {
                foreach (var attribute in property.Attributes())
                {
                    current.SetAttributeValue(attribute.Name, attribute.Value);
                    if (property.Name == W + "rFonts" && attribute.Name.LocalName is "ascii" or "hAnsi" or "eastAsia" or "cs")
                        current.Attribute(W + (attribute.Name.LocalName + "Theme"))?.Remove();
                }
                if (property.HasElements) { current.RemoveNodes(); current.Add(property.Nodes().Select(n => n is XElement e ? new XElement(e) : n)); }
            }
        }
    }
}
