using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    public sealed record MissingSoilLayer(JsonObject Source, JsonObject Target, int Survey, int Layer);

    public static List<MissingSoilLayer> MissingSoilLayers(JsonObject section)
    {
        var result = new List<MissingSoilLayer>();
        var pairs = ComparisonPairs(section).Where(p => SupportsSharedSoils(p.First) && SupportsSharedSoils(p.Second))
            .SelectMany(p => ReferenceEquals(p.First.Parent, p.Second.Parent) ? new[] { p, (First: p.Second, Second: p.First) } : new[] { p });
        foreach (var (source, target) in pairs)
        {
            var surveys = source["dati"]!.AsObject().Array("stratigrafie");
            var targets = target["dati"]!.AsObject().Array("stratigrafie");
            for (int i = 0; i < surveys.Count; i++)
            {
                int count = (surveys[i] as JsonArray)?.Count ?? 0;
                int existing = i < targets.Count ? (targets[i] as JsonArray)?.Count ?? 0 : 0;
                for (int j = existing; j < count; j++) result.Add(new(source, target, i, j));
            }
        }
        return result;
    }

    public static int CopyMissingSoilLayers(JsonObject source, JsonObject section, int survey, int layer)
    {
        if (!SupportsSharedSoils(source) || source["dati"]?["stratigrafie"] is not JsonArray surveys ||
            survey < 0 || survey >= surveys.Count || surveys[survey] is not JsonArray rows || layer < 0 || layer >= rows.Count)
            throw new ArgumentException("Strato di riferimento non presente.");
        var updates = new List<(JsonObject Target, JsonObject Data)>();
        foreach (var target in SubtreeSheets(section).Where(s => !ReferenceEquals(s, source) && SupportsSharedSoils(s)))
        {
            var data = (JsonObject)target["dati"]!.DeepClone();
            if (data["stratigrafie"] is not JsonArray) data["stratigrafie"] = new JsonArray();
            var targetSurveys = data["stratigrafie"]!.AsArray();
            while (targetSurveys.Count <= survey) targetSurveys.Add(new JsonArray());
            var targetRows = targetSurveys[survey]!.AsArray();
            if (targetRows.Count > layer) continue;
            // Fill the preceding missing layers too, preserving the source order without holes.
            while (targetRows.Count <= layer)
            {
                var sourceRow = rows[targetRows.Count]!.AsObject();
                var added = target.S("modulo_id") == "geo_palo_verticale" ? Archivio.NuovoStratoPalo() : new JsonObject();
                foreach (string key in SoilProperties) added[key] = sourceRow[key]?.DeepClone();
                targetRows.Add(added);
            }
            updates.Add((target, data));
        }
        foreach (var (target, data) in updates) target["dati"] = data;
        return updates.Count;
    }

    public static bool SupportsSharedSoils(JsonObject sheet) => sheet.S("modulo_id") is "geo_palo_verticale" or PaloOrizzontale.Module or MicropaloOrizzontale.Module;

    // Compare the shared input schema, never the complete serialized row (which also contains
    // method-specific inputs, calculated columns and internal linking identifiers).
    static JsonNode? SoilValue(JsonObject row, string key) =>
        row[key] is null || string.IsNullOrWhiteSpace(row[key]!.ToString()) ? null : row[key];

    static bool WaterDataActive(JsonObject first, JsonObject second) =>
        (first["dati"]?["generali"]).B("presenza_falda") || (second["dati"]?["generali"]).B("presenza_falda");

    public static bool SameCommonSoilData(JsonObject first, JsonObject second)
    {
        if (!SupportsSharedSoils(first) || !SupportsSharedSoils(second)) return false;
        var a = first["dati"]!.AsObject().Array("stratigrafie");
        var b = second["dati"]!.AsObject().Array("stratigrafie");
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] is not JsonArray sa || b[i] is not JsonArray sb || sa.Count != sb.Count) return false;
            for (int j = 0; j < sa.Count; j++)
            {
                if (sa[j] is not JsonObject ra || sb[j] is not JsonObject rb) return false;
                foreach (string key in SoilProperties)
                    if ((key != "peso_specifico_saturo" || WaterDataActive(first, second)) && !Equal(SoilValue(ra, key), SoilValue(rb, key))) return false;
            }
        }
        return true;
    }

    // Optional explicit initialization copies common inputs; ordinary comparison also works without this step.
    public static void LinkSoils(JsonObject source, JsonObject target)
    {
        if (!SupportsSharedSoils(source) || !SupportsSharedSoils(target)) throw new ArgumentException("Il modello Bustamante mantiene i propri terreni e parametri.");
        var sd = (JsonObject)source["dati"]!.DeepClone(); var td = (JsonObject)target["dati"]!.DeepClone();
        var sa = sd.Array("stratigrafie"); var ta = td.Array("stratigrafie");
        bool empty = ta.All(a => a is JsonArray rows && rows.Count == 0);
        if (!sa.OfType<JsonArray>().Any(a => a.Count > 0)) throw new ArgumentException("Il foglio di riferimento non contiene strati.");
        if (!empty && (sa.Count != ta.Count || sa.Select((a, i) => a is not JsonArray sr || ta[i] is not JsonArray tr || sr.Count != tr.Count ||
            sr.Select((r, j) => !Equal(r?["spessore"], tr[j]?["spessore"])).Any(v => v)).Any(v => v)))
            throw new ArgumentException("Sondaggi o quote degli strati differenti: uniformare prima la suddivisione. Nessun dato è stato sostituito.");
        if (empty) { ta = new JsonArray(); td["stratigrafie"] = ta; }
        var identities = new HashSet<string>();
        for (int i = 0; i < sa.Count; i++)
        {
            if (empty) ta.Add(new JsonArray());
            var sr = sa[i]!.AsArray(); var tr = ta[i]!.AsArray();
            for (int j = 0; j < sr.Count; j++)
            {
                var row = sr[j]!.AsObject(); string id = row.S("shared_layer_id");
                if (id == "" || !identities.Add(id)) { id = Guid.NewGuid().ToString("N"); identities.Add(id); row["shared_layer_id"] = id; }
                if (empty) tr.Add(target.S("modulo_id") == "geo_palo_verticale" ? Archivio.NuovoStratoPalo() : new JsonObject());
                tr[j]!["shared_layer_id"] = id;
                foreach (string key in SoilProperties) tr[j]![key] = row[key]?.DeepClone();
            }
        }
        source["dati"] = sd; target["dati"] = td;
    }

    static IEnumerable<JsonObject> SoilRows(JsonObject data) => data.Array("stratigrafie").OfType<JsonArray>().SelectMany(a => a.OfType<JsonObject>());

    static void AddSoilFields(string module, JsonObject data, Dictionary<string, Field> fields)
    {
        if (module is not ("geo_palo_verticale" or PaloOrizzontale.Module or MicropaloOrizzontale.Module)) return;
        var surveys = data.Array("stratigrafie");
        for (int survey = 0; survey < surveys.Count; survey++)
        {
            if (surveys[survey] is not JsonArray rows) continue;
            for (int layer = 0; layer < rows.Count; layer++)
            {
                if (rows[layer] is not JsonObject row) continue;
                foreach (var key in SoilProperties)
                {
                    string identity = $"Strato · {survey + 1}/{layer + 1}/{key}";
                    fields[identity] = new(identity, "Terreno", identity, SoilValue(row, key)?.DeepClone());
                }
            }
        }
    }

    public static string SoilFieldLabel(string key)
    {
        var parts = key[9..].Split('/');
        string property = parts[2] switch
        {
            "spessore" => "Spessore [m]", "tipologia" => "Tipologia del terreno",
            "peso_specifico" => "Peso specifico [kN/m³]", "peso_specifico_saturo" => "Peso specifico saturo [kN/m³]",
            "angolo_attrito" => "Angolo di attrito [°]", "coesione_efficace" => "Coesione efficace [kPa]",
            "coesione_non_drenata" => "Coesione non drenata [kPa]", _ => parts[2]
        };
        return $"Sondaggio {parts[0]} · Strato {parts[1]} · {property}";
    }

    public static void SetSoilType(JsonObject sheet, string key, string value)
    {
        if (!SupportsSharedSoils(sheet) || !key.StartsWith("Strato · ") || !key.EndsWith("/tipologia") || value is not ("Granulare" or "Coesivo"))
            throw new ArgumentException("Tipologia del terreno non valida.");
        if (!Fields(sheet).ContainsKey(key)) throw new ArgumentException("Strato non presente nel foglio.");
        ApplySoilField(new Field(key, "Terreno", key, JsonValue.Create(value)), sheet["dati"]!.AsObject());
    }

    static bool ApplySoilField(Field field, JsonObject data)
    {
        if (!field.Key.StartsWith("Strato · ")) return false;
        var parts = field.Key[9..].Split('/');
        var row = data["stratigrafie"]![int.Parse(parts[0]) - 1]![int.Parse(parts[1]) - 1]!.AsObject();
        row[parts[2]] = field.Value?.DeepClone(); return true;
    }
}
