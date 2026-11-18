using Anthea.Reports.Pdf;

namespace Anthea.Reports;

/// <summary>
/// A report is saved as DOCX and PDF with the same name, both from the same generated package (user decision of 14/10/2026). The PDF
/// is rendered first: if it fails, or exceeds the page limit, neither file is written.
/// </summary>
public static class ReportFiles
{
    /// <summary>Pages of the short report.</summary>
    public const int ShortReportPages = 2;

    public static string PdfPath(string docxPath) => Path.ChangeExtension(docxPath, ".pdf");

    /// <summary>Save only the Word document; no PDF renderer is invoked.</summary>
    public static void WriteWord(string docxPath, byte[] docx) => DocumentFile.Write(docxPath, docx);

    public static PdfOutput Write(string docxPath, byte[] docx, string title = "", int? maximumPages = null, ReportStyleTemplate? template = null)
    {
        ArgumentNullException.ThrowIfNull(docx);
        var document = DocxWriter.Read(docx);
        if (template is not null && !document.Contains("anthea/layout.xml")) { document = template.Apply(document); docx = DocxWriter.Write(document); }
        var pdf = DocxPdf.Render(document, title);
        if (maximumPages is int maximum && pdf.Pages > maximum)
            throw new InvalidOperationException($"Il contenuto richiede {pdf.Pages} pagine (ad esempio per armature libere o nomi molto lunghi). Nessun dato è stato tagliato e nessun report short è stato salvato. Usare il report completo oppure abbreviare titolo e nomi delle combinazioni.");
        DocumentFile.Write(docxPath, docx);
        DocumentFile.Write(PdfPath(docxPath), pdf.Bytes);
        return pdf;
    }
}
