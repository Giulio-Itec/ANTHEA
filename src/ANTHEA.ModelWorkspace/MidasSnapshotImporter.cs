using System.Globalization;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Temporary adapter for the offline snapshot acquired in the laboratory. No API keys or network requests.</summary>
public static class MidasSnapshotImporter
{
    public static ModelSnapshot ReadDirectory(string directory)
    {
        JsonElement Read(string name)
        {
            var path = Path.Combine(directory, name + ".json"); if (new FileInfo(path).Length > 32000000) throw new InvalidDataException("Tabella troppo grande.");
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.Clone();
        }
        double factor = Read("UNIT").GetProperty("UNIT").GetProperty("1").GetProperty("DIST").GetString()!.ToUpperInvariant() switch { "M" => 1, "MM" => .001, "CM" => .01, _ => throw new InvalidDataException("Unità geometriche non supportate.") };
        var result = new ModelSnapshot { Name = "Modello importato", Source = "Acquisizione MIDAS Civil NX" };
        result.Nodes = Read("NODE").GetProperty("NODE").EnumerateObject().Select(p => new ModelNode(int.Parse(p.Name), p.Value.GetProperty("X").GetDouble() * factor, p.Value.GetProperty("Y").GetDouble() * factor, p.Value.GetProperty("Z").GetDouble() * factor)).ToArray();
        result.Elements = Read("ELEM").GetProperty("ELEM").EnumerateObject().Select(p => new ModelElement(int.Parse(p.Name), p.Value.GetProperty("TYPE").GetString()!, p.Value.GetProperty("SECT").GetInt32(), p.Value.GetProperty("NODE").EnumerateArray().Select(n => n.GetInt32()).Where(n => n > 0).ToArray(), p.Value.TryGetProperty("ANGLE", out var a) ? a.GetDouble() : 0)).ToArray();
        result.Plates = Read("THIK").GetProperty("THIK").EnumerateObject().Select(p =>
        {
            var t = p.Value; if (t.GetProperty("TYPE").GetString() != "VALUE" || t.TryGetProperty("bINOUT", out var io) && io.GetBoolean()) throw new InvalidDataException("La preview richiede uno spessore fisico unico.");
            double thick = t.GetProperty("T_IN").GetDouble() * factor, value = t.TryGetProperty("O_VALUE", out var o) ? o.GetDouble() : 0;
            int kind = t.TryGetProperty("OFFSET", out var opt) ? opt.GetInt32() : 0;
            double offset = kind switch { 0 when value == 0 => 0, 1 => value * thick, 2 => value * factor, _ => throw new InvalidDataException("Offset non riconosciuto: preview solida non disponibile.") };
            return new PlateProperty(int.Parse(p.Name), t.GetProperty("NAME").GetString()!, thick, offset);
        }).ToArray();
        result.Sections = Read("SECT").GetProperty("SECT").EnumerateObject().Select(p =>
        {
            var b = p.Value.GetProperty("SECT_BEFORE"); string shape = b.GetProperty("SHAPE").GetString()!;
            // A missing solid outline does not discard the element or invent an equivalent rectangle.
            if (shape != "SB") return new SectionProperty(int.Parse(p.Name), p.Value.GetProperty("SECT_NAME").GetString()!, shape, 0, 0, false);
            var size = b.GetProperty("SECT_I").GetProperty("vSIZE"); bool centered = b.GetProperty("OFFSET_PT").GetString() == "CC" && (!b.TryGetProperty("USERDEF_OFFSET_YI", out var y) || y.GetDouble() == 0) && (!b.TryGetProperty("USERDEF_OFFSET_ZI", out var z) || z.GetDouble() == 0);
            return new SectionProperty(int.Parse(p.Name), p.Value.GetProperty("SECT_NAME").GetString()!, shape, size[1].GetDouble() * factor, size[0].GetDouble() * factor, centered);
        }).ToArray();
        string resultFile = Path.Combine(directory, "plate-0-nodes.json");
        if (File.Exists(resultFile))
        {
            var root = Read("plate-0-nodes"); var tables = root.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("HEAD", out _)).ToArray();
            if (tables.Length == 1)
            {
                var table = tables[0].Value;
                if (table.GetProperty("FORCE").GetString()!.ToUpperInvariant() != "N" || table.GetProperty("DIST").GetString()!.ToUpperInvariant() != "MM") throw new InvalidDataException("Risultati della copia: attese unità N e mm.");
                var headers = table.GetProperty("HEAD").EnumerateArray().Select(v => v.GetString()!).ToArray();
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
        foreach (string name in new[] { "UNIT", "NODE", "ELEM", "THIK", "SECT", "MATL", "STLD", "LCOM-CONC", "LCOM-GEN", "LCOM-STEEL", "LCOM-SRC", "LCOM-SEISMIC", "LCOM-STLCOMP", "CONS", "ELNK", "RIGD", "FRLS", "PRLS", "NLNK", "NSPR", "MCON", "SKEW", "OFFS", "GRUP", "BNGR", "LDGR", "CNLD", "BMLD", "PRES", "STAG", "BODF", "NBOF", "NLLP" })
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
        result.Validate(); return result;
    }
}
