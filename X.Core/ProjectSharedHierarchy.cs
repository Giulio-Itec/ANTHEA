using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    public static IEnumerable<JsonObject> Sections(JsonObject section)
    {
        yield return section;
        foreach (var child in section.Array("strutture").OfType<JsonObject>())
            foreach (var nested in Sections(child)) yield return nested;
    }
    public static IEnumerable<JsonObject> Ancestors(JsonObject section)
    {
        for (var parent = section.Parent?.Parent as JsonObject; parent is not null && parent.ContainsKey("strutture"); parent = parent.Parent?.Parent as JsonObject)
            yield return parent;
    }
    public static IEnumerable<JsonObject> SubtreeSheets(JsonObject section) => Sections(section).SelectMany(s => s.Array("fogli").OfType<JsonObject>());
    public static IEnumerable<JsonObject> ContextSheets(JsonObject section) => Ancestors(section).Reverse()
        .SelectMany(s => s.Array("fogli").OfType<JsonObject>()).Concat(SubtreeSheets(section));
    public static string Location(JsonObject sheet) => sheet.Parent?.Parent is JsonObject section
        ? string.Join(" › ", Ancestors(section).Reverse().Append(section).Select(s => s.S("nome", "Sezione"))) : "";
    static string SheetOrder(JsonObject s) => s.S("id") is string id && id.Length > 0 ? id : s.S("modulo_id") + "|" + s.S("nome") + "|" + s["dati"]?.ToJsonString();

    // Relate peers and ancestors only. Sibling branches do not acquire a shared scope just
    // because a parent badge summarizes their independent conflicts.
    public static IEnumerable<(JsonObject First, JsonObject Second)> ComparisonPairs(JsonObject section)
    {
        var pairs = new List<(JsonObject First, JsonObject Second)>();
        foreach (var node in Sections(section))
        {
            var local = node.Array("fogli").OfType<JsonObject>().OrderBy(SheetOrder, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < local.Length; i++) for (int j = i + 1; j < local.Length; j++) pairs.Add((local[i], local[j]));
            foreach (var ancestor in Ancestors(node))
                foreach (var parentSheet in ancestor.Array("fogli").OfType<JsonObject>())
                    foreach (var childSheet in local) pairs.Add((parentSheet, childSheet));
        }
        return pairs.OrderBy(p => SheetOrder(p.First), StringComparer.Ordinal).ThenBy(p => SheetOrder(p.Second), StringComparer.Ordinal);
    }
    public static JsonObject[] AncestorReferences(JsonObject sheet, string key)
    {
        if (sheet.Parent?.Parent is not JsonObject section) return [];
        foreach (var level in Ancestors(section).Reverse())
        {
            var candidates = level.Array("fogli").OfType<JsonObject>().Where(s =>
                ComparableFields(s, sheet).Any(p => p.Source.Key == key) || Common(s, sheet).Any(p => p.Source.Key == key))
                .OrderBy(SheetOrder, StringComparer.Ordinal).ToArray();
            if (candidates.Length > 0) return candidates;
        }
        return [];
    }
    public static HashSet<string> ReferenceKeys(JsonObject sheet, IEnumerable<string> keys)
    {
        var result = keys.ToHashSet();
        if (sheet.Parent?.Parent is not JsonObject section) return result;
        foreach (var source in Ancestors(section).SelectMany(s => s.Array("fogli").OfType<JsonObject>()))
        {
            result.ExceptWith(ComparableFields(source, sheet).Select(p => p.Source.Key));
            result.ExceptWith(Common(source, sheet).Select(p => p.Source.Key));
        }
        return result;
    }

    public static int ApplyHierarchy(JsonObject source, HashSet<string> groups, HashSet<string>? keys = null)
    {
        if (source.Parent?.Parent is not JsonObject section) return 0;
        var allowed = ReferenceKeys(source, keys ?? Fields(source).Keys.ToHashSet());
        var authorities = Ancestors(section).SelectMany(s => s.Array("fogli").OfType<JsonObject>()).ToArray();
        return ApplyTargets(source, SubtreeSheets(section), groups, allowed, authorities);
    }

    public static void InheritHierarchy(JsonObject sheet, JsonObject section)
    {
        // Establish the contour before deciding which reinforcement fields are compatible.
        // A new rectangular CA sheet can become circular from its pile reference.
        bool unresolvedShape = false;
        foreach (string group in new[] { "Normativa", "Geometria", "Materiali", "Coefficienti", "Armatura", "Terreno" })
        {
            var claimed = new HashSet<string>();
            var targetKeys = Fields(sheet).Values.Where(f => f.Group == group && (!unresolvedShape || group != "Armatura" || f.Key == "cover_mm")).Select(f => f.Key).ToHashSet();
            // The outermost level defining a property controls it. Conflicting references at
            // that level remain unresolved instead of choosing one by tree/array order.
            foreach (var level in Ancestors(section).Reverse().Append(section))
            {
                var peers = level.Array("fogli").OfType<JsonObject>().Where(s => !ReferenceEquals(s, sheet)).OrderBy(SheetOrder, StringComparer.Ordinal).ToArray();
                var fields = peers.ToDictionary(s => s, Fields);
                var applicable = peers.ToDictionary(s => s, s => ComparableFields(s, sheet).Concat(Common(s, sheet)).Select(p => p.Source.Key).ToHashSet());
                if (group == "Geometria" && !claimed.Contains("shape"))
                {
                    var shapes = peers.Where(s => applicable[s].Contains("shape")).Select(s => Text(fields[s]["shape"].Value)).Distinct().ToArray();
                    if (shapes.Length > 1) { unresolvedShape = true; claimed.UnionWith(targetKeys); continue; }
                }
                var assignments = new Dictionary<JsonObject, HashSet<string>>();
                foreach (string key in targetKeys.Where(k => !claimed.Contains(k)))
                {
                    var sources = peers.Where(s => applicable[s].Contains(key)).ToArray();
                    if (sources.Length == 0) continue;
                    claimed.Add(key);
                    if (sources.Any(s => !Equal(fields[s][key].Value, fields[sources[0]][key].Value))) continue;
                    var source = sources[0];
                    if (!assignments.TryGetValue(source, out var assigned)) assignments[source] = assigned = [];
                    assigned.Add(key);
                }
                foreach (var (source, keys) in assignments)
                    ApplyTargets(source, [sheet], new HashSet<string> { group }, keys);
            }
        }
    }

    // Preserve the ancestors in live previews, while never mutating the saved document.
    public static (JsonObject Section, JsonObject Sheet) PreviewSection(JsonObject section, JsonObject sheet, JsonObject data)
    {
        var top = Ancestors(section).LastOrDefault() ?? section;
        int sectionIndex = Sections(top).ToList().IndexOf(section), sheetIndex = section.Array("fogli").IndexOf(sheet);
        var snapshot = (JsonObject)top.DeepClone(); var preview = Sections(snapshot).ElementAt(sectionIndex);
        var active = preview.Array("fogli")[sheetIndex]!.AsObject(); active["dati"] = data.DeepClone();
        return (preview, active);
    }
}
