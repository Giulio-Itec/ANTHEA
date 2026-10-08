using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Serviceability;

namespace Anthea.Calculations;

/// <summary>
/// Strato di mappatura delle verifiche SLE della sezione (refactoring F2.7b, commit A4; progetto F2.7 §5): insiemi ↔ combinazioni, requisiti,
/// stati, regioni, traccia della fessurazione e rifiuti, dal contratto della libreria (codici stabili, esiti e motivi strutturati, testi inglesi)
/// ai testi del legacy (Ntc2018Checks, ConcreteTensionCracking, ConcreteInnerCracking, ConcreteCodeChecks, LegacyServiceability).
/// Solo testi e conversioni: nessuna costante normativa, nessuna formula. Un codice, un esito, un flag, un'unità o un testo della libreria senza
/// traduzione è un errore di programma (<see cref="InvalidOperationException"/>, prova 5a). Tabelle immutabili (FrozenDictionary, prova 5l).
/// </summary>
public static partial class ConcreteLibraryMapping
{
    // ------------------------------------------------------------------ insiemi e combinazioni
    /// <summary>Insiemi SLE di ANTHEA → combinazioni della libreria. Gli altri insiemi (ED, CURVA, LIMITE, BENCH, SLU, SLV) non sono combinazioni SLE.</summary>
    static readonly FrozenDictionary<string, ServiceabilityCombination> Combinations = new Dictionary<string, ServiceabilityCombination>(StringComparer.Ordinal)
    {
        ["SLE"] = ServiceabilityCombination.Characteristic, ["SLE_FREQ"] = ServiceabilityCombination.Frequent, ["SLE_QP"] = ServiceabilityCombination.QuasiPermanent
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Combinazione SLE dell'insieme; null per gli insiemi senza limiti tensionali (ED, CURVA, LIMITE, BENCH…).</summary>
    public static ServiceabilityCombination? CombinationOf(string set) => Combinations.TryGetValue(set, out var combination) ? combination : null;

    /// <summary>Combinazione SLE dell'insieme per la fessurazione: un insieme non SLE è un dato non valido.</summary>
    public static ServiceabilityCombination RequireCombination(string set)
        => CombinationOf(set) ?? throw new ArgumentException("Fessurazione: «" + set + "» non è un insieme di combinazioni SLE.");

    /// <summary>Combinazione della libreria → insieme di ANTHEA.</summary>
    public static string SetOf(ServiceabilityCombination combination)
    {
        foreach (var (set, value) in Combinations) if (value == combination) return set;
        throw Unmapped("combinazione SLE", combination.ToString());
    }

    // ------------------------------------------------------------------ limiti tensionali
    /// <summary>Stato tensionale del legacy dall'esito della libreria (Satisfied): entro o oltre i limiti, oppure soltanto calcolato.</summary>
    public static string StressStatus(bool? satisfied) => satisfied switch
    {
        null => "Stato tensionale calcolato",
        true => "Entro limiti tensionali",
        false => "Oltre limiti tensionali"
    };

    static readonly FrozenDictionary<string, string> StressLimitMessages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Invalid concrete stress ratio."] = "Checker: tasso tensionale non valido.",
        ["Invalid concrete stress limit."] = "Checker: tasso tensionale non valido.",
        ["Invalid steel stress limit."] = "Checker: tasso tensionale non valido.",
        ["Non-finite stresses: stress analysis not converged."] = "Checker: tensioni non finite.",
        ["Stress analysis without strain plane: not converged."] = "Checker: analisi tensionale non convergente."
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Rifiuto di StressLimitCheck.Evaluate → rifiuto del legacy (DescribeStress, LegacyServiceability).</summary>
    public static ArgumentException StressLimitError(Exception e)
        => StressLimitMessages.TryGetValue(e.Message, out var text) ? new ArgumentException(text) : throw Unmapped("rifiuto dei limiti tensionali", e.Message);

    // ------------------------------------------------------------------ norme, esposizioni e requisiti
    /// <summary>Profilo della libreria → nome della norma in ANTHEA (testi della traccia che citano la norma).</summary>
    static readonly FrozenDictionary<CrackProfile, string> ProfileNames = new Dictionary<CrackProfile, string>
    {
        [CrackProfile.Ntc2018] = "NTC 2018", [CrackProfile.ModelCode2010] = "Model Code 2010", [CrackProfile.EN1992p11] = "EN 1992-1-1",
        [CrackProfile.UniEN1992p11] = "UNI EN 1992-1-1", [CrackProfile.DinEN1992p11] = "DIN EN 1992-1-1", [CrackProfile.DsEN1992p11] = "DS EN 1992-1-1",
        [CrackProfile.NsEN1992p11] = "NS EN 1992-1-1", [CrackProfile.CnrDT200] = "CNR-DT 200"
    }.ToFrozenDictionary();

    public static string ProfileName(CrackProfile profile) => ProfileNames.TryGetValue(profile, out var name) ? name : throw Unmapped("profilo della fessurazione", profile.ToString());

    /// <summary>Profili con la tabella dei requisiti NTC (NTC 2018, UNI EN 1992-1-1, CNR-DT 200): il legacy non legge 'limite_fessure'.</summary>
    static bool NtcRequirementTable(CrackProfile profile) => profile is CrackProfile.Ntc2018 or CrackProfile.UniEN1992p11 or CrackProfile.CnrDT200;

    /// <summary>Profili con la formula NTC 2018 dell'ampiezza (Ntc2018Checks.CalculateCrackWidth): i testi della traccia sono quelli NTC.</summary>
    static bool NtcWidthFormula(CrackProfile profile) => profile is CrackProfile.Ntc2018 or CrackProfile.CnrDT200;

    /// <summary>Vero se il legacy legge 'limite_fessure' (famiglia Eurocodice e Model Code 2010, ConcreteCodeChecks.CrackRequirement).</summary>
    public static bool ReadsDesignLimit(CrackProfile profile) => !NtcRequirementTable(profile);

    /// <summary>Vero se la condizione di hc,eff usa il copriferro (DIN, ConcreteCodeChecks.EffectiveCrackDepth): l'adattatore lo passa sempre.</summary>
    public static bool UsesEffectiveDepthCover(CrackProfile profile) => profile == CrackProfile.DinEN1992p11;

    /// <summary>Classe di esposizione del foglio → classe della libreria; «Da scegliere», vuota o sconosciuta → null (esposizione assente).</summary>
    public static string? ExposureOf(string text) => CrackRequirements.ExposureClasses.Contains(text, StringComparer.Ordinal) ? text : null;

    /// <summary>Disposizione delle barre per l'interasse automatico (TensionBarSpacing.cs:16-19): anelli concentrici solo con i secondi anelli
    /// del modello parametrico, mai con le barre manuali.</summary>
    public static CrackBarLayout BarLayout(string shape, JsonObject sectionInput)
        => shape != "Circolare" ? CrackBarLayout.Rows
         : !sectionInput.ContainsKey("barre_manuali") && sectionInput.B("second_inner_enabled") ? CrackBarLayout.ConcentricRings : CrackBarLayout.Ring;

    /// <summary>
    /// Geometria della fessurazione dal costruttore esplicito della libreria (CrackSectionGeometry.cs:46): contorno, fori e barre ordinarie della
    /// sezione di ANTHEA (mm, stesse coordinate del piano di deformazione; diametro e area di ogni barra come nel legacy), disposizione per l'interasse.
    /// </summary>
    public static CrackSectionGeometry CrackGeometry(SezioneCA section)
        => new(section.Outline.Select(Point), section.Holes.Select(h => h.Select(Point)), section.Bars.Select(b => new CrackBar(b.X, b.Y, b.Diametro, b.Area)),
            BarLayout(section.Shape, section.Input));

    /// <summary>Vertice di ANTHEA [x, y] → punto della libreria (indici delle coordinate, non costanti).</summary>
    static GPC.Geometry.Point2d Point(double[] p) => new(p[0], p[1]);

    /// <summary>Requisito della libreria → testo del criterio del legacy (ConcreteCodeChecks.CrackRequirement, Ntc2018Checks.CrackRequirement).</summary>
    public static string CrackRequirementText(CrackRequirement requirement, CrackProfile profile)
    {
        bool ntc = NtcRequirementTable(profile);
        return requirement.Criterion switch
        {
            CrackCriterion.NotRequired => ntc ? "Non richiesta nella rara"
                : "Non richiesta: verificare " + SectionWorkspace.Label(SetOf(requirement.RequiredCombination ?? throw Unmapped("combinazione richiesta", profile.ToString()))),
            CrackCriterion.ExposureRequired => ntc ? "Selezionare la classe di esposizione" : "Selezionare esposizione XC/XD/XS oppure wlim di progetto",
            CrackCriterion.DesignLimitRequired => profile switch
            {
                CrackProfile.ModelCode2010 => "Selezionare wlim di progetto per Model Code 2010",
                CrackProfile.DsEN1992p11 => "Selezionare wlim di progetto per questa esposizione",
                _ => "Selezionare esposizione XC/XD/XS oppure wlim di progetto"
            },
            CrackCriterion.Decompression => "Decompressione",
            CrackCriterion.CrackFormation => "Formazione fessure",
            CrackCriterion.CrackWidth => "Apertura fessure",
            _ => throw Unmapped("criterio della fessurazione", requirement.Criterion.ToString())
        };
    }

    /// <summary>Criteri della libreria tradotti da <see cref="CrackRequirementText"/> (prova 5a).</summary>
    public static IReadOnlyCollection<CrackCriterion> TranslatedCriteria { get; } = new[]
    {
        CrackCriterion.NotRequired, CrackCriterion.ExposureRequired, CrackCriterion.DesignLimitRequired, CrackCriterion.Decompression,
        CrackCriterion.CrackFormation, CrackCriterion.CrackWidth
    }.ToFrozenSet();

    /// <summary>Profili con un nome in ANTHEA (prova 5a).</summary>
    public static IReadOnlyCollection<CrackProfile> TranslatedProfiles => ProfileNames.Keys;

    // ------------------------------------------------------------------ regioni
    // Chiave della regione → (prefisso delle voci della traccia, nome della regione in 'Regions' e nei riepiloghi).
    static readonly FrozenDictionary<string, (string Prefix, string Name)> RegionNames = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["TensileZone"] = ("Zona tesa efficace", "Zona tesa efficace"),
        ["Face+x"] = ("Faccia +x", "Faccia +x"), ["Face-x"] = ("Faccia −x", "Faccia −x"), ["Face+y"] = ("Faccia +y", "Faccia +y"), ["Face-y"] = ("Faccia −y", "Faccia −y"),
        ["DsCoarseSystem"] = ("Sistema grossolano", "Sistema grossolano DS"),
        ["InnerWall+x"] = ("Parete interna +x", "Parete interna +x"), ["InnerWall-x"] = ("Parete interna −x", "Parete interna −x"),
        ["InnerWall+y"] = ("Parete interna +y", "Parete interna +y"), ["InnerWall-y"] = ("Parete interna −y", "Parete interna −y"),
        ["InnerRing"] = ("Anello interno", "Anello interno"),
        [InnerSurfacesKey] = ("Superfici interne", "Superfici interne")
    }.ToFrozenDictionary(StringComparer.Ordinal);
    const string RadialKey = "Radial(", InnerSurfacesKey = "InnerSurfaces";

    /// <summary>Nome della regione in ANTHEA ('Regions', stati e riepiloghi). Fasce radiali: angolo con il formato 0.## nella cultura corrente,
    /// come ConcreteTensionCracking.cs:28 (la chiave lo porta già arrotondato nella cultura invariante).</summary>
    public static string RegionName(string key) => RegionText(key).Name;

    /// <summary>Prefisso delle voci della traccia di una regione (ConcreteTensionCracking.cs:54, :70; ConcreteInnerCracking.cs:86).</summary>
    public static string TracePrefix(string key) => RegionText(key).Prefix;

    static (string Prefix, string Name) RegionText(string key)
    {
        if (RegionNames.TryGetValue(key, out var text)) return text;
        if (key.StartsWith(RadialKey, StringComparison.Ordinal) && key.EndsWith(')')
            && double.TryParse(key.AsSpan(RadialKey.Length, key.Length - RadialKey.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double angle))
        {
            string name = $"Fascia radiale {angle:0.##}°";
            return (name, name);
        }
        throw Unmapped("regione della fessurazione", key);
    }

    static bool IsInnerSurface(string? key) => key is not null && (key.StartsWith("InnerWall", StringComparison.Ordinal) || key == "InnerRing");
    static bool IsFace(string? key) => key is not null && (key.StartsWith("Face", StringComparison.Ordinal) || key.StartsWith(RadialKey, StringComparison.Ordinal));

    /// <summary>Regione della libreria → regione efficace del legacy (nome, direzione, livello, area, barre, armatura, ampiezza, poligoni).</summary>
    public static ConcreteEffectiveRegion ToRegion(CrackRegion r)
        => new(RegionName(r.Key), r.Qx, r.Qy, r.Level, r.Area, r.BarIndices.ToArray(), r.SteelArea, r.Width,
            r.Outline.Select(p => new[] { p.X, p.Y }).ToArray(), r.Holes.Select(h => h.Select(p => new[] { p.X, p.Y }).ToArray()).ToArray());

    static readonly FrozenDictionary<string, string> SpacingSources = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Automatic"] = "Automatico geometrico", ["Manual"] = "Manuale", ["InnerSurface"] = "Geometria della superficie interna"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static string? SpacingSourceText(string? source)
        => source is null ? null : SpacingSources.TryGetValue(source, out var text) ? text : throw Unmapped("origine dell'interasse", source);

    // ------------------------------------------------------------------ opzione legacy nominata del BarSpacing (F2.7-D5)
    /// <summary>
    /// Difetto del legacy conservato (decisione F2.7-D5, proposta U1 dell'utente da applicare dopo l'interruttore): nella sezione interamente
    /// tesa con la formula NTC il legacy cerca il simbolo «s» nella traccia della faccia governante (ConcreteTensionCracking.cs:75), ma la
    /// formula NTC scrive «s (formula)», quindi 'BarSpacing' resta nullo. Attiva: stesso risultato del legacy.
    /// </summary>
    public const bool LegacyEntirelyTensileNtcBarSpacing = true;

    // ------------------------------------------------------------------ esito della fessurazione
    // Stati del legacy delle verifiche fermate senza ampiezza (Ntc2018Checks.cs:94-167) e testi delle superfici interne incomplete
    // (ConcreteInnerCracking.cs:57, :64; il caso delle superfici non supportate non esiste in ANTHEA, che tratta solo fori rettangolari e anelli).
    static readonly FrozenDictionary<CrackOutcome, string> OuterStops = new Dictionary<CrackOutcome, string>
    {
        [CrackOutcome.PrestressNotSupported] = "Apertura CAP: modello aderenza/decompressione da definire",
        [CrackOutcome.RequiresLinearCrackedAnalysis] = "wk richiede analisi lineare con CLS teso escluso",
        [CrackOutcome.NeutralAxisUndetermined] = "Asse neutro non determinato",
        [CrackOutcome.NoTensileReinforcement] = "Nessuna armatura tesa"
    }.ToFrozenDictionary();
    static readonly FrozenDictionary<CrackOutcome, string> InnerMissing = new Dictionary<CrackOutcome, string>
    {
        [CrackOutcome.InnerSurfaceUnreinforced] = ": superficie del foro tesa senza armatura efficace",
        [CrackOutcome.SpacingUndetermined] = ": inserire l’interasse massimo per la superficie del foro",
        [CrackOutcome.InnerSurfaceNotSupported] = ": implementate per un solo foro rettangolare allineato agli assi o per un anello circolare"
    }.ToFrozenDictionary();
    static readonly FrozenDictionary<CrackReason, string> NoEffectiveAreaStops = new Dictionary<CrackReason, string>
    {
        [CrackReason.ZeroEffectiveDepth] = "Area efficace nulla",
        [CrackReason.NoEffectiveSteelOrArea] = "Armatura/area efficace assente",
        [CrackReason.FaceWithoutAreaOrSteel] = ": area o armatura efficace assente"
    }.ToFrozenDictionary();

    /// <summary>Esiti della libreria tradotti (prova 5a): stati fissi, superfici interne, requisiti, verifiche con ampiezza e rami composti.</summary>
    public static IReadOnlyCollection<CrackOutcome> TranslatedOutcomes { get; } = OuterStops.Keys.Concat(InnerMissing.Keys)
        .Concat(new[] { CrackOutcome.Evaluated, CrackOutcome.NotRequired, CrackOutcome.MissingExposure, CrackOutcome.MissingDesignLimit, CrackOutcome.NoEffectiveArea })
        .ToFrozenSet();

    /// <summary>Motivi della libreria tradotti (prova 5a); None non ha un testo proprio.</summary>
    public static IReadOnlyCollection<CrackReason> TranslatedReasons { get; } = NoEffectiveAreaStops.Keys.Append(CrackReason.None).ToFrozenSet();

    /// <summary>Voci che ripetono gli ingressi della verifica, scritte dalla mappatura prima della traccia (Ntc2018Checks.cs:60-67).</summary>
    public static CrackCalculationDetail[] CrackInputDetails(string set, string exposure, string sensitivity, string duration, string bond, bool linear,
        string tensileConcrete, string axes, double n, double mx, double my, double psi, double gammaC, double gammaS) =>
    [
        new("Verifica", null, "", SectionWorkspace.Label(set) + " · " + exposure + " · " + sensitivity + " · durata " + duration + " · aderenza " + bond),
        new("Modello", null, "", (linear ? "Lineare" : "Non lineare") + " · CLS teso: " + tensileConcrete, "Tensioni e piano di deformazione provenienti da Checker; compressione negativa."),
        new("N", n, "kN", "Azione negli assi " + axes),
        new("Mx", mx, "kNm", "Azione della combinazione"),
        new("My", my, "kNm", "Azione della combinazione"),
        new("φ", psi, "−", "Coefficiente utilizzato nell'analisi tensionale"),
        new("γc (input)", gammaC, "−", "Coefficiente di materiale della sezione", "Non compare direttamente nella formula wk: qui si usa fctm, non fctd."),
        new("γs (input)", gammaS, "−", "Coefficiente di materiale della sezione", "Non è applicato come divisore aggiuntivo di σs nella formula wk.")
    ];

    /// <summary>
    /// Risultato della libreria → DTO del legacy: voci degli ingressi, norma, criterio e wlim, poi la traccia tradotta; stato composto dai dati
    /// strutturati (esito, motivo, esiti per regione, regione governante), mai dal testo inglese.
    /// </summary>
    /// <param name="requirement">Requisito della verifica (quello del risultato o, se la libreria non lo riporta, quello calcolato con gli stessi dati).</param>
    public static Ntc2018Checks.CrackResult ToCrackResult(SectionCrackResult r, CrackRequirement requirement, CrackProfile profile, IEnumerable<CrackCalculationDetail> header, bool hollow)
    {
        string kind = CrackRequirementText(requirement, profile);
        var details = header.ToList();
        details.Add(new("Normativa fessurazione", null, "", ProfileName(profile)));
        details.Add(new("Criterio", null, "", kind, "Criterio selezionato dal codice in funzione di famiglia SLE, esposizione e sensibilità."));
        details.Add(new("wlim", requirement.Limit, "mm", "Limite di apertura selezionato",
            requirement.Limit is null ? "Nessun limite di apertura numerico per questo ramo." : "Confronto wk ≤ wlim"));
        details.AddRange(r.Trace.Select(e => CrackDetail(e, profile)));
        double? spacing = LegacyEntirelyTensileNtcBarSpacing && NtcWidthFormula(profile) && IsFace(r.GoverningRegion) ? null : r.BarSpacing;
        return new(r.Width, r.Limit, r.Ratio, r.Passed, CrackStatus(r, requirement.Criterion, kind, hollow), r.EffectiveArea, r.EffectiveSteel, spacing, SpacingSourceText(r.SpacingSource))
        {
            Details = details.ToArray(),
            Regions = r.Regions.Select(ToRegion).ToArray()
        };
    }

    static string CrackStatus(SectionCrackResult r, CrackCriterion criterion, string kind, bool hollow)
    {
        if (criterion is CrackCriterion.NotRequired or CrackCriterion.ExposureRequired or CrackCriterion.DesignLimitRequired) return kind;
        if (criterion is CrackCriterion.Decompression or CrackCriterion.CrackFormation)
            return kind + (r.Passed == true ? ": soddisfatta" : ": non soddisfatta") + $" · σct,max={r.UncrackedMaximumStress:0.00} MPa; limite={r.StressLimit:0.00}";
        if (criterion != CrackCriterion.CrackWidth) throw Unmapped("criterio della fessurazione", criterion.ToString());
        if (r.Width is null) return StopStatus(r);
        var trace = r.Trace;
        bool Has(string code) => trace.Any(e => e.Code == code);
        double limit = r.Limit ?? throw Unmapped("limite dell'apertura", r.Outcome.ToString());
        string outer;
        if (Has(CrackTraceCodes.GoverningFace))
        {
            var face = trace.Last(e => e.Code == CrackTraceCodes.GoverningFace);
            outer = "Interamente tesa · " + RegionName(face.Region!) + (face.Value <= limit ? " · apertura entro limite" : " · apertura oltre limite");
        }
        else if (Has(CrackTraceCodes.NearestBarDepth)) outer = "Asse neutro nel copriferro: nessuna barra tesa, wk = 0";
        else if (r.RegionOutcomes.FirstOrDefault(o => o.Key == "TensileZone") is { Width: double w })
            outer = trace.Any(e => e.Code == CrackTraceCodes.MaximumCrackSpacing && e.Region is null && e.HasFlag(CrackTraceFlags.UpperBound))
                ? "Nessuna barra in Ac,eff: limite superiore con sr,max da (h − x) · " + (w <= limit ? "apertura entro limite" : "apertura oltre limite")
                : w <= limit ? "Apertura entro limite" : "Apertura oltre limite";
        // Sezione interamente compressa: ampiezza nulla senza regioni né regione governante, prima delle superfici interne (Ntc2018Checks.cs:124).
        else if (!r.Regions.Any() && r.GoverningRegion is null) return "Sezione interamente compressa";
        else throw Unmapped("ramo della fessurazione", r.Outcome + " " + r.GoverningRegion);
        if (!hollow) return outer;
        // Superfici interne (ConcreteInnerCracking.cs:22, :121-136): verificate (nota del contorno interno o superfici non supportate) oppure
        // contorno interno compresso.
        if (!Has(CrackTraceCodes.InnerBoundaryNote) && !r.RegionOutcomes.Any(o => o.Key == InnerSurfacesKey)) return outer + " · contorno interno compresso";
        string governing = IsInnerSurface(r.GoverningRegion) ? RegionName(r.GoverningRegion!) : outer;
        var missing = r.RegionOutcomes.Where(o => (IsInnerSurface(o.Key) || o.Key == InnerSurfacesKey) && o.Outcome != CrackOutcome.Evaluated)
            .Select(o => RegionName(o.Key) + (InnerMissing.TryGetValue(o.Outcome, out var text) ? text : throw Unmapped("esito della superficie interna", o.Outcome.ToString()))).ToArray();
        return missing.Any() ? governing + " · " + string.Join("; ", missing) : governing + " · controllate superfici esterne e interne";
    }

    /// <summary>Stato del legacy di una verifica fermata senza ampiezza (sezione, faccia o sistema grossolano).</summary>
    static string StopStatus(SectionCrackResult r)
    {
        if (OuterStops.TryGetValue(r.Outcome, out var text)) return text;
        if (r.Outcome == CrackOutcome.NoEffectiveArea)
        {
            if (!NoEffectiveAreaStops.TryGetValue(r.Reason, out var reason)) throw Unmapped("motivo della fessurazione", r.Reason.ToString());
            return r.Reason == CrackReason.FaceWithoutAreaOrSteel ? RegionName(LastRegion(r, CrackOutcome.NoEffectiveArea)) + reason : reason;
        }
        if (r.Outcome == CrackOutcome.SpacingUndetermined)
        {
            string key = LastRegion(r, CrackOutcome.SpacingUndetermined);
            return key == "TensileZone" ? "Interasse automatico non determinabile: inserire un valore manuale"
                : key == "DsCoarseSystem" ? "DS sistema grossolano: inserire interasse massimo"
                : IsFace(key) ? RegionName(key) + ": specificare l’interasse massimo delle barre" : throw Unmapped("regione senza interasse", key);
        }
        throw Unmapped("esito della fessurazione", r.Outcome.ToString());
    }

    static string LastRegion(SectionCrackResult r, CrackOutcome outcome)
        => r.RegionOutcomes.LastOrDefault(o => o.Outcome == outcome)?.Key ?? throw Unmapped("regione dell'esito", outcome.ToString());

    // ------------------------------------------------------------------ rifiuti della fessurazione
    // Codice del rifiuto (CrackRejection, in Exception.Data) → messaggio del legacy, per profilo e regola di k₂ (NtcK2FromCompressedBars).
    static readonly FrozenDictionary<string, Func<CrackProfile, bool, string>> CrackRejections = new Dictionary<string, Func<CrackProfile, bool, string>>(StringComparer.Ordinal)
    {
        [CrackRejection.WidthParameters] = (profile, _) => NtcWidthFormula(profile) ? "Parametri fessurazione non validi." : "Parametri di fessurazione non validi.",
        [CrackRejection.UpperBoundParameters] = (_, _) => "Parametri di fessurazione non validi.",
        [CrackRejection.RibbedBarsRequired] = (_, _) => "Modello di fessurazione MC/DIN implementato per barre ad aderenza migliorata.",
        [CrackRejection.BarStresses] = (_, k2FromBars) => k2FromBars ? "k₂: tensioni delle armature mancanti o non finite." : "tensioni delle armature mancanti o non finite.",
        [CrackRejection.UncrackedStressRequired] = (_, _) => "Fessurazione: tensione della sezione non fessurata non disponibile per decompressione o formazione."
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Codici di rifiuto tradotti (prova 5a).</summary>
    public static IReadOnlyCollection<string> TranslatedRejections => CrackRejections.Keys;

    /// <summary>Rifiuto della libreria con codice → stesso rifiuto con il messaggio del legacy; senza codice è un testo non tradotto.</summary>
    public static ArgumentException CrackError(ArgumentException e, CrackProfile profile, bool k2FromBars)
        => CrackRejection.CodeOf(e) is { } code && CrackRejections.TryGetValue(code, out var text) ? new ArgumentException(text(profile, k2FromBars))
         : throw Unmapped("rifiuto della fessurazione", e.Message);

    // ------------------------------------------------------------------ traccia
    /// <summary>Unità della libreria → unità del legacy.</summary>
    static readonly FrozenDictionary<string, string> Units = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [""] = "", ["−"] = "−", ["mm"] = "mm", ["mm2"] = "mm²", ["MPa"] = "MPa", ["1/mm"] = "1/mm"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Flag della traccia letti dalla mappatura (prova 5a): un flag nuovo della libreria senza traduzione è un errore di programma.</summary>
    public static IReadOnlyCollection<string> TranslatedTraceFlags { get; } = new[]
    {
        CrackTraceFlags.Summary, CrackTraceFlags.K2FromBars, CrackTraceFlags.CompressedBar, CrackTraceFlags.ZeroStressBars, CrackTraceFlags.Bending,
        CrackTraceFlags.NotInWidthFormula, CrackTraceFlags.InnerBands, CrackTraceFlags.PartiallyCompressed, CrackTraceFlags.EntirelyTensile,
        CrackTraceFlags.Decompression, CrackTraceFlags.CrackFormation, CrackTraceFlags.MinimumOfThree, CrackTraceFlags.DinCoefficient, CrackTraceFlags.DsBand,
        CrackTraceFlags.InnerBand, CrackTraceFlags.Included, CrackTraceFlags.Excluded, CrackTraceFlags.TensileBars, CrackTraceFlags.EffectiveBars,
        CrackTraceFlags.Formula, CrackTraceFlags.UpperBound, CrackTraceFlags.FormulaStandard, CrackTraceFlags.FormulaModelCode2010, CrackTraceFlags.FormulaDin,
        CrackTraceFlags.FormulaSparseBars, CrackTraceFlags.UpperBoundNtc, CrackTraceFlags.UpperBoundEurocode, CrackTraceFlags.UpperBoundDin,
        CrackTraceFlags.Assigned, CrackTraceFlags.Nominal, CrackTraceFlags.Automatic, CrackTraceFlags.Manual, CrackTraceFlags.ShortTerm, CrackTraceFlags.LongTerm,
        CrackTraceFlags.Ribbed, CrackTraceFlags.Plain, CrackTraceFlags.ComputedGoverns, CrackTraceFlags.MinimumGoverns, CrackTraceFlags.CloseBars,
        CrackTraceFlags.SparseBars, CrackTraceFlags.NearGoverns, CrackTraceFlags.FarGoverns, CrackTraceFlags.DsCoarseHalf, CrackTraceFlags.Envelope,
        CrackTraceFlags.Uniform, CrackTraceFlags.BoundedByHeight, CrackTraceFlags.Local
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Argomenti della traccia letti dalla mappatura (prova 5a).</summary>
    public static IReadOnlyCollection<string> TranslatedTraceArguments { get; } = new[]
    {
        CrackTraceArguments.Bar, CrackTraceArguments.Angle, CrackTraceArguments.SteelStress, CrackTraceArguments.TensionStiffening, CrackTraceArguments.Es,
        CrackTraceArguments.AdoptedSpacing, CrackTraceArguments.MeanStrainDifference, CrackTraceArguments.GradientTimesHeight, CrackTraceArguments.Tolerance,
        CrackTraceArguments.FromNeutralAxis
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Codici della traccia tradotti (prova 5a).</summary>
    public static IReadOnlyCollection<string> TranslatedTraceCodes => TraceTexts.Keys;

    /// <summary>
    /// Voce della traccia della libreria → voce della traccia del legacy: simbolo (con il prefisso della barra o della regione, assente nelle voci
    /// di riepilogo), valore, unità, espressione e nota. I numeri delle note sono formattati nella cultura corrente, come nel legacy.
    /// </summary>
    public static CrackCalculationDetail CrackDetail(CrackTraceEntry e, CrackProfile profile)
    {
        if (!TraceTexts.TryGetValue(e.Code, out var text)) throw Unmapped("codice della traccia", e.Code);
        foreach (var flag in e.Flags) if (!TranslatedTraceFlags.Contains(flag)) throw Unmapped("flag della traccia " + e.Code, flag);
        foreach (var argument in e.Arguments) if (!TranslatedTraceArguments.Contains(argument.Key)) throw Unmapped("argomento della traccia " + e.Code, argument.Key);
        if (!Units.TryGetValue(e.Unit, out var unit)) throw Unmapped("unità della traccia " + e.Code, e.Unit);
        var (symbol, expression, note) = text(e, profile);
        if (e.Region is not null && !e.HasFlag(CrackTraceFlags.Summary) && e.Code != CrackTraceCodes.RegionCheck) symbol = TracePrefix(e.Region) + " · " + symbol;
        return new(symbol, e.Value, unit, expression, note);
    }

    // Testi del legacy di ogni codice: (simbolo, espressione, nota). Righe del legacy nel progetto F2.7 e in k3-codici.md (Ntc2018Checks.cs, ConcreteCodeChecks.cs,
    // ConcreteTensionCracking.cs, ConcreteInnerCracking.cs).
    static readonly FrozenDictionary<string, Func<CrackTraceEntry, CrackProfile, (string Symbol, string Expression, string Note)>> TraceTexts =
        new Dictionary<string, Func<CrackTraceEntry, CrackProfile, (string, string, string)>>(StringComparer.Ordinal)
    {
        // Decompressione e formazione (Ntc2018Checks.cs:85-89)
        [CrackTraceCodes.AuxiliaryAnalysis] = (_, _) => ("Analisi ausiliaria", "Sezione omogeneizzata interamente reagente, lineare, CLS teso incluso", ""),
        [CrackTraceCodes.Fctm] = (_, _) => ("fctm", "Resistenza media a trazione del materiale Checker", ""),
        [CrackTraceCodes.UncrackedMaximumStress] = (_, _) => ("σct,max", "max delle tensioni ai vertici nella sezione interamente reagente", ""),
        [CrackTraceCodes.UncrackedStressLimit] = (e, _) => ("σct,lim", Pick(e, (CrackTraceFlags.Decompression, "0"), (CrackTraceFlags.CrackFormation, "fctm / 1,2")),
            "σct,max ≤ σct,lim; non viene eseguita una divisione per limite nullo."),
        [CrackTraceCodes.FormationDivisor] = (_, _) => ("Divisore formazione", "Coefficiente usato per fctm / 1,2", ""),
        // Barre e k₂ della sezione (Ntc2018Checks.cs:103-142; ConcreteTensionCracking.cs:16-20)
        [CrackTraceCodes.CompressedBars] = (e, _) => e.HasFlag(CrackTraceFlags.K2FromBars)
            ? ("Barre compresse per k₂", "Conteggio σs < 0 su tutte le armature ordinarie, anche fuori dall'area efficace", "")
            : ("Barre compresse", "Conteggio σs < 0 su tutte le armature ordinarie, anche fuori dall'area efficace", "Informativo: k₂ dipende dalla posizione dell'asse neutro, non dalle barre."),
        [CrackTraceCodes.TensileBars] = (e, _) => (e.HasFlag(CrackTraceFlags.K2FromBars) ? "Barre tese per k₂" : "Barre tese", "Conteggio σs > 0", ""),
        [CrackTraceCodes.ZeroStressBars] = (e, _) => (e.HasFlag(CrackTraceFlags.K2FromBars) ? "Barre a tensione nulla per k₂" : "Barre a tensione nulla", "Conteggio σs = 0", ""),
        [CrackTraceCodes.K2Criterion] = (e, profile) => ("Criterio k₂", K2CriterionText(e, profile).Expression, K2CriterionText(e, profile).Note),
        [CrackTraceCodes.EntirelyTensileK2] = (e, _) => ("k₂ · interamente tesa", "(εmax + εmin)/(2 εmax); trazione uniforme: 1",
            e.HasFlag(CrackTraceFlags.K2FromBars) ? "EC2 §7.3.4; sostituisce il criterio binario per questo ramo."
            : "Sezione interamente tesa: EN 1992-1-1 7.3.4(3), eq. (7.13); Circolare 2019 [C4.1.9]."),
        // Piano di deformazione (Ntc2018Checks.cs:121-159)
        [CrackTraceCodes.MinimumStrain] = (_, _) => ("εc,min", "min ε ai vertici, prima dei criteri di applicabilità", ""),
        [CrackTraceCodes.MaximumStrain] = (_, _) => ("εc,max", "max ε ai vertici", ""),
        [CrackTraceCodes.CompressionTolerance] = (_, _) => ("Tolleranza compressione", "Se εc,max ≤ tolleranza, wk = 0", ""),
        [CrackTraceCodes.ChiX] = (_, _) => ("χx", "Componente del gradiente di deformazione restituita da Checker", ""),
        [CrackTraceCodes.ChiY] = (_, _) => ("χy", "Componente del gradiente di deformazione restituita da Checker", ""),
        [CrackTraceCodes.StrainGradient] = (_, _) => ("|∇ε|", "sqrt(χx² + χy²)", ""),
        [CrackTraceCodes.DirectionX] = (_, _) => ("qx", "χx / |∇ε|", ""),
        [CrackTraceCodes.DirectionY] = (_, _) => ("qy", "χy / |∇ε|", ""),
        [CrackTraceCodes.Qmax] = (_, _) => ("Qmax", "max(qx·x + qy·y) sui vertici", ""),
        [CrackTraceCodes.Qmin] = (_, _) => ("Qmin", "min(qx·x + qy·y) sui vertici", ""),
        [CrackTraceCodes.Height] = (_, _) => ("h", "Qmax − Qmin; altezza proiettata lungo il gradiente", ""),
        [CrackTraceCodes.TensileDepth] = (_, _) => ("h − x", "εc,max / |∇ε|; profondità tesa", ""),
        [CrackTraceCodes.CompressedDepth] = (_, _) => ("x", "h − profondità tesa; profondità compressa", ""),
        // Area efficace della sezione parzialmente compressa (Ntc2018Checks.cs:168-190)
        [CrackTraceCodes.NearestBarDepth] = (_, _) => ("h − d,min", "Qmax − Q della barra più vicina al lembo teso", "Maggiore di h − x: nessuna barra tesa, wk = 0."),
        [CrackTraceCodes.TensileBarCount] = (_, _) => ("Numero barre tese", "Barre con ε > 0", ""),
        [CrackTraceCodes.TensileCentroid] = (_, _) => ("QG,s", "Σ(Qi·As,i) / ΣAs,i sulle barre tese", ""),
        [CrackTraceCodes.CoverToCentroid] = (_, _) => ("h − d", "Qmax − QG,s", ""),
        [CrackTraceCodes.EffectiveHeight] = (_, _) => ("d", "h − (h − d)", ""),
        [CrackTraceCodes.EffectiveDepthCandidate1] = (_, _) => ("Candidato 1 hc,eff", "2,5·(h − d)", ""),
        [CrackTraceCodes.EffectiveDepthCandidate2] = (_, _) => ("Candidato 2 hc,eff", "(h − x) / 3", ""),
        [CrackTraceCodes.EffectiveDepthCandidate3] = (_, _) => ("Candidato 3 hc,eff", "h / 2", ""),
        [CrackTraceCodes.EffectiveDepth] = (e, _) => ("hc,eff", Pick(e,
            (CrackTraceFlags.MinimumOfThree, "min[2,5·(h − d); (h − x)/3; h/2]"),
            (CrackTraceFlags.DinCoefficient, "Altezza efficace secondo NCI 7.3.2(3), coefficiente dipendente da h/(h−d)"),
            (CrackTraceFlags.DsBand, "Fascia con baricentro coincidente con As tesa · DK NA Fig.7.100"),
            (CrackTraceFlags.EntirelyTensile, "Profondità efficace della norma selezionata per sezione interamente tesa"),
            (CrackTraceFlags.InnerBand, "Fascia della parete/anello interno, limitata a metà spessore")), ""),
        [CrackTraceCodes.CutLevel] = (_, _) => ("Qtaglio", "Qmax − hc,eff; fascia efficace Q ≥ Qtaglio", ""),
        [CrackTraceCodes.EffectiveArea] = (e, _) => e.HasFlag(CrackTraceFlags.Summary) ? ("Ac,eff", RegionName(Region(e)), "")
            : e.HasFlag(CrackTraceFlags.EntirelyTensile) ? ("Ac,eff", "Area del contorno tagliato, sottratti gli eventuali fori", "")
            : e.HasFlag(CrackTraceFlags.InnerBand) ? ("Ac,eff", "Area della fascia interna tesa, depurata del foro", "")
            : ("Ac,eff", "Somma delle aree delle facce della mesh tagliata nella fascia efficace", "Integrazione geometrica sulla mesh Checker."),
        [CrackTraceCodes.EffectiveBarCount] = (_, _) => ("Numero barre efficaci", "Barre tese con Q ≥ Qtaglio − 10⁻⁸ mm", ""),
        // Barre tese, nell'ordine delle barre (Ntc2018Checks.cs:191-204)
        [CrackTraceCodes.BarStrain] = (e, _) => (Bar(e) + "ε", "ε(x,y) dal piano Checker",
            Pick(e, (CrackTraceFlags.Included, "Inclusa in As,eff"), (CrackTraceFlags.Excluded, "Tesa ma esclusa da As,eff"))),
        [CrackTraceCodes.BarX] = (e, _) => (Bar(e) + "x", "Coordinata della barra", ""),
        [CrackTraceCodes.BarY] = (e, _) => (Bar(e) + "y", "Coordinata della barra", ""),
        [CrackTraceCodes.BarQ] = (e, _) => (Bar(e) + "Q", "qx·x + qy·y", ""),
        [CrackTraceCodes.BarDiameter] = (e, _) => (Bar(e) + "Ø", "Diametro della barra", ""),
        [CrackTraceCodes.BarArea] = (e, _) => (Bar(e) + "As", "Area della barra", ""),
        [CrackTraceCodes.BarStress] = (e, _) => (Bar(e) + "σs", "Tensione nativa nella barra", ""),
        // Armatura efficace, copriferro e interasse (Ntc2018Checks.cs:209-239; ConcreteCodeChecks.cs:206-211)
        [CrackTraceCodes.EquivalentDiameter] = (e, _) => e.HasFlag(CrackTraceFlags.TensileBars) ? ("Øeq", "ΣØ² / ΣØ delle barre tese", "Nessuna barra tesa nella fascia efficace.")
            : ("Øeq", Pick(e, (CrackTraceFlags.EffectiveBars, "ΣØ² / ΣØ"), (CrackTraceFlags.Formula, "Diametro equivalente")), ""),
        [CrackTraceCodes.SteelStress] = (e, _) => e.HasFlag(CrackTraceFlags.EffectiveBars) ? ("σs", "max σs delle barre efficaci", "Si usa il massimo, non la tensione media pesata.")
            : ("σs", Pick(e, (CrackTraceFlags.TensileBars, "max σs delle barre tese, nessuna in Ac,eff"), (CrackTraceFlags.Formula, "Massima tensione nelle barre efficaci")), ""),
        [CrackTraceCodes.EffectiveSteel] = (e, _) => e.HasFlag(CrackTraceFlags.Summary) ? ("As,eff", RegionName(Region(e)), "")
            : e.HasFlag(CrackTraceFlags.EntirelyTensile) || e.HasFlag(CrackTraceFlags.InnerBand)
                ? ("As,eff", string.Join(", ", e.ArgumentsNamed(CrackTraceArguments.Bar).Select(i => $"B{(int)i + 1:00}")), "")
            : ("As,eff", "ΣAs,i delle barre tese incluse nella fascia efficace", ""),
        [CrackTraceCodes.DiameterSquareSum] = (_, _) => ("ΣØ²", "Somma sulle barre efficaci", ""),
        [CrackTraceCodes.DiameterSum] = (_, _) => ("ΣØ", "Somma sulle barre efficaci", ""),
        [CrackTraceCodes.Cover] = (e, _) => ("c", Pick(e, (CrackTraceFlags.Assigned, "Override manuale per fessurazione"), (CrackTraceFlags.Nominal, "Copriferro netto + Ø staffa"),
            (CrackTraceFlags.Formula, "Copriferro delle barre efficaci")), ""),
        [CrackTraceCodes.Spacing] = (e, _) => ("s", Pick(e, (CrackTraceFlags.Automatic, "Interasse massimo geometrico delle barre efficaci tese"),
            (CrackTraceFlags.Manual, "Interasse massimo manuale"), (CrackTraceFlags.Formula, "Interasse massimo adottato")), ""),
        [CrackTraceCodes.AnalysisConcreteModulus] = (_, _) => ("Ecls analisi", "Modulo del materiale nell'analisi Checker", ""),
        [CrackTraceCodes.AnalysisModularRatio] = (_, _) => ("n analisi", "Es·(1 + φ) / Ecls; distinto da αe usato nella formula di fessurazione", ""),
        // Formula NTC 2018 (Ntc2018Checks.cs:270-301) e famiglia Eurocodice (ConcreteCodeChecks.cs:206-211, :230-231)
        [CrackTraceCodes.Es] = (_, _) => ("Es", "Modulo elastico della prima barra efficace", ""),
        [CrackTraceCodes.Ecm] = (_, _) => ("Ecm", "Modulo medio CLS usato per αe", ""),
        [CrackTraceCodes.EffectiveTensileStrength] = (_, _) => ("fct,eff = fctm", "Resistenza media a trazione del materiale", "Nel codice attuale non è applicata una riduzione per l'età di fessurazione."),
        [CrackTraceCodes.Rho] = (_, _) => ("ρp,eff", "As,eff / Ac,eff", ""),
        [CrackTraceCodes.AlphaE] = (_, profile) => NtcWidthFormula(profile) ? ("αe", "Es / Ecm", "Non coincide necessariamente con n dell'analisi con viscosità.") : ("αe", "Es/Ecm", ""),
        [CrackTraceCodes.Kt] = (e, profile) => ("kt", NtcWidthFormula(profile)
            ? Pick(e, (CrackTraceFlags.ShortTerm, "Breve durata → 0,60"), (CrackTraceFlags.LongTerm, "Lunga durata → 0,40")) : "Coefficiente della durata / normativa", ""),
        [CrackTraceCodes.K1] = (e, _) => ("k₁", Pick(e, (CrackTraceFlags.Ribbed, "Aderenza migliorata → 0,80"), (CrackTraceFlags.Plain, "Barre lisce → 1,60")), ""),
        [CrackTraceCodes.K2] = (e, profile) => ("k₂", NtcWidthFormula(profile) ? "Coefficiente della distribuzione delle deformazioni passato dal chiamante"
            : e.HasFlag(CrackTraceFlags.NotInWidthFormula) ? "Distribuzione delle deformazioni; non entra in sr,max di " + ProfileName(profile) : "Distribuzione delle deformazioni", ""),
        [CrackTraceCodes.K3] = (_, _) => ("k₃", "Coefficiente del termine di copriferro", ""),
        [CrackTraceCodes.K4] = (_, _) => ("k₄", "Coefficiente del termine Øeq / ρp,eff", ""),
        [CrackTraceCodes.BetaMinimum] = (e, profile) => ("β minimo deformazione", e.HasFlag(CrackTraceFlags.UpperBound) ? profile == CrackProfile.ModelCode2010 ? "1 − kt" : "0,6"
            : NtcWidthFormula(profile) ? "Limite inferiore = 0,60·σs/Es" : "Limite inferiore", ""),
        [CrackTraceCodes.BetaWidth] = (_, _) => ("β apertura", "wk = 1,70·Δsm·(εsm − εcm)", ""),
        [CrackTraceCodes.FarRegionCoefficient] = (_, _) => ("Coefficiente regione distante", "Δsm,distante = 0,75·(h − x)", ""),
        [CrackTraceCodes.SpacingThresholdCoefficient] = (_, _) => ("Coefficiente soglia interasse", "s_lim = 5·(c + Øeq/2)", ""),
        [CrackTraceCodes.FormulaSteelStress] = (_, _) => ("σs (formula)", "Tensione delle barre adottata nel calcolo", ""),
        [CrackTraceCodes.FormulaDiameter] = (_, _) => ("Øeq (formula)", "Diametro equivalente delle barre efficaci", ""),
        [CrackTraceCodes.FormulaCover] = (_, _) => ("c (formula)", "Copriferro alla superficie della barra longitudinale", ""),
        [CrackTraceCodes.FormulaSpacing] = (_, _) => ("s (formula)", "Interasse massimo adottato", ""),
        [CrackTraceCodes.FormulaTensileDepth] = (_, _) => ("h − x (formula)", "Profondità della zona tesa", ""),
        [CrackTraceCodes.InteractionFactor] = (_, _) => ("1 + αe·ρp,eff", "Fattore di interazione CLS/armatura", ""),
        [CrackTraceCodes.TensionStiffening] = (_, _) => ("Δσ tension stiffening", "kt·fct,eff/ρp,eff·(1 + αe·ρp,eff)", ""),
        [CrackTraceCodes.ComputedStrainDifference] = (e, _) => ("Δε calcolata", "(σs − Δσ) / Es",
            $"({Argument(e, CrackTraceArguments.SteelStress):G10} − {Argument(e, CrackTraceArguments.TensionStiffening):G10}) / {Argument(e, CrackTraceArguments.Es):G10}"),
        [CrackTraceCodes.MinimumStrainDifference] = (e, _) => ("Δε minima", "0,60·σs / Es",
            $"0,60 × {Argument(e, CrackTraceArguments.SteelStress):G10} / {Argument(e, CrackTraceArguments.Es):G10}"),
        [CrackTraceCodes.MeanStrainDifference] = (e, _) => e.HasFlag(CrackTraceFlags.UpperBound) ? ("εsm − εcm", "βmin σs/Es (ρp,eff → 0)", "")
            : e.HasFlag(CrackTraceFlags.ComputedGoverns) || e.HasFlag(CrackTraceFlags.MinimumGoverns)
                ? ("εsm − εcm", "max(Δε calcolata; Δε minima)", Pick(e, (CrackTraceFlags.ComputedGoverns, "Governa il valore calcolato."), (CrackTraceFlags.MinimumGoverns, "Governa il minimo 0,60·σs/Es.")))
            : ("εsm − εcm", "max[(σs−kt fctm/ρeff (1+αe ρeff))/Es; βmin σs/Es]", ""),
        [CrackTraceCodes.CoverTerm] = (_, _) => ("Termine copriferro", "k₃·c", ""),
        [CrackTraceCodes.ReinforcementTerm] = (_, _) => ("Termine armatura", "k₁·k₂·k₄·Øeq / ρp,eff", ""),
        [CrackTraceCodes.NearSpacing] = (_, _) => ("Δsm,vicino", "(k₃·c + k₁·k₂·k₄·Øeq/ρp,eff) / 1,70", "Il divisore 1,70 converte questo termine in distanza media nel percorso di calcolo attuale."),
        [CrackTraceCodes.SpacingLimit] = (_, _) => ("s_lim", "5·(c + Øeq/2)", ""),
        [CrackTraceCodes.SpacingExcess] = (e, _) => ("s − s_lim", "Differenza usata per scegliere il ramo",
            Pick(e, (CrackTraceFlags.CloseBars, "s ≤ s_lim: si usa Δsm,vicino."), (CrackTraceFlags.SparseBars, "s > s_lim: si confrontano regione vicina e regione distante."))),
        [CrackTraceCodes.FarSpacing] = (e, _) => ("Δsm,distante", "0,75·(h − x)",
            Pick(e, (CrackTraceFlags.CloseBars, "Calcolato per confronto, non usato nel ramo attivo."), (CrackTraceFlags.SparseBars, "Candidato nel ramo di barre distanziate."))),
        [CrackTraceCodes.AdoptedSpacing] = (e, _) => ("Δsm adottata", Pick(e, (CrackTraceFlags.CloseBars, "Δsm,vicino"), (CrackTraceFlags.SparseBars, "max(Δsm,vicino; Δsm,distante)")),
            Pick(e, (CrackTraceFlags.NearGoverns, "Governa regione vicina alle barre."), (CrackTraceFlags.FarGoverns, "Governa regione distante dalle barre."))),
        [CrackTraceCodes.MaximumCrackSpacing] = (e, _) => ("sr,max", Pick(e,
            (CrackTraceFlags.FormulaStandard, "k3 c + k1 k2 k4 Ø/ρeff"),
            (CrackTraceFlags.FormulaModelCode2010, "2[c + fctm/(4 τbm) · Ø/ρeff]"),
            (CrackTraceFlags.FormulaDin, "min[sr secondo (7.11)/(7.14); σs Ø/(3,6 fctm)] · DIN"),
            (CrackTraceFlags.FormulaSparseBars, "1,3(h−x), barre distanziate · EC2 (7.14)"),
            (CrackTraceFlags.UpperBoundNtc, "1,7 · 0,75 (h − x), nessuna barra aderente in Ac,eff"),
            (CrackTraceFlags.UpperBoundEurocode, "1,3 (h − x), nessuna barra aderente in Ac,eff · EC2 7.3.4(3), eq. (7.14)"),
            (CrackTraceFlags.UpperBoundDin, "min[1,3 (h − x); σs Ø/(3,6 fctm)], nessuna barra aderente in Ac,eff · DIN")), ""),
        [CrackTraceCodes.Width] = (e, profile) => e.HasFlag(CrackTraceFlags.UpperBound) ? ("wk", "sr,max (εsm−εcm), limite superiore", "")
            : e.HasFlag(CrackTraceFlags.DsCoarseHalf) ? ("wk", "0,5 · apertura Eq.(7.8) con intera area tesa · DK NA 7.3.4(1)", "")
            : e.HasFlag(CrackTraceFlags.Envelope) ? ("wk", "Inviluppo delle superfici esterne e interne", "")
            : NtcWidthFormula(profile) ? ("wk", "max[0; 1,70·Δsm·(εsm − εcm)]",
                $"max[0; 1,70 × {Argument(e, CrackTraceArguments.AdoptedSpacing):G10} × {Argument(e, CrackTraceArguments.MeanStrainDifference):G10}]")
            : ("wk", "sr,max (εsm−εcm)", ""),
        [CrackTraceCodes.WidthRatio] = (e, _) => ("ηw", e.HasFlag(CrackTraceFlags.Envelope) ? "wk / wlim; esito sospeso se una superficie resta senza verifica" : "wk / wlim", ""),
        // Sezione interamente tesa (ConcreteTensionCracking.cs:40, :73) e superfici interne (ConcreteInnerCracking.cs:72-81, :125, :132)
        [CrackTraceCodes.RegionCheck] = (e, _) => (TracePrefix(Region(e)), "Verifica indipendente della fascia; nessuna somma con le aree delle altre facce.", ""),
        [CrackTraceCodes.GoverningFace] = (e, _) => ("Faccia governante", RegionName(Region(e)), ""),
        [CrackTraceCodes.BandTensileDepth] = (e, _) => e.HasFlag(CrackTraceFlags.Uniform)
            ? ("h − x della fascia", "Trazione uniforme: h della sezione normale alla faccia",
                $"|∇ε|·h = {Argument(e, CrackTraceArguments.GradientTimesHeight):G3} ≤ {Argument(e, CrackTraceArguments.Tolerance):G1}·εmax: gradiente trascurabile, senza direzione; fascia tutta tesa, k₂ = 1 (EN 1992-1-1 7.3.4(3), eq. (7.14)).")
            : e.HasFlag(CrackTraceFlags.BoundedByHeight)
                ? ("h − x della fascia", "min[εmax/|∇ε|; h lungo il gradiente]",
                    $"εmax/|∇ε| = {Argument(e, CrackTraceArguments.FromNeutralAxis):G6} mm oltre l'altezza della sezione lungo il gradiente: asse neutro fuori dalla sezione, x = 0 (EN 1992-1-1 7.3.4(3), eq. (7.14)).")
            : throw Unmapped("variante della traccia " + e.Code, string.Join(",", e.Flags)),
        [CrackTraceCodes.BandK2] = (e, _) => e.HasFlag(CrackTraceFlags.Uniform)
            ? ("k₂ della fascia", "Trazione uniforme: 1", "Gradiente trascurabile rispetto alla deformazione (EN 1992-1-1 7.3.4(3), eq. (7.13) con ε1 = ε2).")
            : e.HasFlag(CrackTraceFlags.Local)
                ? ("k₂ della fascia", "(εmax + εmin)/(2 εmax) ai vertici della parte tesa della fascia, limitato fra 0,50 e 1",
                    "Distribuzione locale delle deformazioni (EN 1992-1-1 7.3.4(3), aree locali): 0,50 se l'asse neutro taglia la fascia, oltre 0,50 se la fascia è tutta tesa. Indipendente dal Criterio k₂ della sezione.")
            : throw Unmapped("variante della traccia " + e.Code, string.Join(",", e.Flags)),
        [CrackTraceCodes.GoverningSurface] = (e, _) => ("Superficie governante", RegionName(Region(e)), ""),
        [CrackTraceCodes.InnerBoundaryNote] = (_, _) => ("Contorno interno", "Fasce di parete/anello limitate a metà spessore. Inviluppo con il massimo σs; le fasce sono controlli indipendenti.", "")
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Criterio k₂ della sezione: regola delle barre (prima di D7-b), flessione, sezione parzialmente compressa o interamente tesa.</summary>
    static (string Expression, string Note) K2CriterionText(CrackTraceEntry e, CrackProfile profile)
    {
        if (e.HasFlag(CrackTraceFlags.K2FromBars))
            return (e.HasFlag(CrackTraceFlags.CompressedBar) ? "Flessione: almeno una armatura compressa → k₂ = 0,50" : "Trazione: nessuna armatura compressa → k₂ = 1,00",
                e.HasFlag(CrackTraceFlags.ZeroStressBars) ? "Le barre a tensione esattamente nulla non sono considerate compresse." : "Selezione per la combinazione corrente, dalle tensioni Checker.");
        if (e.HasFlag(CrackTraceFlags.Bending))
            return ("Asse neutro interno alla sezione: flessione, k₂ = 0,50 (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3))",
                (e.HasFlag(CrackTraceFlags.NotInWidthFormula) ? $"Con {ProfileName(profile)} sr,max non contiene k₂: valore solo informativo."
                    : "Entra in wk solo con il termine k₁·k₂·k₄·Øeq/ρp,eff di sr,max (NTC: Δsm,vicino): non entra se l'asse neutro è nel copriferro senza barre tese, "
                      + "se non ci sono barre tese in Ac,eff o, con barre distanziate, se governa il termine in (h − x).")
                + (e.HasFlag(CrackTraceFlags.InnerBands) ? " Le fasce interne dei fori usano il k₂ della propria distribuzione di deformazioni («k₂ della fascia»)." : ""));
        if (e.HasFlag(CrackTraceFlags.PartiallyCompressed)) return ("Sezione parzialmente compressa: flessione, k₂ = 0,50", "");
        if (e.HasFlag(CrackTraceFlags.EntirelyTensile)) return ("Trazione non uniforme: (εmax + εmin)/(2 εmax); uniforme: 1", "");
        throw Unmapped("variante della traccia " + e.Code, string.Join(",", e.Flags));
    }

    /// <summary>Testo del primo flag presente fra quelli della variante; nessuno è un errore di programma.</summary>
    static string Pick(CrackTraceEntry e, params (string Flag, string Text)[] variants)
    {
        foreach (var (flag, text) in variants) if (e.HasFlag(flag)) return text;
        throw Unmapped("variante della traccia " + e.Code, string.Join(",", e.Flags));
    }

    static string Region(CrackTraceEntry e) => e.Region ?? throw Unmapped("regione della voce " + e.Code, "null");

    static double Argument(CrackTraceEntry e, string name) => e.Argument(name) ?? throw Unmapped("argomento della voce " + e.Code, name);

    /// <summary>Prefisso della barra: "B" e l'indice da 1 su due cifre (Ntc2018Checks.cs:195), poi " · ".</summary>
    static string Bar(CrackTraceEntry e) => "B" + ((int)Argument(e, CrackTraceArguments.Bar) + 1).ToString("D2", CultureInfo.InvariantCulture) + " · ";
}
