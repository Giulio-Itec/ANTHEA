using ANTHEA.ModelWorkspace;
using System.Text.Json;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record DisplayCategory(string Key, string Label, string Color, int Count);
public sealed record ElementDisplay(string Key, string Label, string Color);

/// <summary>Stable presentation categories. Group membership does not change the calculation model.</summary>
internal sealed class ModelDisplayCatalog
{
    private static readonly string[] Palette = ["#71B6E8", "#E7AF66", "#73C5AC", "#BB9FE4", "#E58685", "#A6C878", "#67C3D1", "#D9A3CC", "#CBB58B", "#A2B2C9", "#87A4F1", "#DFBE65", "#63BBA1", "#C38B9C", "#87B2A1", "#C79BD4"];
    private readonly Dictionary<int, IReadOnlyDictionary<int, ElementDisplay>> schemes = [];
    public IReadOnlyDictionary<int, ElementDisplay> ForMode(int mode) => schemes[mode];

    public ModelDisplayCatalog(ModelSnapshot snapshot)
    {
        JsonElement? Table(string name) => snapshot.SourceTables?.TryGetValue(name, out var value) == true ? value : null;
        string Name(string table, int id, string fallback)
        {
            if (Table(table) is JsonElement data && data.TryGetProperty(id.ToString(), out var record) && record.TryGetProperty("NAME", out var name))
                return name.GetString() ?? fallback;
            return fallback;
        }
        var memberships = new Dictionary<int, List<(int Id, string Name, int Size)>>();
        if (Table("GRUP") is JsonElement groups)
            foreach (var group in groups.EnumerateObject())
            {
                if (!int.TryParse(group.Name, out int id) || !group.Value.TryGetProperty("E_LIST", out var members)) continue;
                int[] ids = members.EnumerateArray().Select(e => e.GetInt32()).Distinct().ToArray();
                string name = Name("GRUP", id, "Gruppo " + id);
                foreach (int element in ids)
                {
                    if (!memberships.TryGetValue(element, out var list)) memberships[element] = list = [];
                    list.Add((id, name, ids.Length));
                }
            }
        for (int mode = 0; mode < 5; mode++)
        {
            var descriptions = snapshot.Elements.ToDictionary(e => e.Id, e =>
            {
                if (mode == 0) return ("all", "Modello");
                if (mode == 1) return (e.Type, e.Type);
                if (mode == 2)
                {
                    if (Table("ELEM") is JsonElement elements && elements.TryGetProperty(e.Id.ToString(), out var row) && row.TryGetProperty("MATL", out var material))
                    { int id = material.GetInt32(); return ("MATL:" + id, $"{id} · {Name("MATL", id, "Materiale")}"); }
                    return ("none", "Materiale non importato");
                }
                if (mode == 3)
                    return e.Type == "PLATE" ? ("THIK:" + e.Property, "Sp. " + e.Property + " · " + snapshot.Plates.First(p => p.Id == e.Property).Name)
                        : ("SECT:" + e.Property, "Sez. " + e.Property + " · " + snapshot.Sections.First(s => s.Id == e.Property).Name);
                // Prefer the most specific group; ties have a deterministic source-ID order.
                // Every membership remains visible in the model tree/table.
                if (memberships.TryGetValue(e.Id, out var assigned))
                { var group = assigned.OrderBy(g => g.Size).ThenBy(g => g.Id).First(); return ("GRUP:" + group.Id, $"{group.Id} · {group.Name}"); }
                return ("none", "Senza gruppo");
            });
            var colors = descriptions.Values.Select(v => v.Item1).Distinct().Order(StringComparer.Ordinal)
                .Select((key, i) => (key, Color: key == "none" ? "#8994A3" : Palette[i % Palette.Length])).ToDictionary(v => v.key, v => v.Color);
            schemes[mode] = descriptions.ToDictionary(v => v.Key, v => new ElementDisplay(v.Value.Item1, v.Value.Item2, colors[v.Value.Item1]));
        }
    }
}
