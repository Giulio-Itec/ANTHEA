using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Anthea.Reports.Pdf;
using MigraDoc.DocumentObjectModel;

namespace Anthea.Reports;

/// <summary>The same normalized styles for the model module's native PDF and HTML reports.</summary>
public static class ReportTemplateTypography
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static bool Flag(ReportStyleTemplate template, string role, string name)
    {
        var value = XElement.Parse(template.StylesXml).Elements(W + "style").Single(s => (string?)s.Attribute(W + "styleId") == role)
            .Element(W + "rPr")?.Element(W + name);
        return value is not null && (string?)value.Attribute(W + "val") is not ("0" or "false" or "off");
    }
    private static ParagraphFormat Format(ReportStyleTemplate template, string role)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var probe = new DocxDocument();
        probe.SetXml("word/styles.xml", template.StylesXml);
        probe.SetXml("word/document.xml", new XElement(w + "document", new XElement(w + "body", new XElement(w + "p",
            new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", role))),
            new XElement(w + "r", new XElement(w + "t", "Stile"))))).ToString());
        return DocxPdf.Translate(probe).Sections[0].Elements.OfType<Paragraph>().Single().Format.Clone();
    }

    public static void Apply(ReportStyleTemplate template, Document document)
    {
        foreach (string role in new[] { StyleNames.Normal, StyleNames.Heading1 })
        {
            var format = Format(template, role);
            document.Styles[role]!.ParagraphFormat = format;
            document.Styles[role]!.Font = format.Font.Clone();
        }
        // MigraDoc has no all-caps font flag. Apply the display case to this output document, never to the saved recipe.
        foreach (var paragraph in document.Sections.Cast<Section>().SelectMany(s => s.Elements.OfType<Paragraph>()))
        {
            string role = paragraph.Style == StyleNames.Heading1 ? StyleNames.Heading1 : StyleNames.Normal;
            if (Flag(template, role, "caps"))
                foreach (var text in paragraph.Elements.OfType<Text>()) text.Content = text.Content.ToUpperInvariant();
        }
    }

    public static string Css(ReportStyleTemplate template)
    {
        string Rule(string selector, string role)
        {
            var format = Format(template, role);
            string font = JsonSerializer.Serialize(format.Font.Name);
            string size = format.Font.Size.Point.ToString("0.###", CultureInfo.InvariantCulture);
            var color = format.Font.Color;
            string rgb = color.IsEmpty ? "000000" : $"{color.R:X2}{color.G:X2}{color.B:X2}";
            string line = format.LineSpacingRule == LineSpacingRule.Multiple
                ? format.LineSpacing.Point.ToString("0.###", CultureInfo.InvariantCulture)
                : format.LineSpacing.Point.ToString("0.###", CultureInfo.InvariantCulture) + "pt";
            if (format.LineSpacing.Point <= 0) line = "normal";
            return selector + "{font-family:" + font + ";font-size:" + size + "pt;font-weight:" + (format.Font.Bold ? "bold" : "normal")
                + ";font-style:" + (format.Font.Italic ? "italic" : "normal") + ";color:#" + rgb
                + ";text-align:" + format.Alignment.ToString().ToLowerInvariant() + ";line-height:" + line
                + ";text-transform:" + (Flag(template, role, "caps") ? "uppercase" : "none")
                + ";font-variant:" + (Flag(template, role, "smallCaps") ? "small-caps" : "normal") + "}";
        }
        return Rule("body", StyleNames.Normal) + Rule("h1", StyleNames.Heading1);
    }
}
