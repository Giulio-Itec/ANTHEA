using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Strict, values-only XLSX interchange. Does not execute formulas, macros or external links.</summary>
public static class SectionActionsExcel
{
    public static readonly string[] Headers = ["Famiglia", "Nome", "N [kN]", "Mx [kNm]", "My [kNm]", "Vx [kN]", "Vy [kN]", "T [kNm]"];
    public sealed record Row(string Family, string Name, double N, double? Mx, double? My, double? Vx, double? Vy, double? T = null);
    public sealed record Import(IReadOnlyList<Row> Rows, int FormulaCells);
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
                object?[] values = [family switch { "SLE" => "Rara", "SLE_FREQ" => "Frequente", "SLE_QP" => "Quasi permanente", _ => family }, item.Name, item.N, item.Mx, item.My, item.Vx, item.Vy, item.T];
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
            var header=body.Elements(ns+"row").First(r=>(int?)r.Attribute("r")==6);
            header.Elements(ns+"c").Where(c=>(string?)c.Attribute("r")=="H6").Remove();
            header.Add(new XElement(ns+"c",new XAttribute("r","H6"),new XAttribute("t","inlineStr"),new XElement(ns+"is",new XElement(ns+"t",Headers[7]))));
            document.Root.Element(ns + "dimension")?.SetAttributeValue("ref", "A1:H" + end);
            document.Root.Element(ns + "autoFilter")?.SetAttributeValue("ref", "A6:H" + end);
            foreach (var validation in document.Descendants(ns + "dataValidation")) validation.SetAttributeValue("sqref", "A7:A" + Math.Max(10006, end));
            entry.Delete(); using (var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open()) document.Save(stream);
            foreach (var tableEntry in zip.Entries.Where(e => e.FullName.StartsWith("xl/tables/") && e.FullName.EndsWith(".xml")).ToArray())
            {
                XDocument table; using (var stream = tableEntry.Open()) table = XDocument.Load(stream);
                table.Root!.SetAttributeValue("ref", "A6:H" + end); table.Root.Element(ns + "autoFilter")?.SetAttributeValue("ref", "A6:H" + end);
                if(table.Root.Element(ns+"tableColumns") is {} columns)
                {
                    columns.Elements(ns+"tableColumn").Where(c=>(int?)c.Attribute("id")==8).Remove();
                    columns.Add(new XElement(ns+"tableColumn",new XAttribute("id",8),new XAttribute("name",Headers[7])));columns.SetAttributeValue("count",8);
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
        if (bytes.Length > 20_000_000) throw new ArgumentException("File Excel troppo grande (massimo 20 MB).");
        using var stream = new MemoryStream(bytes, false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 80_000_000) throw new ArgumentException("Cartella Excel troppo grande o non valida.");
        XDocument Xml(string name)
        {
            var entry = zip.GetEntry(name) ?? throw new ArgumentException("Componente Excel mancante: " + name);
            using var source = entry.Open();
            using var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 40_000_000 });
            return XDocument.Load(reader);
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var workbook = Xml("xl/workbook.xml");
        var sheet = workbook.Descendants(ns + "sheet").FirstOrDefault(s => string.Equals((string?)s.Attribute("name"), "Azioni", StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Manca il foglio Azioni. Usare il template ANTHEA.");
        string? id = (string?)sheet.Attribute(rel + "id");
        var relation = Xml("xl/_rels/workbook.xml.rels").Root?.Elements().FirstOrDefault(r => (string?)r.Attribute("Id") == id);
        if (relation is null || (string?)relation.Attribute("TargetMode") == "External") throw new ArgumentException("Riferimento al foglio non valido.");
        string target = (string?)relation.Attribute("Target") ?? throw new ArgumentException("Percorso foglio mancante.");
        var uri = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target);
        if (uri.Host != "xlsx.local" || uri.Query.Length > 0) throw new ArgumentException("Percorso del foglio non valido.");
        var document = Xml(Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')));
        string[] strings = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(ns + "si").Select(s => string.Concat(s.Descendants(ns + "t").Select(t => t.Value))).ToArray();
        int formulas = 0;
        string Cell(XElement c)
        {
            string type = (string?)c.Attribute("t") ?? "n", value = c.Element(ns + "v")?.Value ?? "";
            if (type == "e") throw new ArgumentException("Errore Excel nella cella " + c.Attribute("r")?.Value + ": " + value);
            if (c.Element(ns + "f") is not null)
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Formula senza valore salvato in " + c.Attribute("r")?.Value + ". Ricalcolare e salvare il file in Excel, oppure incollare solo valori.");
                formulas++;
            }
            if (type == "s") return int.TryParse(value, out int index) && index >= 0 && index < strings.Length ? strings[index] : throw new ArgumentException("Testo Excel non valido.");
            if (type == "inlineStr") return string.Concat(c.Descendants(ns + "t").Select(t => t.Value));
            if (type is "b" or "d") throw new ArgumentException("Valore booleano/data non ammesso in " + c.Attribute("r")?.Value);
            return value;
        }
        static int Column(XElement cell)
        {
            string address = (string?)cell.Attribute("r") ?? throw new ArgumentException("Indirizzo cella mancante.");
            int result = 0;
            foreach (char ch in address.TakeWhile(char.IsLetter)) result = checked(result * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
            if (result == 0) throw new ArgumentException("Indirizzo cella non valido.");
            return result;
        }
        var rows = new List<Row>(); bool headerFound = false, torqueHeader = false; int visited = 0;
        foreach (var row in document.Descendants(ns + "sheetData").Elements(ns + "row"))
        {
            if (++visited > 10050) throw new ArgumentException("Massimo 10.000 combinazioni per importazione.");
            var cells = row.Elements(ns + "c").ToDictionary(Column, Cell);
            if (!headerFound)
            {
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
            string context = "Riga Excel " + ((string?)row.Attribute("r") ?? visited.ToString(CultureInfo.InvariantCulture));
            try
            {
                if (cells.Any(c => c.Key > 8 && !string.IsNullOrWhiteSpace(c.Value))) throw new ArgumentException("Dati oltre la colonna H: importazione ambigua.");
                if(!torqueHeader&&!string.IsNullOrWhiteSpace(cells.GetValueOrDefault(8)))throw new ArgumentException("Torsione senza intestazione T [kNm].");
                string family = Family(cells.GetValueOrDefault(1) ?? ""), name = cells.GetValueOrDefault(2)?.Trim() ?? "";
                if (name.Length == 0) throw new ArgumentException("Nome combinazione mancante.");
                double Number(int i) => SectionWorkspace.Number(cells.GetValueOrDefault(i) ?? "", Headers[i - 1]);
                double? Optional(int i) => string.IsNullOrWhiteSpace(cells.GetValueOrDefault(i)) ? null : Number(i);
                var item = new Row(family, name, Number(3), family == "Taglio" ? Optional(4) : Number(4), family == "Taglio" ? Optional(5) : Number(5), family == "Taglio" ? Number(6) : Optional(6), family == "Taglio" ? Number(7) : Optional(7), Optional(8));
                if(family!="Taglio"&&item.T is not(null or 0))throw new ArgumentException("Torsione ammessa solo nella famiglia Taglio.");
                if (family == "Taglio" ? item.Mx is not (null or 0) || item.My is not (null or 0) : item.Vx is not (null or 0) || item.Vy is not (null or 0))
                    throw new ArgumentException("Sollecitazioni non utilizzate dalla famiglia: separare le righe di Taglio da quelle di pressoflessione/SLE.");
                rows.Add(item);
            }
            catch (ArgumentException ex) { throw new ArgumentException(context + ": " + ex.Message, ex); }
        }
        if (!headerFound) throw new ArgumentException("Intestazioni non riconosciute: usare il template ANTHEA.");
        return new(rows, formulas);
    }
}
