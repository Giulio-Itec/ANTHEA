// Spostato da src/ANTHEA.Reports/Pdf/DocxPdf.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: il testo del documento MigraDoc
// serve solo alla prova PdfChecks (testo del PDF uguale al DOCX). Codice conservato per le prove, fuori dall'installer.
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Fields;
using MigraDoc.DocumentObjectModel.Tables;

/// <summary>Testo di un documento di <see cref="Anthea.Reports.Pdf.DocxPdf.Translate"/> nell'ordine di lettura.</summary>
internal static class DocxPdfText
{
    /// <summary>Text of the document in reading order: one line per paragraph, one per table cell, footers excluded.</summary>
    internal static IEnumerable<string> Lines(Document document)
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
}
