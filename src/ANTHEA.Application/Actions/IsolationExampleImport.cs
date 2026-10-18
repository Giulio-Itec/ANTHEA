using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Application.Actions;

/// <summary>Values-only import of the user's isolation workbook. Formulas, macros and external links are never executed.</summary>
public static class IsolationExampleImport
{
    public static JsonObject Read(string path, JsonObject current)
    {
        var bytes = File.ReadAllBytes(path); if (bytes.Length > 20_000_000) throw new ArgumentException("Il file supera 20 MB.");
        using var stream = new MemoryStream(bytes); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XDocument Xml(string name) { var entry = zip.GetEntry(name) ?? throw new ArgumentException("File Excel incompleto: " + name); if (entry.Length > 30_000_000) throw new ArgumentException("Foglio troppo grande."); using var s = entry.Open(); return XDocument.Load(s); }
        var strings = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(n + "si").Select(s => string.Concat(s.Descendants(n + "t").Select(t => t.Value))).ToArray();
        var rels = Xml("xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        var sheets = Xml("xl/workbook.xml").Descendants(n + "sheet").ToDictionary(e => (string)e.Attribute("name")!, e => rels[(string)e.Attribute(r + "id")!]);
        Dictionary<string, string> Sheet(string name)
        {
            if (!sheets.TryGetValue(name, out var target)) throw new ArgumentException("Manca il foglio «" + name + "».");
            var entry = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            return Xml(entry).Descendants(n + "c").ToDictionary(c => (string)c.Attribute("r")!, c =>
            {
                string text = c.Element(n + "v")?.Value ?? string.Concat(c.Descendants(n + "t").Select(t => t.Value));
                if ((string?)c.Attribute("t") == "e") return "#ERRORE";
                return (string?)c.Attribute("t") == "s" ? strings[int.Parse(text, CultureInfo.InvariantCulture)] : text;
            });
        }
        var data = (JsonObject)current.DeepClone(); var iso = Sheet("Isolatori");
        double Value(Dictionary<string, string> cells, string cell) { if (cells.TryGetValue(cell, out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)) return value; throw new ArgumentException("Valore memorizzato assente o non numerico: " + cell + ". Ricalcolare e salvare il file in Excel."); }
        foreach (var (key, cell) in new[] { ("mass", "H10"), ("group1_k", "H5"), ("group1_count", "H6"), ("group2_k", "H7"), ("group2_count", "H8"), ("factor_sle", "H36"), ("factor_slu", "H37") }) data[key] = Value(iso, cell);
        data["structure_damping"] = Value(iso, "H35"); data["isolation_damping"] = Value(iso, "H13");
        data["group1_name"] = iso.GetValueOrDefault("C5", "Gruppo 1"); data["group2_name"] = iso.GetValueOrDefault("C7", "Gruppo 2");
        data["soil"] = iso.GetValueOrDefault("H25", "A"); data["topography"] = iso.GetValueOrDefault("H23", "T1"); data["height"] = 1;
        // Explicit worksheet addresses, not engineering coefficients.
        foreach (var (state, row) in new[] { ("slo", 18), ("sld", 19), ("slv", 20), ("slc", 21) })
            foreach (var (key, col) in new[] { ("ag", "C"), ("f0", "D"), ("tc", "E") }) data[state + "_" + key] = Value(iso, col + row);
        var p = Sheet("Modellazione_isolatori_pendolo"); foreach (var (key, cell) in new[] { ("normal_force", "H5"), ("friction", "H4"), ("displacement", "H8"), ("radius", "H9") }) data[key] = Value(p, cell);
        var cat = Sheet("Catalogo FIP"); var catalog = new JsonArray(); double displacement = 0;
        for (int row = 1; row <= 1000; row++)
        {
            if (cat.GetValueOrDefault("D" + row) == "DISPLACEMENT") displacement = Value(cat, "F" + row);
            var name = cat.GetValueOrDefault("D" + row, "");
            if (!name.StartsWith("SI-", StringComparison.Ordinal) || !name.Any(char.IsDigit)) continue;
            if (!cat.TryGetValue("G" + row, out var text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var k) || k <= 0) continue;
            catalog.Add(new JsonObject { ["name"] = name, ["displacement"] = displacement, ["k"] = k });
        }
        data["catalog"] = catalog; data["isolation"] = true; data["pendulum"] = true; data["assigned_hazard"] = true;
        data["import_source"] = new JsonObject { ["file"] = Path.GetFileName(path), ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)), ["mode"] = "Valori memorizzati, non ricalcolati" };
        SpectraDocument.Validate(data); return data;
    }
}
