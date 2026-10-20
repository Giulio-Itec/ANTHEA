using System.Text.RegularExpressions;
using System.Xml.Linq;
using Anthea.Reports;
using Anthea.Reports.Pdf;
using Anthea.Testing.Capture;

/// <summary>
/// PDF of the reports without Word (user decision of 14/10/2026): every report of the headless capture of the corpus is translated and
/// rendered (the project report is exercised by the WPF smoke tests of the full profile). The words of the PDF document equal, in order, the words of the DOCX body (paragraphs and
/// table cells, with the Word rules for spaces); the short reports stay within two pages; ReportFiles writes both files or neither.
/// </summary>
internal static class PdfChecks
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal static int Run(string root, string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        string capture = Path.Combine(output, "pdf-capture"), pdfs = Path.Combine(output, "pdf");
        foreach (var folder in new[] { capture, pdfs }) if (Directory.Exists(folder)) Directory.Delete(folder, true);
        new CaptureRunner(new CaptureOptions(root, capture, "pdf", null, null, false, "it-IT")).Run();
        var files = Directory.GetFiles(Path.Combine(capture, "raw", "docx"), "*.docx", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        Check(files.Length >= 20, "Pochi report nella cattura: " + files.Length);
        var lines = new List<string>();
        void Compare(string name, DocxDocument docx)
        {
            var expected = Words(docx); var actual = DocxPdfText.Lines(DocxPdf.Translate(docx, name)).SelectMany(Split).ToArray();
            int mismatch = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
            Check(expected.Length == actual.Length && mismatch < 0, $"{name}: testo del PDF diverso dal DOCX ({expected.Length} contro {actual.Length} parole; prima differenza alla parola {mismatch}: " +
                $"«{(mismatch >= 0 ? expected[mismatch] : "")}» / «{(mismatch >= 0 ? actual[mismatch] : "")}»)");
            var pdf = DocxPdf.Render(docx, name);
            Check(pdf.Pages > 0 && pdf.Bytes.Length > 0 && System.Text.Encoding.ASCII.GetString(pdf.Bytes, 0, 5) == "%PDF-", name + ": PDF non valido");
            string target = Path.Combine(pdfs, name + ".pdf"); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, pdf.Bytes);
            lines.Add($"{name}\t{expected.Length} parole\t{pdf.Pages} pagine");
            if (name.EndsWith(".sintetico", StringComparison.Ordinal)) Check(pdf.Pages <= ReportFiles.ShortReportPages, name + ": report sintetico oltre due pagine (" + pdf.Pages + ")");
        }
        foreach (string file in files)
            Compare(Path.ChangeExtension(Path.GetRelativePath(Path.Combine(capture, "raw", "docx"), file), null)!.Replace('\\', '/'), DocxWriter.Read(File.ReadAllBytes(file)));
        checks += ConcreteStadiumReportChecks.Run(output, Compare);
        File.WriteAllLines(Path.Combine(output, "pdf-report.tsv"), lines);
        // Both files or neither: a page limit exceeded writes nothing
        string folderOut = Path.Combine(output, "pdf-files"); if (Directory.Exists(folderOut)) Directory.Delete(folderOut, true); Directory.CreateDirectory(folderOut);
        var longest = files.OrderByDescending(f => new FileInfo(f).Length).First(); var bytes = File.ReadAllBytes(longest);
        string docxPath = Path.Combine(folderOut, "relazione.docx");
        var written = ReportFiles.Write(docxPath, bytes, "Prova");
        Check(File.ReadAllBytes(docxPath).SequenceEqual(bytes) && File.Exists(ReportFiles.PdfPath(docxPath)) && written.Pages > 1, "DOCX e PDF non scritti insieme");
        string refused = Path.Combine(folderOut, "rifiutato.docx");
        bool failed = false;
        try { ReportFiles.Write(refused, bytes, "Prova", 1); } catch (InvalidOperationException) { failed = true; }
        Check(failed && !File.Exists(refused) && !File.Exists(ReportFiles.PdfPath(refused)), "Limite di pagine superato ma file scritti");
        Console.WriteLine($"PDF: {files.Length} report convertiti, testo identico al DOCX; elenco in pdf-report.tsv");
        return checks;
    }

    // Words of the DOCX body as Word shows them: text nodes without xml:space="preserve" lose leading and trailing spaces, line feeds are spaces
    static string[] Words(DocxDocument docx)
    {
        var body = docx.GetXml("word/document.xml").Root!.Element(W + "body")!;
        var text = new System.Text.StringBuilder();
        foreach (var node in body.Descendants())
        {
            if (node.Name == W + "t")
            {
                string value = node.Value.Replace('\r', ' ').Replace('\n', ' ');
                text.Append((string?)node.Attribute(XNamespace.Xml + "space") == "preserve" ? value : value.Trim());
            }
            else if (node.Name == W + "p" || node.Name == W + "tc" || node.Name == W + "br" || node.Name == W + "tab") text.Append(' ');
        }
        return Split(text.ToString()).ToArray();
    }

    static IEnumerable<string> Split(string text) => Regex.Split(text.Replace(' ', ' '), @"\s+").Where(w => w.Length > 0);
}
