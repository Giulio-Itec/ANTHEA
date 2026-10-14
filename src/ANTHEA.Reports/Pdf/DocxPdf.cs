using System.Globalization;
using System.Xml.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Fields;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Anthea.Reports.Pdf;

/// <summary>PDF bytes and number of pages.</summary>
public sealed record PdfOutput(byte[] Bytes, int Pages);

/// <summary>
/// PDF of a report built from the same Open XML parts as its DOCX, without Word (user decision of 14/10/2026: every report in DOCX and
/// PDF). It reads the subset of WordprocessingML that the ANTHEA generators write: sections with page size, margins and footers,
/// paragraph styles with document defaults, spacing, alignment, indentation, keep-with-next and page breaks; runs with font, size, bold,
/// italic, colour, vertical alignment, line and page breaks and tabs; inline PNG pictures; simple PAGE fields; internal hyperlinks and
/// bookmarks; tables with grid, cell margins, borders, shading, spans, vertical alignment and heading rows. Contents and order are those of
/// the DOCX; pagination is MigraDoc's and can differ from Word's.
/// </summary>
public static class DocxPdf
{
    public static PdfOutput Render(DocxDocument docx, string title = "")
    {
        ArgumentNullException.ThrowIfNull(docx);
        PdfFonts.Ensure();
        var renderer = new PdfDocumentRenderer { Document = Translate(docx, title) };
        renderer.RenderDocument();
        int pages = renderer.PdfDocument.PageCount; // read before saving: a saved document cannot be queried any more
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, false);
        return new(output.ToArray(), pages);
    }

    /// <summary>MigraDoc document equivalent to the DOCX parts (diagnostics and tests read its text with <see cref="Lines"/>).</summary>
    public static Document Translate(DocxDocument docx, string title = "") => new Translator(docx).Run(title);

    /// <summary>Text of the document in reading order: one line per paragraph, one per table cell, footers excluded.</summary>
    public static IEnumerable<string> Lines(Document document)
    {
        foreach (Section section in document.Sections)
            foreach (var line in Lines(section.Elements)) yield return line;
    }

    static IEnumerable<string> Lines(DocumentElements elements)
    {
        foreach (var element in elements)
            switch (element)
            {
                case Paragraph p: yield return Text(p.Elements); break;
                case Table t:
                    foreach (Row row in t.Rows)
                        for (int c = 0; c < t.Columns.Count; c++)
                            foreach (var line in Lines(row.Cells[c].Elements)) yield return line;
                    break;
            }
    }

    static string Text(ParagraphElements elements) => string.Concat(elements.Select(e => e switch
    {
        MigraDoc.DocumentObjectModel.Text t => t.Content,
        FormattedText f => Text(f.Elements),
        Hyperlink h => Text(h.Elements),
        Character c when c.SymbolName == SymbolName.LineBreak => "\n",
        Character c when c.SymbolName == SymbolName.Tab => "\t",
        _ => ""
    })).Replace(" ", " ");

    sealed record RunProps(string? Font = null, double? Size = null, bool? Bold = null, bool? Italic = null, string? Color = null, string? Vertical = null)
    {
        public RunProps Over(RunProps? top) => top is null ? this : new(top.Font ?? Font, top.Size ?? Size, top.Bold ?? Bold, top.Italic ?? Italic,
            top.Color ?? Color, top.Vertical ?? Vertical);
    }

    sealed record ParaProps(double? Before = null, double? After = null, bool? KeepNext = null, bool? PageBreakBefore = null, string? Align = null,
        double? Indent = null, bool? Widow = null)
    {
        public ParaProps Over(ParaProps? top) => top is null ? this : new(top.Before ?? Before, top.After ?? After, top.KeepNext ?? KeepNext,
            top.PageBreakBefore ?? PageBreakBefore, top.Align ?? Align, top.Indent ?? Indent, top.Widow ?? Widow);
    }

    sealed class Translator(DocxDocument docx)
    {
        static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
        static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        // Units of WordprocessingML: twips (1/20 pt), half points, eighths of a point (borders), EMU (1/12700 pt)
        const double TwipsPerPoint = 20, HalfPointsPerPoint = 2, EighthsPerPoint = 8, EmuPerPoint = 12700;
        // Word values when the document does not state them: A4, margins of 1 inch, cell margins of 0,19 cm, 10 pt
        const double PageWidthTwips = 11906, PageHeightTwips = 16838, MarginTwips = 1440, CellMarginTwips = 108, DefaultSize = 10;
        const double MinimumColumnTwips = 567;

        readonly Dictionary<string, (string? BasedOn, ParaProps Para, RunProps Run)> styles = new(StringComparer.Ordinal);
        readonly Dictionary<string, string> relations = new(StringComparer.Ordinal);
        RunProps defaultRun = new(PdfFonts.DefaultFamily, DefaultSize);
        ParaProps defaultPara = new(0, 0);
        string? defaultStyle;
        XElement? footer;
        double contentWidth;

        public Document Run(string title)
        {
            var document = new Document();
            document.Info.Title = title; document.Info.Author = "ANTHEA";
            ReadStyles(); ReadRelations("word/_rels/document.xml.rels", relations);
            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = PdfFonts.Family(defaultRun.Font); normal.Font.Size = defaultRun.Size ?? DefaultSize;
            normal.ParagraphFormat.SpaceBefore = 0; normal.ParagraphFormat.SpaceAfter = 0;
            var body = docx.GetXml("word/document.xml").Root!.Element(W + "body") ?? throw new InvalidDataException("Documento Word senza corpo.");
            var blocks = new List<XElement>(); XElement? last = null;
            foreach (var child in body.Elements())
            {
                if (child.Name == W + "sectPr") { last = child; continue; }
                blocks.Add(child);
                // A section property in a paragraph closes the section that ends with that paragraph
                if (child.Name == W + "p" && child.Element(W + "pPr")?.Element(W + "sectPr") is XElement closing) { Section(document, blocks, closing); blocks = []; }
            }
            if (blocks.Count > 0 || document.Sections.Count == 0) Section(document, blocks, last);
            return document;
        }

        void Section(Document document, List<XElement> blocks, XElement? properties)
        {
            var section = document.AddSection();
            var size = properties?.Element(W + "pgSz"); var margins = properties?.Element(W + "pgMar");
            double width = Twips(size, "w") ?? PageWidthTwips, height = Twips(size, "h") ?? PageHeightTwips;
            var setup = section.PageSetup;
            if (width > height) { setup.Orientation = Orientation.Landscape; setup.PageWidth = Pt(height); setup.PageHeight = Pt(width); }
            else { setup.Orientation = Orientation.Portrait; setup.PageWidth = Pt(width); setup.PageHeight = Pt(height); }
            double left = Twips(margins, "left") ?? MarginTwips, right = Twips(margins, "right") ?? MarginTwips;
            setup.TopMargin = Pt(Twips(margins, "top") ?? MarginTwips); setup.BottomMargin = Pt(Twips(margins, "bottom") ?? MarginTwips);
            setup.LeftMargin = Pt(left); setup.RightMargin = Pt(right);
            if (Twips(margins, "footer") is double footerDistance) setup.FooterDistance = Pt(footerDistance);
            contentWidth = width - left - right;
            // Word links a section without its own footer to the previous one
            if (properties?.Elements(W + "footerReference").FirstOrDefault(f => (string?)f.Attribute(W + "type") is null or "default") is XElement reference
                && relations.TryGetValue((string?)reference.Attribute(R + "id") ?? "", out var target) && docx.Contains("word/" + target))
                footer = docx.GetXml("word/" + target).Root;
            if (footer is not null) foreach (var p in footer.Elements(W + "p")) Paragraph(section.Footers.Primary, p);
            foreach (var block in blocks) Block(section, block);
        }

        void Block(DocumentObject container, XElement block)
        {
            if (block.Name == W + "p") Paragraph(container, block);
            else if (block.Name == W + "tbl")
            {
                if (container is Section section) Table(section, block);
                else foreach (var p in block.Descendants(W + "p")) Paragraph(container, p); // nested tables: their paragraphs in order
            }
        }

        static Paragraph Add(DocumentObject container) => container switch
        {
            Section s => s.AddParagraph(), HeaderFooter h => h.AddParagraph(), Cell c => c.AddParagraph(),
            _ => throw new InvalidOperationException("Contenitore di paragrafo non previsto.")
        };

        void Paragraph(DocumentObject container, XElement p)
        {
            var pPr = p.Element(W + "pPr");
            var (para, run) = Style((string?)pPr?.Element(W + "pStyle")?.Attribute(W + "val"));
            para = para.Over(ReadPara(pPr));
            var paragraph = Add(container); Format(paragraph, para, run);
            bool content = false;
            foreach (var node in p.Elements()) content |= Inline(container, ref paragraph, node, run, para);
            if (!content) paragraph.AddText(" ");
        }

        bool Inline(DocumentObject container, ref Paragraph paragraph, XElement node, RunProps run, ParaProps para)
        {
            if (node.Name == W + "r") return Run(container, ref paragraph, node, run, para, null);
            if (node.Name == W + "hyperlink")
            {
                string anchor = (string?)node.Attribute(W + "anchor") ?? "";
                var link = anchor.Length > 0 ? paragraph.AddHyperlink(anchor, HyperlinkType.Bookmark) : null;
                bool any = false;
                foreach (var r in node.Elements(W + "r")) any |= Run(container, ref paragraph, r, run, para, link);
                return any;
            }
            if (node.Name == W + "bookmarkStart" && (string?)node.Attribute(W + "name") is { Length: > 0 } name && !name.StartsWith('_')) { paragraph.AddBookmark(name); return false; }
            if (node.Name == W + "fldSimple")
            {
                string instruction = ((string?)node.Attribute(W + "instr") ?? "").Trim();
                if (instruction.StartsWith("PAGE", StringComparison.OrdinalIgnoreCase)) { paragraph.AddPageField(); return true; }
                if (instruction.StartsWith("NUMPAGES", StringComparison.OrdinalIgnoreCase)) { paragraph.AddNumPagesField(); return true; }
                bool any = false;
                foreach (var r in node.Elements(W + "r")) any |= Run(container, ref paragraph, r, run, para, null);
                return any;
            }
            return false;
        }

        bool Run(DocumentObject container, ref Paragraph paragraph, XElement r, RunProps paragraphRun, ParaProps para, Hyperlink? link)
        {
            var props = paragraphRun.Over(ReadRun(r.Element(W + "rPr")));
            bool content = false;
            foreach (var node in r.Elements())
            {
                if (node.Name == W + "t")
                {
                    // Word ignores leading and trailing spaces without xml:space="preserve" and shows a line feed in a text node as a space
                    string text = node.Value.Replace('\r', ' ').Replace('\n', ' ');
                    if ((string?)node.Attribute(XNamespace.Xml + "space") != "preserve") text = text.Trim();
                    if (text.Length == 0) continue;
                    foreach (var (segment, family) in PdfFonts.Segments(text, props.Font ?? PdfFonts.DefaultFamily, props.Bold == true))
                        Apply(link is null ? paragraph.AddFormattedText(segment) : link.AddFormattedText(segment), props with { Font = family });
                    content = true;
                }
                else if (node.Name == W + "tab") { paragraph.AddTab(); content = true; }
                else if (node.Name == W + "br")
                {
                    if ((string?)node.Attribute(W + "type") == "page" && container is Section section)
                    {
                        section.AddPageBreak(); paragraph = section.AddParagraph(); Format(paragraph, para with { PageBreakBefore = false }, paragraphRun);
                    }
                    else paragraph.AddLineBreak();
                    content = true;
                }
                else if (node.Name == W + "drawing" && Picture(paragraph, node)) content = true;
            }
            return content;
        }

        bool Picture(Paragraph paragraph, XElement drawing)
        {
            var extent = drawing.Descendants(WP + "extent").FirstOrDefault();
            string? id = (string?)drawing.Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed");
            if (extent is null || id is null || !relations.TryGetValue(id, out var target) || !docx.Contains("word/" + target)) return false;
            double width = (double?)extent.Attribute("cx") / EmuPerPoint ?? 0, height = (double?)extent.Attribute("cy") / EmuPerPoint ?? 0;
            // A picture wider than the text column is reduced to it (Word would let it run into the margin)
            double limit = contentWidth / TwipsPerPoint;
            if (width > limit && width > 0) { height *= limit / width; width = limit; }
            var image = paragraph.AddImage("base64:" + Convert.ToBase64String(docx.GetBytes("word/" + target)));
            image.LockAspectRatio = false; image.Width = Unit.FromPoint(width); image.Height = Unit.FromPoint(height);
            return true;
        }

        void Table(Section section, XElement tbl)
        {
            var tblPr = tbl.Element(W + "tblPr");
            var rows = tbl.Elements(W + "tr").ToList();
            int columns = rows.Count == 0 ? 0 : rows.Max(r => r.Elements(W + "tc").Sum(Span));
            if (columns == 0) return;
            var widths = Widths(tbl, rows, columns);
            var table = section.AddTable();
            foreach (double w in widths) table.AddColumn(Pt(w));
            var margins = tblPr?.Element(W + "tblCellMar");
            table.LeftPadding = Pt(Twips(margins?.Element(W + "left") ?? margins?.Element(W + "start"), "w") ?? CellMarginTwips);
            table.RightPadding = Pt(Twips(margins?.Element(W + "right") ?? margins?.Element(W + "end"), "w") ?? CellMarginTwips);
            table.TopPadding = Pt(Twips(margins?.Element(W + "top"), "w") ?? 0);
            table.BottomPadding = Pt(Twips(margins?.Element(W + "bottom"), "w") ?? 0);
            var borders = tblPr?.Element(W + "tblBorders");
            for (int r = 0; r < rows.Count; r++)
            {
                var row = table.AddRow();
                if (rows[r].Element(W + "trPr")?.Element(W + "tblHeader") is { } header && Flag(header) == true) row.HeadingFormat = true;
                int column = 0;
                foreach (var tc in rows[r].Elements(W + "tc"))
                {
                    if (column >= columns) break;
                    int span = Math.Min(Span(tc), columns - column);
                    var cell = row.Cells[column];
                    if (span > 1) cell.MergeRight = span - 1;
                    var tcPr = tc.Element(W + "tcPr");
                    cell.VerticalAlignment = (string?)tcPr?.Element(W + "vAlign")?.Attribute(W + "val") switch
                    {
                        "center" => VerticalAlignment.Center, "bottom" => VerticalAlignment.Bottom, _ => VerticalAlignment.Top
                    };
                    if (ColorOf((string?)tcPr?.Element(W + "shd")?.Attribute(W + "fill")) is Color fill) cell.Shading.Color = fill;
                    bool first = column == 0, lastColumn = column + span >= columns, top = r == 0, bottom = r == rows.Count - 1;
                    Border(cell.Borders.Top, borders?.Element(W + (top ? "top" : "insideH")));
                    Border(cell.Borders.Bottom, borders?.Element(W + (bottom ? "bottom" : "insideH")));
                    Border(cell.Borders.Left, borders?.Element(W + (first ? "left" : "insideV")) ?? (first ? borders?.Element(W + "start") : null));
                    Border(cell.Borders.Right, borders?.Element(W + (lastColumn ? "right" : "insideV")) ?? (lastColumn ? borders?.Element(W + "end") : null));
                    bool any = false;
                    foreach (var block in tc.Elements().Where(e => e.Name == W + "p" || e.Name == W + "tbl")) { Block(cell, block); any = true; }
                    if (!any) cell.AddParagraph();
                    column += span;
                }
            }
        }

        static int Span(XElement tc) => (int?)tc.Element(W + "tcPr")?.Element(W + "gridSpan")?.Attribute(W + "val") is int span && span > 0 ? span : 1;

        // Grid of the table when complete, else the cell widths of the first full row, else widths proportional to the longest text
        List<double> Widths(XElement tbl, List<XElement> rows, int columns)
        {
            var grid = tbl.Element(W + "tblGrid")?.Elements(W + "gridCol").Select(g => Twips(g, "w") ?? 0).ToList();
            List<double> widths;
            if (grid is not null && grid.Count == columns && grid.All(w => w > 0)) widths = grid;
            else if (rows.FirstOrDefault(r => r.Elements(W + "tc").Count() == columns) is XElement full
                && full.Elements(W + "tc").Select(tc => (string?)tc.Element(W + "tcPr")?.Element(W + "tcW")?.Attribute(W + "type") == "dxa"
                    ? Twips(tc.Element(W + "tcPr")!.Element(W + "tcW"), "w") ?? 0 : 0).ToList() is var cells && cells.All(w => w > 0))
                widths = cells;
            else
            {
                var longest = Enumerable.Range(0, columns).Select(c => (double)Math.Max(1, rows.Max(r => r.Elements(W + "tc").ElementAtOrDefault(c)?.Value.Length ?? 0))).ToList();
                double total = longest.Sum();
                widths = longest.Select(l => Math.Max(MinimumColumnTwips, contentWidth * l / total)).ToList();
            }
            double sum = widths.Sum();
            // Wider than the text column: reduced proportionally, as the fixed grid would overflow the page
            if (sum > contentWidth && sum > 0) widths = widths.Select(w => w * contentWidth / sum).ToList();
            return widths;
        }

        static void Border(MigraDoc.DocumentObjectModel.Border border, XElement? definition)
        {
            string? kind = (string?)definition?.Attribute(W + "val");
            if (definition is null || kind is "none" or "nil") { border.Visible = false; return; }
            border.Visible = true;
            border.Width = Unit.FromPoint(((double?)definition.Attribute(W + "sz") ?? EighthsPerPoint) / EighthsPerPoint);
            if (ColorOf((string?)definition.Attribute(W + "color")) is Color color) border.Color = color;
        }

        void Format(Paragraph paragraph, ParaProps para, RunProps run)
        {
            var format = paragraph.Format;
            format.SpaceBefore = Pt(para.Before ?? 0); format.SpaceAfter = Pt(para.After ?? 0);
            if (para.KeepNext == true) format.KeepWithNext = true;
            if (para.PageBreakBefore == true) format.PageBreakBefore = true;
            if (para.Widow is bool widow) format.WidowControl = widow;
            if (para.Indent is double indent) format.LeftIndent = Pt(indent);
            format.Alignment = para.Align switch
            {
                "center" => ParagraphAlignment.Center, "right" or "end" => ParagraphAlignment.Right, "both" or "distribute" => ParagraphAlignment.Justify,
                _ => ParagraphAlignment.Left
            };
            Apply(format.Font, run);
        }

        static void Apply(FormattedText text, RunProps props) => Apply(text.Font, props);

        static void Apply(Font font, RunProps props)
        {
            font.Name = PdfFonts.Family(props.Font);
            if (props.Size is double size) font.Size = size;
            if (props.Bold is bool bold) font.Bold = bold;
            if (props.Italic is bool italic) font.Italic = italic;
            if (ColorOf(props.Color) is Color color) font.Color = color;
            if (props.Vertical == "superscript") font.Superscript = true;
            if (props.Vertical == "subscript") font.Subscript = true;
        }

        (ParaProps Para, RunProps Run) Style(string? id)
        {
            var chain = new List<(ParaProps, RunProps)>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            for (string? current = id ?? defaultStyle; current is not null && seen.Add(current) && styles.TryGetValue(current, out var style); current = style.BasedOn)
                chain.Add((style.Para, style.Run));
            var para = defaultPara; var run = defaultRun;
            for (int i = chain.Count - 1; i >= 0; i--) { para = para.Over(chain[i].Item1); run = run.Over(chain[i].Item2); }
            return (para, run);
        }

        void ReadStyles()
        {
            if (!docx.Contains("word/styles.xml")) return;
            var root = docx.GetXml("word/styles.xml").Root!;
            var defaults = root.Element(W + "docDefaults");
            defaultRun = defaultRun.Over(ReadRun(defaults?.Element(W + "rPrDefault")?.Element(W + "rPr")));
            defaultPara = defaultPara.Over(ReadPara(defaults?.Element(W + "pPrDefault")?.Element(W + "pPr")));
            foreach (var style in root.Elements(W + "style").Where(s => (string?)s.Attribute(W + "type") is null or "paragraph"))
            {
                string? id = (string?)style.Attribute(W + "styleId");
                if (id is null) continue;
                styles[id] = ((string?)style.Element(W + "basedOn")?.Attribute(W + "val"), ReadPara(style.Element(W + "pPr")) ?? new(), ReadRun(style.Element(W + "rPr")) ?? new());
                if ((string?)style.Attribute(W + "default") is "1" or "true" or "on") defaultStyle = id;
            }
            defaultStyle ??= styles.ContainsKey("Normal") ? "Normal" : null;
        }

        void ReadRelations(string part, Dictionary<string, string> target)
        {
            if (!docx.Contains(part)) return;
            foreach (var relation in docx.GetXml(part).Root!.Elements(Rel + "Relationship"))
                if ((string?)relation.Attribute("Id") is string id && (string?)relation.Attribute("Target") is string path) target[id] = path.TrimStart('/');
        }

        static ParaProps? ReadPara(XElement? pPr)
        {
            if (pPr is null) return null;
            var spacing = pPr.Element(W + "spacing"); var ind = pPr.Element(W + "ind");
            return new(Twips(spacing, "before"), Twips(spacing, "after"), Flag(pPr.Element(W + "keepNext")), Flag(pPr.Element(W + "pageBreakBefore")),
                (string?)pPr.Element(W + "jc")?.Attribute(W + "val"), Twips(ind, "left") ?? Twips(ind, "start"), Flag(pPr.Element(W + "widowControl")));
        }

        static RunProps? ReadRun(XElement? rPr)
        {
            if (rPr is null) return null;
            var fonts = rPr.Element(W + "rFonts");
            double? size = (double?)rPr.Element(W + "sz")?.Attribute(W + "val") / HalfPointsPerPoint;
            return new((string?)fonts?.Attribute(W + "ascii") ?? (string?)fonts?.Attribute(W + "hAnsi"), size, Flag(rPr.Element(W + "b")),
                Flag(rPr.Element(W + "i")), (string?)rPr.Element(W + "color")?.Attribute(W + "val"), (string?)rPr.Element(W + "vertAlign")?.Attribute(W + "val"));
        }

        // An on/off property: absent = not stated; present without value or with true/1/on = true
        static bool? Flag(XElement? element)
        {
            if (element is null) return null;
            string? value = (string?)element.Attribute(W + "val");
            return value is null || value is "1" or "true" or "on";
        }

        static double? Twips(XElement? element, string attribute) =>
            double.TryParse((string?)element?.Attribute(W + attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;

        static Unit Pt(double twips) => Unit.FromPoint(twips / TwipsPerPoint);

        static Color? ColorOf(string? hex)
        {
            const int Digits = 6, Hex = 16, Green = 2, Blue = 4;
            if (hex is null || hex.Length != Digits || !hex.All(Uri.IsHexDigit)) return null;
            return Color.FromRgb(Convert.ToByte(hex[..Green], Hex), Convert.ToByte(hex[Green..Blue], Hex), Convert.ToByte(hex[Blue..], Hex));
        }
    }
}
