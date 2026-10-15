// Spostato da src/ANTHEA.Application/Documents/SectionActionsExcel.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: il prodotto
// scrive solo il formato unificato; il modello storico a otto colonne e la sua scrittura (Template(legacy: true), Write(…, unified: false))
// servono solo a preparare file vecchi per le prove della lettura. Codice storico conservato per le prove, fuori dall'installer.
// Il modello è la fixture ANTHEA_Sollecitazioni_legacy.xlsx, incorporata con lo stesso nome logico da ogni progetto che collega questo file.
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Scrittura del formato storico delle azioni (famiglia, nome, N, Mx, My, Vx, Vy, T), riletto da <see cref="SectionActionsExcel.Read"/>.</summary>
internal static class LegacySectionActionsExcel
{
    /// <summary>Modello storico vuoto (già <c>SectionActionsExcel.Template(legacy: true)</c>).</summary>
    public static byte[] Template()
    {
        using var stream = typeof(LegacySectionActionsExcel).Assembly.GetManifestResourceStream("ANTHEA.Sollecitazioni.Legacy.xlsx") ?? throw new InvalidOperationException("Template Excel non disponibile.");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
    }

    /// <summary>Ramo storico di <c>SectionActionsExcel.Write(rows, unified: false)</c>, con le espressioni del formato unificato tolte.</summary>
    public static byte[] Write(IEnumerable<SectionActionsExcel.Row> source)
    {
        var rows = source.ToArray();
        if (rows.Length > 10000) throw new ArgumentException("Massimo 10.000 combinazioni per esportazione.");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var buffer = new MemoryStream(); buffer.Write(Template());
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument document;
            using (var stream = entry.Open()) document = XDocument.Load(stream);
            var body = document.Root!.Element(ns + "sheetData")!;
            var styles = body.Elements(ns + "row").FirstOrDefault(r => (int?)r.Attribute("r") == 7)?.Elements(ns + "c").Select(c => (string?)c.Attribute("s")).ToArray() ?? [];
            body.Elements(ns + "row").Where(r => (int?)r.Attribute("r") >= 7).Remove();
            for (int i = 0; i < rows.Length; i++)
            {
                var item = rows[i]; var row = new XElement(ns + "row", new XAttribute("r", i + 7));
                string family = SectionActionsExcel.Family(item.Family);
                string label = family switch { "SLE" => "Rara", "SLE_FREQ" => "Frequente", "SLE_QP" => "Quasi permanente", _ => family };
                object?[] values = [label, item.Name, item.N, item.Mx, item.My, item.Vx, item.Vy, item.T];
                for (int c = 0; c < values.Length; c++)
                {
                    var cell = new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + c)}{i + 7}"));
                    if (styles.ElementAtOrDefault(c) is string style) cell.SetAttributeValue("s", style);
                    if (values[c] is string text) { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text))); }
                    else if (values[c] is double value) { if (!double.IsFinite(value)) throw new ArgumentException("Sollecitazione non finita."); cell.Add(new XElement(ns + "v", value.ToString("G17", CultureInfo.InvariantCulture))); }
                    row.Add(cell);
                }
                body.Add(row);
            }
            int end = Math.Max(7, rows.Length + 6);
            string last = "H";
            var headers = SectionActionsExcel.Headers;
            var header = body.Elements(ns + "row").First(r => (int?)r.Attribute("r") == 6);
            var headerStyle = header.Elements(ns + "c").FirstOrDefault()?.Attribute("s")?.Value;
            header.RemoveNodes();
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + c)}6"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", headers[c])));
                if (headerStyle is not null) cell.SetAttributeValue("s", headerStyle);
                header.Add(cell);
            }
            document.Root.Element(ns + "dimension")?.SetAttributeValue("ref", "A1:" + last + end);
            document.Root.Element(ns + "autoFilter")?.SetAttributeValue("ref", "A6:" + last + end);
            foreach (var validation in document.Descendants(ns + "dataValidation")) validation.SetAttributeValue("sqref", "A7:A" + Math.Max(10006, end));
            entry.Delete(); using (var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open()) document.Save(stream);
            foreach (var tableEntry in zip.Entries.Where(e => e.FullName.StartsWith("xl/tables/") && e.FullName.EndsWith(".xml")).ToArray())
            {
                XDocument table; using (var stream = tableEntry.Open()) table = XDocument.Load(stream);
                table.Root!.SetAttributeValue("ref", "A6:" + last + end); table.Root.Element(ns + "autoFilter")?.SetAttributeValue("ref", "A6:" + last + end);
                if(table.Root.Element(ns+"tableColumns") is {} columns)
                {
                    columns.RemoveNodes();
                    for (int c = 0; c < headers.Length; c++) columns.Add(new XElement(ns + "tableColumn", new XAttribute("id", c + 1), new XAttribute("name", headers[c])));
                    columns.SetAttributeValue("count", headers.Length);
                }
                string name = tableEntry.FullName; tableEntry.Delete(); using var streamOut = zip.CreateEntry(name).Open(); table.Save(streamOut);
            }
        }
        var bytes = buffer.ToArray(); _ = SectionActionsExcel.Read(bytes); return bytes;
    }
}
