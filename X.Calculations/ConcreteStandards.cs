using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>Catalog of concrete implementations actually shipped in GPC.Model.dll, not a claim of full code compliance.</summary>
public static class ConcreteStandards
{
    public static readonly string[] Names = ["NTC 2018", "Model Code 2010", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1", "CNR-DT 204/2006", "CS-TR34"];
    public static readonly string[] OrdinaryNames = Names.Where(n => n is not ("CNR-DT 204/2006" or "CS-TR34")).ToArray();
    public static StandardModelCode2010 Create(string name) => name switch
    {
        "NTC 2018" => new StandardNTC2018Concrete(), "Model Code 2010" => new StandardModelCode2010(),
        "EN 1992-1-1" => new StandardEN1992p11(), "UNI EN 1992-1-1" => new StandardUNIEN1992p11 { AlphaCC = .85 },
        "DIN EN 1992-1-1" => new StandardDINEN1992p11 { AlphaCT = .85 }, "DS EN 1992-1-1" => new StandardDSEN1992p11(),
        "NS EN 1992-1-1" => new StandardNSEN1992p11 { AlphaCT = .85, SteelCoefficientStrainTension = .4 }, "CNR-DT 204/2006" => new StandardCNR204(), "CS-TR34" => new StandardCSTR34(),
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
        "UNI EN 1992-1-1" => "EC2 prima generazione, appendice italiana DM 31/07/2012: taglio con ν nazionale; criteri di fessurazione italiani e formule EC2. αcc predefinito 0,85. Le verifiche locali dell’elemento richiedono dati aggiuntivi.",
        "CNR-DT 204/2006" => "CNR-DT 204/2006 AC:2008: classe disponibile, coefficienti base Model Code. Materiali fibrorinforzati e verifiche specifiche FRC non implementati in questa interfaccia.",
        "CS-TR34" => "CS-TR34: coefficienti della DLL. Verifiche specifiche di pavimentazioni industriali/FRC non implementate in questa interfaccia.",
        "Model Code 2010" => "fib Model Code 2010: taglio livello II dipendente da N, M, V, Asl e aggregato; apertura fessure da lunghezza di trasferimento. Assegnare wlim di progetto. Non è Model Code 2020. Torsione e dettagli specifici MC non ancora coperti.",
        "EN 1992-1-1" => "EN 1992-1-1:2004/AC:2010, prima generazione: taglio §§6.2.2–3; fessurazione §7.3.4, combinazione quasi permanente. Non è EN 1992-1-1:2023.",
        "DIN EN 1992-1-1" => "EC2 con parametri DIN: CRd,c, vmin, ν1, limite di cotθ dipendente dal carico e z ridotto; hc,eff e distanza fessure nazionali. Riferimento materiali DLL NA:2013-04. Verifiche di sezione, senza certificazione dell’intero annesso.",
        "DS EN 1992-1-1" => "DK NA:2024: vmin, ν1, k3 e area efficace nazionali; sistemi fine/grossolano in trazione. Taglio con cotθ ≤ 2 e acciaio B/C. Esposizioni non tabulate: assegnare wlim. Non include verifiche locali di giunti/appoggi.",
        "NS EN 1992-1-1" => "NS NA prima generazione: aggregato, tensione assiale e limite C60 nel taglio; fessurazione secondo combinazione nazionale. wlim usa kc = 1 senza maggiorazione favorevole del copriferro. Coefficienti salvati restano personalizzazioni del foglio.",
        _ => name
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
