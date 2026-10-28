using System.Globalization;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Temporary adapter for the offline snapshot acquired in the laboratory. No API keys or network requests.</summary>
public static class MidasSnapshotImporter
{
    /// <summary>Civil NX databases preserved in the snapshot as source tables, in this order.</summary>
    internal static readonly string[] SourceTableNames = ["UNIT", "NODE", "ELEM", "THIK", "SECT", "MATL", "STLD", "LCOM-CONC", "LCOM-GEN", "LCOM-STEEL", "LCOM-SRC", "LCOM-SEISMIC", "LCOM-STLCOMP", "CONS", "ELNK", "RIGD", "FRLS", "PRLS", "NLNK", "NSPR", "MCON", "SKEW", "OFFS", "GRUP", "BNGR", "LDGR", "CNLD", "BMLD", "PRES", "STAG", "STCT", "BODF", "NBOF", "NLLP"];

    public static ModelSnapshot ReadDirectory(string directory)
    {
        JsonElement Read(string name)
        {
            var path = Path.Combine(directory, name + ".json"); if (new FileInfo(path).Length > 32000000) throw new InvalidDataException("Tabella troppo grande.");
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.Clone();
        }
        var result = new ModelSnapshot { Name = "Modello importato", Source = "Acquisizione MIDAS Civil NX" };
        Geometry(result, name => Read(name).GetProperty(name), null);
        string resultFile = Path.Combine(directory, "plate-0-nodes.json");
        if (File.Exists(resultFile))
        {
            var root = Read("plate-0-nodes"); var tables = root.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("HEAD", out _)).ToArray();
            if (tables.Length == 1)
            {
                var table = tables[0].Value;
                if (table.GetProperty("FORCE").GetString()!.ToUpperInvariant() != "N" || table.GetProperty("DIST").GetString()!.ToUpperInvariant() != "MM") throw new InvalidDataException("Risultati della copia: attese unità N e mm.");
                var headers = table.GetProperty("HEAD").EnumerateArray().Select(v => v.GetString()!).ToArray();
                if (headers.Contains("Stage") || headers.Contains("Step")) throw new InvalidDataException("Risultati per fase: usa l’importazione stage con fase e passo espliciti.");
                int Column(string name) { var positions = headers.Select((s, i) => (s, i)).Where(p => p.s == name).ToArray(); if (positions.Length != 1) throw new InvalidDataException("Colonna mancante o ambigua: " + name); return positions[0].i; }
                int elem = Column("Elem"), node = Column("Node"), load = Column("Load"); var columns = ModelSnapshot.Components.Select(Column).ToArray();
                var rows = table.GetProperty("DATA").EnumerateArray().ToArray();
                if (rows.Any(r => r.GetArrayLength() != headers.Length)) throw new InvalidDataException("Riga risultati non valida.");
                result.Results = rows.GroupBy(r => r[load].GetString()!).Select(group => new ModelResultSet(group.Key, group.Key.EndsWith("(max)") || group.Key.EndsWith("(min)"), group.Select(r => new CornerResult(int.Parse(r[elem].GetString()!), int.Parse(r[node].GetString()!), columns.Select((col, i) => double.Parse(r[col].GetString()!, CultureInfo.InvariantCulture) * (i < 3 ? .001 : 1)).ToArray())).ToArray())).ToArray();
            }
            else throw new InvalidDataException("Nessuna tabella risultati valida nella copia.");
        }
        result.SourceTables = new();
        // Preserve every acquired attribute. Missing exports remain missing, never an invented empty model category.
        foreach (string name in SourceTableNames)
            if (File.Exists(Path.Combine(directory, name + ".json")))
            {
                var root = Read(name);
                if (root.TryGetProperty(name, out var table) && table.ValueKind == JsonValueKind.Object)
                    result.SourceTables.Add(name, table.Clone());
                else if (root.TryGetProperty("message", out var message) && message.GetString() == "")
                    result.SourceTables.Add(name, JsonSerializer.SerializeToElement(new Dictionary<string, object>()));
                else throw new InvalidDataException("Tabella acquisita non riconosciuta: " + name);
            }
        if (Directory.EnumerateFiles(directory, "*-results-*.json").Any())
            result.Results = MidasResultTables.Read(directory, result.Elements.ToDictionary(e => e.Id));
        result.Results = MidasDisplacementTables.Merge(result.Results, MidasDisplacementTables.ReadDirectory(directory, result.Nodes.Select(n => n.Id).ToHashSet()));
        result.Validate(); return result;
    }

    /// <summary>The same snapshot core as <see cref="ReadDirectory"/> from tables already in memory, in the form of the Civil NX API
    /// responses (table name → <c>{"key":{...}}</c>). Geometry and attributes only; the caller adds results, messages and validation.
    /// With <paramref name="text"/> the elements, sections and thicknesses a MIDAS text file may contain without a viewer equivalent are
    /// left out or kept without outline and reported in <see cref="MidasTextSelection"/>; without it the rules are those of the API copy.</summary>
    internal static ModelSnapshot FromTables(IReadOnlyDictionary<string, JsonElement> tables, string name, string source, MidasTextSelection? text = null)
    {
        // A text file without plates or sections has no THIK or SECT command; the API always answers with a table.
        JsonElement Table(string table) => tables.TryGetValue(table, out var value) ? value
            : text != null && table is "THIK" or "SECT" ? JsonSerializer.SerializeToElement(new Dictionary<string, object>())
            : throw new InvalidDataException("Tabella sorgente mancante: " + table);
        var result = new ModelSnapshot { Name = name, Source = source };
        Geometry(result, Table, text);
        result.SourceTables = new();
        foreach (var table in tables)
        {
            if (table.Value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Tabella sorgente non riconosciuta: " + table.Key);
            result.SourceTables.Add(table.Key, table.Value.Clone());
        }
        return result;
    }

    static void Geometry(ModelSnapshot result, Func<string, JsonElement> table, MidasTextSelection? text)
    {
        double factor = table("UNIT").GetProperty("1").GetProperty("DIST").GetString()!.ToUpperInvariant() switch { "M" => 1, "MM" => .001, "CM" => .01, _ => throw new InvalidDataException("Unità geometriche non supportate.") };
        result.Nodes = table("NODE").EnumerateObject().Select(p => new ModelNode(int.Parse(p.Name), p.Value.GetProperty("X").GetDouble() * factor, p.Value.GetProperty("Y").GetDouble() * factor, p.Value.GetProperty("Z").GetDouble() * factor)).ToArray();
        var elements = table("ELEM").EnumerateObject();
        if (text == null)
            result.Elements = elements.Select(p => new ModelElement(int.Parse(p.Name), p.Value.GetProperty("TYPE").GetString()!, p.Value.GetProperty("SECT").GetInt32(), p.Value.GetProperty("NODE").EnumerateArray().Select(n => n.GetInt32()).Where(n => n > 0).ToArray(), p.Value.TryGetProperty("ANGLE", out var a) ? a.GetDouble() : 0)).ToArray();
        else result.Elements = text.Elements(elements);
        PlateProperty? Thickness(JsonProperty p)
        {
            var t = p.Value;
            if (t.GetProperty("TYPE").GetString() != "VALUE" || t.TryGetProperty("bINOUT", out var io) && io.GetBoolean())
            {
                if (text != null) { text.UnsupportedThickness(int.Parse(p.Name)); return null; }
                throw new InvalidDataException("La preview richiede uno spessore fisico unico.");
            }
            double thick = t.GetProperty("T_IN").GetDouble() * factor, value = t.TryGetProperty("O_VALUE", out var o) ? o.GetDouble() : 0;
            int kind = t.TryGetProperty("OFFSET", out var opt) ? opt.GetInt32() : 0;
            // A text file keeps the offset value of a thickness whose offset is switched off (bOFFSET NO): no offset, as for GPC.
            double offset = kind switch { 0 when value == 0 || text != null => 0, 1 => value * thick, 2 => value * factor, _ => throw new InvalidDataException("Offset non riconosciuto: preview solida non disponibile.") };
            return new PlateProperty(int.Parse(p.Name), t.GetProperty("NAME").GetString()!, thick, offset);
        }
        result.Plates = table("THIK").EnumerateObject().Select(Thickness).OfType<PlateProperty>().ToArray();
        result.Sections = table("SECT").EnumerateObject().Select(p =>
        {
            string label = p.Value.GetProperty("SECT_NAME").GetString()!;
            if (text != null && !(p.Value.TryGetProperty("SECT_BEFORE", out var before) && before.TryGetProperty("SHAPE", out var declared) && declared.ValueKind == JsonValueKind.String))
                return new SectionProperty(int.Parse(p.Name), label, p.Value.TryGetProperty("SECTTYPE", out var kind) ? kind.GetString() ?? "" : "", 0, 0, false);
            var b = p.Value.GetProperty("SECT_BEFORE"); string shape = b.GetProperty("SHAPE").GetString()!;
            // A missing solid outline does not discard the element or invent an equivalent rectangle.
            if (shape != "SB") return new SectionProperty(int.Parse(p.Name), label, shape, 0, 0, false);
            if (text != null && !(b.TryGetProperty("SECT_I", out var first) && first.TryGetProperty("vSIZE", out var dimensions) && dimensions.GetArrayLength() >= 2 && dimensions[0].GetDouble() > 0 && dimensions[1].GetDouble() > 0))
                return new SectionProperty(int.Parse(p.Name), label, "SB · dimensioni non disponibili", 0, 0, false);
            var size = b.GetProperty("SECT_I").GetProperty("vSIZE"); bool centered = b.GetProperty("OFFSET_PT").GetString() == "CC" && (!b.TryGetProperty("USERDEF_OFFSET_YI", out var y) || y.GetDouble() == 0) && (!b.TryGetProperty("USERDEF_OFFSET_ZI", out var z) || z.GetDouble() == 0);
            return new SectionProperty(int.Parse(p.Name), label, shape, size[1].GetDouble() * factor, size[0].GetDouble() * factor, centered);
        }).ToArray();
        text?.Complete(result);
    }
}
