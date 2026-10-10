using System.Globalization;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

/// <summary>Reads exported tables without evaluating combinations or transforming result axes.</summary>
internal static class MidasResultTables
{
    public static ModelResultSet[] Read(string directory, IReadOnlyDictionary<int, ModelElement> elements)
    {
        var local = new Dictionary<string, List<CornerResult>>();
        var fields = new Dictionary<(string Case, string Family, string Axes, string Component), List<ResultValue>>();
        foreach (string family in new[] { "plate", "beam" })
        foreach (string file in Directory.EnumerateFiles(directory, family + "-results-*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (new FileInfo(file).Length > 32000000) throw new InvalidDataException("Tabella risultati troppo grande.");
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var tables = document.RootElement.EnumerateObject().Where(t => t.Value.ValueKind == JsonValueKind.Object && t.Value.TryGetProperty("HEAD", out _)).ToArray();
            if (tables.Length != 1) throw new InvalidDataException("Tabella risultati mancante o ambigua.");
            var table = tables[0].Value;
            if (!string.Equals(table.GetProperty("FORCE").GetString(), "N", StringComparison.OrdinalIgnoreCase) || !string.Equals(table.GetProperty("DIST").GetString(), "MM", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Attese tabelle in N e mm.");
            var heads = table.GetProperty("HEAD").EnumerateArray().Select(h => h.GetString()!).ToArray();
            int Column(string name) => Array.IndexOf(heads, name) is var index && index >= 0 ? index : throw new InvalidDataException("Colonna mancante: " + name);
            int elementColumn = Column("Elem"), loadColumn = Column("Load"), positionColumn = Column(family == "plate" ? "Node" : "Part");
            foreach (var row in table.GetProperty("DATA").EnumerateArray())
            {
                if (row.GetArrayLength() != heads.Length) throw new InvalidDataException("Lunghezza della riga risultati non valida.");
                int id = int.Parse(row[elementColumn].GetString()!, CultureInfo.InvariantCulture); string name = row[loadColumn].GetString()!;
                if (!elements.TryGetValue(id, out var element) || element.Type != family.ToUpperInvariant()) throw new InvalidDataException("Risultati non associabili alla famiglia dichiarata.");
                double Number(string component) => double.Parse(row[Column(component)].GetString()!, CultureInfo.InvariantCulture);
                void Add(string axes, string component, int node, double value, double? station = null)
                {
                    var key = (name, family.ToUpperInvariant(), axes, component);
                    if (!fields.TryGetValue(key, out var list)) fields.Add(key, list = []);
                    list.Add(new(id, node, value, station));
                }
                if (family == "plate")
                {
                    int node = int.Parse(row[positionColumn].GetString()!, CultureInfo.InvariantCulture);
                    if (!local.TryGetValue(name, out var corners)) local.Add(name, corners = []);
                    corners.Add(new(id, node, ModelSnapshot.Components.Select(c => Number(c) * (c.StartsWith('M') ? .001 : 1)).ToArray()));
                    foreach (string c in ResultFields.Components("PLATE", "PRINCIPAL"))
                        if (heads.Contains(c)) Add("PRINCIPAL", c, node, Number(c) * (c.StartsWith('M') ? .001 : 1));
                }
                else
                {
                    string part = row[positionColumn].GetString()!.Trim();
                    double station = part.StartsWith("I[", StringComparison.Ordinal) ? 0 : part.StartsWith("J[", StringComparison.Ordinal) ? 1 : part switch
                    { "1/4" => .25, "2/4" => .5, "3/4" => .75, _ => throw new InvalidDataException("Sezione di output beam non riconosciuta: " + part) };
                    int node = station == 0 ? element.Nodes[0] : station == 1 ? element.Nodes[1] : 0;
                    if (node != 0 && part != (station == 0 ? "I[" : "J[") + node + "]") throw new InvalidDataException("Estremità beam incoerente.");
                    string[] source = ["Axial", "Shear-y", "Shear-z", "Torsion", "Moment-y", "Moment-z"];
                    var components = ResultFields.Components("BEAM", "LOCAL");
                    for (int c = 0; c < components.Count; c++) Add("LOCAL", components[c], node, Number(source[c]) * (c < 3 ? .001 : .000001), station);
                }
            }
        }
        return local.Keys.Concat(fields.Keys.Select(k => k.Case)).Distinct().Select(name => new ModelResultSet(name,
            name.EndsWith("(max)", StringComparison.OrdinalIgnoreCase) || name.EndsWith("(min)", StringComparison.OrdinalIgnoreCase), local.GetValueOrDefault(name)?.ToArray() ?? [])
        { Fields = fields.Where(f => f.Key.Case == name).Select(f => new ResultField(f.Key.Family, f.Key.Axes, f.Key.Component, f.Value.ToArray())).ToArray() }).ToArray();
    }
}
