using Anthea.Testing;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Serviceability;

/// <summary>
/// Prove della parte SLE dell'adattatore (refactoring F2.7b, commit A4; progetto F2.7 §6.1-§6.3): limiti tensionali e fessurazione
/// attraverso <see cref="ConcreteServiceabilityAdapter"/>, con ENTRAMBI i motori scelti in modo esplicito.
/// Produzione: Library, salvo fessurazione circolare conservata sul legacy per API-02.
/// - 5a strato di mappatura (letterali, traduzioni lette per riflessione, testo non tradotto = errore di programma);
/// - 5b motore Legacy dell'adattatore uguale al legacy diretto bit per bit; 5c motore Library contro legacy (griglie della cattura densa:
///   936 stati di fessurazione, 2016 tensionali, tabella dei requisiti con valori non validi; culture it-IT e invariante);
/// - 5d-5j percorsi di produzione (ServiceabilityPaths.cs); 5k scansione dei sorgenti di produzione; 5m motore della sessione;
/// - 6 attesi indipendenti con entrambi i motori (ServiceabilityPaths.cs).
/// La sonda (<see cref="ServiceabilityProbe"/>) si verifica con una chiamata nota prima di ogni prova che la usa.
/// </summary>
sealed partial class ServiceabilityChecks
{
    const double Tolerance = 1e-9;
    const int MinimumDiscriminating = 10;
    const string UnmappedText = "senza traduzione nello strato di mappatura";
    static readonly ServiceabilityEngine[] Engines = [ServiceabilityEngine.Legacy, ServiceabilityEngine.Library];
    static readonly string[] SleSets = ["SLE", "SLE_FREQ", "SLE_QP"];
    static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    readonly string root;
    readonly HashSet<string>? only;
    public int Checks;
    public readonly JsonObject Report = new();
    public readonly List<string> Lines = [];

    ServiceabilityChecks(string root, HashSet<string>? only) { this.root = root; this.only = only; }

    /// <summary>Esegue le prove SLE; <paramref name="only"/> limita alle prove indicate (per esempio per le prove negative).</summary>
    public static ServiceabilityChecks Run(string root, HashSet<string>? only)
    {
        var checks = new ServiceabilityChecks(root, only);
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = Italian;
        // Le prove di equivalenza con il motore storico usano le regole della libreria prima del 15/10/2026; la 5n misura quelle del prodotto.
        try { using (PreviousCrackRules.Use()) checks.All(); }
        finally { CultureInfo.CurrentCulture = saved; }
        checks.Report["controlli"] = checks.Checks;
        return checks;
    }

    bool Selected(params string[] proofs) => only is null || proofs.Any(only.Contains);

    void All()
    {
        // Production uses Checker; both engines remain explicit comparison paths in these tests.
        Check(ServiceabilityEngines.Default == ServiceabilityEngine.Library, "motore SLE predefinito " + ServiceabilityEngines.Default + ", atteso Library");
        if (Selected("5a")) MappingLayer();
        if (Selected("5b", "5c")) { CrackGrid(); RequirementTable(); StressGrid(); }
        if (Selected("5d")) Session();
        if (Selected("5e", "5h")) Headless();
        if (Selected("5f")) Design();
        if (Selected("5g")) Walls();
        if (Selected("5i")) Scope();
        if (Selected("5j")) NonServiceabilityStates();
        if (Selected("5k")) Scan();
        if (Selected("5m")) SessionEngine();
        if (Selected("6")) Independent();
    }

    void Check(bool ok, string message) { if (!ok) throw new Exception(message); Checks++; }

    static (string Outcome, string Message, T? Value, Exception? Error) Try<T>(Func<T> work) where T : class
    {
        try { return ("ok", "", work(), null); }
        catch (Exception e) { return ("error:" + e.GetType().Name, e.Message, null, e); }
    }

    static string Text(object? value) => J.Node(value)?.ToJsonString() ?? "null";

    /// <summary>La sonda riceve una chiamata nota: una sonda che non registra nulla farebbe passare per vuote le prove che la usano.</summary>
    void ProbeSelfTest(string proof)
    {
        using var probe = ServiceabilityProbe.Start();
        _ = ServiceabilityEngines.CrackRequirement("NTC 2018", "SLE_QP", J.Obj(("esposizione", "XC3")), ServiceabilityEngine.Library);
        Check(probe.Entries.Count == 1 && probe.Entries[0] == new ServiceabilityProbeEntry(ConcreteServiceabilityAdapter.ProbeName, "CrackRequirement", "Library"),
            proof + ": la sonda non registra la chiamata nota");
        var inner = Task.Run(() => { using var nested = ServiceabilityProbe.Start(); return nested.Entries.Count; }).Result;
        Check(inner == 0 && probe.Entries.Count == 1, proof + ": la sonda di un altro flusso riceve chiamate di questo");
    }

    /// <summary>Voci dell'adattatore SLE: la sonda è condivisa con l'adattatore SLU (revisione di architettura R2), che registra le proprie
    /// voci nei percorsi che calcolano il dominio (calcolo headless, muri, curvature).</summary>
    static IReadOnlyList<ServiceabilityProbeEntry> Sle(ServiceabilityProbe probe)
        => probe.Entries.Where(e => e.Adapter == ConcreteServiceabilityAdapter.ProbeName).ToArray();

    // ================================================================== 5a. strato di mappatura
    void MappingLayer()
    {
        int checksBefore = Checks;
        // Nessuna costante normativa: fuori da testi e commenti solo 0 e 1 nella mappatura (indici delle coordinate [x, y]; barre numerate da 1, "B01")
        // e 0 nell'adattatore (staffe assenti, φ assente, come il legacy).
        foreach (var (file, allowed) in new[] { ("X.Calculations/ConcreteLibraryMapping.Serviceability.cs", new[] { "0", "1" }), ("X.Calculations/ConcreteServiceabilityAdapter.cs", new[] { "0" }) })
        {
            var literals = Literals(File.ReadAllText(Path.Combine(root, file)));
            Check(literals.All(allowed.Contains), file + ": numeri non ammessi: " + string.Join(", ", literals.Except(allowed)));
        }
        // Ogni membro della libreria ha una traduzione (riflessione sulla DLL caricata).
        Check(Enum.GetValues<CrackOutcome>().All(MappingCoverage.TranslatedOutcomes.Contains),
            "5a: esiti senza traduzione: " + string.Join(", ", Enum.GetValues<CrackOutcome>().Except(MappingCoverage.TranslatedOutcomes)));
        Check(Enum.GetValues<CrackReason>().All(MappingCoverage.TranslatedReasons.Contains),
            "5a: motivi senza traduzione: " + string.Join(", ", Enum.GetValues<CrackReason>().Except(MappingCoverage.TranslatedReasons)));
        Check(Enum.GetValues<CrackCriterion>().All(MappingCoverage.TranslatedCriteria.Contains), "5a: criteri senza traduzione");
        Check(Enum.GetValues<CrackProfile>().All(MappingCoverage.TranslatedProfiles.Contains), "5a: profili senza nome");
        foreach (var combination in Enum.GetValues<ServiceabilityCombination>())
            Check(ConcreteLibraryMapping.CombinationOf(ConcreteLibraryMapping.SetOf(combination)) == combination, "5a: combinazione " + combination);
        foreach (var set in new[] { "ED", "CURVA", "LIMITE", "BENCH", "SLU", "SLV", "" })
            Check(ConcreteLibraryMapping.CombinationOf(set) is null, "5a: insieme non SLE con una combinazione: " + set);
        var codes = Constants(typeof(CrackTraceCodes)); var flags = Constants(typeof(CrackTraceFlags)); var arguments = Constants(typeof(CrackTraceArguments));
        var rejections = Constants(typeof(CrackRejection)).Where(c => c != CrackRejection.DataKey).ToArray();
        Check(codes.Length >= 80 && codes.ToHashSet().SetEquals(MappingCoverage.TranslatedTraceCodes),
            "5a: codici della traccia senza traduzione: " + string.Join(", ", codes.Except(MappingCoverage.TranslatedTraceCodes)) + " / tradotti e assenti: "
            + string.Join(", ", MappingCoverage.TranslatedTraceCodes.Except(codes)));
        Check(flags.ToHashSet().SetEquals(ConcreteLibraryMapping.TranslatedTraceFlags), "5a: flag della traccia senza traduzione: " + string.Join(", ", flags.Except(ConcreteLibraryMapping.TranslatedTraceFlags)));
        Check(arguments.ToHashSet().SetEquals(ConcreteLibraryMapping.TranslatedTraceArguments), "5a: argomenti della traccia senza traduzione: " + string.Join(", ", arguments.Except(ConcreteLibraryMapping.TranslatedTraceArguments)));
        Check(rejections.ToHashSet().SetEquals(MappingCoverage.TranslatedRejections), "5a: codici di rifiuto senza traduzione: " + string.Join(", ", rejections.Except(MappingCoverage.TranslatedRejections)));
        // Ogni codice produce una voce per ogni profilo, con le varianti dei flag che lo distinguono.
        int entries = 0;
        foreach (var profile in Enum.GetValues<CrackProfile>())
            foreach (var code in codes)
                foreach (var variant in SyntheticEntries(code))
                {
                    var detail = Try(() => ConcreteLibraryMapping.CrackDetail(variant, profile));
                    Check(detail.Outcome == "ok" && detail.Value!.Symbol.Length > 0 && !detail.Value.Expression.Contains(UnmappedText), $"5a: {code} ({profile}, {string.Join(",", variant.Flags)}): {detail.Message}");
                    entries++;
                }
        // Un testo, un flag, un'unità o un argomento sconosciuti sono errori di programma, mai testi inglesi mostrati.
        foreach (var unknown in new[]
        {
            new CrackTraceEntry("NewCode", null, 1, "mm", null, null), new CrackTraceEntry(CrackTraceCodes.Height, null, 1, "mm", null, new[] { "NewFlag" }),
            new CrackTraceEntry(CrackTraceCodes.Height, null, 1, "cm", null, null),
            new CrackTraceEntry(CrackTraceCodes.Height, null, 1, "mm", new[] { new KeyValuePair<string, double>("NewArgument", 1) }, null),
            new CrackTraceEntry(CrackTraceCodes.EffectiveDepth, null, 1, "mm", null, null), new CrackTraceEntry(CrackTraceCodes.Height, "NewRegion", 1, "mm", null, null)
        })
        {
            var mapped = Try(() => ConcreteLibraryMapping.CrackDetail(unknown, CrackProfile.Ntc2018));
            Check(mapped.Outcome == "error:InvalidOperationException" && mapped.Message.Contains(UnmappedText), "5a: voce sconosciuta accettata: " + unknown);
        }
        var plain = new ArgumentException("Cracking: new message.");
        Check(Try(() => ConcreteLibraryMapping.CrackError(plain, CrackProfile.Ntc2018, false)).Outcome == "error:InvalidOperationException", "5a: rifiuto senza codice accettato");
        Check(Try(() => ConcreteLibraryMapping.StressLimitError(new InvalidOperationException("New stress message."))).Outcome == "error:InvalidOperationException", "5a: rifiuto tensionale non tradotto accettato");
        Check(Try<object>(() => ConcreteLibraryMapping.SpacingSourceText("NewSource")!).Outcome == "error:InvalidOperationException", "5a: origine dell'interasse non tradotta accettata");
        Check(Try(() => ConcreteLibraryMapping.RegionName("NewRegion")).Outcome == "error:InvalidOperationException", "5a: regione non tradotta accettata");
        // Nomi delle regioni: U+2212 nelle facce negative, angolo con 0.## nella cultura corrente.
        Check(ConcreteLibraryMapping.RegionName("Face-x") == "Faccia −x" && ConcreteLibraryMapping.RegionName("InnerWall-y") == "Parete interna −y"
            && ConcreteLibraryMapping.RegionName("Radial(12.5)") == "Fascia radiale 12,5°" && ConcreteLibraryMapping.TracePrefix("DsCoarseSystem") == "Sistema grossolano"
            && ConcreteLibraryMapping.RegionName("DsCoarseSystem") == "Sistema grossolano DS", "5a: nomi delle regioni");
        Report["mappatura_5a"] = new JsonObject { ["controlli"] = Checks - checksBefore, ["voci_sintetiche"] = entries, ["codici_della_traccia"] = codes.Length, ["flag"] = flags.Length };
        Lines.Add($"5a mappatura: {Checks - checksBefore} controlli; {codes.Length} codici della traccia, {flags.Length} flag, {arguments.Length} argomenti, {rejections.Length} codici di rifiuto tradotti; {entries} voci sintetiche su {Enum.GetValues<CrackProfile>().Length} profili");
    }

    static string[] Constants(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!).ToArray();

    /// <summary>Voci sintetiche di un codice: una per variante di flag (e con regione per le voci che la richiedono).</summary>
    static IEnumerable<CrackTraceEntry> SyntheticEntries(string code)
    {
        var bar = new[] { new KeyValuePair<string, double>(CrackTraceArguments.Bar, 0), new KeyValuePair<string, double>(CrackTraceArguments.Bar, 2) };
        var all = new[] { CrackTraceArguments.SteelStress, CrackTraceArguments.TensionStiffening, CrackTraceArguments.Es, CrackTraceArguments.AdoptedSpacing,
            CrackTraceArguments.MeanStrainDifference, CrackTraceArguments.GradientTimesHeight, CrackTraceArguments.Tolerance, CrackTraceArguments.FromNeutralAxis }
            .Select(a => new KeyValuePair<string, double>(a, 1.25)).Concat(bar).ToArray();
        CrackTraceEntry E(string? region, params string[] flags) => new(code, region, 1.5, "mm", all, flags);
        string[][] variants = code switch
        {
            CrackTraceCodes.UncrackedStressLimit => [[CrackTraceFlags.Decompression], [CrackTraceFlags.CrackFormation]],
            CrackTraceCodes.K2Criterion => [[CrackTraceFlags.K2FromBars, CrackTraceFlags.CompressedBar], [CrackTraceFlags.K2FromBars, CrackTraceFlags.ZeroStressBars],
                [CrackTraceFlags.Bending], [CrackTraceFlags.Bending, CrackTraceFlags.NotInWidthFormula, CrackTraceFlags.InnerBands], [CrackTraceFlags.PartiallyCompressed], [CrackTraceFlags.EntirelyTensile]],
            CrackTraceCodes.EffectiveDepth => [[CrackTraceFlags.MinimumOfThree], [CrackTraceFlags.DinCoefficient], [CrackTraceFlags.DsBand], [CrackTraceFlags.EntirelyTensile], [CrackTraceFlags.InnerBand]],
            CrackTraceCodes.EffectiveArea or CrackTraceCodes.EffectiveSteel => [[], [CrackTraceFlags.EntirelyTensile], [CrackTraceFlags.InnerBand], [CrackTraceFlags.Summary]],
            CrackTraceCodes.BarStrain => [[CrackTraceFlags.Included], [CrackTraceFlags.Excluded]],
            CrackTraceCodes.EquivalentDiameter or CrackTraceCodes.SteelStress => [[CrackTraceFlags.TensileBars], [CrackTraceFlags.EffectiveBars], [CrackTraceFlags.Formula]],
            CrackTraceCodes.MeanStrainDifference => [[CrackTraceFlags.ComputedGoverns], [CrackTraceFlags.MinimumGoverns], [], [CrackTraceFlags.UpperBound],
                [CrackTraceFlags.StabilizedCrackStage], [CrackTraceFlags.SingleCrackStage], [CrackTraceFlags.UpperBound, CrackTraceFlags.SingleCrackStage]],
            CrackTraceCodes.BetaMinimum => [[], [CrackTraceFlags.UpperBound], [CrackTraceFlags.StabilizedCrackStage], [CrackTraceFlags.UpperBound, CrackTraceFlags.SingleCrackStage]],
            CrackTraceCodes.CrackingSteelStress or CrackTraceCodes.BondStressRatio => [[CrackTraceFlags.StabilizedCrackStage], [CrackTraceFlags.SingleCrackStage]],
            CrackTraceCodes.MaximumCrackSpacing => [[CrackTraceFlags.FormulaStandard], [CrackTraceFlags.FormulaModelCode2010], [CrackTraceFlags.FormulaDin], [CrackTraceFlags.FormulaSparseBars],
                [CrackTraceFlags.UpperBound, CrackTraceFlags.UpperBoundNtc], [CrackTraceFlags.UpperBound, CrackTraceFlags.UpperBoundEurocode], [CrackTraceFlags.UpperBound, CrackTraceFlags.UpperBoundDin]],
            CrackTraceCodes.Width => [[], [CrackTraceFlags.UpperBound], [CrackTraceFlags.DsCoarseHalf], [CrackTraceFlags.Envelope]],
            CrackTraceCodes.WidthRatio => [[], [CrackTraceFlags.Envelope]],
            CrackTraceCodes.Cover => [[CrackTraceFlags.Assigned], [CrackTraceFlags.Nominal], [CrackTraceFlags.Formula]],
            CrackTraceCodes.Spacing => [[CrackTraceFlags.Automatic], [CrackTraceFlags.Manual], [CrackTraceFlags.Formula]],
            CrackTraceCodes.Kt => [[CrackTraceFlags.ShortTerm], [CrackTraceFlags.LongTerm], [CrackTraceFlags.LongTerm, CrackTraceFlags.StabilizedCrackStage],
                [CrackTraceFlags.ShortTerm, CrackTraceFlags.SingleCrackStage]],
            CrackTraceCodes.K1 => [[CrackTraceFlags.Ribbed], [CrackTraceFlags.Plain]],
            CrackTraceCodes.K2 => [[], [CrackTraceFlags.NotInWidthFormula]],
            CrackTraceCodes.SpacingExcess or CrackTraceCodes.FarSpacing => [[CrackTraceFlags.CloseBars], [CrackTraceFlags.SparseBars]],
            CrackTraceCodes.AdoptedSpacing => [[CrackTraceFlags.CloseBars, CrackTraceFlags.NearGoverns], [CrackTraceFlags.SparseBars, CrackTraceFlags.FarGoverns]],
            CrackTraceCodes.EntirelyTensileK2 or CrackTraceCodes.CompressedBars or CrackTraceCodes.TensileBars or CrackTraceCodes.ZeroStressBars => [[], [CrackTraceFlags.K2FromBars]],
            CrackTraceCodes.BandTensileDepth => [[CrackTraceFlags.Uniform], [CrackTraceFlags.BoundedByHeight]],
            CrackTraceCodes.BandK2 => [[CrackTraceFlags.Uniform], [CrackTraceFlags.Local]],
            _ => [[]]
        };
        // Voci che portano la regione nel nome o nell'espressione; le altre anche con il prefisso di una regione.
        bool regional = code is CrackTraceCodes.RegionCheck or CrackTraceCodes.GoverningFace or CrackTraceCodes.GoverningSurface
            || (code is CrackTraceCodes.EffectiveArea or CrackTraceCodes.EffectiveSteel);
        foreach (var flags in variants)
        {
            if (regional || flags.Contains(CrackTraceFlags.Summary)) { yield return E("Radial(12.5)", flags); yield return E("Face-x", flags); continue; }
            yield return E(null, flags);
            yield return E("InnerWall-y", flags);
        }
    }

    static string[] Literals(string source)
    {
        string code = Regex.Replace(source, @"(?s)/\*.*?\*/|//[^\n]*|@""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""|'(?:[^'\\]|\\.)'", " ");
        return Regex.Matches(code, @"(?<![\w.])(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][-+]?\d+)?[dDfFmM]?(?![\w.])").Select(m => m.Value).Distinct().ToArray();
    }

    // ================================================================== 5b, 5c. griglie della cattura densa
    sealed record CrackState(int Id, string Section, string Standard, string Set, ActionPoint Action, CheckerSectionModel Prepared, JsonObject Input, JsonObject Settings, JsonObject Options);

    /// <summary>Stessi stati e stesse rotazioni di supporto/test/CheckerMigration.Capture (CrackCapture, crack-legacy.csv: 936 stati).</summary>
    static IEnumerable<CrackState> CrackStates()
    {
        var sections = new List<(string Name, Action<JsonObject> Edit)>
        {
            ("R300x500", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3";
                i["side_bar_count_per_side"] = "0"; i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["fck_mpa"] = "30"; }),
            ("T1200x800", i => { i["shape"] = "A T"; i["fck_mpa"] = "35"; }),
            ("C1000", i => { i["shape"] = "Circolare"; i["fck_mpa"] = "40"; }),
            ("R600x800H", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "600"; i["height_mm"] = "800"; i["foro_presente"] = true; i["inner_width_mm"] = "300";
                i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; i["fck_mpa"] = "28"; }),
            ("C1000H", i => { i["shape"] = "Circolare"; i["foro_presente"] = true; i["inner_diameter_mm"] = "500"; i["second_inner_enabled"] = true;
                i["second_inner_diameter"] = "16"; i["second_inner_count"] = "12"; i["second_inner_gap"] = "88"; i["fck_mpa"] = "32"; }),
            ("R400x400", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "400"; i["height_mm"] = "400"; i["top_bar_count"] = "4"; i["bottom_bar_count"] = "4";
                i["side_bar_count_per_side"] = "1"; i["top_bar_diameter_mm"] = "20"; i["bottom_bar_diameter_mm"] = "20"; i["side_bar_diameter_mm"] = "20"; i["cover_mm"] = "35"; i["fck_mpa"] = "25"; })
        };
        var variants = new (string Exposure, string Sensitivity, string Duration, string Bond, string Cover, string Spacing, string Limit)[]
        {
            ("XC3", "Poco sensibile", "Lunga", "Migliorata", "", "", ""), ("XD1", "Sensibile", "Lunga", "Migliorata", "", "", ""),
            ("XS3", "Poco sensibile", "Breve", "Migliorata", "", "", ""), ("XC1", "Sensibile", "Breve", "Liscia", "", "", ""),
            ("XD3", "Poco sensibile", "Lunga", "Migliorata", "45", "180", ""), ("XC2", "Poco sensibile", "Lunga", "Migliorata", "", "", "0.25"),
            ("Da scegliere", "Poco sensibile", "Lunga", "Migliorata", "", "", ""), ("XA1", "Poco sensibile", "Breve", "Migliorata", "", "", "")
        };
        var actions = CrackActions;
        int skipped = 0, id = 0, variant = 0;
        foreach (var (name, edit) in sections)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject(); edit(input);
            var prepared = CheckerSection.PrepareModel(input, settings);
            foreach (var standard in ConcreteStandards.OrdinaryNames)
                foreach (var action in actions)
                    foreach (var set in new[] { "SLE", "SLE_FREQ", "SLE_QP" })
                    {
                        var v = variants[variant++ % variants.Length];
                        bool linear = id % 11 != 5, tension = id % 13 == 7; string psi = id % 2 == 0 ? "0" : "15";
                        settings["normativa"] = standard; input["gettato_sottile"] = "No";
                        var options = (JsonObject)settings["sle"]![set]!.DeepClone();
                        options["modello"] = linear ? "Lineare" : "Non lineare"; options["phi"] = psi; options["trazione_cls"] = tension ? "Sì" : "No"; options["angoli"] = "32";
                        options["esposizione"] = v.Exposure; options["sensibilita"] = v.Sensitivity; options["durata"] = v.Duration; options["aderenza"] = v.Bond;
                        options["copriferro_fessure"] = v.Cover; options["spaziatura_fessure"] = v.Spacing; options["limite_fessure"] = v.Limit;
                        if (standard == "Model Code 2010" && v.Limit == "" && variant % 4 != 0) options["limite_fessure"] = "0.3";
                        if (LegacyConcreteCodeChecks.CrackRequirement(standard, set, options).Kind.StartsWith("Non richiesta") && skipped++ % 4 != 0) continue;
                        yield return new(id++, name, standard, set, action, prepared, (JsonObject)input.DeepClone(), (JsonObject)settings.DeepClone(), options);
                    }
        }
    }

    static readonly ActionPoint[] CrackActions =
    [
        new(-500, 50, 0), new(-200, 150, 40), new(0, 120, 0), new(100, 30, 0), new(-1500, 0, 0), new(-800, -60, 90), new(300, 0, 0), new(400, 20, 15), new(50, -90, 60),
        new(-100, 80, 0), new(0, 60, 40), new(-50, 260, 0), new(20, -150, 10)
    ];

    /// <summary>Primo identificativo degli stati mirati, dopo quelli della griglia.</summary>
    const int TargetedFirstId = 10000;

    /// <summary>
    /// Stati mirati della 5c (ciclo di prototipo, giro 1): rami raggiungibili in ANTHEA che la griglia della cattura densa non tocca (formazione delle
    /// fessure, barre distanziate con la regione distante o vicina governante, precompressione, copriferro e interasse della fessurazione non validi
    /// rifiutati al punto d'uso o non letti; con le armature libere, F2-7: nessuna armatura tesa e faccia senza armatura), sulla R300x500 della griglia;
    /// ognuno con il ramo che deve raggiungere nel legacy (risultato o messaggio del rifiuto). I rami non raggiungibili in ANTHEA (regola di k₂ prima di
    /// D7-b, hc,eff nulla, Ac,eff nulla, asse neutro non determinato, superfici interne non supportate, limite superiore DIN, rifiuti dei parametri della
    /// formula, delle tensioni delle barre e della sezione non fessurata) restano provati dalla libreria e dalla 5a (docs/refactoring/f2-7-rifiuti-sle.md).
    /// </summary>
    static IEnumerable<(CrackState State, string Branch, Func<Ntc2018Checks.CrackResult?, string, bool> Reached)> TargetedCrackStates()
    {
        (CrackState, string, Func<Ntc2018Checks.CrackResult?, string, bool>) Make(int id, string standard, string set, ActionPoint action, string branch,
            Func<Ntc2018Checks.CrackResult?, string, bool> reached, Action<JsonObject> options, bool tendon = false, (double X, double Y, double Phi)[]? bars = null)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
            input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500"; input["top_bar_count"] = "2"; input["bottom_bar_count"] = "3";
            input["side_bar_count_per_side"] = "0"; input["top_bar_diameter_mm"] = "16"; input["bottom_bar_diameter_mm"] = "20"; input["cover_mm"] = "30"; input["fck_mpa"] = "30";
            input["gettato_sottile"] = "No"; settings["normativa"] = standard;
            if (bars is not null) input["barre_manuali"] = new JsonArray(bars.Select(b => (JsonNode)J.Obj(("x", b.X), ("y", b.Y), ("phi", b.Phi))).ToArray());
            if (tendon) settings["trefoli"]!.AsArray().Add(J.Obj(("id", "T1"), ("x", "0"), ("y", "-150"), ("area", "150"), ("sigma0", "1000"), ("Ep", "195000"),
                ("fpyk", "1670"), ("fpk", "1860"), ("eps_u", "35")));
            var prepared = CheckerSection.PrepareModel(input, settings);
            var o = (JsonObject)settings["sle"]![set]!.DeepClone();
            o["modello"] = "Lineare"; o["phi"] = "0"; o["phi_trefoli"] = "0"; o["trazione_cls"] = "No"; o["angoli"] = "32"; o["esposizione"] = "XC3";
            o["sensibilita"] = "Poco sensibile"; o["durata"] = "Lunga"; o["aderenza"] = "Migliorata"; o["copriferro_fessure"] = ""; o["spaziatura_fessure"] = ""; o["limite_fessure"] = "";
            options(o);
            return (new CrackState(id, "R300x500", standard, set, action, prepared, (JsonObject)input.DeepClone(), (JsonObject)settings.DeepClone(), o), branch, reached);
        }
        static bool Adopted(Ntc2018Checks.CrackResult? r, string note) => r?.Details.Any(d => d.Symbol == "Δsm adottata" && d.Note == note) == true;
        var bending = new ActionPoint(0, 120, 0);
        int id = TargetedFirstId;
        foreach (var standard in new[] { "NTC 2018", "UNI EN 1992-1-1" })
            yield return Make(id++, standard, "SLE_FREQ", bending, "formazione delle fessure " + standard, (r, _) => r?.Status.StartsWith("Formazione fessure") == true,
                o => { o["esposizione"] = "XS3"; o["sensibilita"] = "Sensibile"; });
        yield return Make(id++, "NTC 2018", "SLE_QP", bending, "barre distanziate, regione distante governante", (r, _) => Adopted(r, "Governa regione distante dalle barre."),
            o => o["spaziatura_fessure"] = "400");
        // Copriferro della formula 120 mm: Δsm,vicino ≈ (3,4·120 + k₁k₂k₄Øeq/ρ)/1,7 supera 0,75·(h − x) (h − x ≈ 390 mm); s = 700 > s_lim = 5·(120 + 10) = 650.
        yield return Make(id++, "NTC 2018", "SLE_QP", bending, "barre distanziate, regione vicina governante",
            (r, _) => Adopted(r, "Governa regione vicina alle barre.") && r!.Details.Any(d => d.Symbol == "s − s_lim" && d.Note.StartsWith("s > s_lim")),
            o => { o["copriferro_fessure"] = "120"; o["spaziatura_fessure"] = "700"; });
        foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1" })
            yield return Make(id++, standard, "SLE_QP", bending, "precompressione " + standard, (r, _) => r?.Status == "Apertura CAP: modello aderenza/decompressione da definire", _ => { },
                tendon: true);
        // Copriferro e interasse della fessurazione non validi (JsonData.Required): rifiuto del legacy dove il dato entra, con il suo testo; dove il ramo
        // non lo legge (sezione compressa, decompressione) nessun rifiuto.
        static Func<Ntc2018Checks.CrackResult?, string, bool> Refused(string key) => (r, message) => r is null && message.StartsWith(key + ": inserire un numero finito");
        foreach (var (standard, action, key, value, branch) in new[]
        {
            ("NTC 2018", bending, "copriferro_fessure", "abc", "inflessa"), ("EN 1992-1-1", bending, "copriferro_fessure", "-5", "inflessa"),
            ("DIN EN 1992-1-1", bending, "copriferro_fessure", "abc", "inflessa"), ("NTC 2018", bending, "spaziatura_fessure", "0", "inflessa"),
            ("DS EN 1992-1-1", bending, "spaziatura_fessure", "abc", "inflessa"), ("NTC 2018", new ActionPoint(300, 0, 0), "copriferro_fessure", "abc", "interamente tesa"),
            ("EN 1992-1-1", new ActionPoint(300, 0, 0), "spaziatura_fessure", "-5", "interamente tesa")
        })
            yield return Make(id++, standard, "SLE_QP", action, $"{key} «{value}» rifiutato, {standard}, sezione {branch}", Refused(key), o => o[key] = value);
        yield return Make(id++, "NTC 2018", "SLE_QP", new ActionPoint(-1500, 0, 0), "copriferro e interasse non validi non letti, sezione compressa",
            (r, _) => r?.Status == "Sezione interamente compressa", o => { o["copriferro_fessure"] = "abc"; o["spaziatura_fessure"] = "0"; });
        yield return Make(id++, "NTC 2018", "SLE_QP", bending, "copriferro e interasse non validi non letti, decompressione",
            (r, _) => r?.Status.StartsWith("Decompressione") == true, o => { o["esposizione"] = "XD1"; o["sensibilita"] = "Sensibile"; o["copriferro_fessure"] = "abc"; o["spaziatura_fessure"] = "0"; });
        // F2-7 (decisione dell'utente del 14/10, punto 2): armature libere su un solo lato, con il lato non armato teso.
        // Pressoflessione con e = 160 mm > h/6 e lembo superiore teso: le sole barre inferiori sono compresse e distano 450 mm > h/2 dal lembo teso.
        var bottom = new[] { (-100d, -200d, 20d), (0d, -200d, 20d), (100d, -200d, 20d) };
        yield return Make(id++, "NTC 2018", "SLE_QP", new ActionPoint(-500, -80, 0), "nessuna armatura tesa, armature libere",
            (r, _) => r?.Status == "Nessuna armatura tesa", _ => { }, bars: bottom);
        // Trazione con risultante a metà fra due file inferiori (y = −150 mm, M = 300 · 0,15): deformazione uniforme, sezione interamente tesa;
        // la fascia efficace della faccia superiore non contiene barre.
        yield return Make(id++, "NTC 2018", "SLE_QP", new ActionPoint(300, 45, 0), "faccia senza armatura, armature libere",
            (r, _) => r?.Status.Contains(": area o armatura efficace assente") == true, _ => { },
            bars: bottom.Concat(bottom.Select(b => (b.Item1, -100d, 20d))).ToArray());
    }

    void CrackGrid()
    {
        int checksBefore = Checks, states = 0, rejected = 0, discriminating = 0, ntcEntirelyTensile = 0, ntcEntirelyTensileCorrectedSpacing = 0, notesDifferent = 0, compressed = 0,
            libraryValues = 0;
        var stats = new Stats("fessurazione (griglia della cattura densa e stati mirati)");
        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        var coverage = new LibraryCoverage(); var withTargeted = new LibraryCoverage();
        var targeted = TargetedCrackStates().ToArray(); var reachedBranches = new List<string>();
        var rules = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in CrackStates().Concat(targeted.Select(t => t.State)))
        {
            bool grid = s.Id < TargetedFirstId;
            if (grid) states++;
            string id = $"fessurazione {s.Id} {s.Section} {s.Standard} {s.Set}";
            // Dalla decisione D3 il motore non si fissa più alla costruzione della sezione: si sceglie con l'aggancio nell'analisi tensionale.
            var legacySection = Try(() => new CheckerSection(s.Prepared, s.Input, s.Settings, s.Options, "SLU"));
            var librarySection = Try(() => new CheckerSection(s.Prepared, s.Input, s.Settings, s.Options, "SLU"));
            Check(legacySection.Outcome == librarySection.Outcome && legacySection.Message == librarySection.Message, id + ": costruzione diversa fra i motori");
            if (legacySection.Value is null) { rejected++; continue; }
            var legacyState = Try(() => ServiceabilityEngines.Stress(legacySection.Value, s.Action, s.Set, ServiceabilityEngine.Legacy));
            var libraryState = Try(() => ServiceabilityEngines.Stress(librarySection.Value!, s.Action, s.Set, ServiceabilityEngine.Library));
            Check(legacyState.Outcome == libraryState.Outcome && legacyState.Message == libraryState.Message, id + ": analisi diversa fra i motori");
            if (legacyState.Value is null) { rejected++; continue; }
            foreach (var culture in new[] { Italian, Invariant })
            {
                CultureInfo.CurrentCulture = culture;
                string tag = id + " (" + (culture == Italian ? "it-IT" : "invariante") + ")";
                var direct = Try(() => LegacyNtc2018Checks.Cracking(legacySection.Value, legacyState.Value, s.Action, s.Input, s.Settings, s.Options, s.Set));
                ServiceabilityProbe legacyProbe;
                (string Outcome, string Message, Ntc2018Checks.CrackResult? Value, Exception? Error) legacy;
                using (legacyProbe = ServiceabilityProbe.Start())
                    legacy = Try(() => ServiceabilityEngines.Cracking(legacySection.Value, legacyState.Value, s.Action, s.Input, s.Settings, s.Options, s.Set, ServiceabilityEngine.Legacy));
                // Risultati della libreria prima della mappatura, dalla sonda: copertura della 5c e requisito di ogni ramo (ciclo di prototipo, giro 1).
                // Il confronto con il legacy (5c) usa le regole della libreria prima del 15/10/2026; le regole decise quel giorno si misurano a parte (5n).
                ServiceabilityProbe probe;
                (string Outcome, string Message, Ntc2018Checks.CrackResult? Value, Exception? Error) library;
                using (probe = ServiceabilityProbe.Start())
                using (PreviousCrackRules.Use())
                    library = Try(() => ServiceabilityEngines.Cracking(librarySection.Value!, libraryState.Value!, s.Action, s.Input, s.Settings, s.Options, s.Set, ServiceabilityEngine.Library));
                (string Outcome, string Message, Ntc2018Checks.CrackResult? Value, Exception? Error) current;
                using (PreviousCrackRules.Off())
                    current = Try(() => ServiceabilityEngines.Cracking(librarySection.Value!, libraryState.Value!, s.Action, s.Input, s.Settings, s.Options, s.Set, ServiceabilityEngine.Library));
                CompareRules(tag, s, library, current, culture == Italian ? rules : null);
                if (culture == Italian && library.Value is not null) libraryValues++;
                if (culture == Italian && !grid)
                {
                    var (_, branch, reached) = targeted.Single(t => ReferenceEquals(t.State, s));
                    Check(reached(direct.Value, direct.Message), $"{tag}: stato mirato senza il ramo «{branch}» nel legacy: {direct.Value?.Status ?? direct.Message}; "
                        + string.Join(" | ", direct.Value?.Details.Where(d => d.Symbol.Contains("Δsm") || d.Symbol.Contains("s_lim") || d.Symbol.Contains("h − x") || d.Symbol.Contains("(formula)"))
                            .Select(d => $"{d.Symbol}={d.Value} {d.Note}") ?? []));
                    reachedBranches.Add(branch);
                }
                if (culture == Italian && grid) coverage.Add(probe);
                if (culture == Italian)
                    foreach (var raw in withTargeted.Add(probe))
                    {
                        Check(raw.Requirement is not null, tag + ": risultato della libreria senza requisito (" + raw.Outcome + " " + raw.GoverningRegion + ")");
                        bool legacyCompressed = direct.Value?.Status == "Sezione interamente compressa";
                        Check((raw.Reason == CrackReason.EntirelyCompressed) == legacyCompressed, $"{tag}: motivo {raw.Reason} della libreria, stato «{direct.Value?.Status}» del legacy");
                        if (legacyCompressed) compressed++;
                    }
                // 5b: legacy agganciato all'adattatore = legacy diretto, bit per bit; motore di ogni fessurazione dalla sonda (dalla decisione D3 i
                // risultati non portano più il marcatore del motore).
                Check(direct.Outcome == legacy.Outcome && direct.Message == legacy.Message && Text(direct.Value) == Text(legacy.Value), tag + ": il motore Legacy dell'adattatore non coincide con il legacy");
                Check(legacy.Value is null || Sle(legacyProbe).Any(e => e.Operation == "Cracking") && Sle(legacyProbe).All(e => e.Engine == "Legacy"), tag + ": motore Legacy nella sonda");
                Check(library.Value is null || Sle(probe).Any(e => e.Operation == "Cracking") && Sle(probe).All(e => e.Engine == "Library"), tag + ": motore Library nella sonda");
                // 5c: libreria = legacy (esiti, stati, rifiuti, regioni, traccia; numeri entro 1e-9; note con i numeri entro 1e-9).
                Check(!library.Message.Contains(UnmappedText) && !Text(library.Value).Contains(UnmappedText), tag + ": testo non tradotto: " + library.Message);
                Check(direct.Outcome == library.Outcome && direct.Message == library.Message, $"{tag}: rifiuto «{direct.Message}» ({direct.Outcome}) con il legacy, «{library.Message}» ({library.Outcome}) con la libreria"
                    + (library.Error is { } error and not ArgumentException ? Environment.NewLine + error : ""));
                if (direct.Value is null) { if (culture == Italian) rejected++; continue; }
                notesDifferent += CompareCrack(tag, J.Node(direct.Value)!, J.Node(library.Value)!, stats,
                    s.Standard == "NTC 2018" && direct.Value.Status.StartsWith("Interamente tesa"));
                if (culture != Italian) continue;
                stats.Results++;
                if (Text(direct.Value) != Text(library.Value)) discriminating++;
                string key = direct.Value.Status.Split(" · ")[0].Split(':')[0];
                outcomes[key] = outcomes.GetValueOrDefault(key) + 1;
                if (s.Standard == "NTC 2018" && direct.Value.Status.StartsWith("Interamente tesa"))
                {
                    ntcEntirelyTensile++;
                    if (library.Value!.BarSpacing is > 0 && direct.Value.BarSpacing is null) ntcEntirelyTensileCorrectedSpacing++;
                }
            }
            CultureInfo.CurrentCulture = Italian;
        }
        CultureInfo.CurrentCulture = Italian;
        Check(states == 936, "griglia della fessurazione: " + states + " stati invece di 936");
        // U1: le 13 righe NTC interamente tese conservano il null storico nel Legacy e mostrano l’interasse nella Library.
        Check(ntcEntirelyTensileCorrectedSpacing == 13, $"5c: BarSpacing corretto in {ntcEntirelyTensileCorrectedSpacing} righe NTC interamente tese su {ntcEntirelyTensile}, attese 13");
        Check(discriminating >= MinimumDiscriminating, $"5c: {discriminating} stati con uscite dei due motori diverse");
        // Ogni risultato della libreria è passato dalla sonda (autoverifica della sonda); la sezione interamente compressa c'è; ogni stato mirato raggiunge il suo ramo.
        Check(withTargeted.Results == libraryValues && libraryValues > 0 && compressed > 0,
            $"5c: sonda con {withTargeted.Results} risultati per {libraryValues} risultati dell'adattatore; {compressed} sezioni compresse");
        Check(reachedBranches.Count == targeted.Length, $"5c: {reachedBranches.Count} stati mirati su {targeted.Length}");
        stats.Rejected = rejected;
        Report["fessurazione_copertura"] = coverage.Json();
        Report["fessurazione_copertura"]!["sezioni_compresse"] = compressed;
        Report["fessurazione_copertura_con_stati_mirati"] = withTargeted.Json();
        Report["fessurazione_copertura_con_stati_mirati"]!["stati_mirati"] = new JsonArray(reachedBranches.Select(b => (JsonNode)b).ToArray());
        Report["fessurazione_griglia"] = stats.Json();
        Report["fessurazione_griglia"]!["stati"] = states;
        Report["fessurazione_griglia"]!["uscite_dei_motori_diverse"] = discriminating;
        Report["fessurazione_griglia"]!["note_con_numeri_diversi"] = notesDifferent;
        Report["fessurazione_griglia"]!["ntc_interamente_tese_barspacing_corretto"] = ntcEntirelyTensileCorrectedSpacing;
        Report["fessurazione_griglia"]!["stati_per_esito"] = new JsonObject(outcomes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value)));
        // 5n: ogni regola del 15/10/2026 cambia almeno uno stato, gli altri stati restano identici.
        foreach (var rule in new[] { RuleModelCode, RuleDin, RuleUnreinforced })
            Check(rules.GetValueOrDefault(rule) > 0, $"5n: nessuno stato cambiato dalla regola «{rule}»");
        Report["fessurazione_regole_15_10"] = new JsonObject(rules.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value)));
        Lines.Add("5n regole del 15/10/2026 (R1, R2, F2-7 punto 3) contro le regole precedenti della libreria: "
            + string.Join(", ", rules.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}")));
        Lines.Add(stats.Line() + $"; {states} stati, {discriminating} con uscite dei due motori diverse, {notesDifferent} note con numeri diversi entro 1e-9, "
            + $"BarSpacing corretto in {ntcEntirelyTensileCorrectedSpacing} righe NTC interamente tese (correzione U1); {Checks - checksBefore} controlli (5b, 5c; it-IT e invariante)");
        Lines.Add("griglia · " + coverage.Line() + $"; {compressed} sezioni interamente compresse dal motivo della libreria");
        Lines.Add($"griglia e {targeted.Length} stati mirati ({string.Join("; ", reachedBranches)}) · " + withTargeted.Line());
    }

    const string RuleModelCode = "MC2010, stadi della Tab. 7.6-2 (R1)", RuleDin = "DIN breve durata (R2)", RuleUnreinforced = "zona tesa senza armatura (F2-7 punto 3)",
        RuleSame = "stati invariati";
    const string UnreinforcedFailure = " · non soddisfatta: zona tesa senza armatura, apertura delle fessure non controllata";

    /// <summary>
    /// 5n (decisione dell'utente del 15/10/2026): la fessurazione della libreria con le regole del prodotto contro le stesse regole di prima, sullo stesso
    /// stato. Stessi rifiuti; testi tutti tradotti. Uguali bit per bit, salvo: fib MC2010 (stadi di Tab. 7.6-2, R1: wk cambia a lunga durata, a breve
    /// durata solo la traccia con σsr e τbms) e DIN a breve durata (kt = 0,6, R2), dove wk non cresce (a meno di 1e-12 mm di arrotondamento) e una verifica
    /// soddisfatta resta tale; zona tesa senza armatura (F2-7 punto 3), dove la verifica senza esito
    /// diventa non soddisfatta con il motivo e il resto del risultato non cambia. Le note dei numeri seguono la cultura.
    /// </summary>
    void CompareRules(string tag, CrackState s, (string Outcome, string Message, Ntc2018Checks.CrackResult? Value, Exception? Error) previous,
        (string Outcome, string Message, Ntc2018Checks.CrackResult? Value, Exception? Error) current, Dictionary<string, int>? counts)
    {
        Check(previous.Outcome == current.Outcome && previous.Message == current.Message, $"{tag}: 5n rifiuto «{previous.Message}» prima, «{current.Message}» con le regole del 15/10");
        if (previous.Value is not { } p || current.Value is not { } c) return;
        Check(!Text(c).Contains(UnmappedText), tag + ": 5n testo non tradotto");
        bool shortTerm = (string?)s.Options["durata"] == "Breve";
        bool widthRule = s.Standard == "Model Code 2010" || s.Standard == "DIN EN 1992-1-1" && shortTerm;
        string rule;
        if (p.Passed is null && c.Passed == false)
        {
            rule = RuleUnreinforced;
            Check(c.Status == p.Status + UnreinforcedFailure, $"{tag}: 5n stato «{c.Status}» invece di «{p.Status}{UnreinforcedFailure}»");
            if (!widthRule) Check(Text(c with { Passed = null, Status = p.Status }) == Text(p), tag + ": 5n zona tesa senza armatura con altre differenze");
        }
        else if (Text(p) == Text(c)) rule = RuleSame;
        else if (widthRule)
        {
            rule = s.Standard == "Model Code 2010" ? RuleModelCode : RuleDin;
            Check(p.Width is null == c.Width is null && (c.Width is null || c.Width <= p.Width + 1e-12), $"{tag}: 5n wk {c.Width} con le regole del 15/10, {p.Width} prima");
            Check(p.Passed != true || c.Passed == true, $"{tag}: 5n verifica soddisfatta prima e non ora");
        }
        else { Check(false, $"{tag}: 5n differenza fuori dalle regole del 15/10: «{p.Status}» → «{c.Status}»"); return; }
        if (counts is not null) counts[rule] = counts.GetValueOrDefault(rule) + 1;
    }

    /// <summary>
    /// Confronto di due CrackResult: traccia voce per voce (simbolo, unità, espressione esatti; valore entro la tolleranza; nota con i numeri
    /// entro la tolleranza), poi il resto (stati, regioni, esiti) con <see cref="Json.Compare"/>. Restituisce il numero di note diverse.
    /// </summary>
    int CompareCrack(string id, JsonNode legacyNode, JsonNode libraryNode, Stats stats, bool correctedNtcSpacing = false)
    {
        var a = (JsonObject)legacyNode.DeepClone(); var b = (JsonObject)libraryNode.DeepClone();
        var da = a["Details"]!.AsArray(); var db = b["Details"]!.AsArray();
        Check(da.Count == db.Count, $"{id}: {da.Count} voci della traccia con il legacy, {db.Count} con la libreria; " + FirstDetailDifference(da, db));
        int notes = 0;
        for (int i = 0; i < da.Count; i++)
        {
            var x = da[i]!.AsObject(); var y = db[i]!.AsObject();
            foreach (var field in new[] { "Symbol", "Unit", "Expression" })
                Check(x[field]!.ToJsonString() == y[field]!.ToJsonString(), $"{id}: voce {i} ({x["Symbol"]}): {field} {x[field]!.ToJsonString()} con il legacy, {y[field]!.ToJsonString()} con la libreria");
            var errors = new List<string>();
            Json.Compare(x["Value"], y["Value"], id + ".Details[" + i + "].Value", stats, errors, Tolerance);
            Check(errors.Count == 0, $"{id}: voce {i} ({x["Symbol"]}): " + string.Join("; ", errors));
            string na = x["Note"]!.GetValue<string>(), nb = y["Note"]!.GetValue<string>();
            Check(NoteText.Same(na, nb, Tolerance), $"{id}: voce {i} ({x["Symbol"]}): nota «{na}» con il legacy, «{nb}» con la libreria");
            if (na != nb) notes++;
        }
        if (correctedNtcSpacing)
        {
            // Independent of the new mapping: the old engine already used and recorded this spacing
            // in the governing face formula, but forgot to expose it in BarSpacing. Keep both captures intact.
            var historicalSpacing = da.Single(x => x!["Symbol"]!.GetValue<string>() == "s (formula)")!["Value"];
            Check(a["BarSpacing"] is null && historicalSpacing?.GetValue<double>() > 0 && b["BarSpacing"]?.GetValue<double>() > 0,
                id + ": U1 attende null storico e interasse positivo della formula");
            var spacingErrors = new List<string>();
            Json.Compare(historicalSpacing, b["BarSpacing"], id + ".BarSpacing (U1)", stats, spacingErrors, Tolerance);
            Check(spacingErrors.Count == 0, string.Join("; ", spacingErrors));
            a.Remove("BarSpacing"); b.Remove("BarSpacing");
        }
        a.Remove("Details"); b.Remove("Details");
        var rest = new List<string>();
        Json.Compare(a, b, id, stats, rest, Tolerance);
        Check(rest.Count == 0, string.Join(Environment.NewLine, rest.Take(5)));
        return notes;
    }

    static string FirstDetailDifference(JsonArray a, JsonArray b)
    {
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            if (a[i]!["Symbol"]!.ToJsonString() != b[i]!["Symbol"]!.ToJsonString()) return $"prima differenza alla voce {i}: {a[i]!["Symbol"]} / {b[i]!["Symbol"]}";
        return a.Count > b.Count ? "voce in più del legacy: " + a[b.Count]!["Symbol"] : b.Count > a.Count ? "voce in più della libreria: " + b[a.Count]!["Symbol"] : "";
    }

    /// <summary>Tabella dei requisiti (crack-scalar-legacy.csv, righe R) con i valori non validi di 'limite_fessure' e le norme escluse.</summary>
    void RequirementTable()
    {
        int checksBefore = Checks, rows = 0, rejected = 0;
        foreach (var standard in ConcreteStandards.Names.Append("ACI 318"))
            foreach (var set in SleSets)
                foreach (var exposure in LegacyNtc2018Checks.Exposures.Append("").Append("XZ9"))
                    foreach (var sensitive in new[] { false, true })
                        foreach (var limit in new[] { "", "0.25", "abc", "-5", "0", " " })
                        {
                            var o = J.Obj(("esposizione", exposure), ("sensibilita", sensitive ? "Sensibile" : "Poco sensibile"), ("limite_fessure", limit));
                            string id = $"requisito {standard} {set} {exposure} {sensitive} «{limit}»";
                            var direct = Try<object>(() => LegacyConcreteCodeChecks.CrackRequirement(standard, set, o));
                            var legacy = Try<object>(() => ServiceabilityEngines.CrackRequirement(standard, set, o, ServiceabilityEngine.Legacy));
                            var library = Try<object>(() => ServiceabilityEngines.CrackRequirement(standard, set, o, ServiceabilityEngine.Library));
                            Check(direct.Outcome == legacy.Outcome && direct.Message == legacy.Message && Equals(direct.Value, legacy.Value), id + ": il motore Legacy non coincide");
                            Check(direct.Outcome == library.Outcome && direct.Message == library.Message, $"{id}: rifiuto «{direct.Message}» con il legacy, «{library.Message}» con la libreria");
                            Check(Equals(direct.Value, library.Value), $"{id}: {direct.Value} con il legacy, {library.Value} con la libreria");
                            rows++; if (direct.Value is null) rejected++;
                        }
        Report["requisiti"] = new JsonObject { ["righe"] = rows, ["rifiuti"] = rejected };
        Lines.Add($"requisiti della fessurazione: {rows} righe (norme, combinazioni, esposizioni, sensibilità, wlim anche non validi), {rejected} rifiuti uguali; {Checks - checksBefore} controlli (5b, 5c)");
    }

    /// <summary>Stati tensionali di supporto/test/CheckerMigration.Capture (StressCapture, stress-legacy.csv: 2016 righe).</summary>
    void StressGrid()
    {
        int checksBefore = Checks, rows = 0, rejected = 0, discriminating = 0;
        var stats = new Stats("tensioni SLE (griglia della cattura densa)");
        var sections = new List<(string Name, Action<JsonObject> Edit)>
        {
            ("R300x500", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3";
                i["side_bar_count_per_side"] = "0"; i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["fck_mpa"] = "30"; }),
            ("T1200x800", i => { i["shape"] = "A T"; i["fck_mpa"] = "35"; }),
            ("C1000", i => { i["shape"] = "Circolare"; i["fck_mpa"] = "40"; }),
            ("R600x800H", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "600"; i["height_mm"] = "800"; i["foro_presente"] = true; i["inner_width_mm"] = "300";
                i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; i["fck_mpa"] = "28"; })
        };
        var actions = new[] { new ActionPoint(-500, 50, 0), new ActionPoint(-200, 150, 40), new ActionPoint(0, 120, 0), new ActionPoint(100, 30, 0), new ActionPoint(-1500, 0, 0), new ActionPoint(-800, -60, 90) };
        foreach (var (name, edit) in sections)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject(); edit(input);
            var prepared = CheckerSection.PrepareModel(input, settings);
            foreach (var standard in ConcreteStandards.Names)
                foreach (var (linear, psi, tension, thin) in new[] { (false, "0", "No", false), (true, "2", "No", false), (true, "0", "Sì", false), (true, "2", "No", true) })
                {
                    if (thin && standard != "NTC 2018") continue;
                    settings["normativa"] = standard; input["gettato_sottile"] = thin ? "Sì" : "No";
                    var options = (JsonObject)settings["sle"]!["SLE"]!.DeepClone();
                    options["modello"] = linear ? "Lineare" : "Non lineare"; options["phi"] = psi; options["trazione_cls"] = tension; options["angoli"] = "32";
                    var sections2 = Engines.ToDictionary(e => e, e => Try(() => new CheckerSection(prepared, input, settings, options, "SLU")));
                    Check(sections2.Values.Select(v => v.Outcome + v.Message).Distinct().Count() == 1, name + " " + standard + ": costruzione diversa fra i motori");
                    if (sections2[ServiceabilityEngine.Legacy].Value is not { } legacySection) { rejected += actions.Length * 3; rows += actions.Length * 3; continue; }
                    var librarySection = sections2[ServiceabilityEngine.Library].Value!;
                    var effective = ConcreteStandards.Effective(input, settings);
                    double factor = ConcreteLibraryMapping.ThinCastingFactor(effective), reduction = factor != 1 && thin ? factor : 1;
                    double phi = legacySection.Checker.SectionCheckerOptions.PsiCoefficientRebar, phiT = legacySection.Checker.SectionCheckerOptions.PsiCoefficientTendon;
                    foreach (var action in actions)
                        foreach (var set in SleSets)
                        {
                            rows++;
                            string id = $"tensioni {name} {standard} {linear} {psi} {tension} {thin} {action} {set}";
                            using var probe = ServiceabilityProbe.Start();
                            var legacy = Try(() => ServiceabilityEngines.Stress(legacySection, action, set, ServiceabilityEngine.Legacy));
                            var library = Try(() => ServiceabilityEngines.Stress(librarySection, action, set, ServiceabilityEngine.Library));
                            Check(legacy.Outcome == library.Outcome && legacy.Message == library.Message, $"{id}: rifiuto «{legacy.Message}» con il legacy, «{library.Message}» con la libreria");
                            if (legacy.Value is null) { rejected++; continue; }
                            var l = legacy.Value; var b = library.Value!;
                            // 5b: tasso, stato e limiti di CheckerSection con il motore Legacy = LegacyServiceability diretto = adattatore Legacy, bit per bit.
                            var direct = LegacyServiceability.StressLimits(l.Native, set, effective, legacySection.Section, phi, phiT, reduction);
                            var adapted = ServiceabilityEngines.StressLimits(l.Native, set, effective, legacySection.Section, phi, phiT, reduction, ServiceabilityEngine.Legacy);
                            Check(direct == adapted && Same(l.Ratio, direct.Ratio) && l.Status == direct.Status && Same(l.ConcreteStressLimit, direct.ConcreteStressLimit)
                                && Same(l.SteelStressLimit, direct.SteelStressLimit), id + ": il motore Legacy non coincide con LegacyServiceability");
                            // Motore degli stati tensionali: dalla decisione D3 lo dice la sonda qui sotto (due chiamate per motore), non più un marcatore.
                            // Lo stato della libreria viene dall'adattatore (stessi valori bit per bit).
                            var libraryLimits = ServiceabilityEngines.StressLimits(b.Native, set, effective, librarySection.Section, phi, phiT, reduction, ServiceabilityEngine.Library);
                            Check(Same(b.Ratio, libraryLimits.Ratio) && b.Status == libraryLimits.Status && Same(b.ConcreteStressLimit, libraryLimits.ConcreteStressLimit)
                                && Same(b.SteelStressLimit, libraryLimits.SteelStressLimit), id + ": lo stato della libreria non viene dall'adattatore");
                            // Sonda: due chiamate per motore (stato e verifica qui sopra), operazione delle combinazioni SLE.
                            Check(Sle(probe).Count(e => e.Operation == "StressLimits" && e.Engine == "Legacy") == 2 && Sle(probe).Count(e => e.Operation == "StressLimits" && e.Engine == "Library") == 2,
                                id + ": sonda " + string.Join(", ", Sle(probe)));
                            // 5c: stessi stati e testi; numeri entro la tolleranza; il resto dello stato identico.
                            Check(l.Status == b.Status, $"{id}: stato «{l.Status}» con il legacy, «{b.Status}» con la libreria");
                            var errors = new List<string>();
                            Json.Compare(J.Node(l), J.Node(b), id, stats, errors, Tolerance);
                            Check(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(5)));
                            Check(Text(l with { Ratio = null, Status = "", ConcreteStressLimit = null, SteelStressLimit = 0 }) == Text(b with { Ratio = null, Status = "", ConcreteStressLimit = null, SteelStressLimit = 0 }),
                                id + ": la libreria cambia lo stato tensionale oltre a tasso e limiti");
                            stats.Results++;
                            if (Text(l) != Text(b)) discriminating++;
                        }
                }
        }
        Check(rows == 2016, "griglia delle tensioni: " + rows + " righe invece di 2016");
        stats.Rejected = rejected;
        Report["tensioni_griglia"] = stats.Json();
        Report["tensioni_griglia"]!["righe"] = rows;
        Report["tensioni_griglia"]!["uscite_dei_motori_diverse"] = discriminating;
        Lines.Add(stats.Line() + $"; {rows} righe, {discriminating} con uscite dei due motori diverse; {Checks - checksBefore} controlli (5b, 5c)");
    }

    static bool Same(double? a, double? b) => a is null ? b is null : b is not null && BitConverter.DoubleToInt64Bits(a.Value) == BitConverter.DoubleToInt64Bits(b.Value);

    // ================================================================== 5i. ambito della fessurazione
    void Scope()
    {
        int checksBefore = Checks, rows = 0, required = 0;
        ProbeSelfTest("5i");
        foreach (var standard in ConcreteStandards.Names.Append("ACI 318").Append(""))
            foreach (var set in SleSets)
                foreach (var exposure in new[] { "Da scegliere", "XC1", "XC3", "XD1", "XD3", "XS3", "XF1", "", "XZ9" })
                    foreach (var limit in new[] { "", "0.25", "abc", "-5", "0" })
                        foreach (var layout in new[] { "completo", "senza sle", "senza insieme" })
                        {
                            var workspace = J.Obj(("normativa", standard));
                            if (layout != "senza sle") workspace["sle"] = layout == "senza insieme" ? new JsonObject()
                                : J.Obj((set, J.Obj(("esposizione", exposure), ("sensibilita", "Sensibile"), ("limite_fessure", limit))));
                            if (standard == "") workspace.Remove("normativa");
                            bool direct = LegacyServiceability.CrackingRequired(set, workspace);
                            using var probe = ServiceabilityProbe.Start();
                            bool legacy = ServiceabilityEngines.CrackingRequired(set, workspace, ServiceabilityEngine.Legacy);
                            bool library = ServiceabilityEngines.CrackingRequired(set, workspace, ServiceabilityEngine.Library);
                            bool production = SleCheckScope.Cracking(set, workspace);
                            string id = $"ambito {standard} {set} {exposure} «{limit}» {layout}";
                            // F4.14: a missing standard is invalid, rather than implicitly NTC.
                            // Keep the check visible so the calculation can explain the missing input.
                            // The diagnostic legacy path and its original expectation stay unchanged.
                            Check(direct == legacy && (standard == "" ? library && production : direct == library && direct == production),
                                $"{id}: legacy {direct}, adattatore Legacy {legacy}, Library {library}, SleCheckScope {production}");
                            // Sonda: SleCheckScope passa dall'adattatore con il motore predefinito.
                            Check(Sle(probe).SequenceEqual(new ServiceabilityProbeEntry[]
                            {
                                new(ConcreteServiceabilityAdapter.ProbeName, "CrackingRequired", "Legacy"), new(ConcreteServiceabilityAdapter.ProbeName, "CrackingRequired", "Library"),
                                new(ConcreteServiceabilityAdapter.ProbeName, "CrackingRequired", ServiceabilityEngines.Default.ToString())
                            }), id + ": sonda " + string.Join(", ", Sle(probe)));
                            rows++; if (direct) required++;
                        }
        Report["ambito_5i"] = new JsonObject { ["righe"] = rows, ["richieste"] = required };
        Lines.Add($"5i ambito della fessurazione: {rows} righe confrontate ({required} richieste legacy); differenza F4.14 esplicita per normativa assente, verifica visibile; {Checks - checksBefore} controlli");
    }

    // ================================================================== 5k. scansione dei sorgenti
    /// <summary>
    /// Chiamate del legacy SLE fuori dai file legacy e dall'aggancio, vietate nel codice di produzione (X.Calculations, X.Core, X.Desktop, src) e, dal
    /// commit A5, nelle suite (supporto/test, tests), salvo le voci di legacy-allowlist.json: file, numero esatto di chiamate (oppure tutto il file),
    /// testo ammesso di ogni chiamata, motivo e scadenza. Una chiamata nuova, una in più o in meno o una voce senza riscontro fanno fallire la prova
    /// (prova negativa n11). Commenti e stringhe non contano. Dalla decisione D3 del 15/10/2026 il legacy SLE sta in tests/ANTHEA.Legacy con i nomi
    /// LegacyNtc2018Checks e LegacyConcreteCodeChecks; nel prodotto Ntc2018Checks e ConcreteCodeChecks hanno solo DTO e norme.
    /// </summary>
    void Scan()
    {
        int checksBefore = Checks;
        var legacyFiles = new[] { "tests/ANTHEA.Legacy/Serviceability/LegacyNtc2018Checks.cs", "tests/ANTHEA.Legacy/Serviceability/LegacyNtc2018Checks.TensionCracking.cs",
            "tests/ANTHEA.Legacy/Serviceability/LegacyNtc2018Checks.InnerCracking.cs", "tests/ANTHEA.Legacy/Serviceability/LegacyConcreteCodeChecks.cs",
            "tests/ANTHEA.Legacy/Serviceability/LegacyServiceability.cs" };
        const string adapter = "tests/ANTHEA.Legacy/Serviceability/ServiceabilityEngines.cs";
        foreach (var file in legacyFiles.Append(adapter).Append("X.Calculations/ConcreteServiceabilityAdapter.cs"))
            Check(File.Exists(Path.Combine(root, file)), "5k: file dichiarato assente: " + file);
        var forbidden = new Regex(@"\bLegacyNtc2018Checks\s*\.\s*(Cracking|CrackRequirement|CrackWidth|CrackWidthWithDetails|CrackK2)\b"
            + @"|\bLegacyConcreteCodeChecks\s*\.\s*(CrackRequirement|CrackWidth|UnbondedCrackWidthBound|EffectiveCrackDepth)\b"
            + @"|\bLegacyServiceability\s*\.\s*(StressLimits|CrackingRequired)\b|using\s+static\s+Anthea\.Calculations\.(LegacyNtc2018Checks|LegacyConcreteCodeChecks|LegacyServiceability)\b",
            RegexOptions.CultureInvariant);
        string Call(Match m) => Regex.Replace(Regex.Replace(m.Value, @"\s*\.\s*", "."), @"\s+", " ");
        // Il campione: la scansione riconosce le chiamate, anche spezzate, e ignora commenti, testi e stringhe grezze e i DTO rimasti nel prodotto.
        const string sample = "var r = LegacyNtc2018Checks .Cracking(a); var q = LegacyConcreteCodeChecks.CrackRequirement(x); // LegacyNtc2018Checks.Cracking\n var t = \"LegacyServiceability.StressLimits\";"
            + "\n var u = \"\"\"\n LegacyNtc2018Checks.CrackK2\n \"\"\"; var v = $@\"{x} LegacyConcreteCodeChecks.CrackWidth\"; Ntc2018Checks.CrackResult w = null!; ConcreteCodeChecks.RequireOrdinary(n);";
        Check(forbidden.Matches(DurabilityChecks.CodeOnly(sample)).Select(Call).SequenceEqual(["LegacyNtc2018Checks.Cracking", "LegacyConcreteCodeChecks.CrackRequirement"]),
            "5k: la scansione non riconosce il campione");
        var allowlist = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "ConcreteLibraryAdapter.Checks", "legacy-allowlist.json")))!["voci"]!.AsArray()
            .Select(v => (File: v!["file"]!.GetValue<string>(), Calls: v["chiamate"]?.GetValue<int>(), WholeFile: v["tutto_il_file"]?.GetValue<bool>() == true,
                Texts: v["testo"]?.AsArray().Select(t => t!.GetValue<string>()).ToArray() ?? [], Reason: v["motivo"]?.GetValue<string>(), Expiry: v["scadenza"]?.GetValue<string>()))
            .ToList();
        Check(allowlist.Select(a => a.File).Distinct().Count() == allowlist.Count
            && allowlist.All(a => !string.IsNullOrWhiteSpace(a.Reason) && !string.IsNullOrWhiteSpace(a.Expiry) && (a.WholeFile ? a.Calls is null : a.Calls > 0 && a.Texts.Length > 0)),
            "5k: voci di legacy-allowlist.json ripetute o incomplete (file, chiamate e testo oppure tutto il file, motivo, scadenza)");
        int files = 0, suiteFiles = 0; var found = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (folder, production) in new[] { ("X.Calculations", true), ("X.Core", true), ("X.Desktop", true), ("src", true), ("supporto/test", false), ("tests", false) })
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Split('/').Any(part => part is "bin" or "obj") || legacyFiles.Contains(relative) || relative == adapter) continue;
                files++; if (!production) suiteFiles++;
                var calls = forbidden.Matches(DurabilityChecks.CodeOnly(File.ReadAllText(path))).Select(Call).ToList();
                if (calls.Count > 0) found[relative] = calls;
            }
        foreach (var (file, calls) in found)
        {
            var entry = allowlist.FirstOrDefault(a => a.File == file);
            Check(entry.File is not null, $"5k: {file}: chiamate del legacy SLE fuori dall'adattatore e dall'elenco ammesso: {string.Join("; ", calls.Distinct())}");
            if (entry.WholeFile) continue;
            Check(entry.Calls == calls.Count, $"5k: {file}: {calls.Count} chiamate del legacy SLE, {entry.Calls} nell'elenco ammesso ({string.Join("; ", calls)})");
            foreach (var call in calls) Check(entry.Texts.Contains(call), $"5k: {file}: chiamata non prevista dall'elenco ammesso: {call}");
        }
        foreach (var entry in allowlist) Check(found.ContainsKey(entry.File), $"5k: voce dell'elenco ammesso senza riscontro: {entry.File}");
        Check(files - suiteFiles > 100 && suiteFiles > 100, $"5k: file scansionati {files - suiteFiles} di produzione e {suiteFiles} delle suite");
        int allowed = found.Values.Sum(c => c.Count);
        Report["scansione_5k"] = new JsonObject
        {
            ["file"] = files, ["file_delle_suite"] = suiteFiles, ["voci_dell_elenco_ammesso"] = allowlist.Count,
            ["file_interi_ammessi"] = allowlist.Count(a => a.WholeFile), ["chiamate_ammesse"] = allowed
        };
        Lines.Add($"5k scansione: {files} file ({files - suiteFiles} di produzione, {suiteFiles} delle suite) senza chiamate del legacy SLE fuori dall'adattatore, dai "
            + $"{legacyFiles.Length} file legacy e dalle {allowlist.Count} voci di legacy-allowlist.json ({allowed} chiamate ammesse); {Checks - checksBefore} controlli");
    }

    // ================================================================== 5m. motore della sessione
    // Dalla decisione D3 del 15/10/2026 la sessione non ha più un motore proprio: lo stato tensionale usa il motore agganciato nel flusso (libreria
    // senza aggancio) e la firma della cache contiene quel motore (ConcreteServiceabilityAdapter.EngineName). La prova controlla che stati di motori
    // diversi non si mescolino né fra sessioni né nella cache della stessa sessione.
    void SessionEngine()
    {
        int checksBefore = Checks;
        Check(ConcreteServiceabilityAdapter.EngineName == "Library", "5m: motore predefinito della sessione");
        Check(ServiceabilityEngines.Run(ServiceabilityEngine.Legacy, () => ConcreteServiceabilityAdapter.EngineName) == "Legacy", "5m: motore agganciato nella firma");
        var data = SleDocument("NTC 2018", "Rettangolare"); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        var rows = SessionRows(data, "SLE_QP"); var options = settings["sle"]!["SLE_QP"]!.AsObject();
        var sessions = Engines.ToDictionary(e => e, e => new ConcreteAnalysisSession());
        var probes = new Dictionary<ServiceabilityEngine, IReadOnlyList<ServiceabilityProbeEntry>>();
        var outcomes = Engines.ToDictionary(e => e, e =>
        {
            using var probe = ServiceabilityProbe.Start();
            var result = ServiceabilityEngines.Run(e, () => sessions[e].Stress(input, settings, options, "SLE_QP", rows));
            probes[e] = Sle(probe);
            return result;
        });
        foreach (var engine in Engines)
            Check(probes[engine].Count > 0 && probes[engine].All(p => p.Engine == engine.ToString()), $"5m: la sessione con il motore {engine} non usa quel motore: " + string.Join(", ", probes[engine]));
        foreach (var (id, legacy) in outcomes[ServiceabilityEngine.Legacy])
        {
            var library = outcomes[ServiceabilityEngine.Library][id];
            if (legacy.State is null) continue;
            Check(!ReferenceEquals(legacy.State, library.State), "5m: due sessioni con motori diversi condividono uno stato: " + id);
        }
        // La firma della cache contiene il motore: cambiando il motore agganciato, la stessa sessione ricalcola gli stati.
        var session = sessions[ServiceabilityEngine.Legacy];
        var first = ServiceabilityEngines.Run(ServiceabilityEngine.Legacy, () => session.Stress(input, settings, options, "SLE_QP", rows));
        var again = ServiceabilityEngines.Run(ServiceabilityEngine.Legacy, () => session.Stress(input, settings, options, "SLE_QP", rows));
        Check(first.All(p => p.Value.State is null || ReferenceEquals(p.Value.State, again[p.Key].State)), "5m: la cache della sessione non riusa gli stati");
        IReadOnlyList<ServiceabilityProbeEntry> switchedEntries;
        Dictionary<string, StressOutcome> switched;
        using (var probe = ServiceabilityProbe.Start()) { switched = session.Stress(input, settings, options, "SLE_QP", rows); switchedEntries = Sle(probe); }
        Check(first.All(p => p.Value.State is null || !ReferenceEquals(p.Value.State, switched[p.Key].State))
            && switchedEntries.Any(e => e.Operation == "StressLimits") && switchedEntries.All(e => e.Engine == "Library"),
            "5m: la firma della cache non contiene il motore agganciato");
        Report["sessione_5m"] = new JsonObject { ["righe"] = rows.Length, ["controlli"] = Checks - checksBefore };
        Lines.Add($"5m motore della sessione: motore agganciato nella firma della cache ({rows.Length} righe), sessioni e chiamate con motori diversi senza stati comuni; {Checks - checksBefore} controlli");
    }
}

/// <summary>Confronto di testi con numeri (note della traccia, M8): scheletro identico, numeri entro la tolleranza più mezza unità dell'ultima cifra stampata.</summary>
static class NoteText
{
    static readonly Regex Token = new(@"(?<![\p{L}\p{N}_.,])[-+−]?\d+(?:[.,]\d+)*(?:[eE][-+]?\d+)?(?![\p{L}\p{N}_])", RegexOptions.CultureInvariant);

    public static bool Same(string a, string b, double tolerance)
    {
        if (a == b) return true;
        var na = new List<(double Value, double Half)>(); var nb = new List<(double Value, double Half)>();
        string sa = Token.Replace(a, m => Parse(m.Value, na)), sb = Token.Replace(b, m => Parse(m.Value, nb));
        if (sa != sb || na.Count != nb.Count) return false;
        for (int i = 0; i < na.Count; i++)
        {
            var (x, hx) = na[i]; var (y, hy) = nb[i];
            double magnitude = Math.Max(Math.Abs(x), Math.Abs(y));
            if (Math.Abs(x - y) > tolerance + tolerance * magnitude + hx + hy + 8 * (Math.BitIncrement(magnitude) - magnitude)) return false;
        }
        return true;
    }

    static string Parse(string token, List<(double, double)> numbers)
    {
        string t = token.Replace('−', '-'); int sign = 1;
        if (t.StartsWith('-')) { sign = -1; t = t[1..]; } else if (t.StartsWith('+')) t = t[1..];
        int exponent = 0, e = t.IndexOfAny(['e', 'E']);
        if (e >= 0) { if (!int.TryParse(t[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return token; t = t[..e]; }
        int dots = t.Count(c => c == '.'), commas = t.Count(c => c == ',');
        char? separator = dots > 0 && commas > 0 ? (t.LastIndexOf('.') > t.LastIndexOf(',') ? '.' : ',') : dots == 1 ? '.' : commas == 1 ? ',' : null;
        string integer = t, fraction = "";
        if (separator is char s) { int i = t.LastIndexOf(s); integer = t[..i]; fraction = t[(i + 1)..]; }
        integer = integer.Replace(".", "").Replace(",", "");
        if (fraction.Contains('.') || fraction.Contains(',')) return token;
        if (!double.TryParse(integer + (fraction.Length > 0 ? "." + fraction : "") + (e >= 0 ? "E" + exponent.ToString(CultureInfo.InvariantCulture) : ""),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return token;
        numbers.Add((sign * value, separator is null && e < 0 ? 0 : 0.5 * Math.Pow(10, exponent - fraction.Length)));
        return "#";
    }
}
