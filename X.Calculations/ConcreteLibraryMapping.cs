using System.Collections.Frozen;
using System.Text.RegularExpressions;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using GPC.Model.Standards;
// ANTHEA ha già Anthea.Calculations.Homogenization: la classe omonima della libreria si nomina solo con l'alias (progetto F2.7 §2.4 punto 6).
using LibraryHomogenization = GPC.Checkers.Concrete.Serviceability.Homogenization;

namespace Anthea.Calculations;

/// <summary>
/// Strato di mappatura fra il contratto di ANTHEA e GPCChecker.Concrete per taglio e torsione della sezione (refactoring F2.5,
/// docs/refactoring/piano.md). ANTHEA: forze in kN, momenti in kNm, norme per nome, testi italiani nel JSON ('taglio', 'torsione')
/// e nelle relazioni. Libreria: N e N·mm, classi <see cref="Standard"/> per tipo esatto, testi inglesi.
/// Il file contiene soltanto conversioni di unità con nome e testi: nessuna costante normativa, nessuna formula. Ogni testo inglese
/// della libreria che raggiunge ANTHEA (stati, riferimenti, modelli, espressioni dei dettagli, messaggi di rifiuto) è sostituito dal
/// testo del motore legacy; un testo senza traduzione è un errore di programma (<see cref="InvalidOperationException"/>), coperto
/// dalle prove di tests/ConcreteLibraryAdapter.Checks.
/// Nessuno stato statico modificabile: le tabelle dei testi sono <see cref="FrozenDictionary{TKey, TValue}"/> (prova 5l di
/// tests/ConcreteLibraryAdapter.Checks).
/// Dal refactoring F2.7b (commit A3) anche la regola dei getti sottili: il fattore viene da <see cref="ThinCasting"/> della libreria.
/// </summary>
public static class ConcreteLibraryMapping
{
    // ------------------------------------------------------------------ unità
    const double NewtonsPerKilonewton = 1000;
    const double NewtonMillimetresPerKilonewtonMetre = 1e6;

    public static double NewtonsFromKilonewtons(double kilonewtons) => kilonewtons * NewtonsPerKilonewton;
    public static double KilonewtonsFromNewtons(double newtons) => newtons / NewtonsPerKilonewton;
    public static double NewtonMillimetresFromKilonewtonMetres(double kilonewtonMetres) => kilonewtonMetres * NewtonMillimetresPerKilonewtonMetre;
    public static double KilonewtonMetresFromNewtonMillimetres(double newtonMillimetres) => newtonMillimetres / NewtonMillimetresPerKilonewtonMetre;

    // ------------------------------------------------------------------ norme
    /// <summary>Nome della norma di ANTHEA → classe della libreria. Il taglio e la torsione della sezione sono ammessi soltanto per
    /// le norme del calcestruzzo ordinario (<see cref="ConcreteStandards.OrdinaryNames"/>), con il messaggio del legacy
    /// (ConcreteCodeChecks.RequireOrdinary): la libreria accetterebbe anche CNR-DT 204 e CNR-DT 200.</summary>
    public static Standard StandardFor(string name)
    {
        if (!ConcreteStandards.OrdinaryNames.Contains(name))
            throw new ArgumentException(name + ": modello FRC/pavimentazioni escluso; scegliere una norma per calcestruzzo ordinario.");
        return ConcreteStandards.Create(name);
    }

    /// <summary>Nome della norma della torsione accoppiata: il contratto attuale di ANTHEA è solo NTC 2018 (ConcreteShearAnalysis).</summary>
    public const string TorsionStandardName = "NTC 2018";

    // ------------------------------------------------------------------ getti sottili
    /// <summary>Regola dei getti sottili di ANTHEA (decisione F2.7-D2): la regola predefinita della libreria, con i valori usati da
    /// ANTHEA prima di F2.7 (riduzione solo per NTC 2018). L'estensione a UNI EN 1992-1-1 con l'appendice italiana (scostamento R22,
    /// proposta U3) è una decisione dell'utente e si applica dopo l'interruttore, non qui.</summary>
    public const ThinCastingRule ThinCastingRuleOfAnthea = ThinCastingRule.Ntc2018Only;

    /// <summary>Fattore del getto sottile (piano gettato in opera di spessore inferiore a 50 mm) per la norma dell'analisi, riconosciuta
    /// dalla classe effettiva (<see cref="ConcreteStandards.Effective"/>, coefficienti personalizzati compresi). Unica fonte del limite SLE
    /// del calcestruzzo e di αcc (<see cref="CheckerSection"/>) e di fcd (<see cref="ConcreteMaterials.DesignValues"/>), refactoring F2.7b,
    /// commit A3 (rilievo M14). Un fattore uguale a 1 vuol dire che la norma non riduce; il chiamante lo applica solo ai getti sottili
    /// ('gettato_sottile' = «Sì»).</summary>
    public static double ThinCastingFactor(Standard standard) => ThinCasting.Factor(standard, ThinCastingRuleOfAnthea);

    // ------------------------------------------------------------------ taglio
    /// <summary>Risultato della libreria → DTO del JSON 'taglio' e delle relazioni (resistenze in kN, testi italiani).
    /// Il profilo NTC 2018 del legacy non ha traccia: i dettagli della libreria per NTC non sono esposti.</summary>
    public static Ntc2018Checks.ShearResult ToShearResult(SectionShearResult r, string standardName)
        => new(KilonewtonsFromNewtons(r.VRsd), KilonewtonsFromNewtons(r.VRcd), KilonewtonsFromNewtons(r.VRd), r.Ratio, r.CotTheta, ShearStatus(r))
        {
            Reference = ShearReference(r.Profile, standardName),
            Model = ShearModel(r.Profile),
            Details = r.Profile == ShearProfile.Ntc2018 ? Array.Empty<CrackCalculationDetail>() : r.Details.Select(ShearDetail).ToArray()
        };

    public static string ShearReference(ShearProfile profile, string standardName) => profile switch
    {
        ShearProfile.Ntc2018 => "NTC 2018 §§4.1.2.3.5.1–2",
        ShearProfile.ModelCode2010 => "fib MC2010 · livello II · §§7.3.3–7.3.4",
        ShearProfile.EN1992p11 or ShearProfile.UniEN1992p11 or ShearProfile.DinEN1992p11 or ShearProfile.DsEN1992p11 or ShearProfile.NsEN1992p11
            => standardName + " · §§6.2.2–6.2.3",
        _ => throw Unmapped("riferimento del taglio", profile.ToString())
    };

    public static string ShearModel(ShearProfile profile) => profile switch
    {
        ShearProfile.Ntc2018 => "NTC · traliccio a inclinazione variabile",
        ShearProfile.ModelCode2010 => "Model Code 2010 · livello II",
        ShearProfile.EN1992p11 or ShearProfile.UniEN1992p11 or ShearProfile.DinEN1992p11 or ShearProfile.DsEN1992p11 or ShearProfile.NsEN1992p11
            => "Eurocodice 2 · prima generazione",
        _ => throw Unmapped("modello del taglio", profile.ToString())
    };

    public static string ShearStatus(SectionShearResult r) => r.Status switch
    {
        "Resistance sufficient; detailing to be verified" => "Resistenza sufficiente · dettagli da verificare",
        "Resistance insufficient" => "Resistenza insufficiente",
        "Zero resistance" => r.Profile == ShearProfile.Ntc2018 ? "Resistenza nulla / fuori campo" : "Resistenza nulla",
        "Tension without shear reinforcement: not verified automatically" => "Trazione: taglio senza staffe non verificato automaticamente",
        _ => throw Unmapped("stato del taglio", r.Status)
    };

    /// <summary>Espressioni dei dettagli della libreria → testi della traccia del legacy (simboli e unità coincidono, salvo N → kN).</summary>
    static readonly FrozenDictionary<string, string> ShearExpressions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["z/d · d"] = "z/d · d",
        ["−N/Ac, compression positive"] = "−N / Ac; compressione positiva",
        ["max[0; (|M|/z + |V| + N(1/2 + Δe/z))/(2 Es Asl)]"] = "max[0; (|M|/z + |V| + N(1/2 + Δe/z))/(2 Es Asl)]",
        ["0 for fck > 70 MPa"] = "0 per fck > 70 MPa",
        ["max[0.75; 32/(16+dg)]"] = "max[0,75; 32/(16+dg)]",
        ["0.4/(1+1500 εx) · 1300/(1000+kdg z)"] = "0,4/(1+1500 εx) · 1300/(1000+kdg z)",
        ["min[2;1+√(200/d)]"] = "min[2;1+√(200/d)]",
        ["min[0.02;Asl/(bw d)]"] = "min[0,02;Asl/(bw d)]",
        ["Coefficient of the standard"] = "Coefficiente della norma",
        ["Coefficient of σcp"] = "Coefficiente di σcp",
        ["Minimum of the standard"] = "Minimo della norma",
        ["0.24 fck^(1/3) (1−1.2 σcp/fcd) bw z"] = "0,24 fck^(1/3) (1−1,2 σcp/fcd) bw z",
        ["Cautious limit 2, also with curtailed reinforcement; steel B/C"] = "Limite cautelativo 2, anche con armatura interrotta; acciaio B/C",
        ["Limit of the standard"] = "Limite della norma",
        ["Assigned"] = "Assegnato",
        ["Maximises min(VRd,s; VRd,max) within the admissible range"] = "Massimizza min(VRd,s;VRd,max) nell’intervallo ammesso",
        ["Strut reduction"] = "Riduzione del puntone",
        ["Non-prestressed section"] = "Sezione non precompressa"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static CrackCalculationDetail ShearDetail(ShearCalculationDetail d)
    {
        string expression = ShearExpressions.TryGetValue(d.Expression, out var text) ? text : throw Unmapped("espressione del taglio " + d.Symbol, d.Expression);
        return d.Unit == "N" ? new(d.Symbol, KilonewtonsFromNewtons(d.Value), "kN", expression) : new(d.Symbol, d.Value, d.Unit, expression);
    }

    static readonly FrozenDictionary<string, string> ShearMessages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Shear: invalid geometry, materials, actions or shear-reinforcement inclination."] = "Taglio: geometria, materiali, carichi o inclinazione delle staffe non validi.",
        ["Shear: concrete above C90/105 is outside the implemented range."] = "Taglio: calcestruzzo oltre C90/105 fuori dal campo implementato.",
        ["NTC 2018: cot θ must be within [1; 2.5]."] = "NTC: cot θ deve essere tra 1 e 2,5.",
        ["Model Code 2010: an effective Asl is required, also with shear reinforcement."] = "Model Code: occorre Asl efficace anche in presenza di staffe.",
        ["DIN: strut inclination out of range with this axial tension."] = "DIN: inclinazione del puntone fuori campo con questa trazione assiale."
    }.ToFrozenDictionary(StringComparer.Ordinal);
    // Gli estremi sono già formattati dalla libreria con la cultura corrente, come nel messaggio del legacy.
    static readonly Regex ShearCotRange = new(@"^Shear: cot θ outside \[(?<min>[^;\]]+); (?<max>[^;\]]+)\]\.$", RegexOptions.CultureInvariant);

    /// <summary>Rifiuto della libreria → stesso rifiuto con il messaggio del legacy.</summary>
    public static ArgumentException ShearError(ArgumentException e, string standardName)
    {
        if (ShearMessages.TryGetValue(e.Message, out var text)) return new ArgumentException(text);
        var m = ShearCotRange.Match(e.Message);
        if (m.Success) return new ArgumentException($"{standardName}: cot θ fuori intervallo [{m.Groups["min"].Value}; {m.Groups["max"].Value}].");
        throw Unmapped("rifiuto del taglio", e.Message);
    }

    // ------------------------------------------------------------------ torsione
    public const string TorsionInputMessage = "Torsione: controllare geometria, armature, materiali e 1 ≤ cot θ ≤ 2,5.";
    public const string TorsionCotMessage = "Taglio e torsione devono usare lo stesso cot θ.";
    public const string TorsionShapeMessage = "Torsione: profilo periferico automatico per rettangolare o circolare.";
    public const string TorsionThicknessMessage = "Torsione: spessore resistente insufficiente per contenere le armature periferiche.";
    public const string TorsionConcreteClassMessage = "Torsione: calcestruzzo oltre C90/105 fuori dal campo implementato.";

    /// <summary>Risultato della libreria → DTO del JSON 'torsione' e delle relazioni (resistenze in kNm, testi italiani).</summary>
    public static TorsionResult ToTorsionResult(SectionTorsionResult r, TorsionGeometry geometry)
        => new(KilonewtonMetresFromNewtonMillimetres(r.TRcd), KilonewtonMetresFromNewtonMillimetres(r.TRsd), KilonewtonMetresFromNewtonMillimetres(r.TRld),
            KilonewtonMetresFromNewtonMillimetres(r.TRd), r.TorsionRatio, r.ConcreteInteraction, r.LinkInteraction, r.RequiredLongitudinalArea,
            r.Verdict == TorsionVerdict.Satisfied, TorsionStatus(r)) { Geometry = geometry, CotTheta = r.CotTheta };

    public static string TorsionStatus(SectionTorsionResult r) => r.Status switch
    {
        "Torsion and interaction satisfied in the assigned model" => "Torsione e interazione soddisfatte nel modello assegnato",
        "Torsion not satisfied: zero resistance, ratio not defined" => "Torsione non soddisfatta: resistenza nulla; tasso non definito",
        "Torsion / interaction not satisfied" => "Torsione / interazione non soddisfatta",
        _ => throw Unmapped("stato della torsione", r.Status)
    };

    static readonly FrozenDictionary<string, string> TorsionMessages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Torsion: Ak, uk and tef must be positive."] = TorsionInputMessage,
        ["Torsion: invalid shear component."] = TorsionInputMessage,
        ["Torsion: check geometry, reinforcement and materials."] = TorsionInputMessage,
        ["Torsion: concrete above C90/105 is outside the implemented range."] = TorsionConcreteClassMessage,
        ["Shear and torsion must use the same cot θ."] = TorsionCotMessage
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Il limite superiore di cot θ è formattato dalla libreria con la cultura corrente; il legacy ha un solo messaggio per i dati non validi.
    static readonly Regex TorsionCotRange = new(@"^Torsion: cot θ must be within \[[^;\]]+; [^;\]]+\]\.$", RegexOptions.CultureInvariant);

    public static ArgumentException TorsionError(ArgumentException e)
    {
        if (TorsionMessages.TryGetValue(e.Message, out var text)) return new ArgumentException(text);
        if (TorsionCotRange.IsMatch(e.Message)) return new ArgumentException(TorsionInputMessage);
        throw Unmapped("rifiuto della torsione", e.Message);
    }

    static readonly FrozenDictionary<string, string> TorsionGeometryMessages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Torsion: invalid outline."] = TorsionThicknessMessage,
        ["Torsion: the resisting thickness cannot contain the peripheral reinforcement."] = TorsionThicknessMessage
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static ArgumentException TorsionGeometryError(ArgumentException e)
        => TorsionGeometryMessages.TryGetValue(e.Message, out var text) ? new ArgumentException(text) : throw Unmapped("rifiuto del profilo resistente", e.Message);

    static InvalidOperationException Unmapped(string what, string text)
        => new("Testo di GPCChecker.Concrete senza traduzione nello strato di mappatura (" + what + "): " + text);
}
