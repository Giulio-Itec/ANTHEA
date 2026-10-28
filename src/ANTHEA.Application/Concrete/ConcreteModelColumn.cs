using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Slenderness;

namespace Anthea.Application.Concrete;

/// <summary>
/// La colonna modello della sezione c.a. (scheda «Colonna modello», 1.15.0): adattatore della libreria GPCChecker.Concrete 0.27
/// (<see cref="ModelColumnAnalysis"/>, pagina di metodo ca.colonna-modello). Solo conversione delle unità (kN, kNm, m, mm, 1/m, ‰), lettura
/// delle opzioni del foglio (<c>workspace_ca.colonna_modello</c>) e traduzione di rifiuti, avvisi, modo e stato: nessuna formula.
/// Le chiavi assenti valgono i predefiniti dichiarati in <see cref="Defaults"/>, letti senza J.S come le chiavi nuove del M–χ: i fogli salvati
/// prima della 1.15.0 non hanno l'oggetto e si aprono senza scritture.
/// </summary>
public static class ConcreteModelColumn
{
    /// <summary>L'oggetto delle opzioni nelle impostazioni della sezione.</summary>
    public const string OptionsKey = "colonna_modello";

    private const double NewtonsPerKilonewton = 1000, NmmPerKilonewtonMetre = 1e6, MillimetresPerMetre = 1000, PermillePerUnit = 1000;
    private const double SquareMillimetresPerSquareMetre = 1e6, NmmSquaredPerKilonewtonMetreSquared = 1e9;

    /// <summary>Imperfezione geometrica (EN 1992-1-1 5.2; NTC 2018 §4.1.2.3.9.3); la prima è il predefinito della norma (θ0 = 1/200).</summary>
    public static readonly ImmutableArray<string> ImperfectionModes =
        ["EN 1992-1-1 5.2(7)a · ei = θi·l0/2", "EN 1992-1-1 5.2(7) · ei = l0/400", "NTC 2018 §4.1.2.3.9.3 · ei = l/300", "Assegnata", "Nessuna"];

    /// <summary>Eccentricità minima, come minimo del momento totale del primo ordine (riquadro K-1); la prima è quella della norma del foglio.</summary>
    public static readonly ImmutableArray<string> MinimumEccentricityModes =
        ["Della norma", "NTC 2018 §4.1.2.3.4.2 · max(l0/200; 20 mm)", "EN 1992-1-1 6.1(4) · max(h/30; 20 mm)", "Nessuna"];

    /// <summary>Viscosità (EN 1992-1-1 5.8.4 e 5.8.6(4)).</summary>
    public static readonly ImmutableArray<string> CreepModes = ["Trascurata", "φef assegnato", "Da φ(∞, t0) con la (5.19)"];

    /// <summary>
    /// I predefiniti delle chiavi (testi del form): N = 0, θ = 0°, M0Ed vuoto (solo capacità), l0 e l da inserire, c = 10 (EN 1992-1-1 5.8.8.2(4)),
    /// imperfezione EN 5.2(7)a, eccentricità minima della norma, viscosità trascurata con il controllo di 5.8.4(4), rm non noto, 200 passi,
    /// tolleranza su N di MRd(N) 1 kN, momento trasversale fino all'1 %.
    /// </summary>
    public static readonly ImmutableDictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["N"] = "0", ["theta"] = "0", ["M0Ed"] = "", ["l0"] = "", ["l"] = "", ["c"] = "10",
        ["imperfezione"] = ImperfectionModes[0], ["ei"] = "", ["eccentricita_minima"] = MinimumEccentricityModes[0],
        ["viscosita"] = CreepModes[0], ["phi_ef"] = "0", ["phi_inf"] = "", ["M0Eqp"] = "", ["NEqp"] = "", ["rm"] = "",
        ["passi"] = "200", ["tolleranza_n"] = "1", ["limite_trasversale"] = "1"
    }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Il testo di una chiave: il valore salvato o il predefinito (senza J.S: il predefinito è dichiarato, non un ripiego).</summary>
    public static string Text(JsonObject? options, string key) => options?[key] is JsonNode node ? node.ToString() : Defaults[key];

    /// <summary>
    /// La colonna modello sui dati della sezione: verificatore nativo con dominio plastico e calcestruzzo teso trascurato (NTC 2018 §4.1.2.3.9.2),
    /// assi delle forze locali, la sezione del verificatore. Rifiuti della libreria e dati non validi: <see cref="ArgumentException"/> in italiano.
    /// </summary>
    public static ConcreteModelColumnResult Calculate(JsonObject input, JsonObject workspace, JsonObject? options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace);
        token.ThrowIfCancellationRequested();
        if (workspace.Array("trefoli").Count > 0)
            throw new ArgumentException("Colonna modello con trefoli: disponibile per la sola armatura ordinaria.");
        var settings = J.Obj(("criterio", "N costante"), ("assi", "Locali"), ("modello", "Non lineare"), ("strategia", "Iterativo"),
            ("modello_resistente", "Plastico"), ("trazione_cls", "No"));
        var engine = new CheckerSection(input, workspace, settings);
        var request = Request(options, engine);
        ModelColumnResult native;
        try { native = ModelColumnAnalysis.Calculate(request, engine.Checker, engine.Section, engine.ForceAxes, token); }
        catch (ModelColumnException rejection) { throw new ArgumentException(Rejection(rejection, workspace, request), rejection); }
        return Convert(native, Inputs(options), request);
    }

    // ---------------------------------------------------------------- request

    private static double Number(JsonObject? options, string key, string label) => SectionWorkspace.Number(Text(options, key), label);

    private static double? Optional(JsonObject? options, string key, string label)
        => string.IsNullOrWhiteSpace(Text(options, key)) ? null : Number(options, key, label);

    private static int Choice(JsonObject? options, string key, ImmutableArray<string> choices, string label)
    {
        int index = choices.IndexOf(Text(options, key));
        return index >= 0 ? index : throw new ArgumentException(label + ": scelta non riconosciuta.");
    }

    internal static ModelColumnRequest Request(JsonObject? options, CheckerSection engine)
    {
        double axial = Number(options, "N", "N") * NewtonsPerKilonewton;
        double direction = Number(options, "theta", "Direzione θ");
        if (string.IsNullOrWhiteSpace(Text(options, "l0"))) throw new ArgumentException("Inserire la lunghezza libera di inflessione l0.");
        if (string.IsNullOrWhiteSpace(Text(options, "l"))) throw new ArgumentException("Inserire la lunghezza dell'elemento l (per αh di EN 1992-1-1 5.2(5) e per l/300).");
        double effective = Number(options, "l0", "l0") * MillimetresPerMetre, member = Number(options, "l", "l") * MillimetresPerMetre;
        double? moment = Optional(options, "M0Ed", "M0Ed") * NmmPerKilonewtonMetre;
        double factor = Number(options, "c", "c");
        var imperfection = Choice(options, "imperfezione", ImperfectionModes, "Imperfezione") switch
        {
            0 => ModelColumnImperfection.En1992Inclination(),
            1 => ModelColumnImperfection.En1992Simplified,
            2 => ModelColumnImperfection.Ntc2018Straightness,
            3 => ModelColumnImperfection.Assigned(Optional(options, "ei", "ei") ?? throw new ArgumentException("Imperfezione assegnata: inserire ei.")),
            _ => ModelColumnImperfection.None
        };
        var minimum = Choice(options, "eccentricita_minima", MinimumEccentricityModes, "Eccentricità minima") switch
        {
            0 => ModelColumnMinimumEccentricity.Standard,
            1 => ModelColumnMinimumEccentricity.Ntc2018,
            2 => ModelColumnMinimumEccentricity.En1992,
            _ => ModelColumnMinimumEccentricity.None
        };
        double? final = Optional(options, "phi_inf", "φ(∞, t0)");
        var creep = Choice(options, "viscosita", CreepModes, "Viscosità") switch
        {
            0 => ModelColumnCreep.Neglected(final),
            1 => ModelColumnCreep.Effective(Number(options, "phi_ef", "φef"), final),
            _ => ModelColumnCreep.FromFinalCoefficient(final ?? throw new ArgumentException("Viscosità dalla (5.19): inserire φ(∞, t0)."),
                (Optional(options, "M0Eqp", "M0Eqp") ?? throw new ArgumentException("Viscosità dalla (5.19): inserire M0Eqp.")) * NmmPerKilonewtonMetre,
                (Optional(options, "NEqp", "NEqp") ?? throw new ArgumentException("Viscosità dalla (5.19): inserire NEqp.")) * NewtonsPerKilonewton)
        };
        double? ratio = Optional(options, "rm", "rm");
        int steps = SectionWorkspace.Subdivisions(Text(options, "passi"), "Passi di curvatura", 10, 500);
        double tolerance = Number(options, "tolleranza_n", "Tolleranza su N") * NewtonsPerKilonewton;
        // Tolleranza su N di MRd(N) (confronto con la curvatura nominale): almeno la regola della libreria per legame e dimensioni (SLU-3), come
        // il M–χ e la resistenza a N assegnato; un valore non valido resta alla libreria, che lo rifiuta.
        if (double.IsFinite(tolerance) && tolerance > 0) tolerance = Math.Max(tolerance, DomainPointAxialTolerance.Calculate(engine.Checker.SectionSolver, axial));
        double transverse = Number(options, "limite_trasversale", "Limite del momento trasversale") / 100;
        return new ModelColumnRequest(axial, direction, effective, member, moment, factor, imperfection, minimum, creep, ratio, steps, tolerance, transverse);
    }

    /// <summary>Le righe dei dati per la relazione: etichetta e valore effettivo (salvato o predefinito).</summary>
    private static IReadOnlyList<ConcreteModelColumnResult.Row> Inputs(JsonObject? options)
    {
        string T(string key) => Text(options, key);
        var rows = new List<ConcreteModelColumnResult.Row>
        {
            new("N costante (− compressione) [kN]", T("N")), new("Direzione del momento θ [°]", T("theta")),
            new("M0Ed nella sezione critica, senza imperfezione [kNm]", string.IsNullOrWhiteSpace(T("M0Ed")) ? "solo capacità" : T("M0Ed")),
            new("Lunghezza libera di inflessione l0 [m]", T("l0")), new("Lunghezza dell'elemento l [m]", T("l")), new("Fattore della curvatura c", T("c")),
            new("Imperfezione", T("imperfezione") == ImperfectionModes[3] ? T("imperfezione") + " · ei = " + T("ei") + " mm" : T("imperfezione")),
            new("Eccentricità minima", T("eccentricita_minima")), new("Viscosità", T("viscosita"))
        };
        int creep = CreepModes.IndexOf(T("viscosita"));
        if (creep == 1) rows.Add(new("φef", T("phi_ef")));
        if (!string.IsNullOrWhiteSpace(T("phi_inf"))) rows.Add(new("φ(∞, t0)", T("phi_inf")));
        if (creep == 2) { rows.Add(new("M0Eqp [kNm]", T("M0Eqp"))); rows.Add(new("NEqp (− compressione) [kN]", T("NEqp"))); }
        rows.Add(new("rm = M01/M02", string.IsNullOrWhiteSpace(T("rm")) ? "non noto (C = 0,7)" : T("rm")));
        rows.Add(new("Passi di curvatura", T("passi"))); rows.Add(new("Tolleranza su N di MRd(N) [kN]", T("tolleranza_n")));
        rows.Add(new("Momento trasversale ammesso [%]", T("limite_trasversale")));
        return rows;
    }

    // ---------------------------------------------------------------- messages

    /// <summary>Il motivo di un rifiuto della libreria, in italiano.</summary>
    internal static string Rejection(ModelColumnException rejection, JsonObject workspace, ModelColumnRequest request) => rejection.Reason switch
    {
        ModelColumnRejection.InvalidRequest => "Dati della colonna modello non validi: N e θ finiti; l0 e l maggiori di zero; c fra 8 e 10; passi fra 10 e 500; "
            + "tolleranza su N positiva; momento trasversale ammesso fra 0 e 100 %; M0Ed ≥ 0; rm fra −1 e 1; ei ≥ 0; φef e φ(∞, t0) fra 0 e 10; "
            + "la (5.19) richiede M0Ed, M0Eqp e NEqp di compressione.",
        ModelColumnRejection.UnsupportedStandard => "Colonna modello disponibile per NTC 2018, CNR-DT 200, EN 1992-1-1 (valori raccomandati) e UNI EN 1992-1-1 "
            + "con DM 31/07/2012; non per " + workspace.S("normativa") + ".",
        ModelColumnRejection.UnsupportedSection => "Colonna modello disponibile per le sezioni in calcestruzzo ordinario delle norme europee, con barre e senza profili d'acciaio.",
        ModelColumnRejection.UnsupportedChecker => "Colonna modello: dominio plastico e calcestruzzo teso trascurato (NTC 2018 §4.1.2.3.9.2).",
        ModelColumnRejection.TensionAxialForce => "N di trazione: la colonna modello vale per gli elementi compressi (N ≤ 0, compressione negativa).",
        ModelColumnRejection.AxialForceBeyondResistance => "|N| = " + Kilo(Math.Abs(request.AxialForce)) + " kN oltre la resistenza a compressione centrata della sezione NRd,c = "
            + Kilo(Math.Abs(rejection.Value)) + " kN.",
        ModelColumnRejection.TransverseMomentBeyondLimit => "Momento trasversale pari al " + (rejection.Value * 100).ToString("0.##", CultureInfo.CurrentCulture)
            + " % del momento nella direzione θ, oltre il " + (request.TransverseMomentLimit * 100).ToString("0.##", CultureInfo.CurrentCulture)
            + " % ammesso: la sezione non è simmetrica rispetto al piano di flessione. Scegliere una direzione principale (EN 1992-1-1 5.8.9(2)).",
        _ => "Colonna modello non disponibile."
    };

    private static string Kilo(double newtons) => (newtons / NewtonsPerKilonewton).ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Un avviso della libreria, in italiano.</summary>
    public static string Warning(ModelColumnWarning warning) => warning switch
    {
        ModelColumnWarning.NoAxialForce => "N = 0: nessun effetto del secondo ordine; M1Rd,max è il momento massimo della sezione a N = 0.",
        ModelColumnWarning.CreepNeglectedWithoutConditions => "φef = 0 senza le tre condizioni di EN 1992-1-1 5.8.4(4) dimostrate (φ(∞, t0) ≤ 2, λ ≤ 75, M0Ed/NEd ≥ h): "
            + "inserire φef, oppure φ(∞, t0) per il controllo.",
        ModelColumnWarning.TransverseMoment => "Momento trasversale oltre il limite ammesso, accettato: il calcolo non è una flessione retta.",
        ModelColumnWarning.ResistanceNotAvailable => "MRd(N) del dominio non disponibile: il confronto con la curvatura nominale è incompleto.",
        ModelColumnWarning.EquilibriumAtZeroCurvature => "M0Ed,tot non supera il momento della sezione a curvatura nulla (sezione asimmetrica): equilibrio a χ = 0.",
        _ => warning.ToString()
    };

    /// <summary>Il modo di crisi, in italiano.</summary>
    public static string Mode(ModelColumnMode mode) => mode == ModelColumnMode.Instability ? "instabilità" : "rottura della sezione";

    private static string ImperfectionLabel(ModelColumnImperfectionKind kind) => kind switch
    {
        ModelColumnImperfectionKind.En1992Inclination => "EN 1992-1-1 5.2(7)a, ei = θi·l0/2",
        ModelColumnImperfectionKind.En1992Simplified => "EN 1992-1-1 5.2(7), ei = l0/400",
        ModelColumnImperfectionKind.Ntc2018Straightness => "NTC 2018 §4.1.2.3.9.3, ei = l/300",
        ModelColumnImperfectionKind.Assigned => "assegnata",
        _ => "nessuna"
    };

    private static string CreepLabel(ModelColumnCreepKind kind) => kind switch
    {
        ModelColumnCreepKind.Effective => "φef assegnato",
        ModelColumnCreepKind.FinalCoefficient => "φef = φ(∞, t0)·M0Eqp/M0Ed, EN 1992-1-1 (5.19)",
        _ => "trascurata"
    };

    private static string MinimumLabel(ModelColumnMinimumEccentricity rule) => rule switch
    {
        ModelColumnMinimumEccentricity.Ntc2018 => "NTC 2018 §4.1.2.3.4.2, e0 = max(l0/200; 20 mm)",
        ModelColumnMinimumEccentricity.En1992 => "EN 1992-1-1 6.1(4), e0 = max(h/30; 20 mm)",
        ModelColumnMinimumEccentricity.None => "nessuna",
        _ => "della norma"
    };

    // ---------------------------------------------------------------- result

    private static double Kn(double newtons) => newtons / NewtonsPerKilonewton;
    private static double Knm(double nmm) => nmm / NmmPerKilonewtonMetre;
    private static double? Knm(double? nmm) => nmm / NmmPerKilonewtonMetre;
    private static double PerMetre(double perMillimetre) => perMillimetre * MillimetresPerMetre;
    private static double? PerMetre(double? perMillimetre) => perMillimetre * MillimetresPerMetre;
    private static double Permille(double strain) => strain * PermillePerUnit;
    // Un limite infinito (N = 0) non si scrive in JSON: null.
    private static double? Finite(double value) => double.IsFinite(value) ? value : null;
    private static double? Finite(double? value) => value is double v && double.IsFinite(v) ? v : null;

    /// <summary>Un passo della traccia nelle unità di ANTHEA: kN, kNm, kN·m², 1/m; le altre unità come nella libreria.</summary>
    private static ConcreteModelColumnResult.Step Step(ModelColumnTraceStep step)
    {
        var (value, unit) = step.Unit switch
        {
            "N" => (step.Value / NewtonsPerKilonewton, "kN"),
            "Nmm" => (step.Value / NmmPerKilonewtonMetre, "kNm"),
            "N·mm²" => (step.Value / NmmSquaredPerKilonewtonMetreSquared, "kN·m²"),
            "1/mm" => (step.Value * MillimetresPerMetre, "1/m"),
            _ => (step.Value, step.Unit)
        };
        return new(step.Code, step.Description, step.Formula, Finite(value), unit, step.Reference);
    }

    private static ConcreteModelColumnResult Convert(ModelColumnResult r, IReadOnlyList<ConcreteModelColumnResult.Row> inputs, ModelColumnRequest request)
    {
        var s = r.Section; var l = r.Slenderness; var i = r.Imperfection; var f = r.FirstOrder; var c = r.Creep; var q = r.Capacity; var e = r.Equilibrium;
        var n = r.NominalCurvature;
        var result = new ConcreteModelColumnResult
        {
            Profile = ModelColumnProfiles.Reference(r.Profile), AxialKn = Kn(r.AxialForce), DirectionDegrees = r.DirectionDegrees,
            EffectiveLength = r.EffectiveLength / MillimetresPerMetre, MemberLength = r.MemberLength / MillimetresPerMetre, CurvatureFactor = r.CurvatureFactor,
            SecondOrderStiffness = r.SecondOrderStiffness / NmmSquaredPerKilonewtonMetreSquared,
            Section = new()
            {
                ConcreteArea = s.ConcreteArea, Depth = s.Depth, Inertia = s.Inertia, RadiusOfGyration = s.RadiusOfGyration, ReinforcementArea = s.ReinforcementArea,
                CharacteristicStrength = s.CharacteristicStrength, DesignStrength = s.DesignStrength, AxialResistanceKn = Kn(s.AxialResistance),
                CompressionResistanceKn = Kn(s.CompressionResistance), YieldStrain = Permille(s.YieldStrain)
            },
            Slenderness = new()
            {
                Lambda = l.Slenderness, RelativeAxialForce = l.RelativeAxialForce, MechanicalRatio = l.MechanicalRatio, LimitNtc2018 = Finite(l.LimitNtc2018),
                FactorA = l.FactorA, FactorB = l.FactorB, FactorC = l.FactorC, LimitEn1992 = Finite(l.LimitEn1992), Limit = Finite(l.Limit), Reference = l.Reference,
                Negligible = l.SecondOrderEffectsNegligible,
                Text = l.SecondOrderEffectsNegligible ? "effetti del secondo ordine trascurabili" : "effetti del secondo ordine da considerare"
            },
            Imperfection = new()
            {
                Kind = ImperfectionLabel(i.Kind), BaseInclination = i.BaseInclination, HeightFactor = i.HeightFactor, MemberFactor = i.MemberFactor,
                Inclination = i.Inclination, Eccentricity = i.Eccentricity, MomentKnm = Knm(i.Moment), Reference = i.Reference
            },
            FirstOrder = new()
            {
                AppliedKnm = Knm(f.AppliedMoment), ImperfectionKnm = Knm(f.ImperfectionMoment), MinimumRule = MinimumLabel(f.MinimumEccentricityRule),
                MinimumEccentricity = f.MinimumEccentricity, MinimumKnm = Knm(f.MinimumMoment), TotalKnm = Knm(f.TotalMoment), MinimumGoverns = f.MinimumGoverns,
                MinimumReference = f.MinimumReference
            },
            Creep = new()
            {
                Kind = CreepLabel(c.Kind), EffectiveRatio = c.EffectiveRatio, FinalCoefficient = c.FinalCoefficient, MomentRatio = Finite(c.MomentRatio),
                FinalCoefficientCondition = c.FinalCoefficientCondition, SlendernessCondition = c.SlendernessCondition, EccentricityCondition = c.EccentricityCondition,
                NeglectAdmissible = c.NeglectAdmissible
            },
            Capacity = new()
            {
                MaximumFirstOrderKnm = Knm(q.MaximumFirstOrderMoment), CurvatureAtMaximum = PerMetre(q.CurvatureAtMaximum), MomentAtMaximumKnm = Knm(q.MomentAtMaximum),
                DeflectionAtMaximum = q.DeflectionAtMaximum, SecondOrderAtMaximumKnm = Knm(q.SecondOrderMomentAtMaximum), Mode = Mode(q.Mode),
                Instability = q.Mode == ModelColumnMode.Instability, UltimateCurvature = PerMetre(q.UltimateCurvature), UltimateMomentKnm = Knm(q.UltimateMoment),
                YieldCurvature = PerMetre(q.YieldCurvature), YieldMomentKnm = Knm(q.YieldMoment), MaximumAppliedKnm = Knm(q.MaximumAppliedMoment)
            },
            Equilibrium = e is null ? null : new()
            {
                Found = e.Found, FirstOrderKnm = Knm(e.FirstOrderMoment), Curvature = PerMetre(e.Curvature), Deflection = e.Deflection,
                SecondOrderKnm = Knm(e.SecondOrderMoment), DesignKnm = Knm(e.DesignMoment), SectionKnm = Knm(e.SectionMoment), Ratio = Finite(e.Ratio), Verified = e.Verified
            },
            Nominal = new()
            {
                RelativeAxialForce = n.RelativeAxialForce, MechanicalRatio = n.MechanicalRatio, Nu = n.Nu, Nbal = n.Nbal, Kr = n.Kr, Beta = n.Beta, Kphi = n.Kphi,
                EffectiveDepth = n.EffectiveDepth, YieldStrain = Permille(n.YieldStrain), BasicCurvature = PerMetre(n.BasicCurvature), Curvature = PerMetre(n.Curvature),
                Deflection = n.Deflection, SecondOrderKnm = Knm(n.SecondOrderMoment), ResistingKnm = Knm(n.ResistingMoment),
                MaximumFirstOrderKnm = Knm(n.MaximumFirstOrderMoment), DesignKnm = Knm(n.DesignMoment), Ratio = Finite(n.Ratio)
            },
            TransverseMomentRatio = r.TransverseMomentRatio,
            Points = r.Points.Select(p => new ConcreteModelColumnResult.Point
            {
                Curvature = PerMetre(p.Curvature), MomentKnm = Knm(p.Moment), TransverseKnm = Knm(p.TransverseMoment), SecondOrderKnm = Knm(p.SecondOrderMoment),
                FirstOrderCapacityKnm = Knm(p.FirstOrderCapacity), ReferenceStrain = Permille(p.ReferenceStrain),
                ConcreteCompressionStrain = Permille(p.ConcreteCompressionStrain), SteelStrain = Permille(p.SteelStrain), Yielded = p.Yielded, Ultimate = p.Ultimate,
                Maximum = p.Maximum
            }).ToArray(),
            Trace = r.Trace.Select(Step).ToArray(),
            Warnings = r.Warnings.Select(Warning).ToArray(),
            Verified = r.Verified,
            Inputs = inputs,
            TransverseLimit = request.TransverseMomentLimit
        };
        result.Outcome = result.Verified is null ? "Solo capacità" : result.Verified.Value ? "Verificata" : "Non verificata (instabilità)";
        result.Status = Status(result);
        return result;
    }

    private static string G(double value) => value.ToString("G6", CultureInfo.CurrentCulture);

    // Lo stato della libreria (in inglese) riscritto in italiano dai valori del risultato.
    private static string Status(ConcreteModelColumnResult r)
    {
        var q = r.Capacity;
        string status = "Colonna modello (EN 1992-1-1 5.8.6(6) e 5.8.8.2; " + r.Profile + "): M1Rd,max = " + G(q.MaximumFirstOrderKnm) + " kNm a χ* = "
            + G(q.CurvatureAtMaximum) + " 1/m (" + q.Mode + "), χu = " + G(q.UltimateCurvature) + " 1/m, φef = " + G(r.Creep.EffectiveRatio) + ", ei = "
            + G(r.Imperfection.Eccentricity) + " mm";
        if (r.Equilibrium is not { } e) return status + "; solo capacità.";
        return status + (e.Found
            ? "; M0Ed,tot = " + G(e.FirstOrderKnm) + " kNm, MEd = " + G(e.DesignKnm ?? double.NaN) + " kNm, rapporto " + (e.Ratio?.ToString("G4", CultureInfo.CurrentCulture) ?? "—") + "."
            : "; M0Ed,tot = " + G(e.FirstOrderKnm) + " kNm oltre M1Rd,max: nessun equilibrio (instabilità).");
    }
}

/// <summary>
/// Il risultato della colonna modello nelle unità di ANTHEA (kN, kNm, m per l0 e l, mm per eccentricità, spostamenti e sezione, 1/m, ‰ per le
/// deformazioni), con i testi in italiano: è anche il contratto esportato (<c>colonna_modello</c> dei risultati) e letto dalla relazione.
/// Compressione negativa in <see cref="AxialKn"/>; nelle formule NEd è il modulo della compressione.
/// </summary>
public sealed class ConcreteModelColumnResult
{
    public sealed record Row(string Label, string Value);
    public sealed record Step(string Code, string Description, string Formula, double? Value, string Unit, string Reference);

    public sealed class SectionValues
    {
        public double ConcreteArea { get; init; }
        public double Depth { get; init; }
        public double Inertia { get; init; }
        public double RadiusOfGyration { get; init; }
        public double ReinforcementArea { get; init; }
        public double CharacteristicStrength { get; init; }
        public double DesignStrength { get; init; }
        public double AxialResistanceKn { get; init; }
        public double CompressionResistanceKn { get; init; }
        public double YieldStrain { get; init; }
    }

    public sealed class SlendernessValues
    {
        public double Lambda { get; init; }
        public double RelativeAxialForce { get; init; }
        public double MechanicalRatio { get; init; }
        public double? LimitNtc2018 { get; init; }
        public double FactorA { get; init; }
        public double FactorB { get; init; }
        public double FactorC { get; init; }
        public double? LimitEn1992 { get; init; }
        /// <summary>λlim della norma del profilo; null se infinito (N = 0).</summary>
        public double? Limit { get; init; }
        public string Reference { get; init; } = "";
        public bool Negligible { get; init; }
        public string Text { get; init; } = "";
    }

    public sealed class ImperfectionValues
    {
        public string Kind { get; init; } = "";
        public double? BaseInclination { get; init; }
        public double? HeightFactor { get; init; }
        public double? MemberFactor { get; init; }
        public double? Inclination { get; init; }
        public double Eccentricity { get; init; }
        public double MomentKnm { get; init; }
        public string Reference { get; init; } = "";
    }

    public sealed class FirstOrderValues
    {
        public double? AppliedKnm { get; init; }
        public double ImperfectionKnm { get; init; }
        public string MinimumRule { get; init; } = "";
        public double? MinimumEccentricity { get; init; }
        public double? MinimumKnm { get; init; }
        public double? TotalKnm { get; init; }
        public bool MinimumGoverns { get; init; }
        public string MinimumReference { get; init; } = "";
    }

    public sealed class CreepValues
    {
        public string Kind { get; init; } = "";
        public double EffectiveRatio { get; init; }
        public double? FinalCoefficient { get; init; }
        public double? MomentRatio { get; init; }
        public bool? FinalCoefficientCondition { get; init; }
        public bool SlendernessCondition { get; init; }
        public bool? EccentricityCondition { get; init; }
        public bool NeglectAdmissible { get; init; }
    }

    public sealed class CapacityValues
    {
        public double MaximumFirstOrderKnm { get; init; }
        public double CurvatureAtMaximum { get; init; }
        public double MomentAtMaximumKnm { get; init; }
        public double DeflectionAtMaximum { get; init; }
        public double SecondOrderAtMaximumKnm { get; init; }
        public string Mode { get; init; } = "";
        public bool Instability { get; init; }
        public double UltimateCurvature { get; init; }
        public double UltimateMomentKnm { get; init; }
        public double? YieldCurvature { get; init; }
        public double? YieldMomentKnm { get; init; }
        public double? MaximumAppliedKnm { get; init; }
    }

    public sealed class EquilibriumValues
    {
        public bool Found { get; init; }
        public double FirstOrderKnm { get; init; }
        public double? Curvature { get; init; }
        public double? Deflection { get; init; }
        public double? SecondOrderKnm { get; init; }
        public double? DesignKnm { get; init; }
        public double? SectionKnm { get; init; }
        /// <summary>M0Ed,tot/M1Rd,max; null se non finito.</summary>
        public double? Ratio { get; init; }
        public bool Verified { get; init; }
    }

    public sealed class NominalValues
    {
        public double RelativeAxialForce { get; init; }
        public double MechanicalRatio { get; init; }
        public double Nu { get; init; }
        public double Nbal { get; init; }
        public double Kr { get; init; }
        public double Beta { get; init; }
        public double Kphi { get; init; }
        public double EffectiveDepth { get; init; }
        public double YieldStrain { get; init; }
        public double BasicCurvature { get; init; }
        public double Curvature { get; init; }
        public double Deflection { get; init; }
        public double SecondOrderKnm { get; init; }
        public double? ResistingKnm { get; init; }
        public double? MaximumFirstOrderKnm { get; init; }
        public double? DesignKnm { get; init; }
        public double? Ratio { get; init; }
    }

    public sealed class Point
    {
        public double Curvature { get; init; }
        public double MomentKnm { get; init; }
        public double TransverseKnm { get; init; }
        public double SecondOrderKnm { get; init; }
        public double FirstOrderCapacityKnm { get; init; }
        public double ReferenceStrain { get; init; }
        public double ConcreteCompressionStrain { get; init; }
        public double SteelStrain { get; init; }
        public bool Yielded { get; init; }
        public bool Ultimate { get; init; }
        public bool Maximum { get; init; }
    }

    /// <summary>Norma del profilo, in italiano.</summary>
    public string Profile { get; init; } = "";
    public double AxialKn { get; init; }
    public double DirectionDegrees { get; init; }
    /// <summary>l0 [m].</summary>
    public double EffectiveLength { get; init; }
    /// <summary>l [m].</summary>
    public double MemberLength { get; init; }
    public double CurvatureFactor { get; init; }
    /// <summary>k = NEd·l0²/c [kN·m²]: M2 = k·χ.</summary>
    public double SecondOrderStiffness { get; init; }
    public SectionValues Section { get; init; } = new();
    public SlendernessValues Slenderness { get; init; } = new();
    public ImperfectionValues Imperfection { get; init; } = new();
    public FirstOrderValues FirstOrder { get; init; } = new();
    public CreepValues Creep { get; init; } = new();
    public CapacityValues Capacity { get; init; } = new();
    /// <summary>Null per la sola capacità (M0Ed vuoto).</summary>
    public EquilibriumValues? Equilibrium { get; init; }
    public NominalValues Nominal { get; init; } = new();
    public double TransverseMomentRatio { get; init; }
    /// <summary>Limite del momento trasversale (frazione del momento).</summary>
    public double TransverseLimit { get; init; }
    public IReadOnlyList<Point> Points { get; init; } = [];
    public IReadOnlyList<Step> Trace { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<Row> Inputs { get; init; } = [];
    /// <summary>Null per la sola capacità.</summary>
    public bool? Verified { get; init; }
    /// <summary>«Verificata», «Non verificata (instabilità)» o «Solo capacità».</summary>
    public string Outcome { get; internal set; } = "";
    public string Status { get; internal set; } = "";
}
