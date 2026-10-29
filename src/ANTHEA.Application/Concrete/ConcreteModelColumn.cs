using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Slenderness;

namespace Anthea.Application.Concrete;

/// <summary>
/// La colonna modello della sezione c.a. (scheda «Colonna modello», 1.15.0; fase 2 nella 1.16.0): adattatore della libreria GPCChecker.Concrete
/// (<see cref="ModelColumnAnalysis"/>, <see cref="ModelColumnEnvelope"/>, pagina di metodo ca.colonna-modello). Solo conversione delle unità (kN,
/// kNm, m, mm, 1/m, ‰), lettura delle opzioni del foglio (<c>workspace_ca.colonna_modello</c>) e delle combinazioni, traduzione di rifiuti,
/// avvisi, modo e stato: nessuna formula. Le chiavi assenti valgono i predefiniti dichiarati in <see cref="Defaults"/>, letti senza J.S come
/// le chiavi nuove del M–χ: i fogli salvati prima della 1.15.0 non hanno l'oggetto e si aprono senza scritture; quelli della 1.15.0 non hanno
/// «azioni» né «intervalli_dominio» e restano sull'azione singola.
/// </summary>
public static class ConcreteModelColumn
{
    /// <summary>L'oggetto delle opzioni nelle impostazioni della sezione.</summary>
    public const string OptionsKey = "colonna_modello";

    /// <summary>
    /// Le azioni verificate (1.16.0): l'azione singola dei campi N e M0Ed (predefinito, come nella 1.15.0) oppure tutte le combinazioni degli
    /// insiemi SLU, o SLU e SLV, della scheda «Combinazioni» (N, Mx, My negli assi locali), con l'esito peggiore.
    /// </summary>
    public static readonly ImmutableArray<string> ActionSources = ["Azione singola (N, M0Ed)", "Combinazioni SLU", "Combinazioni SLU e SLV"];

    /// <summary>Gli insiemi di combinazioni di una scelta di <see cref="ActionSources"/> (nessuno per l'azione singola).</summary>
    public static IReadOnlyList<string> CombinationSets(int source) => source switch { 1 => ["SLU"], 2 => ["SLU", "SLV"], _ => [] };

    /// <summary>L'indice della scelta delle azioni; una scelta sconosciuta è rifiutata.</summary>
    public static int Source(JsonObject? options) => Choice(options, "azioni", ActionSources, "Azioni");

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
    /// I predefiniti delle chiavi (testi del form): azione singola, N = 0, θ = 0°, M0Ed vuoto (solo capacità), l0 e l da inserire, c = 10
    /// (EN 1992-1-1 5.8.8.2(4)), imperfezione EN 5.2(7)a, eccentricità minima della norma, viscosità trascurata con il controllo di 5.8.4(4),
    /// rm non noto, 200 passi, tolleranza su N di MRd(N) 1 kN, momento trasversale fino all'1 %, 40 intervalli del dominio ridotto (1.16.0).
    /// </summary>
    public static readonly ImmutableDictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["azioni"] = ActionSources[0], ["N"] = "0", ["theta"] = "0", ["M0Ed"] = "", ["l0"] = "", ["l"] = "", ["c"] = "10",
        ["imperfezione"] = ImperfectionModes[0], ["ei"] = "", ["eccentricita_minima"] = MinimumEccentricityModes[0],
        ["viscosita"] = CreepModes[0], ["phi_ef"] = "0", ["phi_inf"] = "", ["M0Eqp"] = "", ["NEqp"] = "", ["rm"] = "",
        ["passi"] = "200", ["tolleranza_n"] = "1", ["limite_trasversale"] = "1", ["intervalli_dominio"] = "40"
    }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Il testo di una chiave: il valore salvato o il predefinito (senza J.S: il predefinito è dichiarato, non un ripiego).</summary>
    public static string Text(JsonObject? options, string key) => options?[key] is JsonNode node ? node.ToString() : Defaults[key];

    /// <summary>
    /// La colonna modello sui dati della sezione: verificatore nativo con dominio plastico e calcestruzzo teso trascurato (NTC 2018 §4.1.2.3.9.2),
    /// assi delle forze locali, la sezione del verificatore. Con l'azione singola la colonna di N e M0Ed e il suo dominio ridotto
    /// (<see cref="ModelColumnAnalysis.Domain"/>); con le combinazioni (<paramref name="combinations"/>: le righe di
    /// <see cref="ConcreteSheetPreparation.ReadActions"/>) l'inviluppo della libreria (<see cref="ModelColumnEnvelope"/>), con i risultati della
    /// combinazione che governa nei campi della colonna singola. Rifiuti della libreria e dati non validi: <see cref="ArgumentException"/> in
    /// italiano. Avanzamento dalla libreria (combinazioni, poi i punti del dominio ridotto).
    /// </summary>
    public static ConcreteModelColumnResult Calculate(JsonObject input, JsonObject workspace, JsonObject? options, CancellationToken token = default,
        IReadOnlyDictionary<string, JsonObject[]>? combinations = null, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace);
        token.ThrowIfCancellationRequested();
        if (workspace.Array("trefoli").Count > 0)
            throw new ArgumentException("Colonna modello con trefoli: disponibile per la sola armatura ordinaria.");
        int source = Source(options);
        int intervals = SectionWorkspace.Subdivisions(Text(options, "intervalli_dominio"), "Intervalli del dominio ridotto",
            ModelColumnAnalysis.MinimumDomainIntervals, ModelColumnAnalysis.MaximumDomainIntervals);
        // the combinations read before the section: a value that is not a number is refused with the name of its row
        var rows = CombinationSets(source).SelectMany(key => (combinations?.GetValueOrDefault(key) ?? []).Select(row => (Key: key, Row: row))).ToList();
        var actions = rows.Select(r =>
        {
            ActionPoint point;
            try { point = SectionWorkspace.ReadAction(r.Row); }
            catch (ArgumentException error) { throw new ArgumentException(SectionActionInputs.ActionLabel(r.Row) + " · " + error.Message, error); }
            return new ModelColumnAction(SectionActionInputs.ActionLabel(r.Row), point.N * NewtonsPerKilonewton, point.Mx * NmmPerKilonewtonMetre, point.My * NmmPerKilonewtonMetre);
        }).ToList();
        if (source > 0 && actions.Count == 0)
            throw new ArgumentException("Nessuna combinazione " + (source == 1 ? "SLU" : "SLU o SLV") + " nella scheda «Combinazioni»: inserirle, oppure scegliere l'azione singola.");
        var settings = J.Obj(("criterio", "N costante"), ("assi", "Locali"), ("modello", "Non lineare"), ("strategia", "Iterativo"),
            ("modello_resistente", "Plastico"), ("trazione_cls", "No"));
        var engine = new CheckerSection(input, workspace, settings);
        if (source == 0)
        {
            var request = Request(options, engine);
            ModelColumnResult native; ModelColumnDomain domain;
            try
            {
                native = ModelColumnAnalysis.Calculate(request, engine.Checker, engine.Section, engine.ForceAxes, token);
                domain = ModelColumnAnalysis.Domain(request, engine.Checker, engine.Section, engine.ForceAxes, intervals, Relay(progress), token);
            }
            catch (ModelColumnException rejection) { throw new ArgumentException(Rejection(rejection, workspace, request), rejection); }
            return Convert(native, Inputs(options, source), request, new Extra(ActionSources[source], [], [Domain(domain)], null, native.Verified, [], null,
                request.DirectionDegrees));
        }
        // the tolerance on N of MRd(N) at least the rule of the library for the largest |N| of the combinations
        var template = Request(options, engine, actions.Max(a => Math.Abs(a.AxialForce)));
        ModelColumnEnvelopeResult envelope;
        try { envelope = ModelColumnEnvelope.Calculate(template, actions, engine.Checker, engine.Section, engine.ForceAxes, intervals, Relay(progress), token); }
        catch (ModelColumnException rejection) { throw new ArgumentException(Rejection(rejection, workspace, template), rejection); }
        var combinationValues = envelope.Actions.Select(a => Combination(a, rows[a.Index].Key, envelope, workspace, template)).ToArray();
        if (envelope.GoverningCalculated is not int governing)
            throw new ArgumentException(envelope.Actions.All(a => a.Status == ModelColumnActionStatus.Tension)
                ? "Nessuna combinazione compressa: la colonna modello vale per gli elementi compressi (N < 0, compressione negativa)."
                : "Nessuna combinazione calcolabile: " + string.Join("; ", combinationValues.Where(c => c.Status != "Trazione").Select(c => c.Name + " · " + c.Outcome)) + ".");
        var warnings = new List<string>();
        if (envelope.Warnings.Contains(ModelColumnEnvelopeWarning.TransverseFirstOrderMoment))
            warnings.Add("Momento del primo ordine normale alla direzione θ in " + combinationValues.Count(c => c.TransverseNeglected) + " combinazioni (oltre "
                + (template.TransverseMomentLimit * 100).ToString("0.##", CultureInfo.CurrentCulture) + " % del momento in θ): non verificato. Ripetere la verifica "
                + "con θ + 90° e combinare le due direzioni con EN 1992-1-1 5.8.9.");
        if (envelope.Warnings.Contains(ModelColumnEnvelopeWarning.RejectedActions))
            warnings.Add(combinationValues.Count(c => c.Status == "Non calcolabile") + " combinazioni non calcolabili: esito incompleto.");
        string status = "Colonna modello, inviluppo di " + actions.Count + " " + (source == 1 ? "combinazioni SLU" : "combinazioni SLU e SLV") + " (θ = "
            + G(template.DirectionDegrees) + "°): " + Count("Verificata") + " verificate, " + Count("Non verificata") + " non verificate, " + Count("Trazione")
            + " di trazione (non pertinenti)" + (Count("Non calcolabile") > 0 ? ", " + Count("Non calcolabile") + " non calcolabili" : "")
            + (envelope.Governing is int worst ? "; governa «" + combinationValues[worst].Name + "», η = " + (combinationValues[worst].Ratio is double ratio
                ? ratio.ToString("G4", CultureInfo.CurrentCulture) : "∞") : "") + ".";
        int Count(string kind) => combinationValues.Count(c => c.Status == kind);
        var shown = envelope.Actions[governing];
        return Convert(shown.Result, Inputs(options, source), template, new Extra(ActionSources[source], combinationValues,
            envelope.Domains.Select(Domain).ToArray(), combinationValues[governing].Name, envelope.Verified, warnings, status, template.DirectionDegrees));
    }

    /// <summary>L'avanzamento della libreria come testo in italiano (le combinazioni, poi i punti del dominio ridotto).</summary>
    private static IProgress<ModelColumnProgress>? Relay(IProgress<string>? target) => target is null ? null : new ProgressText(target);

    /// <summary>Il testo dell'avanzamento della libreria.</summary>
    public static string ProgressMessage(bool domain, int completed, int total) => domain
        ? "Colonna modello: dominio ridotto N–M1Rd,max, punto " + completed + " di " + total + "…"
        : "Colonna modello: combinazione " + completed + " di " + total + "…";

    private sealed class ProgressText(IProgress<string> target) : IProgress<ModelColumnProgress>
    {
        public void Report(ModelColumnProgress value) => target.Report(ProgressMessage(value.Stage == ModelColumnStage.Domain, value.Completed, value.Total));
    }

    /// <summary>Le parti della fase 2 del risultato: azioni, combinazioni, domini, combinazione mostrata, esito complessivo, avvisi e stato dell'inviluppo.</summary>
    private sealed record Extra(string Source, IReadOnlyList<ConcreteModelColumnResult.CombinationValues> Combinations,
        IReadOnlyList<ConcreteModelColumnResult.DomainValues> Domains, string? Governing, bool? Verified, IReadOnlyList<string> Warnings, string? EnvelopeStatus,
        double Theta);

    private static ConcreteModelColumnResult.CombinationValues Combination(ModelColumnActionResult a, string state, ModelColumnEnvelopeResult envelope,
        JsonObject workspace, ModelColumnRequest template)
    {
        var r = a.Result;
        (string status, string outcome) = a.Status switch
        {
            ModelColumnActionStatus.Verified => ("Verificata", "Verificata"),
            ModelColumnActionStatus.NotVerified => ("Non verificata", "Non verificata (instabilità)"),
            ModelColumnActionStatus.Tension => ("Trazione", "Trazione: non pertinente"),
            _ when a.Rejection == ModelColumnRejection.AxialForceBeyondResistance => ("Non verificata", "Non verificata: "
                + RejectionText(a.Rejection.Value, a.RejectionValue, a.Action.AxialForce, template.TransverseMomentLimit, workspace)),
            _ => ("Non calcolabile", "Non calcolabile: " + RejectionText(a.Rejection!.Value, a.RejectionValue, a.Action.AxialForce, template.TransverseMomentLimit, workspace))
        };
        return new()
        {
            Name = a.Action.Name, State = state, AxialKn = Kn(a.Action.AxialForce), MxKnm = Knm(a.Action.Moment1), MyKnm = Knm(a.Action.Moment2),
            MomentKnm = Knm(a.Moment), TransverseKnm = Knm(a.TransverseMoment), TransverseNeglected = a.TransverseNeglected,
            DirectionDegrees = a.Status == ModelColumnActionStatus.Tension ? null : a.DirectionDegrees, FirstOrderTotalKnm = Knm(r?.FirstOrder.TotalMoment),
            MaximumFirstOrderKnm = r is null ? null : Knm(r.Capacity.MaximumFirstOrderMoment), Ratio = Finite(a.Ratio),
            BeyondResistance = a.Rejection == ModelColumnRejection.AxialForceBeyondResistance, Mode = r is null ? "" : Mode(r.Capacity.Mode),
            EffectiveCreepRatio = r?.Creep.EffectiveRatio, Verified = a.Verified, Status = status, Outcome = outcome,
            Governing = envelope.Governing == a.Index, Shown = envelope.GoverningCalculated == a.Index
        };
    }

    private static ConcreteModelColumnResult.DomainValues Domain(ModelColumnDomain d) => new()
    {
        DirectionDegrees = d.DirectionDegrees, EffectiveCreepRatio = d.EffectiveCreepRatio, CompressionResistanceKn = Kn(d.CompressionResistance),
        ImperfectionEccentricity = d.ImperfectionEccentricity, MinimumEccentricity = d.MinimumEccentricity,
        Points = d.Points.Select(p => new ConcreteModelColumnResult.DomainPoint
        {
            AxialKn = Kn(p.AxialForce), MaximumFirstOrderKnm = Knm(p.MaximumFirstOrderMoment), SectionKnm = Knm(p.UltimateMoment),
            Mode = p.Mode is ModelColumnMode mode ? Mode(mode) : "", Rejected = p.Rejection is not null
        }).ToArray()
    };

    // ---------------------------------------------------------------- request

    private static double Number(JsonObject? options, string key, string label) => SectionWorkspace.Number(Text(options, key), label);

    private static double? Optional(JsonObject? options, string key, string label)
        => string.IsNullOrWhiteSpace(Text(options, key)) ? null : Number(options, key, label);

    private static int Choice(JsonObject? options, string key, ImmutableArray<string> choices, string label)
    {
        int index = choices.IndexOf(Text(options, key));
        return index >= 0 ? index : throw new ArgumentException(label + ": scelta non riconosciuta.");
    }

    /// <summary>
    /// La richiesta dei campi del form. Con le combinazioni è il modello dell'inviluppo: N e M0Ed non servono (M0Ed vuoto non è richiesto) e la
    /// tolleranza su N di MRd(N) segue la regola della libreria per <paramref name="largestAxialForce"/> (il |N| maggiore delle combinazioni, N).
    /// </summary>
    internal static ModelColumnRequest Request(JsonObject? options, CheckerSection engine, double? largestAxialForce = null)
    {
        // with the combinations N and M0Ed of the form are hidden and not read (the envelope takes them from each combination)
        bool single = largestAxialForce is null;
        double axial = single ? Number(options, "N", "N") * NewtonsPerKilonewton : 0;
        double direction = Number(options, "theta", "Direzione θ");
        if (string.IsNullOrWhiteSpace(Text(options, "l0"))) throw new ArgumentException("Inserire la lunghezza libera di inflessione l0.");
        if (string.IsNullOrWhiteSpace(Text(options, "l"))) throw new ArgumentException("Inserire la lunghezza dell'elemento l (per αh di EN 1992-1-1 5.2(5) e per l/300).");
        double effective = Number(options, "l0", "l0") * MillimetresPerMetre, member = Number(options, "l", "l") * MillimetresPerMetre;
        double? moment = single ? Optional(options, "M0Ed", "M0Ed") * NmmPerKilonewtonMetre : null;
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
        if (double.IsFinite(tolerance) && tolerance > 0)
            tolerance = Math.Max(tolerance, DomainPointAxialTolerance.Calculate(engine.Checker.SectionSolver, largestAxialForce is double largest ? -largest : axial));
        double transverse = Number(options, "limite_trasversale", "Limite del momento trasversale") / 100;
        return new ModelColumnRequest(axial, direction, effective, member, moment, factor, imperfection, minimum, creep, ratio, steps, tolerance, transverse);
    }

    /// <summary>Le righe dei dati per la relazione: etichetta e valore effettivo (salvato o predefinito).</summary>
    private static IReadOnlyList<ConcreteModelColumnResult.Row> Inputs(JsonObject? options, int source)
    {
        string T(string key) => Text(options, key);
        var rows = new List<ConcreteModelColumnResult.Row> { new("Azioni", T("azioni")) };
        if (source == 0)
        {
            rows.Add(new("N costante (− compressione) [kN]", T("N")));
            rows.Add(new("Direzione del momento θ [°]", T("theta")));
            rows.Add(new("M0Ed nella sezione critica, senza imperfezione [kNm]", string.IsNullOrWhiteSpace(T("M0Ed")) ? "solo capacità" : T("M0Ed")));
        }
        else rows.Add(new("Direzione del momento θ [°] (Mθ = Mx·cos θ + My·sin θ)", T("theta")));
        rows.AddRange(new ConcreteModelColumnResult.Row[]
        {
            new("Lunghezza libera di inflessione l0 [m]", T("l0")), new("Lunghezza dell'elemento l [m]", T("l")), new("Fattore della curvatura c", T("c")),
            new("Imperfezione", T("imperfezione") == ImperfectionModes[3] ? T("imperfezione") + " · ei = " + T("ei") + " mm" : T("imperfezione")),
            new("Eccentricità minima", T("eccentricita_minima")), new("Viscosità", T("viscosita"))
        });
        int creep = CreepModes.IndexOf(T("viscosita"));
        if (creep == 1) rows.Add(new("φef", T("phi_ef")));
        if (!string.IsNullOrWhiteSpace(T("phi_inf"))) rows.Add(new("φ(∞, t0)", T("phi_inf")));
        if (creep == 2) { rows.Add(new("M0Eqp [kNm]", T("M0Eqp"))); rows.Add(new("NEqp (− compressione) [kN]", T("NEqp"))); }
        rows.Add(new("rm = M01/M02", string.IsNullOrWhiteSpace(T("rm")) ? "non noto (C = 0,7)" : T("rm")));
        rows.Add(new("Passi di curvatura", T("passi"))); rows.Add(new("Tolleranza su N di MRd(N) [kN]", T("tolleranza_n")));
        rows.Add(new("Momento trasversale ammesso [%]", T("limite_trasversale")));
        rows.Add(new("Intervalli del dominio ridotto", T("intervalli_dominio")));
        return rows;
    }

    // ---------------------------------------------------------------- messages

    /// <summary>Il motivo di un rifiuto della libreria, in italiano.</summary>
    internal static string Rejection(ModelColumnException rejection, JsonObject workspace, ModelColumnRequest request)
        => RejectionText(rejection.Reason, rejection.Value, request.AxialForce, request.TransverseMomentLimit, workspace);

    /// <summary>Il motivo di un rifiuto, in italiano: il valore del rifiuto, lo sforzo normale della richiesta o della combinazione (N) e il limite trasversale.</summary>
    private static string RejectionText(ModelColumnRejection reason, double value, double axialForce, double transverseLimit, JsonObject workspace) => reason switch
    {
        ModelColumnRejection.InvalidRequest => "Dati della colonna modello non validi: N e θ finiti; l0 e l maggiori di zero; c fra 8 e 10; passi fra 10 e 500; "
            + "tolleranza su N positiva; momento trasversale ammesso fra 0 e 100 %; M0Ed ≥ 0; rm fra −1 e 1; ei ≥ 0; φef e φ(∞, t0) fra 0 e 10; "
            + "la (5.19) richiede M0Ed, M0Eqp e NEqp di compressione.",
        ModelColumnRejection.UnsupportedStandard => "Colonna modello disponibile per NTC 2018, CNR-DT 200, EN 1992-1-1 (valori raccomandati) e UNI EN 1992-1-1 "
            + "con DM 31/07/2012; non per " + workspace.S("normativa") + ".",
        ModelColumnRejection.UnsupportedSection => "Colonna modello disponibile per le sezioni in calcestruzzo ordinario delle norme europee, con barre e senza profili d'acciaio.",
        ModelColumnRejection.UnsupportedChecker => "Colonna modello: dominio plastico e calcestruzzo teso trascurato (NTC 2018 §4.1.2.3.9.2).",
        ModelColumnRejection.TensionAxialForce => "N di trazione: la colonna modello vale per gli elementi compressi (N ≤ 0, compressione negativa).",
        ModelColumnRejection.AxialForceBeyondResistance => "|N| = " + Kilo(Math.Abs(axialForce)) + " kN oltre la resistenza a compressione centrata della sezione NRd,c = "
            + Kilo(Math.Abs(value)) + " kN.",
        ModelColumnRejection.TransverseMomentBeyondLimit => "Momento trasversale pari al " + (value * 100).ToString("0.##", CultureInfo.CurrentCulture)
            + " % del momento nella direzione θ, oltre il " + (transverseLimit * 100).ToString("0.##", CultureInfo.CurrentCulture)
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

    private static ConcreteModelColumnResult Convert(ModelColumnResult r, IReadOnlyList<ConcreteModelColumnResult.Row> inputs, ModelColumnRequest request, Extra extra)
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
            Warnings = extra.Warnings.Concat(r.Warnings.Select(Warning)).ToArray(),
            Verified = extra.Verified,
            Inputs = inputs,
            TransverseLimit = request.TransverseMomentLimit,
            Source = extra.Source, Combinations = extra.Combinations, Domains = extra.Domains, Governing = extra.Governing, Theta = extra.Theta
        };
        if (extra.Combinations.Count == 0)
            result.Outcome = result.Verified is null ? "Solo capacità" : result.Verified.Value ? "Verificata" : "Non verificata (instabilità)";
        else result.Outcome = result.Verified switch
        {
            true => "Verificata (tutte le combinazioni)",
            false => "Non verificata (" + extra.Combinations.Count(c => c.Verified == false) + " combinazioni)",
            null => "Esito incompleto (combinazioni non calcolabili)"
        };
        result.Status = extra.EnvelopeStatus is null ? Status(result) : extra.EnvelopeStatus + " Combinazione mostrata «" + extra.Governing + "»: " + Status(result);
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

    /// <summary>
    /// Una combinazione dell'inviluppo (1.16.0): nome, insieme (SLU, SLV), N, Mx, My [kN, kNm], il momento nella direzione θ e quello normale
    /// [kNm], la direzione della colonna (θ o θ + 180°; null per la trazione), M0Ed,tot, M1Rd,max, η (null se infinito, con
    /// <see cref="BeyondResistance"/> oltre NRd,c), modo, φef, esito; <see cref="Governing"/> per la combinazione con η massimo,
    /// <see cref="Shown"/> per quella dei grafici e dei riquadri (la peggiore fra le calcolate).
    /// </summary>
    public sealed class CombinationValues
    {
        public string Name { get; init; } = "";
        public string State { get; init; } = "";
        public double AxialKn { get; init; }
        public double MxKnm { get; init; }
        public double MyKnm { get; init; }
        public double MomentKnm { get; init; }
        public double TransverseKnm { get; init; }
        public bool TransverseNeglected { get; init; }
        public double? DirectionDegrees { get; init; }
        public double? FirstOrderTotalKnm { get; init; }
        public double? MaximumFirstOrderKnm { get; init; }
        public double? Ratio { get; init; }
        public bool BeyondResistance { get; init; }
        public string Mode { get; init; } = "";
        public double? EffectiveCreepRatio { get; init; }
        public bool? Verified { get; init; }
        /// <summary>«Verificata», «Non verificata», «Trazione» o «Non calcolabile».</summary>
        public string Status { get; init; } = "";
        public string Outcome { get; init; } = "";
        public bool Governing { get; init; }
        public bool Shown { get; init; }
    }

    /// <summary>Un punto del dominio ridotto: N [kN, − compressione], M1Rd,max e MRd = M(χu) della sezione [kNm] (null se rifiutato), modo.</summary>
    public sealed class DomainPoint
    {
        public double AxialKn { get; init; }
        public double? MaximumFirstOrderKnm { get; init; }
        public double? SectionKnm { get; init; }
        public string Mode { get; init; } = "";
        public bool Rejected { get; init; }
    }

    /// <summary>
    /// Il dominio ridotto N–M1Rd,max di una direzione (1.16.0): θ [°], φef dei punti, NRd,c [kN, − compressione], eccentricità
    /// dell'imperfezione e minima [mm], punti da N = 0 a NRd,c.
    /// </summary>
    public sealed class DomainValues
    {
        public double DirectionDegrees { get; init; }
        public double EffectiveCreepRatio { get; init; }
        public double CompressionResistanceKn { get; init; }
        public double ImperfectionEccentricity { get; init; }
        public double? MinimumEccentricity { get; init; }
        public IReadOnlyList<DomainPoint> Points { get; init; } = [];
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
    /// <summary>Null per la sola capacità; con le combinazioni l'esito dell'inviluppo (null se una combinazione non è calcolabile).</summary>
    public bool? Verified { get; init; }
    /// <summary>
    /// «Verificata», «Non verificata (instabilità)» o «Solo capacità»; con le combinazioni «Verificata (tutte le combinazioni)», «Non verificata
    /// (n combinazioni)» o «Esito incompleto (combinazioni non calcolabili)».
    /// </summary>
    public string Outcome { get; internal set; } = "";
    public string Status { get; internal set; } = "";
    /// <summary>Le azioni verificate (scelta di <see cref="ConcreteModelColumn.ActionSources"/>, 1.16.0).</summary>
    public string Source { get; init; } = "";
    /// <summary>Le combinazioni dell'inviluppo; vuoto per l'azione singola. I campi della colonna singola sono quelli di <see cref="Governing"/>.</summary>
    public IReadOnlyList<CombinationValues> Combinations { get; init; } = [];
    /// <summary>I domini ridotti: θ e, con momenti negativi nelle combinazioni, θ + 180°.</summary>
    public IReadOnlyList<DomainValues> Domains { get; init; } = [];
    /// <summary>La combinazione mostrata nei grafici e nei riquadri (la peggiore fra le calcolate); null per l'azione singola.</summary>
    public string? Governing { get; init; }
    /// <summary>La direzione θ del form [°]: i domini di θ + 180° si disegnano con i momenti negativi.</summary>
    public double Theta { get; init; }
}
