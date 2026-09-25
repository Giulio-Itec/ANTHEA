using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Immutable revision trees and content-addressed sheet data. Live editors always get independent objects.</summary>
public static class ProjectRevisions
{
    public static IEnumerable<JsonObject> Nodes(JsonObject document) => document.Array("progetti").OfType<JsonObject>()
        .SelectMany(p => ProjectSharedData.Sections(p).SelectMany(s => new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>())));
    public static void EnsureIds(JsonObject document)
    {
        var seen = new HashSet<string>();
        foreach (var node in Nodes(document))
            if (node.S("id").Length == 0 || !seen.Add(node.S("id"))) { node["id"] = Guid.NewGuid().ToString("N"); seen.Add(node.S("id")); }
    }
    public static JsonObject? Find(JsonObject document, string id) => Nodes(document).FirstOrDefault(n => n.S("id") == id);
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
        JsonArray arr => new JsonArray(arr.Select(Canonical).ToArray()), _ => node?.DeepClone()
    };
    private static string Key(JsonNode node) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(node)!.ToJsonString())));
    private static void StoreSheets(JsonObject root, JsonObject pool)
    {
        foreach (var sheet in ProjectSharedData.SubtreeSheets(root))
        {
            var data = sheet["dati"] as JsonObject ?? Archivio.NuovoFoglio(sheet.S("modulo_id"));
            string key = Key(data); if (!pool.ContainsKey(key)) pool[key] = data.DeepClone();
            sheet.Remove("dati"); sheet["dati_ref"] = key;
        }
    }
    private static void ExpandSheets(JsonObject root, JsonObject pool)
    {
        foreach (var sheet in ProjectSharedData.SubtreeSheets(root))
        {
            if (!sheet.ContainsKey("dati_ref")) continue;
            string key = sheet.S("dati_ref");
            if (pool[key] is not JsonObject data || Key(data) != key) throw new ArgumentException("Dati di revisione mancanti o danneggiati.");
            sheet["dati"] = data.DeepClone(); sheet.Remove("dati_ref");
        }
    }
    private static void StripHistory(JsonObject root, bool resetNumbers = true)
    {
        foreach (var section in ProjectSharedData.Sections(root)) { section.Remove("revisioni"); if (resetNumbers) section.Remove("revisione"); }
    }
    public static JsonObject Context(JsonObject section)
    {
        var result = (JsonObject)section.DeepClone(); StripHistory(result, resetNumbers: false);
        foreach (var parent in ProjectSharedData.Ancestors(section))
        {
            var outer = (JsonObject)parent.DeepClone(); outer.Remove("revisioni");
            outer["strutture"] = new JsonArray(result); result = outer;
        }
        return result;
    }
    public static JsonObject Snapshot(JsonObject document, JsonObject entry)
    {
        var root = entry["contesto"]?.DeepClone() as JsonObject ?? throw new ArgumentException("Revisione non valida.");
        ExpandSheets(root, document["archivio_revisioni"] as JsonObject ?? new());
        return root;
    }
    public static void NewRevision(JsonObject document, JsonObject section, string note)
    {
        EnsureIds(document);
        if (document["archivio_revisioni"] is not JsonObject) document["archivio_revisioni"] = new JsonObject();
        var pool = document["archivio_revisioni"]!.AsObject();
        if (section["revisioni"] is not JsonArray) section["revisioni"] = new JsonArray();
        var history = section.Array("revisioni");
        var current = section["revisione"] as JsonObject ?? J.Obj(("numero", 0), ("data", DateTime.Now.ToString("O")), ("nota", "Versione iniziale"));
        var changes = Changes(document, section);
        var context = Context(section); StoreSheets(context, pool);
        var entry = (JsonObject)current.DeepClone(); entry["contesto"] = context; entry["sezione_id"] = section.S("id");
        entry["modifiche"] = J.Node(changes); history.Add(entry);
        section["revisione"] = J.Obj(("numero", (int)current.D("numero") + 1), ("data", DateTime.Now.ToString("O")), ("nota", note));
    }
    public static string[] Changes(JsonObject document, JsonObject section)
    {
        if (section.Array("revisioni").LastOrDefault() is not JsonObject previous)
            return [section["revisione"].D("numero") == 0 ? "Versione iniziale" : "Nessuna revisione precedente conservata"];
        var root = Snapshot(document, previous);
        var old = ProjectSharedData.Sections(root).First(s => s.S("id") == section.S("id"));
        return Compare(section, old);
    }
    private static string[] Compare(JsonObject section, JsonObject old)
    {
        var before = ProjectSharedData.Sections(old).SelectMany(s => new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>())).ToDictionary(n => n.S("id"));
        var after = ProjectSharedData.Sections(section).SelectMany(s => new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>())).ToDictionary(n => n.S("id"));
        var changes = new List<string>();
        foreach (var (id, node) in after)
        {
            if (!before.TryGetValue(id, out var prior)) { changes.Add("Aggiunta: " + node.S("nome")); continue; }
            if (node.S("nome") != prior.S("nome")) changes.Add(prior.S("nome") + " → " + node.S("nome"));
            if (node.ContainsKey("modulo_id"))
            {
                var fields = ProjectSharedData.Fields(node); var oldFields = ProjectSharedData.Fields(prior);
                var common = fields.Keys.Intersect(oldFields.Keys).Where(k => !ProjectSharedData.Equal(fields[k].Value, oldFields[k].Value)).ToArray();
                foreach (string key in common) changes.Add(node.S("nome") + " · " + ProjectReportPlan.Label(key) + ": " + ProjectSharedData.Text(oldFields[key].Value) + " → " + ProjectSharedData.Text(fields[key].Value));
                if (!JsonNode.DeepEquals(Canonical(node["dati"]), Canonical(prior["dati"])))
                    changes.Add(node.S("nome") + ": dati della scheda modificati");
            }
            if (node.S("id") != section.S("id") && node.Parent?.Parent?.S("id") != prior.Parent?.Parent?.S("id")) changes.Add(node.S("nome") + ": spostato");
            foreach (string key in new[] { "fogli", "strutture" })
            {
                var a = node.Array(key).Select(n => n.S("id")).ToArray(); var b = prior.Array(key).Select(n => n.S("id")).ToArray();
                if (a.Length > 1 && a.ToHashSet().SetEquals(b) && !a.SequenceEqual(b)) changes.Add(node.S("nome") + ": ordine modificato");
            }
        }
        changes.AddRange(before.Where(p => !after.ContainsKey(p.Key)).Select(p => "Eliminata: " + p.Value.S("nome")));
        return changes.Count == 0 ? ["Nessuna modifica rispetto alla revisione precedente"] : changes.ToArray();
    }
    public static void DeleteRevision(JsonObject document, string sectionId, int number)
    {
        var section = Find(document, sectionId) ?? throw new ArgumentException("Sezione della revisione non trovata.");
        int current = (int)(section["revisione"]?.D("numero") ?? 0);
        var history = section.Array("revisioni");
        if (number != current)
        {
            var entry = history.OfType<JsonObject>().SingleOrDefault(r => (int)r.D("numero") == number)
                ?? throw new ArgumentException("Revisione non trovata.");
            history.Remove(entry);
        }
        else
        {
            var previous = history.OfType<JsonObject>().LastOrDefault()
                ?? throw new ArgumentException("L’unica versione rimasta non può essere eliminata.");
            var snapshot = Snapshot(document, previous);
            var restored = (JsonObject)ProjectSharedData.Sections(snapshot).Single(s => s.S("id") == sectionId).DeepClone();
            var liveSections = ProjectSharedData.Sections(section).ToDictionary(s => s.S("id"));
            // Restore only this branch. Keep the older histories of surviving subsections,
            // excluding revisions created after the version we are bringing back.
            foreach (var child in ProjectSharedData.Sections(restored).Skip(1))
                if (child["revisione"] is JsonObject revision && liveSections.TryGetValue(child.S("id"), out var live))
                    child["revisioni"] = new JsonArray(live.Array("revisioni").OfType<JsonObject>()
                        .Where(r => r.D("numero") < revision.D("numero")).Select(r => r.DeepClone()).ToArray());
            restored["revisioni"] = new JsonArray(history.OfType<JsonObject>().Where(r => !ReferenceEquals(r, previous)).Select(r => r.DeepClone()).ToArray());
            restored["revisione"] = J.Obj(("numero", previous.D("numero")), ("data", previous.S("data")), ("nota", previous.S("nota")));
            // A sheet/section may have moved to another branch since the snapshot.
            // Preserve that live branch and give the restored copy independent identities.
            var own = ProjectSharedData.Sections(section).SelectMany(s => new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>())).ToHashSet();
            var outside = Nodes(document).Where(n => !own.Contains(n)).Select(n => n.S("id")).ToHashSet();
            var remap = ProjectSharedData.Sections(restored).SelectMany(s => new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>()))
                .Where(n => outside.Contains(n.S("id"))).ToDictionary(n => n.S("id"), _ => Guid.NewGuid().ToString("N"));
            void Remap(JsonObject branch)
            {
                foreach (var s in ProjectSharedData.Sections(branch))
                {
                    foreach (var n in new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>()))
                        if (remap.TryGetValue(n.S("id"), out string? id)) n["id"] = id;
                    foreach (var entry in s.Array("revisioni").OfType<JsonObject>())
                    {
                        if (remap.TryGetValue(entry.S("sezione_id"), out string? id)) entry["sezione_id"] = id;
                        if (entry["contesto"] is JsonObject context) Remap(context);
                    }
                }
            }
            if (remap.Count > 0) Remap(restored);
            section.Clear(); foreach (var (key, value) in restored) section[key] = value?.DeepClone();
        }
        // Summaries must refer to the preceding revision still present in the archive.
        JsonObject? prior = null;
        foreach (var entry in section.Array("revisioni").OfType<JsonObject>())
        {
            var root = Snapshot(document, entry);
            var version = ProjectSharedData.Sections(root).Single(s => s.S("id") == sectionId);
            entry["modifiche"] = J.Node(prior is null ? new[] { entry.D("numero") == 0 ? "Versione iniziale" : "Prima revisione conservata" } : Compare(version, prior));
            prior = version;
        }
    }
    public static JsonObject Duplicate(JsonObject section)
    {
        var copy = (JsonObject)section.DeepClone(); StripHistory(copy);
        foreach (var node in ProjectSharedData.Sections(copy).SelectMany(s => new[] { s }.Concat(s.Array("fogli").OfType<JsonObject>()))) node["id"] = Guid.NewGuid().ToString("N");
        return copy;
    }
    public static JsonObject Pack(JsonObject document)
    {
        var packed = (JsonObject)document.DeepClone();
        if (packed.S("tipo") != "progetti" || packed["archivio_revisioni"] is not JsonObject pool) return packed;
        foreach (var root in packed.Array("progetti").OfType<JsonObject>()) StoreSheets(root, pool);
        var used = new HashSet<string>();
        void References(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                if (obj.S("dati_ref").Length > 0) used.Add(obj.S("dati_ref"));
                foreach (var pair in obj.Where(p => p.Key != "archivio_revisioni")) References(pair.Value);
            }
            else if (node is JsonArray array) foreach (var child in array) References(child);
        }
        References(packed);
        foreach (string key in pool.Select(p => p.Key).Where(k => !used.Contains(k)).ToArray()) pool.Remove(key);
        packed["versione"] = 2; return packed;
    }
    public static void Unpack(JsonObject document)
    {
        if (document.D("versione") != 2) return;
        if (document.S("tipo") != "progetti" || document["archivio_revisioni"] is not JsonObject pool) throw new ArgumentException("Archivio revisioni mancante.");
        foreach (var root in document.Array("progetti").OfType<JsonObject>()) ExpandSheets(root, pool);
        document["versione"] = 1;
    }
    public static void Validate(JsonObject document)
    {
        foreach (var section in Nodes(document).Where(n => n.ContainsKey("revisioni")))
        {
            if (section["revisioni"] is not JsonArray entries) throw new ArgumentException("Elenco revisioni non valido.");
            foreach (var entry in entries)
            {
                if (entry is not JsonObject obj) throw new ArgumentException("Revisione non valida.");
                var snapshot = Snapshot(document, obj);
                if (!ProjectSharedData.Sections(snapshot).Any(s => s.S("id") == obj.S("sezione_id"))) throw new ArgumentException("Sezione della revisione mancante.");
                var check = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray(snapshot)));
                Archivio.Valida(check);
            }
        }
    }
}
