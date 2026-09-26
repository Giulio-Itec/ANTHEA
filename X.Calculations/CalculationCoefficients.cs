using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record CalculationCoefficient(string Key, string Label, string Path, JsonNode? Value, string Scope);

/// <summary>Authoritative coefficient paths and meanings. Sharing never infers a conversion between standards.</summary>
public static class CalculationCoefficients
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Labels = new(() => ModuleCatalog.All
        .SelectMany(m => Read(m.Id, m.NewData())).GroupBy(c => c.Key).ToDictionary(g => g.Key, g => g.First().Label));
    public static string? Label(string key) => Labels.Value.GetValueOrDefault(key);
    public static bool Active(string module, JsonObject data, string key)
    {
        if (module == "str_palo" && key.Contains("Prestress")) return data["workspace_ca"].Array("trefoli").Count > 0;
        if (module != BridgeSection.Module) return true;
        return key switch {
            "Ponte · gamma_v" => data.B("pioli"),
            "Ponte · gamma_m2" => data.B("irrigidimenti") && data.B("saldature_irr") || data.B("appoggio") && data.B("saldature_app", true),
            "Ponte · gamma_ff" or "Ponte · gamma_mf_pioli" => data.B("pioli") && data.B("fatica_pioli"),
            "Ponte · gamma_mf_flangia" => data.B("pioli") && data.B("fatica_pioli") && data.B("flangia_fat_tesa"),
            _ => true
        };
    }
    public static string Standard(string module, JsonObject data) => module switch {
        "str_palo" => data["workspace_ca"].S("normativa", "NTC 2018"),
        BridgeSection.Module => data.S("normativa", BridgeSection.Standards[0]).StartsWith("NTC 2018") ? "NTC 2018" : data.S("normativa"),
        RebarMaterial.Module => "Assegnato", _ => "NTC 2018"
    };
    public static IReadOnlyList<CalculationCoefficient> Read(string module, JsonObject data)
    {
        var result = new List<CalculationCoefficient>();
        string standard = Standard(module, data);
        void Add(string key, string label, string path, JsonNode? fallback = null, string? scope = null)
        {
            JsonNode? value = data;
            foreach (string segment in path.Split('/')) value = (value as JsonObject)?[segment];
            result.Add(new(key, label, path, (value ?? fallback)?.DeepClone(), scope ?? standard));
        }
        if (module is "str_palo" or PaloOrizzontale.Module or BridgeSection.Module)
        {
            string prefix = module == "str_palo" ? "input/" : module == PaloOrizzontale.Module ? "sezione/" : "";
            var defaults = module == BridgeSection.Module ? BridgeSection.Defaults() : SezioneCA.DefaultInput();
            foreach (var (key, label) in new[] { ("alpha_cc", "αcc · resistenza del calcestruzzo a compressione"),
                ("gamma_c", "γc · resistenza del calcestruzzo"), ("gamma_s", "γs · resistenza delle armature") })
                Add(key, label, prefix + key, defaults[key]);
        }
        if (module == "str_palo")
        {
            var defaults = ConcreteStandards.Defaults(ConcreteStandards.Names.Contains(standard) ? standard : "NTC 2018");
            var common = ConcreteCalculationSettings.CommonCoefficients.Select(c => c.Standard).ToHashSet();
            foreach (var (key, label) in ConcreteStandards.Coefficients.Where(c => !common.Contains(c.Key)))
                Add("CA · " + key, label, "workspace_ca/coefficienti/" + key, defaults[key]);
        }
        if (module == RebarMaterial.Module) Add("gamma_s", "γs · resistenza delle armature", "input/gamma_s", JsonValue.Create("1.15"));
        if (module == BridgeSection.Module)
        {
            var defaults = BridgeSection.Defaults();
            foreach (var (key, value) in BridgeSection.AccessoryDefaults().Concat(BridgeSection.DetailDefaults())) defaults[key] = value?.DeepClone();
            foreach (var (key, label) in new[] {
                ("gamma_m0", "γM0 · resistenza della carpenteria"), ("gamma_m1", "γM1 · instabilità della carpenteria"),
                ("gamma_m2", "γM2 · saldature"), ("gamma_v", "γV · connessione a pioli"), ("gamma_ff", "γFf · azioni di fatica"),
                ("gamma_mf_pioli", "γMf,s · resistenza a fatica dei pioli"), ("gamma_mf_flangia", "γMf · resistenza a fatica della flangia") })
                Add("Ponte · " + key, label, key, defaults[key]);
        }
        if (module == MicropaloOrizzontale.Module)
            Add("CHS · gamma_m0", "γM0 · resistenza della sezione CHS", "sezione/gamma_m0", JsonValue.Create("1.05"));
        if (module is "geo_palo_verticale" or "geo_micropalo_verticale")
        {
            var defaults = CalculationDefaults.Vertical(module.Contains("micropalo"))["generali"]!;
            foreach (var (key, label) in new[] {
                ("sicurezza_laterale_compressione", "γs,c · resistenza laterale a compressione"),
                ("sicurezza_laterale_trazione", "γs,t · resistenza laterale a trazione"), ("sicurezza_base", "γb · resistenza alla base"),
                ("peso_palo_sfavorevole", "γG · peso proprio sfavorevole"), ("peso_palo_favorevole", "γG · peso proprio favorevole") })
                Add("Geotecnica · " + key, label, "generali/" + key, defaults[key], standard + "/verticale");
        }
        return result;
    }
    public static bool Compatible(CalculationCoefficient left, CalculationCoefficient right) => left.Key == right.Key &&
        (left.Scope == right.Scope || left.Key == "gamma_s" && (left.Scope == "Assegnato" || right.Scope == "Assegnato"));

    public static void Synchronize(string module, JsonObject data)
    {
        // Existing archive aliases are caches, never a second independent input.
        if (module != "str_palo" || data["input"] is not JsonObject input || data["workspace_ca"]?["coefficienti"] is not JsonObject options) return;
        foreach (var (key, alias) in ConcreteCalculationSettings.CommonCoefficients) options[alias] = input[key]?.DeepClone();
    }
}

public sealed record CalculationInputIssue(string Path, string Label, string Message);
public static class CalculationValidation
{
    public static IReadOnlyList<CalculationInputIssue> Coefficients(string module, JsonObject data)
    {
        var issues = CalculationCoefficients.Read(module, data)
        .Where(c => CalculationCoefficients.Active(module, data, c.Key))
        .Where(c => J.Number(c.Value) is not > 0)
        .Select(c => new CalculationInputIssue(c.Path, c.Label, c.Label + ": inserire un numero finito positivo.")).ToList();
        if (module == "str_palo" && !ConcreteStandards.Names.Contains(CalculationCoefficients.Standard(module, data)) ||
            module == BridgeSection.Module && !BridgeSection.Standards.Contains(data.S("normativa", BridgeSection.Standards[0])))
            issues.Add(new("normativa", "Normativa", "Normativa non riconosciuta per il modulo selezionato."));
        return issues;
    }

    public static void RequireValidCoefficients(string module, JsonObject data)
    {
        var issues = Coefficients(module, data);
        if (issues.Count > 0) throw new ArgumentException(string.Join("\n", issues.Select(i => i.Message)));
    }
}
