using System.Globalization;
using System.Text.Json.Nodes;

namespace X.Core;

// Only explicit input fields participate. Loads, combinations and result caches never do.
public static partial class ProjectSharedData
{
    public sealed record Field(string Key, string Group, string Path, JsonNode? Value, double Scale = 1);
    public sealed record Difference(JsonObject First, JsonObject Second, string Group, string Key, string Left, string Right);
    static readonly string[] Geometry = ["shape", "diameter_mm", "width_mm", "height_mm", "flange_width_mm", "web_width_mm", "flange_thickness_mm"];
    static bool IsRebar(string key) => key == "cover_mm" || key == "barre_manuali" || key.StartsWith("second_") ||
        key.StartsWith("longitudinal_") || key.StartsWith("top_bar_") || key.StartsWith("bottom_bar_") ||
        key.StartsWith("side_bar_") || key.StartsWith("flange_bottom_") || key.StartsWith("transverse_");
    static bool IsMaterial(string key) => key is "fyk_mpa" or "alpha_cc" or "gamma_c" or "gamma_s" or "classe_acciaio" or "cls_diagramma" or "gettato_sottile" ||
        key.StartsWith("steel_") || key == "materiale_acciaio_nome";
    public static string Text(JsonNode? value) => value is null ? "non definito" : value is JsonArray or JsonObject ? value.ToJsonString() : value.ToString();
    static JsonNode? Normalize(JsonNode? value)
    {
        if (value is JsonObject o) { var result = new JsonObject(); foreach (var (k, v) in o.OrderBy(p => p.Key)) result[k] = Normalize(v); return result; }
        if (value is JsonArray a) return new JsonArray(a.Select(Normalize).ToArray());
        return J.Number(value) is double n ? JsonValue.Create(n) : value?.DeepClone();
    }
    public static bool Equal(JsonNode? a, JsonNode? b) => JsonNode.DeepEquals(Normalize(a), Normalize(b));
    public static Dictionary<string, Field> Fields(JsonObject sheet)
    {
        string module = sheet.S("modulo_id");
        var data = sheet["dati"] as JsonObject ?? Archivio.NuovoFoglio(module);
        var result = new Dictionary<string, Field>();
        void Add(string key, string group, string path, double scale = 1, JsonNode? forced = null)
        {
            JsonNode? value = data;
            foreach (var segment in path.Split('/')) value = value is JsonObject obj ? obj[segment] : null;
            if (forced is not null) value = forced;
            if (scale != 1 && J.Number(value) is double number) value = JsonValue.Create(number * scale);
            result[key] = new(key, group, path, value?.DeepClone(), scale);
        }
        if (module == "mat_calcestruzzo")
        {
            var name = data.S("classe", "C30/37");
            double.TryParse(name.Split('/')[0].TrimStart('C'), NumberStyles.Float, CultureInfo.InvariantCulture, out double fck);
            Add("CLS · fck [MPa]", "Materiali", "classe", forced: JsonValue.Create(fck));
            foreach (string container in new[] { "numeri", "scelte", "opzioni" })
                if (data[container] is JsonObject values)
                    foreach (var (key, _) in values) Add("Scheda CLS · " + container + "/" + key, "Materiali", container + "/" + key);
            Add("esposizione", "Materiali", "esposizione_principale", forced: JsonValue.Create(data.S("esposizione_principale", "XC1")));
        }
        if (module == RebarMaterial.Module)
            foreach (var key in RebarMaterial.Keys) Add(key, "Materiali", "input/" + key);
        bool rc = module is "str_palo" or PaloOrizzontale.Module;
        if (rc)
        {
            string root = module == "str_palo" ? "input" : "sezione";
            var input = (JsonObject)SezioneCA.DefaultInput().DeepClone();
            if (data[root] is JsonObject stored) foreach (var (k, v) in stored) input[k] = v?.DeepClone();
            RebarMaterial.CompleteLegacyInput(input);
            foreach (var (key, value) in input)
            {
                if (key == "fck_mpa") Add("CLS · fck [MPa]", "Materiali", root + "/" + key, forced: value);
                else if (IsMaterial(key)) Add(key, "Materiali", root + "/" + key, forced: value);
                else if (Geometry.Contains(key)) Add(key, "Geometria", root + "/" + key, forced: value);
                else if (IsRebar(key)) Add(key, "Armatura", root + "/" + key, forced: value);
            }
            foreach (string key in new[] { "second_inner_enabled", "second_inner_count", "second_inner_diameter", "second_inner_gap",
                "second_top_enabled", "second_top_count", "second_top_diameter", "second_top_gap",
                "second_bottom_enabled", "second_bottom_count", "second_bottom_diameter", "second_bottom_gap",
                "flange_bottom_count", "flange_bottom_diameter_mm", "flange_bottom_offset_mm" })
                if (!result.ContainsKey(key)) Add(key, "Armatura", root + "/" + key);
            Add("barre_manuali", "Armatura", root + "/barre_manuali");
            Add("esposizione", "Materiali", module == "str_palo" ? "workspace_ca/sle_comuni/esposizione" : "sezione/esposizione",
                forced: JsonValue.Create(module == "str_palo" ? (data["workspace_ca"]?["sle_comuni"] ?? data["workspace_ca"]?["sle"]?["SLE"]).S("esposizione", "Da scegliere") : data["sezione"].S("esposizione", "Da scegliere")));
            if (module == "str_palo")
                foreach (var (key, value) in new[] { ("tipo_staffa", "Staffa chiusa"), ("rami_x", "2"), ("rami_y", "2"), ("rami_interni", "0"), ("schema_interno", "Bracci paralleli"), ("rotazione_staffa", "0") })
                    Add("Staffe · " + key, "Armatura", "workspace_ca/taglio/" + key, forced: data["workspace_ca"]?["taglio"]?[key] ?? JsonValue.Create(value));
            if (module == "str_palo") Add("trefoli", "Armatura", "workspace_ca/trefoli", forced: data["workspace_ca"]?["trefoli"] ?? new JsonArray());
            if (module == PaloOrizzontale.Module)
            {
                Add("shape", "Geometria", "sezione/shape", forced: JsonValue.Create("Circolare"));
                Add("diameter_mm", "Geometria", "generali/diametro", 1000);
            }
        }
        if (module.StartsWith("geo_"))
        {
            Add(module.Contains("micropalo") ? "perforazione_mm" : "diameter_mm", "Geometria", "generali/diametro", 1000);
            Add(module.Contains("micropalo") ? "lunghezza_micropalo" : "lunghezza", "Geometria", "generali/lunghezza");
            if (!rc) Add("shape", "Geometria", "generali/__shape", forced: JsonValue.Create("Circolare"));
        }
        if (module == MicropaloOrizzontale.Module && data["sezione"] is JsonObject chs)
            foreach (var (key, _) in chs) Add("CHS · " + key, key is "fy_chs_mpa" or "gamma_m0" ? "Materiali" : "Armatura", "sezione/" + key);
        AddAdditionalFields(module, data, result);
        return result;
    }
    static bool Compatible(Field field, JsonObject source, JsonObject target)
    {
        if (!AdditionalCompatible(field, source, target)) return false;
        if (source.S("modulo_id") == target.S("modulo_id")) return true;
        if (field.Group == "Terreno" || field.Key is "CHS · profilo_chs" or "perforazione_mm" or "lunghezza_micropalo") return true;
        if (field.Group == "Materiali") return true;
        bool rcPair = source.S("modulo_id") is "str_palo" or PaloOrizzontale.Module &&
            target.S("modulo_id") is "str_palo" or PaloOrizzontale.Module;
        if (rcPair && field.Key is "shape" or "cover_mm") return true;
        if (source.S("modulo_id") == PaloOrizzontale.Module && target.S("modulo_id") == "str_palo" &&
            field.Group == "Geometria") return field.Key is "shape" or "diameter_mm";
        // Different module families share geometry/rebar only for a circular, ordinary section.
        var sf = Fields(source); var tf = Fields(target);
        if (!sf.TryGetValue("shape", out var s) || !tf.TryGetValue("shape", out var t) || Text(s.Value) != "Circolare" || Text(t.Value) != "Circolare") return false;
        if (field.Key == "shape") return false;
        if (field.Group == "Geometria" && field.Key is not ("diameter_mm" or "lunghezza")) return false;
        if (field.Group == "Armatura" && (field.Key.StartsWith("top_") || field.Key.StartsWith("bottom_") ||
            field.Key.StartsWith("side_") || field.Key.StartsWith("flange_"))) return false;
        if (field.Group == "Armatura" && (field.Key.StartsWith("second_") || field.Key == "barre_manuali")) return false;
        if (field.Key != "cover_mm" && field.Group == "Armatura" && ((source["dati"]?["input"]?["barre_manuali"] as JsonArray)?.Count > 0 ||
            (target["dati"]?["input"]?["barre_manuali"] as JsonArray)?.Count > 0)) return false;
        return true;
    }
    public static IEnumerable<(Field Source, Field Target)> Common(JsonObject source, JsonObject target)
    {
        var fields = Fields(target);
        foreach (var field in Fields(source).Values)
            if (fields.TryGetValue(field.Key, out var other) && Compatible(field, source, target)) yield return (field, other);
    }
    public static List<Difference> Differences(JsonObject section)
    {
        var result = new List<Difference>();
        foreach (var (first, second) in ComparisonPairs(section))
            foreach (var (a, b) in ComparableFields(first, second))
                if (!Equal(a.Value, b.Value)) result.Add(new(first, second, a.Group, a.Key, Text(a.Value), Text(b.Value)));
        return result;
    }
    public static HashSet<string> ChangedGroups(JsonObject before, JsonObject after)
    {
        var previous = Fields(before); var current = Fields(after);
        return current.Values.Where(f => !previous.TryGetValue(f.Key, out var old) || !Equal(old.Value, f.Value)).Select(f => f.Group).ToHashSet();
    }
    public static int Apply(JsonObject source, JsonObject section, HashSet<string> groups, JsonObject? onlyTarget = null, HashSet<string>? keys = null)
        => ApplyTargets(source, section.Array("fogli").OfType<JsonObject>().Where(s => onlyTarget is null || ReferenceEquals(s, onlyTarget)), groups, keys);

    static int ApplyTargets(JsonObject source, IEnumerable<JsonObject> targets, HashSet<string> groups, HashSet<string>? keys, JsonObject[]? authorities = null)
    {
        // Stage the entire operation, so unsupported conversions cannot partially update the project.
        var updates = new List<(JsonObject Sheet, JsonObject Data)>();
        foreach (var target in targets.Where(s => !ReferenceEquals(s, source)).Distinct())
        {
            var data = (JsonObject)(target["dati"] as JsonObject ?? Archivio.NuovoFoglio(target.S("modulo_id"))).DeepClone();
            bool changed = false;
            // Use the final shape for rebar compatibility in this same operation.
            var effectiveTarget = (JsonObject)target.DeepClone();
            effectiveTarget["dati"] = data.DeepClone();
            if (groups.Contains("Geometria") && (keys is null || keys.Contains("shape")) && source.S("modulo_id") == PaloOrizzontale.Module && target.S("modulo_id") == "str_palo")
                effectiveTarget["dati"]!["input"]!["shape"] = "Circolare";
            var governed = authorities?.SelectMany(s => ComparableFields(s, effectiveTarget).Concat(Common(s, effectiveTarget))).Select(p => p.Source.Key).ToHashSet() ?? [];
            foreach (var (a, b) in Common(source, effectiveTarget).Where(p => groups.Contains(p.Source.Group) && (keys is null || keys.Contains(p.Source.Key)) && !governed.Contains(p.Source.Key)))
            {
                if (a.Key == "shape" && target.S("modulo_id") == PaloOrizzontale.Module) continue;
                var original = Fields(target)[b.Key];
                if (Equal(a.Value, original.Value)) continue;
                var value = a.Value?.DeepClone();
                if (ApplyAdditional(a, b, data, target, ref changed)) continue;
                if (a.Key == "CLS · fck [MPa]")
                {
                    var standard = ConcreteMaterialCatalog.Concrete().FirstOrDefault(m => Equal(m["fck_mpa"], value));
                    if (target.S("modulo_id") == "mat_calcestruzzo")
                    {
                        if (standard is null) continue; // A custom fck cannot be represented by a standard-only selector.
                        data["classe"] = standard.S("nome"); changed = true; continue;
                    }
                    string root = target.S("modulo_id") == "str_palo" ? "input" : "sezione";
                    data[root]!["classe_cls"] = standard?.S("nome") ?? "Personalizzato";
                    data[root]!["materiale_cls_nome"] = standard?.S("nome") ?? "Personalizzato";
                }
                if (b.Scale != 1 && J.Number(value) is double n) value = JsonValue.Create((n / b.Scale).ToString("G17", CultureInfo.InvariantCulture));
                if (b.Path.EndsWith("/__shape")) continue;
                string[] path = b.Path.Split('/'); JsonObject parent = data;
                foreach (var key in path.SkipLast(1)) { if (parent[key] is not JsonObject) parent[key] = new JsonObject(); parent = parent[key]!.AsObject(); }
                if (value is null) parent.Remove(path[^1]); else parent[path[^1]] = value;
                changed = true;
            }
            if (changed)
            {
                if (target.S("modulo_id") == PaloOrizzontale.Module && data["sezione"] is JsonObject pile)
                { pile["shape"] = "Circolare"; pile["diameter_mm"] = data["generali"].D("diametro") * 1000; }
                updates.Add((target, data));
            }
        }
        foreach (var (sheet, data) in updates) sheet["dati"] = data;
        return updates.Count;
    }
}
