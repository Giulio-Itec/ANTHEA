using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Serviceability;

/// <summary>
/// Prove della parte SLE dell'adattatore (refactoring F2.7b, commit A4; progetto F2.7 §6.1-§6.3): limiti tensionali e fessurazione
/// attraverso <see cref="ConcreteServiceabilityAdapter"/>, con ENTRAMBI i motori scelti in modo esplicito (il predefinito è Legacy fino ad A8).
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
        try { checks.All(); }
        finally { CultureInfo.CurrentCulture = saved; }
        checks.Report["controlli"] = checks.Checks;
        return checks;
    }

    bool Selected(params string[] proofs) => only is null || proofs.Any(only.Contains);

    void All()
    {
        // Interruttore: Legacy fino al commit A8 di F2.7b; le prove scelgono il motore in modo esplicito.
        Check(ConcreteServiceabilityAdapter.Default == ServiceabilityEngine.Legacy, "motore SLE predefinito " + ConcreteServiceabilityAdapter.Default + ", atteso Legacy");
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
        _ = ConcreteServiceabilityAdapter.CrackRequirement("NTC 2018", "SLE_QP", J.Obj(("esposizione", "XC3")), ServiceabilityEngine.Library);
        Check(probe.Entries.Count == 1 && probe.Entries[0] == new ServiceabilityProbeEntry(ConcreteServiceabilityAdapter.ProbeName, "CrackRequirement", "Library"),
            proof + ": la sonda non registra la chiamata nota");
        var inner = Task.Run(() => { using var nested = ServiceabilityProbe.Start(); return nested.Entries.Count; }).Result;
        Check(inner == 0 && probe.Entries.Count == 1, proof + ": la sonda di un altro flusso riceve chiamate di questo");
    }

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
        Check(Enum.GetValues<CrackOutcome>().All(ConcreteLibraryMapping.TranslatedOutcomes.Contains),
            "5a: esiti senza traduzione: " + string.Join(", ", Enum.GetValues<CrackOutcome>().Except(ConcreteLibraryMapping.TranslatedOutcomes)));
        Check(Enum.GetValues<CrackReason>().All(ConcreteLibraryMapping.TranslatedReasons.Contains),
            "5a: motivi senza traduzione: " + string.Join(", ", Enum.GetValues<CrackReason>().Except(ConcreteLibraryMapping.TranslatedReasons)));
        Check(Enum.GetValues<CrackCriterion>().All(ConcreteLibraryMapping.TranslatedCriteria.Contains), "5a: criteri senza traduzione");
        Check(Enum.GetValues<CrackProfile>().All(ConcreteLibraryMapping.TranslatedProfiles.Contains), "5a: profili senza nome");
        foreach (var combination in Enum.GetValues<ServiceabilityCombination>())
            Check(ConcreteLibraryMapping.CombinationOf(ConcreteLibraryMapping.SetOf(combination)) == combination, "5a: combinazione " + combination);
        foreach (var set in new[] { "ED", "CURVA", "LIMITE", "BENCH", "SLU", "SLV", "" })
            Check(ConcreteLibraryMapping.CombinationOf(set) is null, "5a: insieme non SLE con una combinazione: " + set);
        var codes = Constants(typeof(CrackTraceCodes)); var flags = Constants(typeof(CrackTraceFlags)); var arguments = Constants(typeof(CrackTraceArguments));
        var rejections = Constants(typeof(CrackRejection)).Where(c => c != CrackRejection.DataKey).ToArray();
        Check(codes.Length >= 80 && codes.ToHashSet().SetEquals(ConcreteLibraryMapping.TranslatedTraceCodes),
            "5a: codici della traccia senza traduzione: " + string.Join(", ", codes.Except(ConcreteLibraryMapping.TranslatedTraceCodes)) + " / tradotti e assenti: "
            + string.Join(", ", ConcreteLibraryMapping.TranslatedTraceCodes.Except(codes)));
        Check(flags.ToHashSet().SetEquals(ConcreteLibraryMapping.TranslatedTraceFlags), "5a: flag della traccia senza traduzione: " + string.Join(", ", flags.Except(ConcreteLibraryMapping.TranslatedTraceFlags)));
        Check(arguments.ToHashSet().SetEquals(ConcreteLibraryMapping.TranslatedTraceArguments), "5a: argomenti della traccia senza traduzione: " + string.Join(", ", arguments.Except(ConcreteLibraryMapping.TranslatedTraceArguments)));
        Check(rejections.ToHashSet().SetEquals(ConcreteLibraryMapping.TranslatedRejections), "5a: codici di rifiuto senza traduzione: " + string.Join(", ", rejections.Except(ConcreteLibraryMapping.TranslatedRejections)));
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
            CrackTraceCodes.MeanStrainDifference => [[CrackTraceFlags.ComputedGoverns], [CrackTraceFlags.MinimumGoverns], [], [CrackTraceFlags.UpperBound]],
            CrackTraceCodes.BetaMinimum => [[], [CrackTraceFlags.UpperBound]],
            CrackTraceCodes.MaximumCrackSpacing => [[CrackTraceFlags.FormulaStandard], [CrackTraceFlags.FormulaModelCode2010], [CrackTraceFlags.FormulaDin], [CrackTraceFlags.FormulaSparseBars],
                [CrackTraceFlags.UpperBound, CrackTraceFlags.UpperBoundNtc], [CrackTraceFlags.UpperBound, CrackTraceFlags.UpperBoundEurocode], [CrackTraceFlags.UpperBound, CrackTraceFlags.UpperBoundDin]],
            CrackTraceCodes.Width => [[], [CrackTraceFlags.UpperBound], [CrackTraceFlags.DsCoarseHalf], [CrackTraceFlags.Envelope]],
            CrackTraceCodes.WidthRatio => [[], [CrackTraceFlags.Envelope]],
            CrackTraceCodes.Cover => [[CrackTraceFlags.Assigned], [CrackTraceFlags.Nominal], [CrackTraceFlags.Formula]],
            CrackTraceCodes.Spacing => [[CrackTraceFlags.Automatic], [CrackTraceFlags.Manual], [CrackTraceFlags.Formula]],
            CrackTraceCodes.Kt => [[CrackTraceFlags.ShortTerm], [CrackTraceFlags.LongTerm]],
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
                        if (ConcreteCodeChecks.CrackRequirement(standard, set, options).Kind.StartsWith("Non richiesta") && skipped++ % 4 != 0) continue;
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
    /// rifiutati al punto d'uso o non letti), sulla R300x500 della griglia; ognuno con il ramo che deve raggiungere nel legacy (risultato o messaggio
    /// del rifiuto). I rami non raggiungibili in ANTHEA (regola di k₂ prima di D7-b, hc,eff nulla, Ac,eff nulla, faccia senza armatura, asse neutro non
    /// determinato, nessuna armatura tesa, superfici interne non supportate, limite superiore DIN, rifiuti dei parametri della formula, delle tensioni delle
    /// barre e della sezione non fessurata) restano provati dalla libreria e dalla 5a.
    /// </summary>
    static IEnumerable<(CrackState State, string Branch, Func<Ntc2018Checks.CrackResult?, string, bool> Reached)> TargetedCrackStates()
    {
        (CrackState, string, Func<Ntc2018Checks.CrackResult?, string, bool>) Make(int id, string standard, string set, ActionPoint action, string branch,
            Func<Ntc2018Checks.CrackResult?, string, bool> reached, Action<JsonObject> options, bool tendon = false)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
            input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500"; input["top_bar_count"] = "2"; input["bottom_bar_count"] = "3";
            input["side_bar_count_per_side"] = "0"; input["top_bar_diameter_mm"] = "16"; input["bottom_bar_diameter_mm"] = "20"; input["cover_mm"] = "30"; input["fck_mpa"] = "30";
            input["gettato_sottile"] = "No"; settings["normativa"] = standard;
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
    }

    void CrackGrid()
    {
        int checksBefore = Checks, states = 0, rejected = 0, discriminating = 0, ntcEntirelyTensile = 0, ntcEntirelyTensileNullSpacing = 0, notesDifferent = 0, compressed = 0,
            libraryValues = 0;
        var stats = new Stats("fessurazione (griglia della cattura densa e stati mirati)");
        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        var coverage = new LibraryCoverage(); var withTargeted = new LibraryCoverage();
        var targeted = TargetedCrackStates().ToArray(); var reachedBranches = new List<string>();
        foreach (var s in CrackStates().Concat(targeted.Select(t => t.State)))
        {
            bool grid = s.Id < TargetedFirstId;
            if (grid) states++;
            string id = $"fessurazione {s.Id} {s.Section} {s.Standard} {s.Set}";
            var legacySection = Try(() => new CheckerSection(s.Prepared, s.Input, s.Settings, s.Options, "SLU", ServiceabilityEngine.Legacy));
            var librarySection = Try(() => new CheckerSection(s.Prepared, s.Input, s.Settings, s.Options, "SLU", ServiceabilityEngine.Library));
            Check(legacySection.Outcome == librarySection.Outcome && legacySection.Message == librarySection.Message, id + ": costruzione diversa fra i motori");
            if (legacySection.Value is null) { rejected++; continue; }
            var legacyState = Try(() => legacySection.Value.Stress(s.Action, s.Set));
            var libraryState = Try(() => librarySection.Value!.Stress(s.Action, s.Set));
            Check(legacyState.Outcome == libraryState.Outcome && legacyState.Message == libraryState.Message, id + ": analisi diversa fra i motori");
            if (legacyState.Value is null) { rejected++; continue; }
            foreach (var culture in new[] { Italian, Invariant })
            {
                CultureInfo.CurrentCulture = culture;
                string tag = id + " (" + (culture == Italian ? "it-IT" : "invariante") + ")";
                var direct = Try(() => Ntc2018Checks.Cracking(legacySection.Value, legacyState.Value, s.Action, s.Input, s.Settings, s.Options, s.Set));
                var legacy = Try(() => ConcreteServiceabilityAdapter.Cracking(legacySection.Value, legacyState.Value, s.Action, s.Input, s.Settings, s.Options, s.Set, ServiceabilityEngine.Legacy));
                // Risultati della libreria prima della mappatura, dalla sonda: copertura della 5c e requisito di ogni ramo (ciclo di prototipo, giro 1).
                ServiceabilityProbe probe;
                (string Outcome, string Message, Ntc2018Checks.CrackResult? Value, Exception? Error) library;
                using (probe = ServiceabilityProbe.Start())
                    library = Try(() => ConcreteServiceabilityAdapter.Cracking(librarySection.Value!, libraryState.Value!, s.Action, s.Input, s.Settings, s.Options, s.Set, ServiceabilityEngine.Library));
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
                // 5b: legacy dell'adattatore = legacy diretto, bit per bit; marcatori dei due motori.
                Check(direct.Outcome == legacy.Outcome && direct.Message == legacy.Message && Text(direct.Value) == Text(legacy.Value), tag + ": il motore Legacy dell'adattatore non coincide con il legacy");
                Check(legacy.Value is null || legacy.Value.Engine == ServiceabilityEngine.Legacy, tag + ": marcatore del motore Legacy");
                Check(library.Value is null || library.Value.Engine == ServiceabilityEngine.Library, tag + ": marcatore del motore Library");
                // 5c: libreria = legacy (esiti, stati, rifiuti, regioni, traccia; numeri entro 1e-9; note con i numeri entro 1e-9).
                Check(!library.Message.Contains(UnmappedText) && !Text(library.Value).Contains(UnmappedText), tag + ": testo non tradotto: " + library.Message);
                Check(direct.Outcome == library.Outcome && direct.Message == library.Message, $"{tag}: rifiuto «{direct.Message}» ({direct.Outcome}) con il legacy, «{library.Message}» ({library.Outcome}) con la libreria"
                    + (library.Error is { } error and not ArgumentException ? Environment.NewLine + error : ""));
                if (direct.Value is null) { if (culture == Italian) rejected++; continue; }
                notesDifferent += CompareCrack(tag, J.Node(direct.Value)!, J.Node(library.Value)!, stats);
                if (culture != Italian) continue;
                stats.Results++;
                if (Text(direct.Value) != Text(library.Value)) discriminating++;
                string key = direct.Value.Status.Split(" · ")[0].Split(':')[0];
                outcomes[key] = outcomes.GetValueOrDefault(key) + 1;
                if (s.Standard == "NTC 2018" && direct.Value.Status.StartsWith("Interamente tesa"))
                {
                    ntcEntirelyTensile++;
                    if (library.Value!.BarSpacing is null && direct.Value.BarSpacing is null) ntcEntirelyTensileNullSpacing++;
                }
            }
            CultureInfo.CurrentCulture = Italian;
        }
        CultureInfo.CurrentCulture = Italian;
        Check(states == 936, "griglia della fessurazione: " + states + " stati invece di 936");
        // Opzione legacy nominata del BarSpacing (F2.7-D5): le 13 righe NTC interamente tese con BarSpacing nullo, nei due motori.
        Check(ntcEntirelyTensileNullSpacing == 13, $"5c: BarSpacing nullo in {ntcEntirelyTensileNullSpacing} righe NTC interamente tese su {ntcEntirelyTensile}, attese 13");
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
        Report["fessurazione_griglia"]!["ntc_interamente_tese_barspacing_nullo"] = ntcEntirelyTensileNullSpacing;
        Report["fessurazione_griglia"]!["stati_per_esito"] = new JsonObject(outcomes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value)));
        Lines.Add(stats.Line() + $"; {states} stati, {discriminating} con uscite dei due motori diverse, {notesDifferent} note con numeri diversi entro 1e-9, "
            + $"BarSpacing nullo in {ntcEntirelyTensileNullSpacing} righe NTC interamente tese (opzione legacy F2.7-D5); {Checks - checksBefore} controlli (5b, 5c; it-IT e invariante)");
        Lines.Add("griglia · " + coverage.Line() + $"; {compressed} sezioni interamente compresse dal motivo della libreria");
        Lines.Add($"griglia e {targeted.Length} stati mirati ({string.Join("; ", reachedBranches)}) · " + withTargeted.Line());
    }

    /// <summary>
    /// Confronto di due CrackResult: traccia voce per voce (simbolo, unità, espressione esatti; valore entro la tolleranza; nota con i numeri
    /// entro la tolleranza), poi il resto (stati, regioni, esiti) con <see cref="Json.Compare"/>. Restituisce il numero di note diverse.
    /// </summary>
    int CompareCrack(string id, JsonNode legacyNode, JsonNode libraryNode, Stats stats)
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
                foreach (var exposure in Ntc2018Checks.Exposures.Append("").Append("XZ9"))
                    foreach (var sensitive in new[] { false, true })
                        foreach (var limit in new[] { "", "0.25", "abc", "-5", "0", " " })
                        {
                            var o = J.Obj(("esposizione", exposure), ("sensibilita", sensitive ? "Sensibile" : "Poco sensibile"), ("limite_fessure", limit));
                            string id = $"requisito {standard} {set} {exposure} {sensitive} «{limit}»";
                            var direct = Try<object>(() => ConcreteCodeChecks.CrackRequirement(standard, set, o));
                            var legacy = Try<object>(() => ConcreteServiceabilityAdapter.CrackRequirement(standard, set, o, ServiceabilityEngine.Legacy));
                            var library = Try<object>(() => ConcreteServiceabilityAdapter.CrackRequirement(standard, set, o, ServiceabilityEngine.Library));
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
                    var sections2 = Engines.ToDictionary(e => e, e => Try(() => new CheckerSection(prepared, input, settings, options, "SLU", e)));
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
                            var legacy = Try(() => legacySection.Stress(action, set));
                            var library = Try(() => librarySection.Stress(action, set));
                            Check(legacy.Outcome == library.Outcome && legacy.Message == library.Message, $"{id}: rifiuto «{legacy.Message}» con il legacy, «{library.Message}» con la libreria");
                            if (legacy.Value is null) { rejected++; continue; }
                            var l = legacy.Value; var b = library.Value!;
                            // 5b: tasso, stato e limiti di CheckerSection con il motore Legacy = LegacyServiceability diretto = adattatore Legacy, bit per bit.
                            var direct = LegacyServiceability.StressLimits(l.Native, set, effective, legacySection.Section, phi, phiT, reduction);
                            var adapted = ConcreteServiceabilityAdapter.StressLimits(l.Native, set, effective, legacySection.Section, phi, phiT, reduction, ServiceabilityEngine.Legacy);
                            Check(direct == adapted with { Engine = null } && Same(l.Ratio, direct.Ratio) && l.Status == direct.Status && Same(l.ConcreteStressLimit, direct.ConcreteStressLimit)
                                && Same(l.SteelStressLimit, direct.SteelStressLimit), id + ": il motore Legacy non coincide con LegacyServiceability");
                            Check(l.Engine == ServiceabilityEngine.Legacy && b.Engine == ServiceabilityEngine.Library, id + ": marcatori degli stati tensionali");
                            // Lo stato della libreria viene dall'adattatore (stessi valori bit per bit).
                            var libraryLimits = ConcreteServiceabilityAdapter.StressLimits(b.Native, set, effective, librarySection.Section, phi, phiT, reduction, ServiceabilityEngine.Library);
                            Check(Same(b.Ratio, libraryLimits.Ratio) && b.Status == libraryLimits.Status && Same(b.ConcreteStressLimit, libraryLimits.ConcreteStressLimit)
                                && Same(b.SteelStressLimit, libraryLimits.SteelStressLimit), id + ": lo stato della libreria non viene dall'adattatore");
                            // Sonda: due chiamate per motore (stato e verifica qui sopra), operazione delle combinazioni SLE.
                            Check(probe.Entries.Count(e => e.Operation == "StressLimits" && e.Engine == "Legacy") == 2 && probe.Entries.Count(e => e.Operation == "StressLimits" && e.Engine == "Library") == 2,
                                id + ": sonda " + string.Join(", ", probe.Entries));
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
                            bool legacy = ConcreteServiceabilityAdapter.CrackingRequired(set, workspace, ServiceabilityEngine.Legacy);
                            bool library = ConcreteServiceabilityAdapter.CrackingRequired(set, workspace, ServiceabilityEngine.Library);
                            bool production = SleCheckScope.Cracking(set, workspace);
                            string id = $"ambito {standard} {set} {exposure} «{limit}» {layout}";
                            Check(direct == legacy && direct == library && direct == production, $"{id}: legacy {direct}, adattatore Legacy {legacy}, Library {library}, SleCheckScope {production}");
                            // Sonda: SleCheckScope passa dall'adattatore con il motore predefinito.
                            Check(probe.Entries.SequenceEqual(new ServiceabilityProbeEntry[]
                            {
                                new(ConcreteServiceabilityAdapter.ProbeName, "CrackingRequired", "Legacy"), new(ConcreteServiceabilityAdapter.ProbeName, "CrackingRequired", "Library"),
                                new(ConcreteServiceabilityAdapter.ProbeName, "CrackingRequired", ConcreteServiceabilityAdapter.Default.ToString())
                            }), id + ": sonda " + string.Join(", ", probe.Entries));
                            rows++; if (direct) required++;
                        }
        Report["ambito_5i"] = new JsonObject { ["righe"] = rows, ["richieste"] = required };
        Lines.Add($"5i ambito della fessurazione: {rows} righe uguali nei due motori e in SleCheckScope ({required} richieste, norme escluse e dati non validi compresi); {Checks - checksBefore} controlli");
    }

    // ================================================================== 5k. scansione dei sorgenti
    /// <summary>
    /// Chiamate del legacy SLE fuori dall'adattatore e dai file legacy, vietate nel codice di produzione (X.Calculations, X.Core, X.Desktop) e, dal
    /// commit A5, nelle suite (supporto/test, tests), salvo le voci di legacy-allowlist.json: file, numero esatto di chiamate (oppure tutto il file),
    /// testo ammesso di ogni chiamata, motivo e scadenza. Una chiamata nuova, una in più o in meno o una voce senza riscontro fanno fallire la prova
    /// (prova negativa n11). Commenti e stringhe non contano.
    /// </summary>
    void Scan()
    {
        int checksBefore = Checks;
        var legacyFiles = new[] { "X.Calculations/Ntc2018Checks.cs", "X.Calculations/ConcreteTensionCracking.cs", "X.Calculations/ConcreteInnerCracking.cs",
            "X.Calculations/ConcreteCodeChecks.cs", "X.Calculations/LegacyServiceability.cs" };
        const string adapter = "X.Calculations/ConcreteServiceabilityAdapter.cs";
        foreach (var file in legacyFiles.Append(adapter)) Check(File.Exists(Path.Combine(root, file)), "5k: file dichiarato assente: " + file);
        var forbidden = new Regex(@"\bNtc2018Checks\s*\.\s*(Cracking|CrackRequirement|CrackWidth|CrackWidthWithDetails|CrackK2)\b"
            + @"|\bConcreteCodeChecks\s*\.\s*(CrackRequirement|CrackWidth|UnbondedCrackWidthBound|EffectiveCrackDepth)\b"
            + @"|\bLegacyServiceability\s*\.\s*(StressLimits|CrackingRequired)\b|using\s+static\s+Anthea\.Calculations\.(Ntc2018Checks|ConcreteCodeChecks|LegacyServiceability)\b",
            RegexOptions.CultureInvariant);
        string Call(Match m) => Regex.Replace(Regex.Replace(m.Value, @"\s*\.\s*", "."), @"\s+", " ");
        // Il campione: la scansione riconosce le chiamate, anche spezzate, e ignora commenti, testi e stringhe grezze.
        const string sample = "var r = Ntc2018Checks .Cracking(a); var q = ConcreteCodeChecks.CrackRequirement(x); // Ntc2018Checks.Cracking\n var t = \"LegacyServiceability.StressLimits\";"
            + "\n var u = \"\"\"\n Ntc2018Checks.CrackK2\n \"\"\"; var v = $@\"{x} ConcreteCodeChecks.CrackWidth\";";
        Check(forbidden.Matches(DurabilityChecks.CodeOnly(sample)).Select(Call).SequenceEqual(["Ntc2018Checks.Cracking", "ConcreteCodeChecks.CrackRequirement"]),
            "5k: la scansione non riconosce il campione");
        var allowlist = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "ConcreteLibraryAdapter.Checks", "legacy-allowlist.json")))!["voci"]!.AsArray()
            .Select(v => (File: v!["file"]!.GetValue<string>(), Calls: v["chiamate"]?.GetValue<int>(), WholeFile: v["tutto_il_file"]?.GetValue<bool>() == true,
                Texts: v["testo"]?.AsArray().Select(t => t!.GetValue<string>()).ToArray() ?? [], Reason: v["motivo"]?.GetValue<string>(), Expiry: v["scadenza"]?.GetValue<string>()))
            .ToList();
        Check(allowlist.Select(a => a.File).Distinct().Count() == allowlist.Count
            && allowlist.All(a => !string.IsNullOrWhiteSpace(a.Reason) && !string.IsNullOrWhiteSpace(a.Expiry) && (a.WholeFile ? a.Calls is null : a.Calls > 0 && a.Texts.Length > 0)),
            "5k: voci di legacy-allowlist.json ripetute o incomplete (file, chiamate e testo oppure tutto il file, motivo, scadenza)");
        int files = 0, suiteFiles = 0; var found = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (folder, production) in new[] { ("X.Calculations", true), ("X.Core", true), ("X.Desktop", true), ("supporto/test", false), ("tests", false) })
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
    void SessionEngine()
    {
        int checksBefore = Checks;
        var property = typeof(ConcreteAnalysisSession).GetProperty(nameof(ConcreteAnalysisSession.Engine))!;
        var field = typeof(ConcreteAnalysisSession).GetField("<Engine>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(property.SetMethod is null && field is { IsInitOnly: true }, "5m: il motore della sessione è modificabile");
        Check(typeof(ConcreteAnalysisSession).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(ServiceabilityEngine) || f.FieldType == typeof(ServiceabilityEngine?)).All(f => f.IsInitOnly), "5m: un campo del motore è scrivibile");
        Check(new ConcreteAnalysisSession().Engine == ConcreteServiceabilityAdapter.Default, "5m: motore predefinito della sessione");
        var data = SleDocument("NTC 2018", "Rettangolare"); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        var rows = SessionRows(data, "SLE_QP"); var options = settings["sle"]!["SLE_QP"]!.AsObject();
        var sessions = Engines.ToDictionary(e => e, e => new ConcreteAnalysisSession(e));
        var outcomes = Engines.ToDictionary(e => e, e => sessions[e].Stress(input, settings, options, "SLE_QP", rows));
        foreach (var (id, legacy) in outcomes[ServiceabilityEngine.Legacy])
        {
            var library = outcomes[ServiceabilityEngine.Library][id];
            if (legacy.State is null) continue;
            Check(!ReferenceEquals(legacy.State, library.State) && legacy.State.Engine == ServiceabilityEngine.Legacy && library.State!.Engine == ServiceabilityEngine.Library,
                "5m: due sessioni con motori diversi condividono uno stato o hanno lo stesso marcatore: " + id);
        }
        // La firma della cache contiene il motore: cambiando per riflessione il motore della sessione, gli stati si ricalcolano.
        var session = sessions[ServiceabilityEngine.Legacy];
        var first = session.Stress(input, settings, options, "SLE_QP", rows);
        var again = session.Stress(input, settings, options, "SLE_QP", rows);
        Check(first.All(p => p.Value.State is null || ReferenceEquals(p.Value.State, again[p.Key].State)), "5m: la cache della sessione non riusa gli stati");
        field!.SetValue(session, ServiceabilityEngine.Library);
        var switched = session.Stress(input, settings, options, "SLE_QP", rows);
        Check(first.All(p => p.Value.State is null || !ReferenceEquals(p.Value.State, switched[p.Key].State) && switched[p.Key].State!.Engine == ServiceabilityEngine.Library),
            "5m: la firma della cache non contiene il motore della sessione");
        Report["sessione_5m"] = new JsonObject { ["righe"] = rows.Length, ["controlli"] = Checks - checksBefore };
        Lines.Add($"5m motore della sessione: di sola lettura, nella firma della cache ({rows.Length} righe), sessioni con motori diversi senza stati comuni; {Checks - checksBefore} controlli");
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
