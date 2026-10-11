using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;

/// <summary>
/// Sezione 12 delle prove dell'adattatore: punto resistente SLU della sezione c.a. (revisione di architettura R2, registro F2-17).
/// Ogni messaggio comincia con il numero della prova (12a…12g).
/// 12a. Mappatura dei motivi della libreria: ogni testo noto ha la traduzione, la domanda nulla dà «Azione nulla», un testo inventato dà
///      InvalidOperationException.
/// 12b. Interruttore: motore predefinito atteso dell'adattatore e della sessione di calcolo.
/// 12c. Motore Legacy: punti dell'adattatore uguali bit per bit al vettore nativo SectionSolver.CalculateDomainPoint; verifiche del dominio 3D
///      (CheckMany) uguali alla formula di prima di R2 (tasso CalculateWorkingRatio del punto nativo, punto resistente, stato).
/// 12d. Motore Library contro Legacy sulla stessa griglia (4 sezioni, 5 criteri, SLU e SLV, strategie iterativa e di intersezione): quando il
///      punto grezzo rispetta i vincoli della ricerca (DomainPointConvergence della libreria) il punto, il tasso, lo stato e la risposta della
///      sezione sono gli stessi; altrimenti la libreria dà il punto di un'altra strategia che li rispetta, oppure nessun punto con il motivo
///      tradotto. Ogni punto della libreria rispetta i vincoli. Conteggi e scarti in misura.json: è la misura della voce F2-17.
/// 12e. Percorsi del modulo: dominio 3D (CheckMany e Check), resistenza a N assegnato e calcolo headless passano dall'adattatore con il
///      motore richiesto (sonda); con il motore Legacy il calcolo headless coincide bit per bit con quello senza motore.
/// 12f. Scansione dei sorgenti di X.Calculations, X.Core e X.Desktop: nessuna chiamata ai punti grezzi della libreria fuori dall'adattatore.
/// 12g. Annullamento con il motore Library.
/// </summary>
internal static class ResistanceChecks
{
    /// <summary>Motore predefinito atteso: Legacy finché l'utente non decide il passaggio (registro F2-17).</summary>
    public const ResistanceEngine ExpectedDefault = ResistanceEngine.Legacy;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static JsonObject Run(string root, Action<bool, string> check)
    {
        var report = new JsonObject { ["motore_predefinito"] = ConcreteResistanceAdapter.Default.ToString() };
        // Prima le prove brevi, poi la griglia.
        report["12a_mappatura"] = Mapping(check);
        report["12b_interruttore"] = Switch(check);
        report["12e_percorsi"] = Paths(check);
        report["12f_scansione"] = Scan(root, check);
        report["12g_annullamento"] = Cancellation(check);
        report["12cd_griglia"] = Grid(check);
        return report;
    }

    /// <summary>Interruttore, per lo stadio rapido.</summary>
    public static void CheckDefault(Action<bool, string> check)
    {
        check(ConcreteResistanceAdapter.Default == ExpectedDefault, $"12b: motore SLU predefinito {ConcreteResistanceAdapter.Default}, atteso {ExpectedDefault}");
        check(new ConcreteAnalysisSession().Resistance == ExpectedDefault, "12b: motore SLU della sessione senza motore diverso dal predefinito");
    }

    // ------------------------------------------------------------------ 12a
    static JsonObject Mapping(Action<bool, string> check)
    {
        int texts = 0;
        foreach (var text in ConcreteLibraryMapping.ResistanceRejectionTexts)
        {
            var response = new SectionResistanceResponse(new SolverDiagnostics(CalculationStatus.NotConverged, "prova", "1", text), "ConstantN");
            var mapped = ConcreteLibraryMapping.ResistanceRejection(response);
            check(mapped.Length > 0 && mapped != text && !Regex.IsMatch(mapped, @"\b(the|point|missing|invalid|domain|ratio|returned|search)\b", RegexOptions.IgnoreCase), "12a: traduzione assente o inglese per «" + text + "»: «" + mapped + "»");
            texts++;
        }
        var invented = new SectionResistanceResponse(new SolverDiagnostics(CalculationStatus.NotConverged, "prova", "1", "Invented message."), "ConstantN");
        check(Outcome(() => ConcreteLibraryMapping.ResistanceRejection(invented)).StartsWith("error:InvalidOperationException"), "12a: testo inventato della libreria accettato");
        var axes = new GPC.Geometry.CoordinateSystem(new GPC.Geometry.Point3d(0, 0, 0), new GPC.Geometry.Vector3d(1, 0, 0), new GPC.Geometry.Vector3d(0, 1, 0));
        var zero = SectionResistanceResponse.ForZeroDemand(new SectionAnalysisInput(new ResultBeamForces(0, 0, 0, 0, 0, 0, axes)),
            new SolverDiagnostics(CalculationStatus.Completed, "prova", "1"), "ConstantN");
        check(ConcreteLibraryMapping.ResistanceRejection(zero) == "Azione nulla", "12a: domanda nulla");
        return new JsonObject { ["testi_tradotti"] = texts };
    }

    // ------------------------------------------------------------------ 12b
    static JsonObject Switch(Action<bool, string> check)
    {
        CheckDefault(check);
        var c = Sections()[0];
        var section = new CheckerSection(c.Input, c.Settings, DomainOptions(c, "N costante", "Iterativo"), "SLU");
        check(section.ResistanceEngine is null, "12b: sezione senza motore con un motore assegnato");
        using var probe = ServiceabilityProbe.Start();
        ConcreteResistanceAdapter.Points(section, [section.Force(new ActionPoint(-300, 50, 10))]);
        check(probe.Entries.Single() == new ServiceabilityProbeEntry(ConcreteResistanceAdapter.ProbeName, "Points", ExpectedDefault.ToString()),
            "12b: la sezione senza motore non usa il predefinito");
        return new JsonObject { ["motore_predefinito"] = ConcreteResistanceAdapter.Default.ToString() };
    }

    // ------------------------------------------------------------------ 12c, 12d
    sealed record SectionCase(string Id, JsonObject Input, JsonObject Settings, double AxialScale, double MomentScale);

    static string F(double v) => v.ToString("R", Invariant);

    /// <summary>Sezione rettangolare NTC 2018 con barre manuali (x, y, Ø), come il banco F2.1.</summary>
    static SectionCase Rectangle(string id, double b, double h, double fck, (double X, double Y, double Phi)[] bars, string diagram = "Parabola-rettangolo")
    {
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        input["shape"] = "Rettangolare"; input["foro_presente"] = false; input["width_mm"] = F(b); input["height_mm"] = F(h);
        return Complete(id, data, settings, input, fck, bars, diagram, b * h, h);
    }

    static SectionCase Circle(string id, double d, double fck, int count, double radius, double phi)
    {
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        input["shape"] = "Circolare"; input["foro_presente"] = false; input["diameter_mm"] = F(d); input["circular_sides"] = "32";
        var bars = Enumerable.Range(0, count).Select(i => (radius * Math.Cos(2 * Math.PI * i / count), radius * Math.Sin(2 * Math.PI * i / count), phi)).ToArray();
        return Complete(id, data, settings, input, fck, bars, "Parabola-rettangolo", Math.PI * d * d / 4, d);
    }

    static SectionCase Complete(string id, JsonObject data, JsonObject settings, JsonObject input, double fck, (double X, double Y, double Phi)[] bars, string diagram,
        double area, double depth)
    {
        input["barre_manuali"] = new JsonArray(bars.Select(b => (JsonNode)J.Obj(("x", b.X), ("y", b.Y), ("phi", b.Phi))).ToArray());
        input["fck_mpa"] = F(fck); input["fyk_mpa"] = "450"; input["steel_modulus_mpa"] = "200000"; input["steel_fu_mpa"] = "450"; input["steel_eps_u"] = "75";
        input["steel_diagramma"] = "Elastoplastico"; input["cls_diagramma"] = diagram;
        input["alpha_cc"] = "0.85"; input["gamma_c"] = "1.5"; input["gamma_s"] = "1.15"; input["gettato_sottile"] = "No";
        input["cover_mm"] = "30"; input["transverse_bar_diameter_mm"] = "10"; input["staffe_presenti"] = "Sì";
        settings["normativa"] = "NTC 2018"; settings["coefficienti"] = ConcreteStandards.Defaults("NTC 2018");
        ConcreteCalculationSettings.Prepare(input, settings);
        // Scale delle azioni: N di riferimento Ac·fcd e momento di riferimento 0,1·Ac·fcd·h.
        double axial = area * .85 * fck / 1.5 / 1000;
        return new(id, input, settings, axial, .1 * axial * depth / 1000);
    }

    /// <summary>
    /// Sezioni della griglia: la rettangolare 300×500 C25/30 con 4Ø16 agli spigoli (il caso della revisione, ModelChecker.Tests
    /// CheckReportIntegrationTest), la stessa con lo stress block, una rettangolare asimmetrica e una circolare.
    /// </summary>
    static SectionCase[] Sections() =>
    [
        Rectangle("R300x500-4Ø16", 300, 500, 25, [(-102, -202, 16), (102, -202, 16), (102, 202, 16), (-102, 202, 16)]),
        Rectangle("R300x500-4Ø16-stress-block", 300, 500, 25, [(-102, -202, 16), (102, -202, 16), (102, 202, 16), (-102, 202, 16)], "Stress block"),
        Rectangle("R400x700-3Ø20+5Ø20", 400, 700, 30, [(-140, 300, 20), (0, 300, 20), (140, 300, 20), (-150, -300, 20), (-75, -300, 20), (0, -300, 20), (75, -300, 20), (150, -300, 20)]),
        Circle("C600-10Ø20", 600, 35, 10, 240, 20)
    ];

    static JsonObject DomainOptions(SectionCase c, string criterion, string strategy)
    {
        var o = (JsonObject)c.Settings["dominio3d"]!.DeepClone();
        o["criterio"] = criterion; o["strategia"] = strategy; o["assi"] = "Locali"; o["angoli"] = "16"; o["suddivisioni_n"] = "8";
        return o;
    }

    /// <summary>Azioni della griglia: 5 livelli di N (da −0,8 a +0,05 Ac·fcd), 6 direzioni del momento, 2 intensità.</summary>
    static ActionPoint[] Actions(SectionCase c)
    {
        var actions = new List<ActionPoint>();
        foreach (double n in new[] { -.8, -.45, -.15, 0, .05 })
            foreach (double degrees in new[] { 0.0, 30, 90, 150, 225, 300 })
                foreach (double m in new[] { .5, 1.5 })
                {
                    // Componenti esattamente nulle a 0° e 90° (cos 90° in virgola mobile vale 6e-17): sono le domande con direzione di ricerca nulla.
                    double t = degrees * Math.PI / 180, cos = degrees == 90 ? 0 : Math.Cos(t), sin = degrees == 0 ? 0 : Math.Sin(t);
                    actions.Add(new ActionPoint(n * c.AxialScale, m * c.MomentScale * cos, m * c.MomentScale * sin));
                }
        return actions.ToArray();
    }

    static JsonObject Grid(Action<bool, string> check)
    {
        int configurations = 0, forces = 0, legacyMissing = 0, rawOutside = 0, same = 0, moved = 0, rejected = 0, recovered = 0, bothMissing = 0;
        double maxRatioChange = 0; string maxRatioCase = "";
        // Esiti del motore Legacy nei casi in cui la libreria cambia il punto o non lo dà: verdetti che cambiano e cause dei rifiuti.
        var rejectedLegacyVerdicts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var rejectionCauses = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int movedUnconservative = 0, movedConservative = 0; double maxRelativeChange = 0; string maxRelativeCase = "";
        var rejectedCross = new SortedDictionary<string, int>(StringComparer.Ordinal); // criterio · esito legacy · causa
        var legacyInsideExamples = new List<string>();
        var rejections = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var byCriterion = new SortedDictionary<string, int[]>(StringComparer.Ordinal); // criterio → [grezzi fuori dai vincoli, rifiutati, spostati]
        var stats = new Stats("risposta della sezione");
        var sections = Sections();
        foreach (var c in sections)
            foreach (string criterion in CheckerSection.Criteria)
                foreach (string state in c.Id.StartsWith("R300x500-4Ø16") && !c.Id.EndsWith("block") ? new[] { "SLU", "SLV" } : new[] { "SLU" })
                    foreach (string strategy in c.Id == "R300x500-4Ø16" && state == "SLU" ? new[] { "Iterativo", "Intersezione" } : new[] { "Iterativo" })
                    {
                        string config = $"{c.Id} {state} {criterion} {strategy}";
                        var options = DomainOptions(c, criterion, strategy);
                        var legacySection = new CheckerSection(c.Input, c.Settings, options, state, null, ResistanceEngine.Legacy);
                        var librarySection = new CheckerSection(c.Input, c.Settings, options, state, null, ResistanceEngine.Library);
                        var legacyDomain = legacySection.Domain3D(); legacyDomain.ConfigureVerification(options);
                        var libraryDomain = librarySection.Domain3D(); libraryDomain.ConfigureVerification(options);
                        var search = legacySection.Checker.SectionCheckerOptions.FailureAnalysisType;
                        check(search == CheckerSection.Criterion(criterion) && legacyDomain.Native.FailureAnalysisType == search
                            && librarySection.Checker.SectionCheckerOptions.FailureAnalysisType == search, $"12d {config}: criterio della ricerca diverso da quello del tasso");
                        var actions = Actions(c);
                        var demand = actions.Select((a, i) => legacySection.Force(a, i + 1)).ToArray();

                        // 12c. Motore Legacy: vettore nativo e formula di prima di R2.
#pragma warning disable CS0618 // percorso grezzo provato di proposito
                        var direct = legacySection.Checker.SectionSolver.CalculateDomainPoint(demand);
#pragma warning restore CS0618
                        var adapted = ConcreteResistanceAdapter.Points(legacySection, demand);
                        var legacyChecks = legacyDomain.CheckMany(actions);
                        for (int i = 0; i < demand.Length; i++)
                        {
                            string at = $"{config} azione {i}";
                            var p = direct[i]; var a = adapted[i].Point;
                            check(p is null ? a is null : a is not null && a.NRd.Equals(p.NRd) && a.MxRd.Equals(p.MxRd) && a.MyRd.Equals(p.MyRd) && a.Engine == ResistanceEngine.Legacy,
                                $"12c {at}: punto del motore Legacy diverso dal vettore nativo");
                            check(adapted[i].Rejection is null, $"12c {at}: motivo con il motore Legacy");
                            var expected = p is null ? new DomainCheck(null, null, "Checker: punto resistente non trovato")
                                : Expected(p.CalculateWorkingRatio(search, demand[i], 1e6, 1000), CheckerSection.Point(p));
                            var actual = legacyChecks[i];
                            check(Same(actual.Utilization, expected.Utilization) && actual.Resistance == expected.Resistance && actual.Status == expected.Status,
                                $"12c {at}: verifica del dominio {actual.Utilization} «{actual.Status}», prima di R2 {expected.Utilization} «{expected.Status}»");
                            if (p is null) legacyMissing++;
                        }

                        // 12d. Motore Library contro Legacy.
                        using var probe = ServiceabilityProbe.Start();
                        var library = ConcreteResistanceAdapter.Points(librarySection, demand);
                        var responses = probe.Results.OfType<SectionResistanceResponse>().ToArray();
                        check(responses.Length == demand.Length && probe.Entries.All(e => e.Engine == nameof(ResistanceEngine.Library)), $"12d {config}: sonda del motore Library");
                        var libraryChecks = libraryDomain.CheckMany(actions);
                        var counters = byCriterion.TryGetValue(criterion, out var existing) ? existing : byCriterion[criterion] = new int[3];
                        for (int i = 0; i < demand.Length; i++)
                        {
                            string at = $"{config} azione {i}";
                            var raw = direct[i]; var outcome = library[i]; var response = responses[i];
                            bool rawAccepted = raw?.StrainPlane is not null && double.IsFinite(raw.NRd + raw.MxRd + raw.MyRd)
                                && DomainPointConvergence.Evaluate(legacySection.Checker.SectionSolver, demand[i], raw, search).Accepted;
                            if (raw is not null && !rawAccepted) { rawOutside++; counters[0]++; }
                            if (outcome.Point is { } point)
                            {
                                check(point.Engine == ResistanceEngine.Library && response.Diagnostics.Status == CalculationStatus.Completed
                                    && response.Diagnostics.ResistanceConvergence?.Accepted == true, $"12d {at}: punto della libreria senza i vincoli della ricerca");
                                check(Same(libraryChecks[i].Utilization, Expected(point.WorkingRatio(search, demand[i]), CheckerSection.Point(point)).Utilization),
                                    $"12d {at}: verifica del dominio con il motore Library diversa dal punto dell'adattatore");
                                if (rawAccepted && raw is not null)
                                {
                                    check(point.NRd.Equals(raw.NRd) && point.MxRd.Equals(raw.MxRd) && point.MyRd.Equals(raw.MyRd),
                                        $"12d {at}: punto grezzo nei vincoli ma punto della libreria diverso");
                                    var l = legacyChecks[i]; var r = libraryChecks[i];
                                    check(Same(l.Utilization, r.Utilization) && l.Resistance == r.Resistance && l.Status == r.Status, $"12d {at}: verifica diversa con lo stesso punto");
                                    var errors = new List<string>();
                                    Json.Compare(J.Node(l.Response), J.Node(r.Response), at + " risposta", stats, errors, 1e-9);
                                    check(errors.Count == 0, "12d " + string.Join("; ", errors.Take(3)));
                                    stats.Results++;
                                    same++;
                                }
                                else if (raw is null) recovered++;
                                else
                                {
                                    moved++; counters[2]++;
                                    if (legacyChecks[i].Utilization is double before && libraryChecks[i].Utilization is double after)
                                    {
                                        if (Math.Abs(after - before) > maxRatioChange) { maxRatioChange = Math.Abs(after - before); maxRatioCase = $"{at}: η {before:R} → {after:R}"; }
                                        double relative = Math.Abs(after - before) / Math.Max(Math.Abs(before), 1e-12);
                                        if (relative > maxRelativeChange) { maxRelativeChange = relative; maxRelativeCase = $"{at}: η {before:R} → {after:R}"; }
                                        // Verdetto che cambia: il legacy dichiarava entro il dominio e la libreria fuori (legacy non cautelativo), o il contrario.
                                        if (before <= 1 && after > 1) movedUnconservative++;
                                        if (before > 1 && after <= 1) movedConservative++;
                                    }
                                }
                            }
                            else
                            {
                                check(outcome.Rejection is { Length: > 0 } && libraryChecks[i].Utilization is null && libraryChecks[i].Status == "Checker: " + outcome.Rejection,
                                    $"12d {at}: punto mancante senza motivo");
                                check(!rawAccepted || legacyChecks[i].Utilization is null, $"12d {at}: punto grezzo nei vincoli e valido, rifiutato dalla libreria");
                                rejections[outcome.Rejection!] = rejections.GetValueOrDefault(outcome.Rejection!) + 1;
                                if (raw is null) bothMissing++; else { rejected++; counters[1]++; }
                                // Causa: direzione di ricerca nulla (componente della domanda che fissa il verso uguale a zero) oppure residui fuori tolleranza.
                                var convergence = response.Diagnostics.ResistanceConvergence;
                                string cause = convergence is null ? "senza valutazione dei vincoli"
                                    : !convergence.HasSearchDirection ? "direzione di ricerca nulla"
                                    : "residui fuori tolleranza: " + string.Join("+", convergence.Residuals.Where(x => !x.Accepted).Select(x => x.Quantity));
                                rejectionCauses[cause] = rejectionCauses.GetValueOrDefault(cause) + 1;
                                string verdict = legacyChecks[i].Utilization is not double eta ? "legacy senza tasso" : eta <= 1 ? "legacy entro il dominio" : "legacy fuori dominio";
                                rejectedLegacyVerdicts[verdict] = rejectedLegacyVerdicts.GetValueOrDefault(verdict) + 1;
                                string cross = $"{criterion} · {verdict} · {cause}";
                                rejectedCross[cross] = rejectedCross.GetValueOrDefault(cross) + 1;
                                if (verdict == "legacy entro il dominio" && legacyInsideExamples.Count < 5)
                                    legacyInsideExamples.Add($"{at}: N={actions[i].N:R} Mx={actions[i].Mx:R} My={actions[i].My:R} kN·kNm, η legacy {legacyChecks[i].Utilization:R}, "
                                        + $"punto grezzo N={raw!.NRd / 1000:R} Mx={raw.MxRd / 1e6:R} My={raw.MyRd / 1e6:R}; {cause}");
                            }
                        }
                        configurations++; forces += demand.Length;
                    }
        check(configurations == 4 * 5 + 5 + 5 && forces == configurations * 60, $"12d: griglia di {configurations} configurazioni e {forces} forze");
        // La griglia deve contenere casi che distinguono i motori, altrimenti la prova non dice nulla sulla voce F2-17.
        check(rawOutside > 0 && same > 0, $"12d: griglia senza punti grezzi fuori dai vincoli ({rawOutside}) o senza punti uguali ({same})");
        var criteria = new JsonObject();
        foreach (var (name, v) in byCriterion) criteria[name] = new JsonObject { ["grezzi_fuori_dai_vincoli"] = v[0], ["rifiutati_dalla_libreria"] = v[1], ["spostati_dalla_libreria"] = v[2] };
        return new JsonObject
        {
            ["configurazioni"] = configurations, ["forze"] = forces,
            ["legacy_senza_punto"] = legacyMissing, ["grezzi_fuori_dai_vincoli"] = rawOutside,
            ["uguali"] = same, ["spostati"] = moved, ["rifiutati"] = rejected, ["recuperati"] = recovered, ["senza_punto_con_entrambi"] = bothMissing,
            ["massima_variazione_del_tasso"] = maxRatioChange, ["caso_della_massima_variazione"] = maxRatioCase,
            ["massima_variazione_relativa_del_tasso"] = maxRelativeChange, ["caso_della_massima_variazione_relativa"] = maxRelativeCase,
            ["spostati_da_entro_a_fuori_dominio"] = movedUnconservative, ["spostati_da_fuori_a_entro_dominio"] = movedConservative,
            ["motivi"] = new JsonObject(rejections.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            ["cause_dei_rifiuti"] = new JsonObject(rejectionCauses.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            ["esito_legacy_dei_rifiutati"] = new JsonObject(rejectedLegacyVerdicts.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            ["rifiutati_per_criterio_esito_e_causa"] = new JsonObject(rejectedCross.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            ["esempi_legacy_entro_il_dominio_rifiutati"] = new JsonArray(legacyInsideExamples.Select(e => (JsonNode)e).ToArray()),
            ["per_criterio"] = criteria, ["risposte_confrontate"] = stats.Json()
        };
    }

    static bool Same(double? a, double? b) => a is null ? b is null : b is double y && a.Value.Equals(y);

    /// <summary>Esito di una verifica del dominio dal tasso e dal punto (copia di CheckerDomain3D.Outcome, interno a X.Calculations).</summary>
    static DomainCheck Expected(double ratio, ActionPoint p)
        => !double.IsFinite(ratio) || ratio < 0 || !double.IsFinite(p.N + p.Mx + p.My) ? new(null, null, "Checker: resistenza non determinata")
         : new(ratio, p, ratio <= 1 ? "Entro il dominio" : "Fuori dominio");

    // ------------------------------------------------------------------ 12e
    static JsonObject Paths(Action<bool, string> check)
    {
        var c = Sections()[0];
        int paths = 0;
        void Engines(string path, Func<ResistanceEngine?, object?> call)
        {
            foreach (var engine in new ResistanceEngine?[] { null, ResistanceEngine.Legacy, ResistanceEngine.Library })
            {
                using var probe = ServiceabilityProbe.Start();
                call(engine);
                var expected = (engine ?? ExpectedDefault).ToString();
                var entries = probe.Entries.Where(e => e.Adapter == ConcreteResistanceAdapter.ProbeName).ToArray();
                check(entries.Length > 0 && entries.All(e => e.Engine == expected), $"12e {path} ({engine?.ToString() ?? "predefinito"}): sonda {string.Join(", ", entries.Select(e => e.Operation + "/" + e.Engine))}");
            }
            paths++;
        }
        var options = DomainOptions(c, "Eccentricità costante", "Iterativo");
        Engines("CheckerDomain3D.CheckMany", e => { var d = new CheckerSection(c.Input, c.Settings, options, "SLU", null, e).Domain3D(); d.ConfigureVerification(options); return d.CheckMany([new(-400, 60, 20)]); });
        Engines("CheckerDomain3D.Check", e => { var d = new CheckerSection(c.Input, c.Settings, options, "SLU", null, e).Domain3D(); d.ConfigureVerification(options); return d.Check(new(-400, 60, 20)); });
        Engines("SectionMomentResistance.Calculate", e => SectionMomentResistance.Calculate(c.Input, c.Settings, -400, false, default, e));
        Engines("ConcreteAnalysisSession.Domain", e =>
            new ConcreteAnalysisSession(null, e).Domain(c.Input, c.Settings, options, "SLU", true, [J.Obj(("id", "r1"), ("N", "-400"), ("Mx", "60"), ("My", "20"))]));

        // Calcolo headless: con il motore Legacy uguale bit per bit al calcolo senza motore; con il motore Library cambiano solo i domini.
        var data = SezioneCA.DefaultData();
        // Identificativi univoci in tutti gli insiemi: la preparazione genera un GUID per le righe senza identificativo o con uno ripetuto.
        foreach (string set in new[] { "SLU", "SLV" })
            data["combinazioni"]![set] = new JsonArray(
                J.Obj(("id", set + "-a"), ("nome", "A"), ("azioni", new JsonArray("-2500", "500", "250"))),
                J.Obj(("id", set + "-b"), ("nome", "B"), ("azioni", new JsonArray("-800", "-300", "120"))),
                J.Obj(("id", set + "-c"), ("nome", "C"), ("azioni", new JsonArray("200", "150", "0"))));
        data["combinazioni"]!["SLE"] = new JsonArray(J.Obj(("id", "SLE-a"), ("nome", "A"), ("azioni", new JsonArray("-2500", "500", "250"))));
        // Forma canonica (chiavi ordinate): le tensioni SLE si calcolano in parallelo e l'ordine delle righe del dizionario non è fissato.
        static JsonNode? Canonical(JsonNode? node) => node switch
        {
            JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
            JsonArray a => new JsonArray(a.Select(Canonical).ToArray()),
            _ => node?.DeepClone()
        };
        // GUID generati a ogni calcolo sostituiti con segnaposto nell'ordine di comparsa (come la GuidMap della cattura headless).
        static string Text(JsonNode? r)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return Regex.Replace(Canonical(r)?.ToJsonString() ?? "null", "[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}",
                m => map.TryGetValue(m.Value, out var token) ? token : map[m.Value] = "<GUID" + map.Count + ">");
        }
        var plain = ConcreteAnalysis.Calculate(data);
        var legacy = ConcreteAnalysis.Calculate(data, default, null, ResistanceEngine.Legacy);
        var differing = plain.Select(p => p.Key).Where(k => Text(plain[k]) != Text(legacy[k])).ToArray();
        if (differing.Length > 0 && Environment.GetEnvironmentVariable("ANTHEA_DUMP_12E") is { Length: > 0 } dump)
        {
            Directory.CreateDirectory(dump);
            File.WriteAllText(Path.Combine(dump, "senza-motore.json"), Text(plain)); File.WriteAllText(Path.Combine(dump, "legacy.json"), Text(legacy));
        }
        check(differing.Length == 0, "12e: calcolo headless con il motore Legacy diverso da quello senza motore: " + string.Join(", ", differing));
        using (var probe = ServiceabilityProbe.Start())
        {
            var library = ConcreteAnalysis.Calculate(data, default, null, ResistanceEngine.Library);
            var entries = probe.Entries.Where(e => e.Adapter == ConcreteResistanceAdapter.ProbeName).ToArray();
            check(entries.Length > 0 && entries.All(e => e.Engine == nameof(ResistanceEngine.Library)), "12e: calcolo headless senza il motore Library");
            foreach (var key in plain.Select(p => p.Key).Where(k => k != "domini" && k != "errori_calcolo"))
                check(Text(plain[key]) == Text(library[key]), "12e: calcolo headless con il motore Library diverso fuori dai domini: " + key);
        }
        paths++;
        return new JsonObject { ["percorsi"] = paths };
    }

    // ------------------------------------------------------------------ 12f
    static JsonObject Scan(string root, Action<bool, string> check)
    {
        var raw = new Regex(@"\.(CalculateDomainPoint|CalculateFailureDomainPoint|CalculateForce)\s*\(", RegexOptions.CultureInvariant);
        int files = 0;
        foreach (string project in new[] { "X.Calculations", "X.Core", "X.Desktop" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                files++;
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                string code = Regex.Replace(File.ReadAllText(file), @"(?s)/\*.*?\*/|//[^\n]*", " ");
                var calls = raw.Matches(code).Select(m => m.Groups[1].Value).ToArray();
                if (relative == "X.Calculations/ConcreteResistanceAdapter.cs")
                    check(calls.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(new[] { "CalculateDomainPoint", "CalculateForce" }), "12f: l'adattatore deve avere solo i due rami Legacy: " + string.Join(", ", calls));
                else check(calls.Length == 0, $"12f: {relative}: punto grezzo della libreria fuori dall'adattatore ({string.Join(", ", calls)})");
            }
        return new JsonObject { ["file_scansionati"] = files };
    }

    // ------------------------------------------------------------------ 12g
    static JsonObject Cancellation(Action<bool, string> check)
    {
        var c = Sections()[0];
        var section = new CheckerSection(c.Input, c.Settings, DomainOptions(c, "N costante", "Iterativo"), "SLU", null, ResistanceEngine.Library);
        using var stopped = new CancellationTokenSource(); stopped.Cancel();
        check(Outcome(() => ConcreteResistanceAdapter.Points(section, [section.Force(new ActionPoint(-300, 50, 10))], stopped.Token)).StartsWith("error:OperationCanceledException"),
            "12g: annullamento ignorato dal motore Library");
        return new JsonObject { ["annullamento"] = true };
    }

    static string Outcome(Func<object?> call)
    {
        try { call(); return "ok"; }
        catch (Exception e) { return "error:" + e.GetType().Name + " " + e.Message; }
    }
}
