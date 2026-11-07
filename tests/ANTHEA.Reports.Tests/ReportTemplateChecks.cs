using System.IO.Compression;
using System.Xml.Linq;
using Anthea.Reports;
using Anthea.Reports.Pdf;

internal static class ReportTemplateChecks
{
    internal static int Run(string root, string output)
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        void Reject(Action action) { try { action(); } catch (Exception e) when (e is InvalidDataException or ArgumentException or System.Xml.XmlException) { checks++; return; } throw new Exception("Template non valido accettato"); }
        string directory = Path.Combine(output, "templates"); Directory.CreateDirectory(directory);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        string path = Path.Combine(directory, "prova.docx");
        var reference = new DocxDocument();
        reference.SetXml("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        reference.SetXml("word/document.xml", $"<w:document xmlns:w=\"{w}\"><w:body><w:p><w:r><w:t>CARTIGLIO DA ESCLUDERE</w:t></w:r></w:p><w:sectPr><w:pgSz w:w=\"4000\" w:h=\"6000\"/></w:sectPr></w:body></w:document>");
        reference.SetXml("word/header1.xml", $"<w:hdr xmlns:w=\"{w}\"><w:p><w:r><w:t>INTESTAZIONE PRIVATA</w:t></w:r></w:p></w:hdr>");
        reference.SetXml("word/styles.xml", $"""
            <w:styles xmlns:w="{w}">
              <w:docDefaults><w:rPrDefault><w:rPr><w:sz w:val="22"/><w:rFonts w:asciiTheme="minorHAnsi"/></w:rPr></w:rPrDefault></w:docDefaults>
              <w:style w:type="paragraph" w:default="1" w:styleId="Normale"><w:name w:val="Normal"/><w:pPr><w:spacing w:after="40" w:line="240"/></w:pPr><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/></w:rPr></w:style>
              <w:style w:type="paragraph" w:styleId="Titolo1"><w:name w:val="heading 1"/><w:basedOn w:val="Normale"/><w:pPr><w:spacing w:before="120"/><w:numPr><w:numId w:val="77"/></w:numPr></w:pPr><w:rPr><w:b/><w:sz w:val="28"/></w:rPr></w:style>
            </w:styles>
            """);
        File.WriteAllBytes(path, DocxWriter.Write(reference));
        byte[] original = File.ReadAllBytes(path);
        var template = ReportStyleTemplate.Read(path);
        var input = DocxWriter.Read(original);
        input.SetXml("word/document.xml", $"""
            <w:document xmlns:w="{w}"><w:body>
             <w:p><w:r><w:rPr><w:rFonts w:ascii="Calibri"/><w:sz w:val="46"/><w:b/></w:rPr><w:t>Titolo del report</w:t></w:r></w:p>
             <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Risultati</w:t></w:r></w:p>
             <w:p><w:r><w:t>N = −123,456 kN; μφ = 3,25; σ = 12 MPa</w:t></w:r></w:p>
             <w:tbl><w:tblPr><w:tblW w:w="4000" w:type="dxa"/></w:tblPr><w:tblGrid><w:gridCol w:w="4000"/></w:tblGrid><w:tr><w:trPr><w:tblHeader/></w:trPr><w:tc><w:p><w:r><w:t>Tabella</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
             <w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:bottom="1134" w:left="1273" w:right="1273"/></w:sectPr>
            </w:body></w:document>
            """);
        // A real generated document does not carry any of the reference's furniture.
        var report = new DocxDocument();
        report.SetXml("[Content_Types].xml", reference.GetXml("[Content_Types].xml").ToString());
        report.SetXml("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        report.SetXml("word/document.xml", input.GetXml("word/document.xml").ToString());
        string before = report.GetXml("word/document.xml").ToString();
        var applied = template.Apply(report);
        var document = applied.GetXml("word/document.xml");
        Check(document.Descendants(w + "t").Select(t => t.Value).SequenceEqual(report.GetXml("word/document.xml").Descendants(w + "t").Select(t => t.Value)), "Template altera numeri o testo");
        Check(report.GetXml("word/document.xml").ToString() == before && File.ReadAllBytes(path).SequenceEqual(original), "Template modifica una sorgente");
        Check(!applied.Contains("word/header1.xml") && !document.ToString().Contains("CARTIGLIO"), "Contenuti prestampati importati");
        Check(XNode.DeepEquals(document.Descendants(w + "sectPr").Single(), report.GetXml("word/document.xml").Descendants(w + "sectPr").Single()), "Template cambia geometria della pagina");
        var heading = applied.GetXml("word/styles.xml").Descendants(w + "style").Single(s => (string?)s.Attribute(w + "styleId") == "Heading1");
        Check((string?)heading.Descendants(w + "spacing").Single().Attribute(w + "after") == "40" && (string?)heading.Descendants(w + "spacing").Single().Attribute(w + "before") == "120", "Ereditarietà delle proprietà parziali persa");
        Check(!heading.Descendants(w + "numPr").Any() && (string?)heading.Descendants(w + "rFonts").Single().Attribute(w + "ascii") == "Arial", "Numerazione importata o font esplicito ignorato");
        Check(document.Descendants(w + "pStyle").Select(s => (string?)s.Attribute(w + "val")).SequenceEqual(new[] { "Title", "Heading1", "Normal", "TableHeader" }), "Ruoli del report non associati");
        Check(XNode.DeepEquals(template.Apply(applied).GetXml("word/document.xml"), document), "Applicazione ripetuta non idempotente");
        string settings = Path.Combine(directory, "preference.json");
        var store = new ReportTemplateStore(settings); store.Select(path);
        File.Delete(path);
        Check(new ReportTemplateStore(settings).Current?.Name == "prova.docx", "La preferenza dipende dal file di rete originale");
        string bad = Path.Combine(directory, "non-word.docx"); File.WriteAllText(bad, "non è uno zip");
        Reject(() => store.Select(bad)); Check(store.Name == "prova.docx", "Selezione invalida perde la preferenza precedente");
        store.Reset(); Check(new ReportTemplateStore(settings).Current is null, "Ripristino non persistito");
        File.WriteAllText(settings, "corrotto");
        var broken = new ReportTemplateStore(settings);
        Check(broken.Error is not null, "Preferenza corrotta ignorata");
        broken.Reset(); Check(broken.Current is null, "Impossibile ripristinare una preferenza corrotta");
        File.WriteAllText(settings, "{\"Name\":\"incompleto\"}");
        Check(new ReportTemplateStore(settings).Error is not null, "Preferenza incompleta non gestita");
        string outputPath = Path.Combine(directory, "report-stili.docx");
        ReportFiles.Write(outputPath, DocxWriter.Write(report), "Report di prova", template: template);
        Check(File.Exists(Path.ChangeExtension(outputPath, ".pdf")), "PDF del template assente");
        Check(DocxWriter.Read(File.ReadAllBytes(outputPath)).Contains("word/styles.xml"), "Esportazione ignora il template");
        var translated = DocxPdf.Translate(applied);
        Check(translated.Sections[0].Elements.OfType<MigraDoc.DocumentObjectModel.Paragraph>().First().Format.Font.Name == "Arial", "PDF ignora il carattere del template");
        var model = new MigraDoc.DocumentObjectModel.Document();
        ReportTemplateTypography.Apply(template, model);
        Check(model.Styles["Normal"]!.Font.Name == "Arial" && model.Styles["Heading1"]!.Font.Size.Point == 14, "Stili del PDF Modelli diversi dal template");
        Check(ReportTemplateTypography.Css(template).Contains("font-family:\"Arial\"") && ReportTemplateTypography.Css(template).Contains("font-size:14pt"), "Stili HTML Modelli diversi dal template");

        // Optional real user reference: never version or require the user's company document on another machine.
        string itec = Path.Combine(root, "supporto/artefatti/report-template/reference/MODELLO-RELAZIONE-ITEC-AA.docx");
        if (File.Exists(itec))
        {
            var real = ReportStyleTemplate.Read(itec);
            Check(PdfFonts.Family("Manrope") == "Manrope", "Manrope installato non risolto nel PDF");
            foreach (string file in Directory.GetFiles(Path.Combine(output, "pdf-capture", "raw", "docx"), "*.docx", SearchOption.AllDirectories))
            {
                string target = Path.Combine(directory, "ITEC", Path.GetRelativePath(Path.Combine(output, "pdf-capture", "raw", "docx"), file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var raw = DocxWriter.Read(File.ReadAllBytes(file)); var styled = real.Apply(raw);
                Check(raw.GetXml("word/document.xml").Descendants(w + "t").Select(t => t.Value).SequenceEqual(styled.GetXml("word/document.xml").Descendants(w + "t").Select(t => t.Value)), "ITEC modifica contenuti: " + file);
                ReportFiles.Write(target, File.ReadAllBytes(file), Path.GetFileNameWithoutExtension(file), template: real);
            }
        }
        Console.WriteLine($"Template di stili: {checks} controlli");
        return checks;
    }
}
