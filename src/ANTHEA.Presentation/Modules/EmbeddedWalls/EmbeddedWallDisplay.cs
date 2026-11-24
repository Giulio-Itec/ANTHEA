using System.Globalization;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// How the paratie module shows its numbers and its terms (cifre significative, 16/11). Presentation only: the documents, the results, the
/// checks, the report and the CSV files keep their values.
/// <para>Numbers: the rule of <see cref="Forms.NumericDisplay"/> of ANTHEA (fixed point, at most a few decimals, no negative zero, the
/// culture of the application, that is the decimal separator chosen in «Aspetto»), with the decimals of the quantity instead of the two of
/// every field: quotes in m 3 (mm), displacements in mm 2, rotations in rad 5, forces, moments, pressures and loads 2, MPa 3, angles 2,
/// pure numbers (utilisations, factors, gradients) 3, flows in L/s/m 4; permeabilities and velocities in m/s as a power of ten (1,5·10⁻⁷);
/// in the tables of the results fixed decimals with the final zeros (approved on 16/11). A value that rounds to zero at
/// that precision (the noise of the solver, 4,25E-10 kNm) shows 0, never «-0»; no exponential notation. Missing values show «—».</para>
/// <para>Terms: the values of the results and the choices of the forms written in English (Below, Anchor, PermanentStructural…) show an
/// Italian label; the value kept stays the same.</para>
/// </summary>
public static class EmbeddedWallDisplay
{
    /// <summary>Text of a missing value.</summary>
    public const string Missing = "—";

    /// <summary>Decimals shown for a unit as the module writes it; an unknown unit has those of a pure number.</summary>
    public static int Decimals(string? unit) => (unit ?? "").Trim() switch
    {
        "m" => 3,
        "mm" => 2,
        "rad" => 5,
        "kN" or "kN/m" or "kNm" or "kNm/m" or "kPa" or "kN/m²" or "kN/m³" or "N" or "Nmm" or "kNm²" or "kNm²/m" or "°" => 2,
        "MPa" => 3,
        "L/s/m" or "mm²/s" => 4,
        "m/s" => 10,
        "%" => 1,
        _ => 3
    };

    /// <summary>
    /// A number with the decimals of its unit (<see cref="Decimals"/>). In the tables of the results (<paramref name="table"/>, approved on 16/11)
    /// the decimals are fixed, with the final zeros (110,90 next to 111,09); elsewhere the rule of ANTHEA drops them. A velocity or permeability in
    /// m/s shows as a power of ten (<see cref="Scientific"/>).
    /// </summary>
    public static string Number(double? value, string? unit, bool table = false) =>
        value is not double v ? Missing : (unit ?? "").Trim() == "m/s" ? Scientific(v) : Number(v, Decimals(unit), table);

    /// <summary>A number with at most (or, in a table, exactly) <paramref name="decimals"/> decimals in the culture of the application; zero when
    /// it rounds to zero, never «-0».</summary>
    public static string Number(double value, int decimals, bool table = false)
    {
        if (!double.IsFinite(value)) return Missing;
        int places = Math.Clamp(decimals, 0, 15);
        double rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
        string format = places == 0 ? "0" : "0." + new string(table ? '0' : '#', places);
        return rounded == 0 ? (table ? 0d.ToString(format, CultureInfo.CurrentCulture) : "0") : rounded.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// A value in m/s (permeability, filtration velocity) as a power of ten: mantissa with two significant digits, «·10» and the exponent in
    /// superscript, the decimal separator of the application (1,5·10⁻⁷, approved on 16/11). Zero shows 0, a missing value «—».
    /// </summary>
    public static string Scientific(double? value)
    {
        if (value is not double v || !double.IsFinite(v)) return Missing;
        if (v == 0) return "0";
        string text = v.ToString("0.0E+0", CultureInfo.InvariantCulture);
        int mark = text.IndexOf('E');
        string mantissa = text[..mark].Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
        string exponent = text[(mark + 1)..].TrimStart('+');
        return exponent == "0" ? mantissa : mantissa + "·10" + new string(exponent.Select(c => Superscript(c)).ToArray());
    }

    private static char Superscript(char c) => c switch
    {
        '-' => '⁻', '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴', '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', '9' => '⁹', _ => c
    };

    /// <summary>
    /// The value of a field of a form as the hints, the tree and the table «Dati e fasi» show it: a boolean as sì/no, a choice with its Italian
    /// label, a number in m/s (the permeabilities) as a power of ten; any other text as typed. The document keeps the value.
    /// </summary>
    public static string Field(Forms.FieldSpec field, string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Bool) return value == "true" ? "sì" : value == "false" ? "no" : value;
        if (field.Choices is not null) return Choice(field.Key, value);
        if (field.Unit == "m/s" && double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            return Scientific(number);
        return value;
    }

    /// <summary>The unit of a quantity of the diagrams («Momento [kNm]» → kNm), empty without brackets.</summary>
    public static string UnitOf(string? quantity)
    {
        if (string.IsNullOrEmpty(quantity)) return "";
        int open = quantity.LastIndexOf('['), close = quantity.LastIndexOf(']');
        return open >= 0 && close > open ? quantity[(open + 1)..close] : "";
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> terms = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
    {
        // Point of a result at a node: the value just below it, just above it, or inside an element (GPC: Below, Above, Interior).
        ["side"] = Map(("Below", "sotto"), ("Above", "sopra"), ("Interior", "interno")),
        ["kind"] = Map(("Anchor", "Tirante"), ("Strut", "Puntone"), ("Bilateral", "Vincolo bilaterale")),
        ["category"] = Map(("PermanentStructural", "G1"), ("PermanentNonStructural", "G2"), ("Variable", "Q")),
        ["search"] = Map(("NoSurface", "nessuna superficie"), ("Incomplete", "incompleta"), ("BoundaryMinimum", "minimo al bordo"),
            ("NotConverged", "non convergente"), ("Converged", "convergente")),
        ["seepage"] = Map(("Converged", "Convergente"), ("NonConvergent", "Non convergente"), ("IllConditioned", "Mal condizionata")),
    };

    /// <summary>The Italian label of a value of a result column (<paramref name="domain"/>: side, kind, category, search, seepage); other values unchanged.</summary>
    public static string Term(string? domain, string? value) =>
        value is not null && domain is not null && terms.TryGetValue(domain, out var map) && map.TryGetValue(value, out var label) ? label : value ?? "";

    /// <summary>
    /// Labels of the choices of the forms whose values are written in English, by key of the field and value (FieldForm.ChoiceLabels): the box
    /// shows the label, the document keeps the value. Keys shared by more tables have the same values in all of them.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ChoiceLabels { get; } = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
    {
        ["kind"] = terms["kind"],
        ["drainage"] = Map(("Drained", "Drenato, tensioni efficaci"), ("UndrainedTotalStress", "Non drenato, tensioni totali")),
        ["check_kind"] = Map(("Analisi", "Analisi"), ("Ultimate", "SLU"), ("Characteristic", "SLE caratteristica"), ("Frequent", "SLE frequente"),
            ("QuasiPermanent", "SLE quasi permanente")),
        ["rc_pile_check_kind"] = Map(("Analisi", "Analisi"), ("Ultimate", "SLU"), ("Characteristic", "SLE caratteristica"), ("Frequent", "SLE frequente"),
            ("QuasiPermanent", "SLE quasi permanente"), ("Seismic", "Sismica")),
        ["steel_check_kind"] = Steel(),
        ["tube_check_kind"] = Steel(),
        ["anchor_situation"] = Situation(),
        ["hydraulic_situation"] = Situation(),
        ["situation"] = Situation(),
        ["water_category"] = Category(),
        ["category"] = Category(),
        ["method"] = Map(("Mean", "Gradiente medio"), ("Exit", "Gradiente in uscita"), ("Loaded", "Colonna caricata")),
        ["evidence"] = Map(("CalculatedProfiles", "Profili d'indagine"), ("MeasuredAnchorTests", "Prove su tiranti")),
        ["soil_kind"] = Map(("Gravel", "Ghiaia"), ("SandyGravel", "Ghiaia sabbiosa"), ("GravellySand", "Sabbia ghiaiosa"), ("CoarseSand", "Sabbia grossa"),
            ("MediumSand", "Sabbia media"), ("FineSand", "Sabbia fine"), ("SiltySand", "Sabbia limosa"), ("Silt", "Limo"), ("Clay", "Argilla"),
            ("Marl", "Marna"), ("MarlyLimestone", "Calcare marnoso"), ("WeatheredLimestone", "Calcare alterato"), ("WeatheredRock", "Roccia alterata")),
        ["detailing_mode"] = Map(("CommonOnly", "Solo regole comuni"), ("Beam", "Trave"), ("Column", "Pilastro")),
        ["formation"] = Map(("HotFinished", "Finito a caldo"), ("ColdFormed", "Formato a freddo")),
    };

    /// <summary>The label of the value of a choice field (<see cref="ChoiceLabels"/>), otherwise the value as it is.</summary>
    public static string Choice(string key, string value) => ChoiceLabels.TryGetValue(key, out var map) && map.TryGetValue(value, out var label) ? label : value;

    private static IReadOnlyDictionary<string, string> Steel() =>
        Map(("Analysis", "Solo analisi"), ("StaticUltimate", "SLU statica"), ("SeismicUltimate", "SLU sismica"), ("Serviceability", "SLE"));
    private static IReadOnlyDictionary<string, string> Situation() => Map(("PersistentTransient", "Persistente o transitoria"), ("Seismic", "Sismica"));
    private static IReadOnlyDictionary<string, string> Category() =>
        Map(("PermanentStructural", "G1 · permanente strutturale"), ("PermanentNonStructural", "G2 · permanente non strutturale"), ("Variable", "Q · variabile"));
    private static IReadOnlyDictionary<string, string> Map(params (string Value, string Label)[] items) =>
        items.ToDictionary(i => i.Value, i => i.Label, StringComparer.Ordinal);
}
