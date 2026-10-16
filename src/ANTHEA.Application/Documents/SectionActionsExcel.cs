using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace X.Core;

/// <summary>
/// Strict, values-only XLSX interchange. Does not execute formulas, macros or external links.
/// Il formato storico a otto colonne si legge ancora (<see cref="Read"/>, <see cref="Import.Legacy"/>); il suo modello e la sua scrittura
/// servono solo alle prove e stanno in tests/Shared/LegacySectionActionsExcel.cs (decisione D3 del 15/10/2026).
/// </summary>
public static class SectionActionsExcel
{
    public static readonly System.Collections.Immutable.ImmutableArray<string> Headers = ["Famiglia", "Nome", "N [kN]", "Mx [kNm]", "My [kNm]", "Vx [kN]", "Vy [kN]", "T [kNm]"];
    public static readonly System.Collections.Immutable.ImmutableArray<string> UnifiedHeaders = ["Elemento", "Nome combo", "Stato limite", "N [kN]", "Vx [kN]", "Vy [kN]", "T [kNm]", "Mx [kNm]", "My [kNm]"];
    public sealed record Row(string Family, string Name, double N, double? Mx, double? My, double? Vx, double? Vy, double? T = null, string Element = "");
    public sealed record Import(IReadOnlyList<Row> Rows, int FormulaCells, bool Legacy = false);
    public static byte[] Template()
    {
        using var stream = typeof(SectionActionsExcel).Assembly.GetManifestResourceStream("ANTHEA.Sollecitazioni.xlsx") ?? throw new InvalidOperationException("Template Excel non disponibile.");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
    }
    public static byte[] Write(IEnumerable<Row> source)
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
                string family = Family(item.Family);
                if (family == "Taglio" || item.Mx is null || item.My is null || item.Vx is null || item.Vy is null || item.T is null) throw new ArgumentException("Indicare lo stato limite e tutte le sei componenti, anche quando nulle (0).");
                string label = family switch { "SLE" => "Rara", "SLE_FREQ" => "Frequente", "SLE_QP" => "Quasi permanente", _ => family };
                object?[] values = [item.Element, item.Name, label, item.N, item.Vx, item.Vy, item.T, item.Mx, item.My];
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
            string last = "I";
            var headers = UnifiedHeaders;
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
            foreach (var validation in document.Descendants(ns + "dataValidation")) validation.SetAttributeValue("sqref", "C7:C" + Math.Max(10006, end));
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
        var bytes = buffer.ToArray(); _ = Read(bytes); return bytes;
    }
    public static string Family(string value) => value.Trim().ToUpperInvariant() switch
    {
        "SLU" => "SLU", "SLV" => "SLV", "RARA" or "SLE" => "SLE", "FREQUENTE" or "SLE_FREQ" => "SLE_FREQ",
        "QUASI PERMANENTE" or "SLE_QP" => "SLE_QP", "TAGLIO" => "Taglio",
        _ => throw new ArgumentException("Famiglia non riconosciuta: " + value)
    };
    public static Import Read(byte[] bytes)
    {
        var sheet = Anthea.Serialization.WorkbookCells.Read(bytes, "Azioni");
        var rows = new List<Row>(); bool headerFound = false, torqueHeader = false, unified = false; int visited = 0;
        foreach (var row in sheet.Rows)
        {
            if (++visited > 10050) throw new ArgumentException("Massimo 10.000 combinazioni per importazione.");
            var cells = row.Cells;
            if (!headerFound)
            {
                unified = Enumerable.Range(1, 9).All(i => string.Equals(cells.GetValueOrDefault(i)?.Trim(), UnifiedHeaders[i - 1], StringComparison.OrdinalIgnoreCase));
                if (unified) { headerFound = true; continue; }
                headerFound = Enumerable.Range(1, 7).All(i => string.Equals(cells.GetValueOrDefault(i)?.Trim(), Headers[i - 1], StringComparison.OrdinalIgnoreCase));
                if(headerFound)
                {
                    torqueHeader=string.Equals(cells.GetValueOrDefault(8)?.Trim(),Headers[7],StringComparison.OrdinalIgnoreCase);
                    if(!torqueHeader&&!string.IsNullOrWhiteSpace(cells.GetValueOrDefault(8)))throw new ArgumentException("Intestazione H non riconosciuta: attesa T [kNm].");
                }
                if (!headerFound && visited > 50) throw new ArgumentException("Intestazioni non riconosciute: usare il template ANTHEA senza modificare le colonne.");
                continue;
            }
            if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
            string context = "Riga Excel " + row.Number;
            try
            {
                if (unified)
                {
                    if (cells.Any(c => c.Key > 9 && !string.IsNullOrWhiteSpace(c.Value))) throw new ArgumentException("Dati oltre la colonna I.");
                    string state = Family(cells.GetValueOrDefault(3) ?? "");
                    if (state == "Taglio") throw new ArgumentException("Taglio non è uno stato limite: scegliere SLU o SLV.");
                    string combo = cells.GetValueOrDefault(2)?.Trim() ?? "";
                    if (combo.Length == 0) throw new ArgumentException("Nome combinazione mancante.");
                    double Value(int c) => SectionWorkspace.Number(cells.GetValueOrDefault(c) ?? "", UnifiedHeaders[c - 1]);
                    rows.Add(new(state, combo, Value(4), Value(8), Value(9), Value(5), Value(6), Value(7), cells.GetValueOrDefault(1)?.Trim() ?? ""));
                    continue;
                }
                if (cells.Any(c => c.Key > 8 && !string.IsNullOrWhiteSpace(c.Value))) throw new ArgumentException("Dati oltre la colonna H: importazione ambigua.");
                if(!torqueHeader&&!string.IsNullOrWhiteSpace(cells.GetValueOrDefault(8)))throw new ArgumentException("Torsione senza intestazione T [kNm].");
                string family = Family(cells.GetValueOrDefault(1) ?? ""), name = cells.GetValueOrDefault(2)?.Trim() ?? "";
                if (name.Length == 0) throw new ArgumentException("Nome combinazione mancante.");
                double Number(int i) => SectionWorkspace.Number(cells.GetValueOrDefault(i) ?? "", Headers[i - 1]);
                double? Optional(int i) => string.IsNullOrWhiteSpace(cells.GetValueOrDefault(i)) ? null : Number(i);
                var item = new Row(family, name, Number(3), family == "Taglio" ? Optional(4) : Number(4), family == "Taglio" ? Optional(5) : Number(5), family == "Taglio" ? Number(6) : Optional(6), family == "Taglio" ? Number(7) : Optional(7), Optional(8));
                if(family!="Taglio"&&item.T is not(null or 0))throw new ArgumentException("Torsione ammessa solo nella famiglia Taglio.");
                if (family != "Taglio" && (item.Vx is not (null or 0) || item.Vy is not (null or 0)))
                    throw new ArgumentException("Sollecitazioni non utilizzate dalla famiglia: separare le righe di Taglio da quelle di pressoflessione/SLE.");
                rows.Add(item);
            }
            catch (ArgumentException ex) { throw new ArgumentException(context + ": " + ex.Message, ex); }
        }
        if (!headerFound) throw new ArgumentException("Intestazioni non riconosciute: usare il template ANTHEA.");
        return new(rows, sheet.FormulaCells, !unified);
    }
}
