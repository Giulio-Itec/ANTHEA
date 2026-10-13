using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
namespace Anthea.Serialization;

/// <summary>Shared bounded, values-only OOXML reader. Does not calculate formulae or follow external links.</summary>
public static class WorkbookCells
{
    public sealed record Row(string Number, IReadOnlyDictionary<int, string> Cells);
    public sealed record Sheet(IReadOnlyList<Row> Rows, int FormulaCells);
    public static Sheet Read(byte[] bytes, string sheetName, int maximumRows = 100050)
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
        var sheet = workbook.Descendants(ns + "sheet").FirstOrDefault(s => string.Equals((string?)s.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Manca il foglio " + sheetName + ". Usare il template ANTHEA.");
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
        var rows = new List<Row>();
        foreach (var row in document.Descendants(ns + "sheetData").Elements(ns + "row"))
        {
            if (rows.Count >= maximumRows) throw new ArgumentException("Numero massimo di righe Excel superato.");
            rows.Add(new((string?)row.Attribute("r") ?? (rows.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                new System.Collections.ObjectModel.ReadOnlyDictionary<int, string>(row.Elements(ns + "c").ToDictionary(Column, Cell))));
        }
        return new(rows.AsReadOnly(), formulas);
    }
}
