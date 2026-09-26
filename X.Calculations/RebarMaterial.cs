using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Reinforcing steel only. Historical A5 values must never become constitutive strains.</summary>
public static class RebarMaterial
{
    public const string Module = "mat_acciaio_armatura";
    public static readonly string[] Keys = ["classe_acciaio", "materiale_acciaio_nome", "fyk_mpa", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "gamma_s"];
    public sealed record Preset(string Name, string Surface, double? A5, JsonObject Input);
    public static Preset[] Catalog()
    {
        var current = ConcreteMaterialCatalog.Steel(false, "NTC 2018").OrderBy(m => m.S("nome") == "B450C" ? 0 : 1)
            .Select(m => { var input = (JsonObject)m.DeepClone(); input.Remove("nome"); return new Preset(m.S("nome"), "Aderenza migliorata", null, input); });
        // D.M. 9 January 1996, part I, section I, tables 1-I and 2-I.
        // These are nominal historical minima, not measured properties of an existing structure.
        Preset Historic(string name, double fy, double fu, double a5, bool smooth) => new(name,
            smooth ? "Barre lisce" : "Aderenza migliorata", a5,
            J.Obj(("classe_acciaio", "Personalizzato"), ("materiale_acciaio_nome", name), ("fyk_mpa", fy),
                ("steel_fu_mpa", fu), ("steel_modulus_mpa", 200000), ("steel_eps_u", ""), ("steel_diagramma", "Elastoplastico")));
        return [.. current, Historic("FeB22k", 215, 335, 24, true), Historic("FeB32k", 315, 490, 23, true),
            Historic("FeB38k", 375, 450, 14, false), Historic("FeB44k", 430, 540, 12, false)];
    }
    public static JsonObject Defaults()
    {
        var input = (JsonObject)Catalog().First().Input.DeepClone(); input["gamma_s"] = "1.15";
        return J.Obj(("versione_acciaio", 1), ("input", input), ("riferimento", ""));
    }
    public static Preset? Match(JsonObject input) => Catalog().FirstOrDefault(p =>
        p.Input.All(v => p.A5 is not null && v.Key is "steel_eps_u" or "steel_diagramma" || J.Equivalent(v.Value, input[v.Key])));
    public static void Select(JsonObject input, string name)
    {
        if (name == "Personalizzato")
        { input["classe_acciaio"] = "Personalizzato"; input["materiale_acciaio_nome"] = "Acciaio personalizzato"; return; }
        foreach (var (key, value) in Catalog().Single(p => p.Name == name).Input) input[key] = value?.DeepClone();
    }
    // Match the existing calculation fallbacks without replacing any explicitly saved value.
    public static void CompleteLegacyInput(JsonObject input)
    {
        if (!input.ContainsKey("classe_acciaio")) input["classe_acciaio"] = "Personalizzato";
        if (!input.ContainsKey("materiale_acciaio_nome")) input["materiale_acciaio_nome"] = "Armatura";
        if (!input.ContainsKey("steel_fu_mpa")) input["steel_fu_mpa"] = input["fyk_mpa"]?.DeepClone();
        if (!input.ContainsKey("steel_eps_u")) input["steel_eps_u"] = "100";
        if (!input.ContainsKey("steel_diagramma")) input["steel_diagramma"] = "Elastoplastico";
    }
    public static void ValidateShape(JsonObject data)
    {
        if (data.D("versione_acciaio") != 1 || data["input"] is not JsonObject input ||
            Keys.Any(k => input[k] is not JsonValue) || data["riferimento"] is JsonObject or JsonArray)
            throw new ArgumentException("Formato della scheda acciaio non valido.");
    }
    public sealed record Values(double Fy, double Fu, double E, double Gamma, double EpsilonU)
    {
        public double Fyd => Fy / Gamma;
        public double EpsilonY => 1000 * Fy / E;
        public double EpsilonYd => 1000 * Fyd / E;
        public double Ratio => Fu / Fy;
    }
    public static Values Evaluate(JsonObject input)
    {
        double Positive(string key, string label)
        {
            double? number = J.Number(input[key]);
            if (number is not double v || !double.IsFinite(v) || v <= 0) throw new ArgumentException(label + ": inserire un numero maggiore di zero.");
            return v;
        }
        var values = new Values(Positive("fyk_mpa", "fyk"), Positive("steel_fu_mpa", "fu"), Positive("steel_modulus_mpa", "Es"),
            Positive("gamma_s", "γs"), Positive("steel_eps_u", "εu [‰]"));
        if (values.Fu < values.Fy) throw new ArgumentException("fu deve essere maggiore o uguale a fyk.");
        if (values.Gamma < 1) throw new ArgumentException("γs deve essere maggiore o uguale a 1.");
        if (values.EpsilonU <= values.EpsilonY) throw new ArgumentException("εu deve essere maggiore di fyk / Es (espresso in ‰).");
        if (input.S("steel_diagramma") is not ("Incrudente" or "Elastoplastico")) throw new ArgumentException("Scegliere il diagramma dell'acciaio.");
        return values;
    }
    public static string? Error(JsonObject input) { try { Evaluate(input); return null; } catch (ArgumentException ex) { return ex.Message; } }
    public static List<double[]> Curve(JsonObject input)
    {
        var v = Evaluate(input);
        return [[0, 0], [v.EpsilonY, v.Fy], [v.EpsilonU, input.S("steel_diagramma") == "Incrudente" ? v.Fu : v.Fy]];
    }
}
