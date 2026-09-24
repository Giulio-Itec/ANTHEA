using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    static readonly string[] SoilProperties = ["spessore", "tipologia", "peso_specifico", "peso_specifico_saturo", "angolo_attrito", "coesione_efficace", "coesione_non_drenata"];
    static void AddAdditionalFields(string module, JsonObject data, Dictionary<string, Field> result)
    {
        void Add(string key, string group, string path, JsonNode? fallback = null)
        {
            JsonNode? value = data;
            foreach (var part in path.Split('/')) value = value?[part];
            result[key] = new(key, group, path, (value ?? fallback)?.DeepClone());
        }
        if (module == "mat_calcestruzzo")
        {
            foreach (var (path, value) in new[] {
                ("numeri/diameter", "16"), ("numeri/aggregate", "20"), ("numeri/bondAlpha", "1,0"), ("numeri/bondGamma", "1,5"), ("numeri/cementName", ""),
                ("scelte/ntcElement", "Trave / pilastro"), ("scelte/coverMethod", "NTC + Circ. 2019"), ("scelte/life", "50 anni"),
                ("scelte/deviationControl", "Ordinario"), ("scelte/deviationValue", "10 mm"), ("scelte/ground", "Casseratura"), ("scelte/abrasion", "Nessuno"),
                ("scelte/bondCondition", "Altre · η₁ = 0,7"), ("scelte/consistency", "Da definire"), ("scelte/cement", "CEM I"), ("scelte/cementClass", "32,5"), ("scelte/cementEarly", "N") })
                Add("Scheda CLS · " + path, "Materiali", path, JsonValue.Create(value));
            foreach (string flag in new[] { "highStrength", "slab", "quality", "rough", "ntcQuality" })
                Add("Scheda CLS · opzioni/" + flag, "Materiali", "opzioni/" + flag, JsonValue.Create(false));
        }
        AddSoilFields(module, data, result);
        if (module.StartsWith("geo_"))
            Add("verticali_indagate", "Terreno", (module.EndsWith("_orizzontale") ? "verifica/" : "generali/") + "verticali_indagate", JsonValue.Create("1"));
        if (module is "geo_palo_verticale" or PaloOrizzontale.Module or MicropaloOrizzontale.Module)
        {
            Add("presenza_falda", "Terreno", "generali/presenza_falda", JsonValue.Create(false));
            Add("profondita_falda", "Terreno", "generali/profondita_falda");
        }
        if (module == "geo_micropalo_verticale") Add("CHS · profilo_chs", "Armatura", "generali/profilo_chs");
    }

    static bool AdditionalCompatible(Field field, JsonObject source, JsonObject target)
    {
        if ((field.Key == "profondita_falda" || field.Key.StartsWith("Strato · ") && field.Key.EndsWith("/peso_specifico_saturo")) &&
            !WaterDataActive(source, target)) return false;
        if (field.Key == "lunghezza_micropalo" && source.S("modulo_id") != target.S("modulo_id") &&
            (source["dati"]?["generali"].D("inclinazione") != 0 || target["dati"]?["generali"].D("inclinazione") != 0)) return false;
        if (field.Key == "CHS · profilo_chs")
        {
            if (source.S("modulo_id") == MicropaloOrizzontale.Module && source["dati"]?["sezione"].S("modo_chs") != "Catalogo") return false;
            if (target.S("modulo_id") == MicropaloOrizzontale.Module && target["dati"]?["sezione"].S("modo_chs") != "Catalogo") return false;
            if (!Chs.Catalogo.ContainsKey(Text(field.Value))) return false;
        }
        if (field.Key == "esposizione" && Text(field.Value) == "Da scegliere" && target.S("modulo_id") == "mat_calcestruzzo") return false;
        return true;
    }

    public static HashSet<string> ChangedKeys(JsonObject before, JsonObject after)
    {
        var previous = Fields(before); var current = Fields(after);
        var keys = current.Values.Where(f => !previous.TryGetValue(f.Key, out var old) || !Equal(old.Value, f.Value)).Select(f => f.Key).ToHashSet();
        // Changing the shape defines a complete new contour. An isolated cover edit does not reset the reinforcement.
        if (keys.Contains("shape")) keys.UnionWith(Geometry);
        if (keys.Any(k => k is "fyk_mpa" or "classe_acciaio" or "materiale_acciaio_nome" || k.StartsWith("steel_")))
            keys.UnionWith(current.Keys.Where(k => k is "fyk_mpa" or "classe_acciaio" or "materiale_acciaio_nome" || k.StartsWith("steel_")));
        if (keys.Contains("Scheda CLS · scelte/deviationControl")) keys.Add("Scheda CLS · scelte/deviationValue");
        if (keys.Contains("presenza_falda")) keys.Add("profondita_falda");
        if (keys.Contains("CHS · modo_chs")) keys.UnionWith(current.Keys.Where(k => k.StartsWith("CHS · ")));
        return keys;
    }

    static void Put(JsonObject data, string path, JsonNode? value)
    {
        var parts = path.Split('/'); var parent = data;
        foreach (var part in parts.SkipLast(1))
        { if (parent[part] is not JsonObject) parent[part] = new JsonObject(); parent = parent[part]!.AsObject(); }
        parent[parts[^1]] = value?.DeepClone();
    }

    static bool ApplyAdditional(Field source, Field target, JsonObject data, JsonObject sheet, ref bool changed)
    {
        if (ApplySoilField(source, data)) { changed = true; return true; }
        if (source.Key == "esposizione")
        {
            Put(data, target.Path, source.Value);
            if (sheet.S("modulo_id") == "mat_calcestruzzo") data["esposizioni"] = new JsonArray(source.Value?.DeepClone());
            if (sheet.S("modulo_id") == "str_palo")
                foreach (var set in SectionWorkspace.Sets.Skip(2)) Put(data, "workspace_ca/sle/" + set + "/esposizione", source.Value);
            changed = true; return true;
        }
        if (source.Key == "CHS · profilo_chs" && sheet.S("modulo_id") == MicropaloOrizzontale.Module && Chs.Catalogo.TryGetValue(Text(source.Value), out var profile))
        {
            Put(data, "sezione/profilo_chs", source.Value);
            Put(data, "sezione/diametro_chs_mm", JsonValue.Create(profile.Item1));
            Put(data, "sezione/spessore_chs_mm", JsonValue.Create(profile.Item2));
            changed = true; return true;
        }
        return false;
    }

    public static List<string> Limitations(JsonObject section)
    {
        var sheets = ComparisonSheets(section); var result = new List<string>();
        var pairs = ComparisonPairs(section).ToArray();
        foreach (var sheet in sheets)
        {
            var related = pairs.Where(p => ReferenceEquals(p.First, sheet) || ReferenceEquals(p.Second, sheet))
                .Select(p => ReferenceEquals(p.First, sheet) ? p.Second : p.First).ToArray();
            bool hasPile = related.Any(s => s.S("modulo_id") == PaloOrizzontale.Module);
            string name = sheet.S("nome"); var fields = Fields(sheet);
            if (sheet.S("modulo_id") is RebarMaterial.Module or "str_palo" or PaloOrizzontale.Module)
            {
                var steel = new JsonObject();
                foreach (string key in RebarMaterial.Keys) if (fields.TryGetValue(key, out var field)) steel[key] = field.Value?.DeepClone();
                if (RebarMaterial.Error(steel) is string error) result.Add(name + ": acciaio — " + error);
                if (sheet.S("modulo_id") == PaloOrizzontale.Module && steel.S("steel_diagramma") == "Incrudente")
                    result.Add(name + ": il momento resistente del palo usa il modello elastoplastico; fu ed εu sono conservati, ma l'incrudimento non è utilizzato dal calcolo del palo.");
            }
            if (sheet.S("modulo_id") == "str_palo" && hasPile &&
                (fields["shape"].Value?.ToString() != "Circolare" || (fields["barre_manuali"].Value as JsonArray)?.Count > 0 ||
                 fields.Any(p => p.Key.StartsWith("second_") && p.Key.EndsWith("enabled") && p.Value.Value?.ToString() is "true" or "True" or "Sì")))
                result.Add(name + ": forma o armatura avanzata non trasferibile al palo orizzontale; verificare separatamente la sezione resistente.");
            if (sheet.S("modulo_id") == MicropaloOrizzontale.Module && sheet["dati"]?["sezione"].S("modo_chs") == "Manuale" && related.Any(s => s.S("modulo_id") == "geo_micropalo_verticale"))
                result.Add(name + ": CHS manuale non rappresentabile nel catalogo del micropalo verticale.");
            if (sheet.S("modulo_id") == "geo_micropalo_verticale" && sheet["dati"]?["generali"].D("inclinazione") != 0)
                result.Add(name + ": lunghezza inclinata non collegata alla lunghezza del micropalo orizzontale.");
            if (sheet.S("modulo_id") is "str_palo" or PaloOrizzontale.Module && related.Any(s => s.S("modulo_id") == "mat_calcestruzzo") &&
                !ConcreteMaterialCatalog.Concrete().Any(c => Equal(c["fck_mpa"], fields["CLS · fck [MPa]"].Value)))
                result.Add(name + ": resistenza CLS personalizzata non rappresentabile nel selettore delle classi di Materiali.");
        }
        foreach (var (first, second) in pairs.Where(p => SupportsSharedSoils(p.First) && SupportsSharedSoils(p.Second)))
        {
            var a = first["dati"]!.AsObject().Array("stratigrafie");
            var b = second["dati"]!.AsObject().Array("stratigrafie");
            for (int survey = 0; survey < Math.Max(a.Count, b.Count); survey++)
            {
                if (survey >= a.Count || survey >= b.Count)
                {
                    var missing = survey >= a.Count ? first : second;
                    result.Add($"{first.S("nome")} / {second.S("nome")}: sondaggio {survey + 1} assente in {missing.S("nome")}.");
                    continue;
                }
                int ac = (a[survey] as JsonArray)?.Count ?? 0, bc = (b[survey] as JsonArray)?.Count ?? 0;
                for (int layer = Math.Min(ac, bc); layer < Math.Max(ac, bc); layer++)
                {
                    var missing = layer >= ac ? first : second;
                    result.Add($"{first.S("nome")} / {second.S("nome")}: sondaggio {survey + 1}, strato {layer + 1} assente in {missing.S("nome")}. I dati degli altri strati sono confrontati normalmente.");
                }
            }
        }
        return result;
    }
}
