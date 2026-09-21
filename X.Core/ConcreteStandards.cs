using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Model.Standards;

namespace X.Core;

/// <summary>Catalog of concrete implementations actually shipped in GPC.Model.dll, not a claim of full code compliance.</summary>
public static class ConcreteStandards
{
    public static readonly string[] Names = ["NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1", "CNR-DT 204/2006", "CS-TR34"];
    public static StandardModelCode2010 Create(string name) => name switch
    {
        "NTC 2018" => new StandardNTC2018Concrete(), "Model Code 2010" => new StandardModelCode2010(),
        "EN 1992-1-1" => new StandardEN1992p11(), "UNI EN 1992-1-1" => new StandardUNIEN1992p11(),
        "DIN EN 1992-1-1" => new StandardDINEN1992p11(), "DS EN 1992-1-1" => new StandardDSEN1992p11(),
        "NS EN 1992-1-1" => new StandardNSEN1992p11(), "CNR-DT 204/2006" => new StandardCNR204(), "CS-TR34" => new StandardCSTR34(),
        _ => throw new ArgumentException("Normativa non disponibile nelle DLL: " + name)
    };
    public static readonly (string Key, string Label)[] Coefficients =
    [
        ("AlphaCC", "αcc · compressione"), ("AlphaCT", "αct · trazione"), ("GammaC", "γc · CLS"), ("GammaS", "γs · armature"),
        ("GammaSPrestress", "γp · trefoli"), ("GammaCAccidental", "γc · accidentale"), ("GammaCE", "γcE · modulo"),
        ("GammaSAccidental", "γs · accidentale"), ("GammaSPrestressAccidental", "γp · accidentale"), ("GammaF", "γF · fibre (predisposizione)"),
        ("SteelCoefficientStrainTension", "kε · deformazione acciaio"),
        ("ServiceabilityStressConcreteCoefficientForCharacteristicCombination", "SLE rara · σc / fck"),
        ("ServiceabilityStressConcreteCoefficientForQuasiPermanentCombination", "SLE QP · σc / fck"),
        ("ServiceabilityStressSteelCoefficientForCharacteristicCombination", "SLE rara · σs / fyk"),
        ("ServiceabilityStressPrestressSteelCoefficientForCharacteristicCombination", "SLE rara · σp / fpyk (DLL)")
    ];
    public static JsonObject Defaults(string name)
    {
        var standard = Create(name); var result = new JsonObject();
        foreach (var (key, _) in Coefficients) result[key] = ((double)typeof(StandardModelCode2010).GetProperty(key)!.GetValue(standard)!).ToString("G12", CultureInfo.InvariantCulture);
        return result;
    }
    public static StandardModelCode2010 Effective(JsonObject input, JsonObject workspace)
    {
        var standard = Create(workspace.S("normativa", "NTC 2018"));
        if (workspace["coefficienti"] is JsonObject overrides)
            foreach (var (key, label) in Coefficients)
                if (overrides.ContainsKey(key) && (!key.Contains("Prestress") || workspace.Array("trefoli").Count > 0))
                {
                    double value = SectionWorkspace.Number(overrides.S(key), label);
                    if (value <= 0) throw new ArgumentException(label + ": inserire un valore positivo.");
                    typeof(StandardModelCode2010).GetProperty(key)!.SetValue(standard, value);
                }
        // Existing sheets keep these three authoritative input values, also shown in the coefficients form.
        standard.AlphaCC = input.Required("alpha_cc", strict: true);
        standard.GammaC = input.Required("gamma_c", strict: true);
        standard.GammaS = input.Required("gamma_s", strict: true);
        return standard;
    }
    public static string Note(string name) => name switch
    {
        "NTC 2018" => "NTC 2018 · coefficienti della DLL; taglio e fessurazione nei limiti documentati del modulo. Coefficienti modificabili: verificare eventuali deroghe.",
        "UNI EN 1992-1-1" => "UNI EN 1992-1-1 (riferimento DLL: 2005): la classe eredita i coefficienti EC2 base; NON costituisce un annesso nazionale italiano completo.",
        "CNR-DT 204/2006" => "CNR-DT 204/2006 AC:2008: classe disponibile, coefficienti base Model Code. Materiali fibrorinforzati e verifiche specifiche FRC non implementati in questa interfaccia.",
        "CS-TR34" => "CS-TR34: coefficienti della DLL. Verifiche specifiche di pavimentazioni industriali/FRC non implementate in questa interfaccia.",
        "Model Code 2010" => "fib Model Code 2010: motore e coefficienti della DLL. Non è Model Code 2020; taglio e fessurazione specifici da implementare.",
        "EN 1992-1-1" => "EC2 di prima generazione, riferimento DLL EN 1992-1-1:2004/AC:2010. Taglio e fessurazione specifici da implementare.",
        _ => name + ": collegata la classe nazionale della DLL; non tutti i parametri e le regole dell’annesso sono implementati. Verificare edizione e coefficienti. Taglio e fessurazione specifici da implementare."
    };
}

public static class EngineeringFormat
{
    public static string Number(double? value)
    {
        if (value is not double v || !double.IsFinite(v)) return "—";
        return v == 0 || Math.Abs(v) >= .01 ? v.ToString("0.00", CultureInfo.CurrentCulture) : v.ToString("0.00E+0", CultureInfo.CurrentCulture);
    }
}
