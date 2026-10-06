using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Anthea.Testing.Normalization;

/// <summary>
/// Text of a DOCX for the comparisons: one line per paragraph ("¶ text") and one per table cell ("[T1 r2 c3] text"), from the w:t runs of
/// the document, headers, footers and notes. Package properties (docProps), styles, layout and images are excluded. Every line goes
/// through <see cref="TextNormalizer"/> (GUIDs, dates, times, durations, absolute paths).
/// </summary>
public static class DocxText
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    const string Break = " ⏎ ";

    public static string Extract(byte[] docx, TextNormalizer normalizer, GuidMap? guids = null)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        var parts = zip.Entries.Select(e => e.FullName)
            .Where(n => n == "word/document.xml" || System.Text.RegularExpressions.Regex.IsMatch(n, @"^word/(header|footer)\d*\.xml$") || n is "word/footnotes.xml" or "word/endnotes.xml")
            .OrderBy(n => n == "word/document.xml" ? 0 : 1).ThenBy(n => n, StringComparer.Ordinal).ToArray();
        var output = new StringBuilder();
        foreach (var name in parts)
        {
            XDocument xml;
            using (var stream = zip.GetEntry(name)!.Open()) xml = XDocument.Load(stream);
            output.Append("# ").Append(name).Append('\n');
            var root = xml.Root?.Element(W + "body") ?? xml.Root;
            if (root is null) continue;
            int tables = 0;
            Block(root, output, normalizer, guids, ref tables, "");
        }
        return output.ToString();
    }

    static void Block(XElement container, StringBuilder output, TextNormalizer normalizer, GuidMap? guids, ref int tables, string prefix)
    {
        foreach (var element in container.Elements())
        {
            if (element.Name == W + "p")
            {
                string text = Paragraph(element);
                if (text.Trim().Length > 0) output.Append(prefix.Length == 0 ? "¶ " : prefix + " ¶ ").Append(normalizer.Normalize(text, guids)).Append('\n');
            }
            else if (element.Name == W + "tbl")
            {
                int index = ++tables;
                int row = 0;
                foreach (var tr in element.Elements(W + "tr"))
                {
                    row++; int column = 0;
                    foreach (var tc in tr.Elements(W + "tc"))
                    {
                        column++;
                        string label = $"{prefix}[T{index} r{row} c{column}]";
                        var nested = tc.Elements(W + "tbl").Any();
                        string text = string.Join(Break, tc.Elements(W + "p").Select(Paragraph).Where(t => t.Trim().Length > 0));
                        output.Append(label).Append(text.Length > 0 ? " " + normalizer.Normalize(text, guids) : "").Append('\n');
                        if (nested)
                        {
                            int inner = 0;
                            foreach (var table in tc.Elements(W + "tbl")) { var holder = new XElement(W + "body", table); Block(holder, output, normalizer, guids, ref inner, label); }
                        }
                    }
                }
            }
            else if (element.Name == W + "sdt")
            {
                var content = element.Element(W + "sdtContent");
                if (content is not null) Block(content, output, normalizer, guids, ref tables, prefix);
            }
        }
    }

    static string Paragraph(XElement paragraph)
    {
        var text = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            if (node.Name == W + "t") text.Append(node.Value);
            else if (node.Name == W + "tab") text.Append('\t');
            else if (node.Name == W + "br" || node.Name == W + "cr") text.Append(Break);
            else if (node.Name == W + "noBreakHyphen") text.Append('-');
        }
        return text.ToString();
    }
}
