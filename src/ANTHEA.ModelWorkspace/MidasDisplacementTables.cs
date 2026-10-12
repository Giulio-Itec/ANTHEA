using System.Globalization;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Offline reader of global nodal translations; source metres/mm/cm become metres. No solver or combination evaluation.</summary>
public static class MidasDisplacementTables
{
    public static IReadOnlyDictionary<string, ModelDisplacement[]> ReadDirectory(string directory, IReadOnlySet<int> nodes)
    {
        var result = new Dictionary<string, Dictionary<int, ModelDisplacement>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "displacements-global-*.json").Order(StringComparer.Ordinal))
        {
            if (new FileInfo(file).Length > 32_000_000) throw new InvalidDataException("Tabella degli spostamenti troppo grande.");
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var tables = document.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("HEAD", out _)).ToArray();
            if (tables.Length != 1) throw new InvalidDataException("Tabella degli spostamenti mancante o ambigua.");
            var table = tables[0].Value;
            double scale = table.GetProperty("DIST").GetString()?.ToUpperInvariant() switch
            { "M" => 1, "MM" => .001, "CM" => .01, _ => throw new InvalidDataException("Unità degli spostamenti non supportata.") };
            var headers = table.GetProperty("HEAD").EnumerateArray().Select(h => h.GetString()).ToArray();
            if (headers.Contains("Stage") || headers.Contains("Step"))
                throw new InvalidDataException("Spostamenti per fase o step: serve un identificatore di risultato dedicato. I dati non vengono uniti ai casi statici.");
            int Column(string name)
            {
                var matches = headers.Select((h, i) => (h, i)).Where(p => p.h == name).ToArray();
                return matches.Length == 1 ? matches[0].i : throw new InvalidDataException("Colonna globale mancante o ambigua: " + name);
            }
            // Capitalization distinguishes the documented global DX/DY/DZ table from local Dx/Dy/Dz.
            int node = Column("Node"), load = Column("Load"), x = Column("DX"), y = Column("DY"), z = Column("DZ");
            bool rotations=new[]{"RX","RY","RZ"}.All(headers.Contains);
            if(!rotations&&new[]{"RX","RY","RZ"}.Any(headers.Contains))throw new InvalidDataException("Rotazioni nodali incomplete: servono RX, RY e RZ.");
            int rx=rotations?Column("RX"):-1,ry=rotations?Column("RY"):-1,rz=rotations?Column("RZ"):-1;
            foreach (var row in table.GetProperty("DATA").EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != headers.Length)
                    throw new InvalidDataException("Riga degli spostamenti non valida.");
                string Text(int column) => row[column].ValueKind == JsonValueKind.String ? row[column].GetString()! : row[column].GetRawText();
                if (!int.TryParse(Text(node), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || !nodes.Contains(id))
                    throw new InvalidDataException("Spostamento non associabile a un nodo del modello.");
                string name = Text(load);
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Caso degli spostamenti non identificato.");
                double Number(int column,double unitScale)
                {
                    if (!double.TryParse(Text(column), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value * unitScale))
                        throw new InvalidDataException("Componente di spostamento non numerica o non finita.");
                    return value * unitScale;
                }
                if (!result.TryGetValue(name, out var values)) result[name] = values = [];
                if (!values.TryAdd(id, new(id, Number(x,scale), Number(y,scale), Number(z,scale)){Rotation=rotations?new(Number(rx,1),Number(ry,1),Number(rz,1)):null}))
                    throw new InvalidDataException($"Spostamento duplicato: caso {name}, nodo {id}.");
            }
        }
        return result.ToDictionary(p => p.Key, p => p.Value.Values.OrderBy(d => d.Node).ToArray(), StringComparer.Ordinal);
    }

    public static ModelResultSet[] Merge(IReadOnlyList<ModelResultSet> forces, IReadOnlyDictionary<string, ModelDisplacement[]> displacements)
    {
        return forces.Select(r => displacements.TryGetValue(r.Name, out var d) ? r with { Displacements = d } : r)
            .Concat(displacements.Where(d => forces.All(r => r.Name != d.Key)).Select(d => new ModelResultSet(d.Key,
                d.Key.EndsWith("(max)", StringComparison.OrdinalIgnoreCase) || d.Key.EndsWith("(min)", StringComparison.OrdinalIgnoreCase), []) { Displacements = d.Value })).ToArray();
    }
}
