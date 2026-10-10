using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record AttributeRow(string Name, string Value);

public sealed partial class ModelTreeItem : ObservableObject
{
    public string Label { get; init; } = "";
    public string Key { get; init; } = "";
    public string Badge { get; init; } = "";
    public string Icon { get; init; } = "◇";
    public string Table { get; init; } = "";
    public int? Id { get; init; }
    public IReadOnlyList<int> Elements { get; init; } = [];
    public IReadOnlyList<ModelTreeItem> Children { get; init; } = [];
    public IReadOnlyList<AttributeRow> Attributes { get; init; } = [];
    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private bool isSelected;
    public override string ToString() => Label;
}

internal static class ModelTreeBuilder
{
    public static IReadOnlyList<ModelTreeItem> Build(ModelSnapshot model)
    {
        var adjacency = model.Elements.SelectMany(e => e.Nodes.Select(n => (n, e.Id))).ToLookup(p => p.n, p => p.Id);
        ModelTreeItem Item(string table, int id, string label, string icon, IReadOnlyList<int> elements, params AttributeRow[] attributes) => new()
        {
            Key = table + ":" + id, Table = table, Id = id, Label = label, Icon = icon, Elements = elements,
            Attributes = attributes.Concat(SourceAttributes(model, table, id)).ToArray()
        };
        var nodes = model.Nodes.Select(n => Item("NODE", n.Id, $"Nodo {n.Id}", "○", adjacency[n.Id].ToArray(),
            new("X [m]", n.X.ToString("G9")), new("Y [m]", n.Y.ToString("G9")), new("Z [m]", n.Z.ToString("G9")))).ToArray();
        ModelTreeItem[] Elements(string type) => model.Elements.Where(e => e.Type == type).Select(e => Item("ELEM", e.Id, $"{type} {e.Id}", type == "PLATE" ? "▱" : "╱", [e.Id],
            new("Tipo", e.Type), new("Nodi", string.Join(", ", e.Nodes)), new("Proprietà", e.Property.ToString()), new("Rotazione [°]", e.Angle.ToString("G6")))).ToArray();
        var plates = model.Plates.Select(p => Item("THIK", p.Id, $"{p.Id} · {p.Name}", "▤", model.Elements.Where(e => e.Type == "PLATE" && e.Property == p.Id).Select(e => e.Id).ToArray(),
            new("Spessore [m]", p.Thickness.ToString("G6")), new("Offset [m]", p.Offset.ToString("G6")))).ToArray();
        var sections = model.Sections.Select(s => Item("SECT", s.Id, $"{s.Id} · {s.Name}", "▣", model.Elements.Where(e => ElementFamilies.IsLine(e.Type) && e.Property == s.Id).Select(e => e.Id).ToArray(),
            new("Larghezza [m]", s.Width > 0 ? s.Width.ToString("G6") : "non importata"), new("Altezza [m]", s.Height > 0 ? s.Height.ToString("G6") : "non importata"), new("Forma", s.Shape), new("Preview solida", s.Shape == "SB" && s.Centered ? "Disponibile" : "Non disponibile: conserva la connettività"))).ToArray();

        ModelTreeItem Source(string table, string label, string icon)
        {
            if (model.SourceTables?.TryGetValue(table, out var data) != true)
                return new() { Key = table, Label = label, Icon = icon, Badge = "non importato", Attributes = [new("Disponibilità", "Questa tabella non è presente nella copia importata.")] };
            var children = data.EnumerateObject().Select(p =>
            {
                int? id = int.TryParse(p.Name, out var number) ? number : null;
                string name = p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("NAME", out var n) ? n.GetString() ?? p.Name : p.Name;
                IReadOnlyList<int> ids = table == "MATL" && id.HasValue && model.SourceTables.TryGetValue("ELEM", out var elementTable)
                    ? elementTable.EnumerateObject().Where(e => e.Value.TryGetProperty("MATL", out var mat) && mat.GetInt32() == id).Select(e => int.Parse(e.Name)).ToArray() : [];
                if (id.HasValue && table is "CONS" or "SKEW" or "CNLD" or "NBOF" or "NSPR") ids = adjacency[id.Value].ToArray();
                if (id.HasValue && table is "FRLS" or "PRLS" or "OFFS" or "BMLD" or "PRES") ids = model.Elements.Any(e => e.Id == id) ? [id.Value] : [];
                if (table == "GRUP" && p.Value.TryGetProperty("E_LIST", out var groupElements)) ids = groupElements.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                if (table == "ELNK" && p.Value.TryGetProperty("NODE", out var linkNodes)) ids = linkNodes.EnumerateArray().SelectMany(n => adjacency[n.GetInt32()]).Distinct().ToArray();
                if (table == "RIGD" && id.HasValue && p.Value.TryGetProperty("ITEMS", out var rigidItems))
                    ids = rigidItems.EnumerateArray().Where(item => item.TryGetProperty("S_NODE", out _)).SelectMany(item => item.GetProperty("S_NODE").EnumerateArray().Select(n => n.GetInt32()))
                        .Append(id.Value).SelectMany(n => adjacency[n]).Distinct().ToArray();
                return new ModelTreeItem { Key = table + ":" + p.Name, Table = table, Id = id, Label = $"{p.Name} · {name}", Icon = icon, Elements = ids,
                    Attributes = Flatten(p.Value).Concat(model.SourceTables.TryGetValue("UNIT", out var unit) ? Flatten(unit, "UNIT") : []).ToArray() };
            }).ToArray();
            return Group(table, label, icon, children);
        }
        var combinations = new[] { "LCOM-CONC", "LCOM-GEN", "LCOM-STEEL", "LCOM-SRC", "LCOM-SEISMIC", "LCOM-STLCOMP" }
            .Where(t => model.SourceTables?.ContainsKey(t) == true).Select(t => Source(t, t[5..], "Σ")).ToArray();
        return [
            Group("geometry", "Geometria", "⌘", new[] { Group("nodes", "Nodi", "○", nodes) }.Concat(model.Elements.Select(e => e.Type).Distinct().Order().Select(type => Group(type, ElementFamilies.Label(type), type == "PLATE" ? "▱" : "╱", Elements(type)))).ToArray(), true),
            Group("properties", "Proprietà", "◇", [Source("MATL", "Materiali", "◇"), Group("sections", "Sezioni", "▣", sections), Group("thickness", "Spessori e offset", "▤", plates)], true),
            Group("boundaries", "Vincoli e collegamenti", "⊥", [Source("CONS", "Restrain · vincoli nodali", "⊥"), Source("RIGD", "Constraint · link rigidi", "↔"), Source("MCON", "Relazioni cinematiche", "↔"), Source("FRLS", "Release · estremità beam", "○"), Source("PRLS", "Release · bordi plate", "○"), Source("ELNK", "Link · rigidi / elastici", "↔"), Source("NLNK", "Link generali", "↔"), Source("NLLP", "Leggi dei link", "⌁"), Source("NSPR", "Molle nodali", "⌁"), Source("SKEW", "Assi locali dei nodi", "⌘"), Source("OFFS", "Offset delle aste", "↔")]),
            Group("loads", "Carichi e combinazioni", "↓", [Source("STLD", "Casi di carico", "↓"), Source("CNLD", "Carichi nodali", "↓"), Source("BMLD", "Carichi sulle aste", "↓"), Source("PRES", "Pressioni", "↓"), Source("BODF", "Forze di volume / peso proprio", "↓"), Source("NBOF", "Forze inerziali nodali", "↓"), combinations.Length > 0 ? Group("combinations", "Combinazioni", "Σ", combinations) : new() { Label = "Combinazioni", Key = "combinations", Badge = "non importato" }]),
            Group("groups", "Gruppi", "▦", [Source("GRUP", "Struttura", "▦"), Source("BNGR", "Vincoli", "▦"), Source("LDGR", "Carichi", "▦")]),
            Source("STAG", "Fasi costruttive", "◷"),
            Group("import", "Rapporto di importazione", "ⓘ", (model.ImportMessages ?? []).Select((m, i) => new ModelTreeItem { Key = "import/" + i, Label = m.Code, Icon = "ⓘ", Attributes = [new("Livello", m.Severity), new("Sorgente", m.Record), new("Dettaglio", m.Message)] }).ToArray()),
            Group("source", "Tabelle originali acquisite", "▦", (model.SourceTables?.Keys.Order().Select(t => Source(t, t, "▦")) ?? []).ToArray())
        ];
    }

    static ModelTreeItem Group(string key, string label, string icon, IReadOnlyList<ModelTreeItem> children, bool expanded = false)
    {
        var elements = children.SelectMany(n => n.Elements).Distinct().ToArray();
        var displayed = children.Count <= 100 ? children : children.Chunk(100).Select((chunk, i) => new ModelTreeItem
        { Key = key + "/" + i, Label = $"{i * 100 + 1}–{i * 100 + chunk.Length}", Badge = chunk.Length.ToString(), Icon = "⋯", Children = chunk, Elements = chunk.SelectMany(n => n.Elements).Distinct().ToArray() }).ToArray();
        return new() { Key = key, Label = label, Icon = icon, Badge = children.Count.ToString("N0"), Children = displayed, Elements = elements, IsExpanded = expanded,
            Attributes = [new("Voci", children.Count.ToString("N0")), new("Elementi associati", elements.Length.ToString("N0"))] };
    }

    static IEnumerable<AttributeRow> SourceAttributes(ModelSnapshot model, string table, int id)
    {
        if (model.SourceTables?.TryGetValue(table, out var data) != true || !data.TryGetProperty(id.ToString(), out var record)) yield break;
        yield return new("Attributi originali", "Unità della sorgente (vedi sotto)");
        if (model.SourceTables.TryGetValue("UNIT", out var unit))
            foreach (var row in Flatten(unit, "UNIT")) yield return row;
        foreach (var row in Flatten(record)) yield return row;
    }

    static IEnumerable<AttributeRow> Flatten(JsonElement value, string path = "")
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
                foreach (var row in Flatten(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name)) yield return row;
        else if (value.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in value.EnumerateArray())
                foreach (var row in Flatten(item, path + "[" + i++ + "]")) yield return row;
        }
        else yield return new(path, value.ToString());
    }

    public static IEnumerable<ModelTreeItem> Descendants(IEnumerable<ModelTreeItem> items)
    {
        foreach (var item in items) { yield return item; foreach (var child in Descendants(item.Children)) yield return child; }
    }
    public static bool ExpandPath(IEnumerable<ModelTreeItem> items, string key)
    {
        foreach (var item in items)
        {
            if (item.Key == key) return true;
            if (ExpandPath(item.Children, key)) { item.IsExpanded = true; return true; }
        }
        return false;
    }

    public static IReadOnlyList<ModelTreeItem> Filter(IEnumerable<ModelTreeItem> items, string text) => items.Select(item =>
    {
        if (item.Label.Contains(text, StringComparison.OrdinalIgnoreCase)) return item;
        var children = Filter(item.Children, text);
        return children.Count == 0 ? null : new ModelTreeItem { Key = item.Key, Label = item.Label, Icon = item.Icon, Badge = item.Badge, Children = children, Elements = item.Elements, Attributes = item.Attributes, IsExpanded = true };
    }).OfType<ModelTreeItem>().ToArray();
}
