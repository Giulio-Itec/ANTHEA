using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;

/// <summary>
/// Percorsi di produzione delle verifiche SLE (progetto F2.7 §6.1, prove 5d-5j) e attesi indipendenti (prova 6), con entrambi i motori.
/// Ogni percorso è confrontato bit per bit con l'adattatore chiamato con lo stesso motore; i casi con uscite dei due motori diverse
/// (Ac,eff da poligoni anziché dalla mesh, ultime cifre) rendono la prova capace di riconoscere un percorso che aggiri l'adattatore.
/// </summary>
sealed partial class ServiceabilityChecks
{
    static readonly string[] Norms = ["NTC 2018", "EN 1992-1-1", "Model Code 2010", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1"];

    /// <summary>Documento str_palo con le tre famiglie SLE (13 azioni della cattura densa, identificativi fissi) e parametri SLE comuni.</summary>
    static JsonObject SleDocument(string norm, string shape, string exposure = "XC3")
    {
        var data = SezioneCA.DefaultData(); var input = data["input"]!.AsObject(); var settings = SectionWorkspace.Prepare(data);
        settings["normativa"] = norm; settings["coefficienti"] = ConcreteStandards.Defaults(norm);
        foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
        input["shape"] = shape.Replace(" cava", "");
        if (shape.EndsWith("cava")) { input["foro_presente"] = true; input["inner_width_mm"] = "200"; input["inner_height_mm"] = "300"; input["inner_diameter_mm"] = "400"; }
        ConcreteCalculationSettings.Prepare(input, settings);
        var common = settings["sle_comuni"]!.AsObject();
        common["modello"] = "Lineare"; common["trazione_cls"] = "No"; common["esposizione"] = exposure; common["sensibilita"] = "Poco sensibile"; common["durata"] = "Lunga";
        common["aderenza"] = "Migliorata"; common["copriferro_fessure"] = ""; common["spaziatura_fessure"] = ""; common["limite_fessure"] = norm == "Model Code 2010" ? "0.3" : "";
        var combinations = data["combinazioni"]!.AsObject();
        foreach (var set in SleSets)
            combinations[set] = new JsonArray(CrackActions.Select((a, i) => (JsonNode)J.Obj(("id", set + "-" + i), ("nome", "Azione " + i),
                ("azioni", new[] { a.N.ToString("R", Invariant), a.Mx.ToString("R", Invariant), a.My.ToString("R", Invariant) }))).ToArray());
        SectionWorkspace.Prepare(data);
        return data;
    }

    /// <summary>Righe della sessione come in ConcreteAnalysis (id, nome, N, Mx, My).</summary>
    static JsonObject[] SessionRows(JsonObject data, string set) => data["combinazioni"]!.Array(set).OfType<JsonObject>()
        .Select(row => J.Obj(("id", row.S("id")), ("nome", row.S("nome")), ("N", row["azioni"]![0]), ("Mx", row["azioni"]![1]), ("My", row["azioni"]![2]))).ToArray();

    /// <summary>Esito di una riga come lo produce ConcreteAnalysisSession.Stress, ricostruito con l'adattatore e lo stesso motore.</summary>
    static StressOutcome Expected(CheckerSection section, JsonObject row, JsonObject input, JsonObject settings, JsonObject options, string set, ServiceabilityEngine engine)
    {
        try
        {
            var force = ConcreteAnalysisSession.ReadAction(row);
            var state = section.Stress(force, set);
            Ntc2018Checks.CrackResult crack;
            try { crack = ConcreteServiceabilityAdapter.Cracking(section, state, force, input, settings, options, set, engine); }
            catch (Exception ex) { crack = new(null, null, null, null, "Fessurazione non calcolata: " + ex.Message); }
            return new(state, state.Ratio, state.Status, crack.Status, crack);
        }
        catch (Exception ex) { return new(null, null, "Checker: " + ex.Message); }
    }

    static string Export(string id, StressOutcome outcome) => ConcreteAnalysisSession.ExportStress(new Dictionary<string, StressOutcome> { [id] = outcome })[id]!.ToJsonString();

    /// <summary>Esito di una riga come lo produceva la sessione prima di A4: fessurazione del legacy chiamata direttamente (prova 5b sui calcoli del modulo).</summary>
    static StressOutcome LegacyDirect(CheckerSection section, JsonObject row, JsonObject input, JsonObject settings, JsonObject options, string set)
    {
        try
        {
            var force = ConcreteAnalysisSession.ReadAction(row);
            var state = section.Stress(force, set);
            Ntc2018Checks.CrackResult crack;
            try { crack = Ntc2018Checks.Cracking(section, state, force, input, settings, options, set); }
            catch (Exception ex) { crack = new(null, null, null, null, "Fessurazione non calcolata: " + ex.Message); }
            return new(state, state.Ratio, state.Status, crack.Status, crack);
        }
        catch (Exception ex) { return new(null, null, "Checker: " + ex.Message); }
    }

    // ================================================================== 5d. sessione (P1: scheda SLE WPF)
    void Session()
    {
        int checksBefore = Checks, rows = 0, discriminating = 0, markers = 0;
        ProbeSelfTest("5d");
        foreach (var norm in new[] { "NTC 2018", "EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1" })
            foreach (var shape in new[] { "Rettangolare", "Circolare", "Rettangolare cava" })
            {
                var data = SleDocument(norm, shape); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
                foreach (var set in SleSets)
                {
                    var options = settings["sle"]![set]!.AsObject(); var sessionRows = SessionRows(data, set);
                    var outputs = new Dictionary<ServiceabilityEngine, Dictionary<string, StressOutcome>>();
                    foreach (var engine in Engines)
                    {
                        using var probe = ServiceabilityProbe.Start();
                        outputs[engine] = new ConcreteAnalysisSession(engine).Stress(input, settings, options, set, sessionRows);
                        var entries = probe.Entries;
                        Check(entries.All(e => e.Engine == engine.ToString()) && entries.Count(e => e.Operation == "StressLimits") >= sessionRows.Length
                            && entries.Count(e => e.Operation == "Cracking") == outputs[engine].Count(p => p.Value.State is not null),
                            $"5d {norm} {shape} {set} ({engine}): sonda {string.Join(", ", entries.GroupBy(e => e.Operation + "/" + e.Engine).Select(g => g.Key + " " + g.Count()))}");
                        var section = new CheckerSection(input, settings, options, "SLU", engine);
                        foreach (var row in sessionRows)
                        {
                            string id = row.S("id"); var outcome = outputs[engine][id];
                            Check(Export(id, outcome) == Export(id, Expected(section, row, input, settings, options, set, engine)),
                                $"5d {norm} {shape} {set} {id} ({engine}): la sessione non coincide con l'adattatore del motore");
                            // 5b sui calcoli del modulo: con il motore Legacy la sessione coincide con la fessurazione legacy chiamata direttamente.
                            if (engine == ServiceabilityEngine.Legacy)
                                Check(Export(id, outcome) == Export(id, LegacyDirect(section, row, input, settings, options, set)), $"5b {norm} {shape} {set} {id}: la sessione Legacy non coincide con il legacy diretto");
                            if (outcome.State is not null) { Check(outcome.State.Engine == engine, $"5d {id} ({engine}): marcatore dello stato"); markers++; }
                            if (outcome.CrackResult is { } crack && !crack.Status.StartsWith("Fessurazione non calcolata")) { Check(crack.Engine == engine, $"5d {id} ({engine}): marcatore della fessurazione"); markers++; }
                        }
                    }
                    foreach (var row in sessionRows)
                    {
                        string id = row.S("id"); rows++;
                        if (Export(id, outputs[ServiceabilityEngine.Legacy][id]) != Export(id, outputs[ServiceabilityEngine.Library][id])) discriminating++;
                    }
                }
            }
        Check(discriminating >= MinimumDiscriminating, $"5d: {discriminating} righe della sessione con uscite dei due motori diverse");
        Report["sessione_5d"] = new JsonObject { ["righe"] = rows, ["uscite_dei_motori_diverse"] = discriminating, ["marcatori"] = markers };
        Lines.Add($"5d sessione: {rows} righe per motore uguali bit per bit all'adattatore con lo stesso motore, {discriminating} con uscite dei due motori diverse, {markers} marcatori; {Checks - checksBefore} controlli");
    }

    // ================================================================== 5e, 5h. calcolo headless (P2) e relazioni (P7, P8)
    void Headless()
    {
        int checksBefore = Checks, rows = 0, discriminating = 0, reports = 0, symbols = 0;
        ProbeSelfTest("5e");
        var complete = X.Core.ReportConcrete.Sections.Select(s => s.Key).ToHashSet();
        foreach (var norm in Norms)
            foreach (var shape in new[] { "Rettangolare", "Rettangolare cava" })
            {
                var data = SleDocument(norm, shape, norm.StartsWith("NS") ? "XD3" : "XC3");
                var results = new Dictionary<ServiceabilityEngine, JsonObject>();
                foreach (var engine in Engines)
                {
                    using var probe = ServiceabilityProbe.Start();
                    results[engine] = ConcreteAnalysis.Calculate(data, default, engine);
                    Check(probe.Entries.Count > 0 && probe.Entries.All(e => e.Engine == engine.ToString()) && probe.Entries.Any(e => e.Operation == "StressLimits"),
                        $"5e {norm} {shape} ({engine}): il calcolo headless non passa dall'adattatore con il motore richiesto");
                    // Il JSON 'tensioni' è quello della sessione con lo stesso motore, sugli stessi dati preparati.
                    var prepared = (JsonObject)data.DeepClone(); var settings = SectionWorkspace.Prepare(prepared); var input = prepared["input"]!.AsObject();
                    var model = CheckerSection.PrepareModel(input, settings);
                    foreach (var set in SleSets)
                    {
                        var session = ConcreteAnalysisSession.ExportStress(new ConcreteAnalysisSession(engine).Stress(input, settings, settings["sle"]![set]!.AsObject(), set, SessionRows(prepared, set), default, model));
                        var headless = results[engine]["tensioni"]![set]!.AsObject();
                        Check(headless.Count == session.Count && headless.Count == CrackActions.Length, $"5e {norm} {shape} {set} ({engine}): righe {headless.Count} / {session.Count}");
                        foreach (var (id, row) in headless)
                            Check(row!.ToJsonString() == session[id]?.ToJsonString(), $"5e {norm} {shape} {set} {id} ({engine}): 'tensioni' del calcolo headless diverse dalla sessione con lo stesso motore: "
                                + FirstLineDifference(row.ToJsonString(J.Options), session[id]?.ToJsonString(J.Options) ?? ""));
                    }
                }
                // Stesso risultato del motore predefinito (le righe di 'tensioni' arrivano dal calcolo parallelo: confronto senza l'ordine delle chiavi).
                Check(J.Equivalent(ConcreteAnalysis.Calculate(data), results[ConcreteServiceabilityAdapter.Default]), $"5e {norm} {shape}: il motore predefinito non è {ConcreteServiceabilityAdapter.Default}");
                foreach (var set in SleSets)
                    foreach (var (id, legacy) in results[ServiceabilityEngine.Legacy]["tensioni"]![set]!.AsObject())
                    {
                        rows++;
                        if (legacy!.ToJsonString() != results[ServiceabilityEngine.Library]["tensioni"]![set]![id]!.ToJsonString()) discriminating++;
                    }
                // P8: i simboli cercati dalla relazione sintetica esistono con entrambi i motori.
                foreach (var engine in Engines)
                {
                    var details = results[engine]["tensioni"]!.AsObject().SelectMany(s => s.Value!.AsObject()).Select(r => r.Value!["CrackResult"]?["Details"]).OfType<JsonArray>()
                        .SelectMany(d => d).Select(d => d!["Symbol"]!.GetValue<string>()).ToHashSet();
                    var wanted = norm == "NTC 2018" ? new[] { "Δsm adottata", "β apertura", "εsm − εcm" } : new[] { "sr,max", "εsm − εcm" };
                    Check(wanted.All(details.Contains), $"5h {norm} {shape} ({engine}): simboli della relazione sintetica assenti: " + string.Join(", ", wanted.Where(w => !details.Contains(w))));
                    symbols += wanted.Length;
                }
                // 5h: relazioni completa e sintetica identiche fra i JSON dei due motori (due decimali); i requisiti passano dall'adattatore
                // con il motore predefinito.
                foreach (var (kind, make) in new (string, Func<JsonObject, byte[]>)[]
                {
                    ("completa", r => X.Core.ReportConcrete.Create("Prova SLE", (JsonObject)r["dati"]!.DeepClone(), (JsonObject)r.DeepClone(), complete, null, true)),
                    ("sintetica", r => X.Core.ReportConcreteShort.Create("Prova SLE", (JsonObject)r["dati"]!.DeepClone(), (JsonObject)r.DeepClone()))
                })
                {
                    using var probe = ServiceabilityProbe.Start();
                    string a = DocxText(make(Ordered(results[ServiceabilityEngine.Legacy], data))), b = DocxText(make(Ordered(results[ServiceabilityEngine.Library], data)));
                    Check(a.Contains("SLE") && a == b, $"5h {norm} {shape}: relazione {kind} diversa fra i motori: " + FirstLineDifference(a, b));
                    if (kind == "completa")
                        Check(probe.Entries.Count >= 2 * SleSets.Length && probe.Entries.All(e => e.Operation == "CrackingRequired" && e.Engine == ConcreteServiceabilityAdapter.Default.ToString()),
                            $"5h {norm} {shape}: la relazione non passa dall'adattatore per i requisiti: " + string.Join(", ", probe.Entries));
                    reports++;
                }
            }
        Check(discriminating >= MinimumDiscriminating, $"5e: {discriminating} righe del calcolo headless con uscite dei due motori diverse");
        Report["headless_5e"] = new JsonObject { ["righe"] = rows, ["uscite_dei_motori_diverse"] = discriminating };
        Report["relazioni_5h"] = new JsonObject { ["relazioni"] = reports, ["simboli_della_sintetica"] = symbols };
        Lines.Add($"5e calcolo headless: {rows} righe 'tensioni' uguali alla sessione con lo stesso motore (7 norme, 2 forme), {discriminating} con uscite dei due motori diverse; "
            + $"5h: {reports} relazioni identiche fra i motori, {symbols} simboli della sintetica presenti; {Checks - checksBefore} controlli");
    }

    /// <summary>
    /// Risultato con le righe di 'tensioni' nell'ordine delle combinazioni del documento: la sessione le restituisce nell'ordine del calcolo
    /// parallelo (ConcurrentDictionary), che varia da un calcolo all'altro anche con lo stesso motore e ordina le righe della relazione.
    /// </summary>
    static JsonObject Ordered(JsonObject result, JsonObject data)
    {
        var copy = (JsonObject)result.DeepClone();
        foreach (var set in SleSets)
            if (copy["tensioni"]?[set] is JsonObject rows)
            {
                var order = data["combinazioni"]!.Array(set).OfType<JsonObject>().Select(r => r.S("id")).ToList();
                copy["tensioni"]![set] = new JsonObject(rows.OrderBy(p => order.IndexOf(p.Key)).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            }
        return copy;
    }

    static string DocxText(byte[] docx)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx), System.IO.Compression.ZipArchiveMode.Read);
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var xml = System.Xml.Linq.XDocument.Load(stream);
        System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join("\n", xml.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));
    }

    static string FirstLineDifference(string a, string b)
    {
        var x = a.Split('\n'); var y = b.Split('\n');
        for (int i = 0; i < Math.Min(x.Length, y.Length); i++) if (x[i] != y[i]) return $"riga {i + 1}: «{x[i]}» / «{y[i]}»";
        return $"righe {x.Length} / {y.Length}";
    }

    // ================================================================== 5f. progetto armature (P3)
    void Design()
    {
        int checksBefore = Checks, values = 0, discriminating = 0, candidates = 0;
        ProbeSelfTest("5f");
        foreach (var shape in new[] { "Rettangolare", "Circolare" })
        {
            var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data); var input = data["input"]!;
            input["shape"] = shape; input["cover_mm"] = "40"; input["width_mm"] = "400"; input["height_mm"] = "600"; input["diameter_mm"] = "600";
            ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1"; ws["sle_comuni"]!["modello"] = "Lineare";
            ws["sle_comuni"]!["spaziatura_fessure"] = "150";
            ws["taglio"]!["modello_circolare"] = "Parametri assegnati"; ws["ancoraggi"]!["lunghezza"] = "2000";
            foreach (string set in SectionWorkspace.Sets) data["combinazioni"]![set] = new JsonArray();
            data["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("id", "u1"), ("nome", "Flessione"), ("azioni", new[] { "0", "100", "20" })));
            foreach (var set in new[] { "SLE_FREQ", "SLE_QP" })
                data["combinazioni"]![set] = new JsonArray(new[] { ("-50", "70", "10"), ("30", "55", "-15"), ("-200", "40", "25") }
                    .Select((a, i) => (JsonNode)J.Obj(("id", set + i), ("nome", set + " " + i), ("azioni", new[] { a.Item1, a.Item2, a.Item3 }))).ToArray());
            var prepared = (JsonObject)data.DeepClone(); var settings = SectionWorkspace.Prepare(prepared);
            var results = new Dictionary<ServiceabilityEngine, ConcreteDesignResult>();
            foreach (var engine in Engines)
            {
                var options = new ConcreteDesignOptions { Diameters = [16, 20], TopCounts = [3, 4], BottomCounts = [3, 4], SideCounts = [2], CircularCounts = [8, 12, 16],
                    StirrupDiameters = [8], StirrupSpacings = [100, 150, 200], MaxEvaluations = 1000, MaxParallelism = 1, ServiceabilityEngine = engine };
                using var probe = ServiceabilityProbe.Start();
                results[engine] = ConcreteReinforcementDesign.Optimize(data, options);
                Check(probe.Entries.Any(e => e.Operation == "Cracking") && probe.Entries.All(e => e.Engine == engine.ToString()),
                    $"5f {shape} ({engine}): il progetto armature non passa dall'adattatore con il motore delle opzioni");
            }
            foreach (var engine in Engines)
            {
                var other = engine == ServiceabilityEngine.Legacy ? ServiceabilityEngine.Library : ServiceabilityEngine.Legacy;
                foreach (var candidate in results[engine].Candidates)
                {
                    bool different = false;
                    foreach (var check in candidate.Checks.Where(c => c.Key is "wk_freq" or "wk_qp"))
                    {
                        string set = check.Key == "wk_freq" ? "SLE_FREQ" : "SLE_QP";
                        var row = prepared["combinazioni"]!.Array(set).OfType<JsonObject>().Single(r => r.S("nome") == check.Combination);
                        var action = new ActionPoint(SectionWorkspace.Number(row["azioni"]![0]!.ToString(), "N"), SectionWorkspace.Number(row["azioni"]![1]!.ToString(), "Mx"),
                            SectionWorkspace.Number(row["azioni"]![2]!.ToString(), "My"));
                        double? Ratio(ServiceabilityEngine e)
                        {
                            var o = settings["sle"]![set]!.AsObject();
                            var section = new CheckerSection(CheckerSection.PrepareModel(candidate.Input, settings), candidate.Input, settings, o, "SLU", e);
                            return ConcreteServiceabilityAdapter.Cracking(section, section.Stress(action, set), action, candidate.Input, settings, o, set, e).Ratio;
                        }
                        Check(Same(check.Ratio, Ratio(engine)), $"5f {shape} candidato {candidate.Id} {check.Key} {check.Combination} ({engine}): {check.Ratio} invece di {Ratio(engine)}");
                        if (!Same(Ratio(other), check.Ratio)) different = true;
                        values++;
                    }
                    candidates++;
                    if (different) discriminating++;
                }
            }
            Check(results.Values.All(r => r.Candidates.Count > 0), $"5f {shape}: nessun candidato; esclusioni " + string.Join("; ", results.Values.SelectMany(r => r.Exclusions).Select(p => p.Key + " " + p.Value))
                + "; esempi " + string.Join("; ", results.Values.SelectMany(r => r.ExclusionExamples).Select(p => p.Key + ": " + p.Value.Detail)));
        }
        Check(discriminating >= MinimumDiscriminating, $"5f: {discriminating} candidati con wk dei due motori diversi");
        Report["progetto_armature_5f"] = new JsonObject { ["candidati"] = candidates, ["valori_wk"] = values, ["candidati_con_uscite_dei_motori_diverse"] = discriminating };
        Lines.Add($"5f progetto armature: {values} valori wk di {candidates} candidati uguali all'adattatore con lo stesso motore, {discriminating} candidati con wk dei due motori diversi; {Checks - checksBefore} controlli");
    }

    // ================================================================== 5g. muri (P4, P5)
    void Walls()
    {
        int checksBefore = Checks, values = 0, discriminating = 0, uncracked = 0;
        ProbeSelfTest("5g");
        var documents = new List<(string Name, JsonObject Data)> { ("predefinito", RetainingWall.Defaults()) };
        foreach (var (exposure, count, diameter) in new[] { ("XC1", 5, 14), ("XD1", 8, 16), ("XS3", 6, 20) })
        {
            var d = RetainingWall.Defaults(); d["materials"]!["exposure"] = exposure;
            foreach (var key in new[] { "stem", "toe", "heel" }) { d["reinforcement"]![key]!["count"] = count; d["reinforcement"]![key]!["diameter"] = diameter; }
            documents.Add((exposure, d));
        }
        foreach (var (name, document) in documents)
        {
            var results = new Dictionary<ServiceabilityEngine, RetainingWall.Result>();
            foreach (var engine in Engines)
            {
                using var probe = ServiceabilityProbe.Start();
                results[engine] = RetainingWall.Calculate(document, default, engine);
                Check(probe.Entries.Any(e => e.Operation == "Cracking") && probe.Entries.All(e => e.Engine == engine.ToString()), $"5g {name} ({engine}): i muri non passano dall'adattatore con il motore del calcolo");
                var wall = results[engine];
                foreach (var check in wall.Structural.Where(c => c.Name.EndsWith(" · fessurazione")))
                {
                    var c = wall.Cases.Single(x => x.Name == check.Combination);
                    var f = c.Sections.Single(s => s.Name == check.Member && s.Position == check.Position);
                    var expected = WallCrack(wall.Input, c, f, engine);
                    Check(Text(check with { Member = null, Position = null }) == Text(expected), $"5g {name} {check.Name} {check.Combination} ({engine}): {Text(check)} invece di {Text(expected)}");
                    values++;
                    if (engine == ServiceabilityEngine.Legacy)
                    {
                        if (check.Status.StartsWith("Non fessurata")) uncracked++;
                        var library = results.GetValueOrDefault(ServiceabilityEngine.Library);
                        _ = library;
                    }
                }
            }
            var legacyChecks = results[ServiceabilityEngine.Legacy].Structural.Where(c => c.Name.EndsWith(" · fessurazione")).ToArray();
            var libraryChecks = results[ServiceabilityEngine.Library].Structural.Where(c => c.Name.EndsWith(" · fessurazione")).ToArray();
            Check(legacyChecks.Length == libraryChecks.Length && legacyChecks.Length > 0, $"5g {name}: verifiche di fessurazione {legacyChecks.Length} / {libraryChecks.Length}");
            for (int i = 0; i < legacyChecks.Length; i++)
            {
                Check(legacyChecks[i].Name == libraryChecks[i].Name && legacyChecks[i].Status == libraryChecks[i].Status, $"5g {name}: stato «{legacyChecks[i].Status}» / «{libraryChecks[i].Status}»");
                Check(Math.Abs(legacyChecks[i].Demand - libraryChecks[i].Demand) <= Tolerance + Tolerance * Math.Abs(legacyChecks[i].Demand), $"5g {name}: wk {legacyChecks[i].Demand} / {libraryChecks[i].Demand}");
                if (Text(legacyChecks[i]) != Text(libraryChecks[i])) discriminating++;
            }
        }
        Check(discriminating >= MinimumDiscriminating, $"5g: {discriminating} verifiche di fessurazione dei muri con uscite dei due motori diverse");
        // P5: predimensionamento delle armature, stesso candidato con i due motori.
        var proposals = new Dictionary<ServiceabilityEngine, RetainingWall.ReinforcementProposal>();
        foreach (var engine in Engines)
        {
            using var probe = ServiceabilityProbe.Start();
            proposals[engine] = RetainingWall.DesignReinforcement(RetainingWall.Defaults(), default, engine);
            Check(probe.Entries.Any(e => e.Operation == "Cracking") && probe.Entries.All(e => e.Engine == engine.ToString()), $"5g predimensionamento ({engine}): sonda");
        }
        Check(proposals[ServiceabilityEngine.Legacy].Input["reinforcement"]!.ToJsonString() == proposals[ServiceabilityEngine.Library].Input["reinforcement"]!.ToJsonString()
            && proposals[ServiceabilityEngine.Legacy].Passed == proposals[ServiceabilityEngine.Library].Passed, "5g: il predimensionamento sceglie candidati diversi nei due motori");
        Report["muri_5g"] = new JsonObject { ["verifiche_fessurazione"] = values, ["uscite_dei_motori_diverse"] = discriminating, ["non_fessurate"] = uncracked, ["documenti"] = documents.Count };
        Lines.Add($"5g muri: {values} verifiche di fessurazione uguali all'adattatore con lo stesso motore ({documents.Count} documenti), {discriminating} con uscite dei due motori diverse, "
            + $"{uncracked} «Non fessurata» (regola dei muri, F2.7-D4); predimensionamento con lo stesso candidato; {Checks - checksBefore} controlli");
    }

    /// <summary>Verifica di fessurazione di una sezione del muro come RetainingWall.StructuralChecks, con l'adattatore e il motore indicato.</summary>
    static RetainingWall.Check WallCrack(JsonObject d, RetainingWall.LoadCase c, RetainingWall.SectionForce f, ServiceabilityEngine engine)
    {
        var m = d["materials"]!;
        string label = f.Name + " z=" + f.Position.ToString("0.00", CultureInfo.InvariantCulture) + " m";
        var input = RetainingWall.SectionInput(d, f.Name, f.Thickness, f.Position);
        var def = SezioneCA.DefaultData(); def["input"] = input.DeepClone();
        var ws = SectionWorkspace.Prepare(def);
        var opt = ws["sle"]!["SLE_QP"]!.AsObject(); opt["modello"] = "Lineare"; opt["trazione_cls"] = "No"; opt["phi"] = m.D("creep");
        opt["esposizione"] = m.S("exposure"); opt["sensibilita"] = "Non sensibile"; opt["aderenza"] = "Migliorata"; opt["durata"] = "Lunga";
        opt["spaziatura_fessure"] = new[] { "top", "bottom" }.Max(face => (1000 - 2 * m.D("cover") - input.D(face + "_bar_diameter_mm")) / (input.D(face + "_bar_count") - 1)); opt["copriferro_fessure"] = m.D("cover");
        try
        {
            var service = new CheckerSection(input, ws, opt, "SLU", engine);
            var action = new ActionPoint(-f.N, f.M, 0); var state = service.Stress(action, c.State);
            var crack = ConcreteServiceabilityAdapter.Cracking(service, state, action, input, ws, opt, c.State, engine);
            if (crack.Width is null && crack.Status is "Nessuna armatura tesa" or "Armatura/area efficace assente")
            {
                var elasticOptions = (JsonObject)opt.DeepClone(); elasticOptions["trazione_cls"] = "Sì";
                var elasticState = new CheckerSection(input, ws, elasticOptions, "SLU", engine).Stress(action, c.State);
                double tensile = elasticState.Native.GetConcreteVerticesTension(m.D("creep")).Max(x => x.tension);
                double fctk = ConcreteMaterials.Concrete(input).Fctk05;
                if (tensile <= fctk) return RetainingWall.CheckValue(label + " · fessurazione", c.Name, 0, crack.Limit, "mm") with { Status = $"Non fessurata: σt,el={tensile:0.###} ≤ fctk,0.05={fctk:0.###} MPa (GPC)" };
            }
            return crack.Width is double wk ? RetainingWall.CheckValue(label + " · fessurazione", c.Name, wk, crack.Limit, "mm", crack.Status) : RetainingWall.CheckValue(label + " · fessurazione", c.Name, 0, null, "mm", crack.Status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return RetainingWall.CheckValue(label + " · " + c.State, c.Name, Math.Abs(f.M), null, "kNm/m", "Non calcolata: " + ex.Message); }
    }

    // ================================================================== 5j. stati non SLE (ED, LIMITE, CURVA)
    void NonServiceabilityStates()
    {
        int checksBefore = Checks, states = 0;
        ProbeSelfTest("5j");
        foreach (var shape in new[] { "Rettangolare", "Circolare" })
        {
            var data = SleDocument("NTC 2018", shape); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
            var steel = new Dictionary<ServiceabilityEngine, double>();
            foreach (var engine in Engines)
            {
                var nonlinear = (JsonObject)settings["sle"]!["SLE"]!.DeepClone(); nonlinear["modello"] = "Non lineare";
                var section = new CheckerSection(input, settings, nonlinear, "SLU", engine);
                foreach (var (set, action) in new[] { ("ED", new ActionPoint(-300, 120, 30)), ("CURVA", new ActionPoint(-500, 200, 0)), ("BENCH", new ActionPoint(0, 80, 0)) })
                {
                    using var probe = ServiceabilityProbe.Start();
                    var state = section.Stress(action, set);
                    Check(probe.Entries.SequenceEqual(new[] { new ServiceabilityProbeEntry(ConcreteServiceabilityAdapter.ProbeName, "SteelLimit", engine.ToString()) }),
                        $"5j {shape} {set} ({engine}): sonda " + string.Join(", ", probe.Entries));
                    Check(state.Engine == engine && state.Ratio is null && state.ConcreteStressLimit is null && state.Status == "Stato tensionale calcolato", $"5j {shape} {set} ({engine}): stato non SLE");
                    steel[engine] = state.SteelStressLimit; states++;
                }
                // LIMITE: stato del punto limite di un controllo del dominio, dalla CheckerSection del dominio con il motore indicato.
                var domainOptions = settings["dominio3d"]!.AsObject();
                var domain = new CheckerSection(input, settings, domainOptions, "SLU", engine).Domain3D();
                var check = domain.Check(new ActionPoint(-800, 150, 60));
                using (var probe = ServiceabilityProbe.Start())
                {
                    var limit = check.LimitState!.Value;
                    Check(probe.Entries.SequenceEqual(new[] { new ServiceabilityProbeEntry(ConcreteServiceabilityAdapter.ProbeName, "SteelLimit", engine.ToString()) })
                        && limit.Engine == engine && limit.Ratio is null && Same(limit.SteelStressLimit, steel[engine]), $"5j {shape} LIMITE ({engine}): sonda " + string.Join(", ", probe.Entries));
                    states++;
                }
            }
            Check(Same(steel[ServiceabilityEngine.Legacy], steel[ServiceabilityEngine.Library]), $"5j {shape}: SteelStressLimit {steel[ServiceabilityEngine.Legacy]} / {steel[ServiceabilityEngine.Library]}");
            // CURVA dal percorso di produzione (ConcreteCurvatureAnalysis): motore predefinito, solo il limite dell'acciaio.
            var curve = J.Obj(("N", "-500"), ("theta", "0"), ("passi", "10"), ("frazione", "0.9"), ("campionamento", "Lineare"), ("tolleranza_n", "1"), ("raffina_snervamento", "4"), ("angoli", "32"));
            using (var probe = ServiceabilityProbe.Start())
            {
                var result = Try(() => ConcreteCurvatureAnalysis.Calculate(input, settings, curve));
                Check(result.Value is not null && probe.Entries.Count > 0 && probe.Entries.All(e => e == new ServiceabilityProbeEntry(ConcreteServiceabilityAdapter.ProbeName, "SteelLimit", ConcreteServiceabilityAdapter.Default.ToString())),
                    $"5j {shape} CURVA: {result.Message} sonda " + string.Join(", ", probe.Entries.Distinct()));
                states += probe.Entries.Count;
            }
        }
        Report["stati_non_sle_5j"] = new JsonObject { ["stati"] = states };
        Lines.Add($"5j stati non SLE: {states} stati ED, CURVA, BENCH e LIMITE con il solo limite dell'acciaio (nessuna Evaluate), marcatore e sonda del motore; {Checks - checksBefore} controlli");
    }

    // ================================================================== 6. attesi indipendenti
    void Independent()
    {
        int checksBefore = Checks, values = 0;
        void Near(double value, double expected, string message, double tolerance = 1e-9)
        {
            Check(double.IsFinite(value) && Math.Abs(value - expected) <= tolerance * Math.Max(1, Math.Abs(expected)), $"6: {message}: {value:G17} invece di {expected:G17}");
            values++;
        }
        CrackProfile Profile(string norm) => CrackProfiles.Resolve(ConcreteLibraryMapping.StandardFor(norm));
        // Formule scalari: legacy (ConcreteCodeChecks, Ntc2018Checks) e libreria (CrackWidthCalculator, CrackSectionGeometry) con gli stessi attesi.
        var widths = new (string Name, Func<string, double, double, double, double, double, double, double, double, double, bool, bool, double, double> Width)[]
        {
            ("Legacy", (n, s, es, ecm, fct, rho, phi, c, sp, d, st, rb, k2) => n == "NTC 2018" ? Ntc2018Checks.CrackWidth(s, es, ecm, fct, rho, phi, c, sp, d, st, rb, k2)
                : ConcreteCodeChecks.CrackWidth(n, s, es, ecm, fct, rho, phi, c, sp, d, st, rb, k2)),
            ("Library", (n, s, es, ecm, fct, rho, phi, c, sp, d, st, rb, k2) => CrackWidthCalculator.Width(Profile(n), new CrackWidthInput(s, es, ecm, fct, rho, phi, c, sp, d, st, rb, k2)))
        };
        var references = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "supporto", "test", "ConcreteCode.Checks", "reference.json")))!;
        foreach (var (name, width) in widths)
        {
            foreach (var row in references["cracks"]!.AsArray())
            {
                var p = row!["input"]!.AsArray(); double D(int i) => p[i]!.GetValue<double>();
                Near(width("EN 1992-1-1", D(0), D(1), D(2), D(3), D(4), D(5), D(6), D(7), D(8), p[9]!.GetValue<bool>(), p[10]!.GetValue<bool>(), D(11)), row.D("expected"), $"reference.json wk ({name})");
            }
            double Crack(string norm, bool shortTerm = true, double spacing = 100) => width(norm, 200, 200000, 33000, 3, .01, 20, 35, spacing, 350, shortTerm, true, .5);
            Near(Crack("DIN EN 1992-1-1"), (200 * 20 / (3.6 * 3)) * .6 * 200 / 200000, $"DIN vicino alle barre, governa il limite di σs ({name})");
            Near(width("DIN EN 1992-1-1", 200, 200000, 33000, 3, .01, 20, 35, 500, 200, true, true, .5), 1.3 * 200 * .6 * 200 / 200000, $"DIN barre distanziate (7.14) ({name})");
            Near(Crack("DIN EN 1992-1-1", false), Crack("DIN EN 1992-1-1"), $"DIN kt = 0,4 per le due durate ({name})");
            Near(Crack("DS EN 1992-1-1"), (3.4 * Math.Pow(25d / 35, 2d / 3) * 35 + .8 * .5 * .425 * 20 / .01) * .6 * 200 / 200000, $"DS k3 nazionale ({name})");
            Near(Crack("Model Code 2010"), 2 * (35 + 20 / (4 * 1.8 * .01)) * .4 * 200 / 200000, $"MC durata breve a mano ({name})");
            foreach (var norm in ConcreteStandards.OrdinaryNames) Near(width(norm, 0, 200000, 33000, 3, .01, 20, 35, 100, 350, false, true, 1), 0, $"σs nulla, wk nulla {norm} ({name})");
            // X.Verifiche (SectionWorkspaceChecks.cs:97-99): formula NTC.
            Near(width("NTC 2018", 200, 200000, 30000, 2.6, .02, 16, 40, 150, 400, true, true, .5), .1632, $"NTC breve durata e deformazione minima ({name})", 1e-12);
            Near(width("NTC 2018", 200, 200000, 30000, 2.6, .02, 16, 40, 500, 400, false, true, .5), .35972, $"NTC C4.1.10 distanza media 0,75(h−x) ({name})", 1e-12);
        }
        // hc,eff (ConcreteCode.Checks Program.cs:95-99) con il legacy e con CrackSectionGeometry.EffectiveDepth.
        var geometryInput = SezioneCA.DefaultData()["input"]!.AsObject(); geometryInput["shape"] = "Rettangolare";
        var rectangular = new SezioneCA(geometryInput);
        var crackGeometry = new CrackSectionGeometry(rectangular.Outline.Select(p => new Point2d(p[0], p[1])), rectangular.Holes.Select(h => h.Select(p => new Point2d(p[0], p[1]))),
            rectangular.Bars.Select(b => new CrackBar(b.X, b.Y, b.Diametro, b.Area)), CrackBarLayout.Rows);
        double top = rectangular.Outline.Max(p => p[1]);
        foreach (var (norm, height, cover, depth, tensile, expected) in new[] { ("DS EN 1992-1-1", rectangular.Height, 50.0, 300.0, false, 100.0), ("EN 1992-1-1", 600.0, 50.0, 150.0, false, 50.0), ("DIN EN 1992-1-1", 600.0, 50.0, 450.0, true, 160.0) })
        {
            Near(ConcreteCodeChecks.EffectiveCrackDepth(norm, rectangular, 0, 1, top, height, cover, depth, tensile), expected, $"hc,eff {norm} (Legacy)");
            Near(crackGeometry.EffectiveDepth(Profile(norm), 0, 1, top, height, cover, depth, tensile, rectangular.Input.D("cover_mm")), expected, $"hc,eff {norm} (Library)");
        }
        // Sezioni (ca.fessurazione §10, C1-C4) e limiti tensionali (ca.sle-tensioni §10) attraverso l'adattatore, con entrambi i motori.
        foreach (var engine in Engines)
        {
            values += CrackExamples(engine);
            values += StressExample(engine);
        }
        Report["attesi_indipendenti_6"] = new JsonObject { ["valori"] = values };
        Lines.Add($"6 attesi indipendenti: {values} valori con i motori Legacy e Library (reference.json, benchmark DIN/DS/MC, formule NTC di X.Verifiche, hc,eff, esempi C1-C4 di ca.fessurazione §10, "
            + $"η di ca.sle-tensioni §10); {Checks - checksBefore} controlli");
    }

    /// <summary>
    /// Esempi C1-C4 di ca.fessurazione §10 su piani di deformazione assegnati, attraverso l'adattatore: attesi calcolati qui con le formule
    /// della norma (NTC 2018 C4.1.2.2.4.5, EN 1992-1-1 7.3.4) e con Ecm e fctm del materiale della sezione.
    /// </summary>
    int CrackExamples(ServiceabilityEngine engine)
    {
        int values = 0;
        void Near(double? value, double expected, string message)
        {
            Check(value is double v && Math.Abs(v - expected) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"6 ({engine}): {message}: {value:G17} invece di {expected:G17}");
            values++;
        }
        JsonObject Input(double width, double height, (double X, double Y, double Phi)[] bars, double? holeWidth = null, double? holeHeight = null)
        {
            var input = SezioneCA.DefaultInput(); input["shape"] = "Rettangolare"; input["width_mm"] = width; input["height_mm"] = height; input["cover_mm"] = "40";
            input["staffe_presenti"] = "No"; input["fck_mpa"] = "30"; input["classe_cls"] = "C30/37"; input["fyk_mpa"] = "450"; input["steel_modulus_mpa"] = "200000";
            input["barre_manuali"] = new JsonArray(bars.Select(b => (JsonNode)J.Obj(("x", b.X), ("y", b.Y), ("phi", b.Phi))).ToArray());
            if (holeWidth is double hw) { input["foro_presente"] = true; input["inner_width_mm"] = hw; input["inner_height_mm"] = holeHeight!.Value; }
            return input;
        }
        var c1Bars = new[] { (-100d, -200d, 20d), (0d, -200d, 20d), (100d, -200d, 20d), (-100d, 200d, 16d), (100d, 200d, 16d) };
        var c2Bars = c1Bars.Take(3).ToArray();
        var c3Bars = new[] { (-130d, -200d, 20d), (130d, -200d, 20d), (-100d, 200d, 16d), (100d, 200d, 16d) };
        var c4Bars = new[] { -150d, -50d, 50d, 150d }.SelectMany(x => new[] { (x, -250d, 20d), (x, 250d, 20d) })
            .Concat(new[] { -100d, 0d, 100d }.SelectMany(y => new[] { (-150d, y, 20d), (150d, y, 20d) })).ToArray();
        double chi = 1.25e-3 / 260;
        foreach (var norm in new[] { "NTC 2018", "EN 1992-1-1" })
        {
            bool ntc = norm == "NTC 2018";
            foreach (var (name, bars, sparse) in new[] { ("C1", c1Bars, false), ("C2", c2Bars, false), ("C3", c3Bars, true) })
            {
                var input = Input(300, 500, bars);
                // ε(y) = χ (60 − y): asse neutro a y = 60 mm, ε = 1,25·10⁻³ alle barre tese (y = −200).
                var r = ExampleCrack(input, norm, a: 60 * chi, b: 0, c: -chi, engine, new());
                var material = ConcreteMaterials.Concrete(input);
                double es = 200000, ecm = material.Ecm, fctm = material.Fctm, phi = 20, cover = 40, depth = 310, sigma = es * 1.25e-3;
                double area = 300 * Math.Min(Math.Min(2.5 * 50, depth / 3), 500 / 2.0), steel = (sparse ? 2 : 3) * Math.PI * 20 * 20 / 4, rho = steel / area;
                double strain = Math.Max((sigma - .4 * fctm / rho * (1 + es / ecm * rho)) / es, .6 * sigma / es);
                double near = 3.4 * cover + .8 * .5 * .425 * phi / rho;
                double expected = ntc ? 1.7 * (sparse ? Math.Max(near / 1.7, .75 * depth) : near / 1.7) * strain : (sparse ? 1.3 * depth : near) * strain;
                Near(r.EffectiveArea, area, $"{name} {norm} Ac,eff");
                Near(r.Width, expected, $"{name} {norm} wk");
                Near(r.Limit, .3, $"{name} {norm} wlim");
                Check(r.Passed == expected <= .3 && r.Status == (expected <= .3 ? "Apertura entro limite" : "Apertura oltre limite"), $"6 ({engine}): {name} {norm} esito «{r.Status}»");
            }
            // C4: cassone 400 × 600 con foro 200 × 400 in trazione eccentrica, ε(y) = 4,5·10⁻⁴ − 10⁻⁶ y: governa la fascia della parete inferiore.
            var box = Input(400, 600, c4Bars, 200, 400);
            var c4 = ExampleCrack(box, norm, a: 4.5e-4, b: 0, c: -1e-6, engine, J.Obj(("spaziatura_fessure", "300"), ("copriferro_fessure", "40")));
            var boxMaterial = ConcreteMaterials.Concrete(box);
            double esB = 200000, ecmB = boxMaterial.Ecm, fctmB = boxMaterial.Fctm, k2 = (6.5e-4 + 7e-4) / (2 * 7e-4), rhoB = 2 * Math.PI * 20 * 20 / 4 / (200 * 50), sigmaB = esB * 7e-4;
            double strainB = Math.Max((sigmaB - .4 * fctmB / rhoB * (1 + esB / ecmB * rhoB)) / esB, .6 * sigmaB / esB);
            double nearB = (3.4 * 40 + .8 * k2 * .425 * 20 / rhoB) / 1.7;
            double expectedB = ntc ? 1.7 * Math.Max(nearB, .75 * 600) * strainB : 1.3 * 600 * strainB;
            Near(c4.Width, expectedB, $"C4 {norm} wk della fascia inferiore");
            Near(c4.EffectiveArea, 200 * 50, $"C4 {norm} Ac,eff della fascia");
            Check(c4.Status.StartsWith("Parete interna −y"), $"6 ({engine}): C4 {norm} superficie governante «{c4.Status}»");
            Near(c4.Details.Last(d => d.Symbol == "Parete interna −y · k₂ della fascia").Value, k2, $"C4 {norm} k₂ della fascia");
            Near(c4.Details.Last(d => d.Symbol == "Parete interna −y · h − x della fascia").Value, 600, $"C4 {norm} h − x della fascia");
        }
        return values;
    }

    /// <summary>Fessurazione di una sezione su un piano assegnato ε(x, y) = a + b x + c y (analisi lineare, φ = 0), combinazione quasi permanente XC3.</summary>
    static Ntc2018Checks.CrackResult ExampleCrack(JsonObject input, string norm, double a, double b, double c, ServiceabilityEngine engine, JsonObject crackOptions)
    {
        var data = SezioneCA.DefaultData(); data["input"] = input; var settings = SectionWorkspace.Prepare(data); settings["normativa"] = norm; settings["coefficienti"] = ConcreteStandards.Defaults(norm);
        var sectionInput = data["input"]!.AsObject();
        foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) sectionInput[k] = settings["coefficienti"]![v]!.DeepClone();
        var options = (JsonObject)settings["sle"]!["SLE_QP"]!.DeepClone();
        options["modello"] = "Lineare"; options["trazione_cls"] = "No"; options["phi"] = "0"; options["esposizione"] = "XC3"; options["sensibilita"] = "Poco sensibile";
        options["durata"] = "Lunga"; options["aderenza"] = "Migliorata"; options["copriferro_fessure"] = ""; options["spaziatura_fessure"] = ""; options["limite_fessure"] = "";
        foreach (var (k, v) in crackOptions) options[k] = v!.DeepClone();
        var section = new CheckerSection(sectionInput, settings, options, "SLU", engine);
        var plane = new StrainPlane(b, c, new Point2d(0, 0), a, 1, "esempio");
        if (Math.Abs(plane.GetStrain(new Point2d(0, 0)) - a) > 1e-15 || Math.Abs(plane.GetStrain(new Point2d(0, 100)) - (a + 100 * c)) > 1e-15)
            plane = new StrainPlane(-b, -c, new Point2d(0, 0), a, 1, "esempio");
        if (Math.Abs(plane.GetStrain(new Point2d(100, 100)) - (a + 100 * b + 100 * c)) > 1e-15) throw new InvalidOperationException("Convenzione del piano di deformazione non riconosciuta.");
        var native = new StressAnalysisResult(section.Section, new ResultBeamForces(0, 0, 0, 0, 0, 0, section.Local), plane, section.Checker.SectionSolver,
            ConcreteStandards.Effective(sectionInput, settings), true, 0, 0, 1, null);
        var bars = native.GetRebarsTension(0, 0).Select(t => t.tension).ToArray();
        var state = new CheckerStressState(0, bars.Max(Math.Abs), bars, [], null, "", native);
        return ConcreteServiceabilityAdapter.Cracking(section, state, new ActionPoint(0, 0, 0), sectionInput, settings, options, "SLE_QP", engine);
    }

    /// <summary>
    /// ca.sle-tensioni §10: sezione 300 × 500, C30/37, 3Ø20 tese e 2Ø16 compresse, M = 80 kNm, analisi lineare NTC: η caratteristica 0,60898,
    /// quasi permanente 0,81197, getto sottile 0,76122 (calcolo a mano a sezione fessurata; solutore entro 2·10⁻⁵); limiti 18, 13,5 e 14,4 MPa e
    /// 360 MPa esatti; tasso uguale a |σc,min|/limite dello stato del solutore entro 10⁻⁹.
    /// </summary>
    int StressExample(ServiceabilityEngine engine)
    {
        int values = 0;
        foreach (var (thin, set, ratio, limit) in new[] { (false, "SLE", .60898, 18.0), (false, "SLE_QP", .81197, 13.5), (true, "SLE", .76122, 14.4) })
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
            input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500"; input["fck_mpa"] = "30"; input["classe_cls"] = "C30/37"; input["fyk_mpa"] = "450";
            input["steel_modulus_mpa"] = "200000"; input["cover_mm"] = "30"; input["gettato_sottile"] = thin ? "Sì" : "No";
            input["barre_manuali"] = new JsonArray(new[] { (-100d, -200d, 20d), (0d, -200d, 20d), (100d, -200d, 20d), (-100d, 202d, 16d), (100d, 202d, 16d) }
                .Select(b => (JsonNode)J.Obj(("x", b.Item1), ("y", b.Item2), ("phi", b.Item3))).ToArray());
            settings["normativa"] = "NTC 2018"; settings["coefficienti"] = ConcreteStandards.Defaults("NTC 2018");
            foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
            var options = (JsonObject)settings["sle"]![set]!.DeepClone(); options["modello"] = "Lineare"; options["trazione_cls"] = "No"; options["phi"] = "0";
            var section = new CheckerSection(input, settings, options, "SLU", engine);
            var state = section.Stress(new ActionPoint(0, 80, 0), set);
            if (state.tensioni_barre[0] < 0) state = section.Stress(new ActionPoint(0, -80, 0), set);
            Check(state.Engine == engine && state.tensioni_barre[0] > 0, $"6 ({engine}): ca.sle-tensioni §10, barre inferiori tese");
            Check(state.Ratio is double r && Math.Abs(r - ratio) <= 5e-5 * ratio, $"6 ({engine}): ca.sle-tensioni §10 {set} {(thin ? "getto sottile" : "")}: η {state.Ratio} invece di {ratio}");
            Check(state.ConcreteStressLimit is double l && Math.Abs(l - limit) <= 1e-9 * limit && Math.Abs(state.SteelStressLimit - 360) <= 1e-9 * 360,
                $"6 ({engine}): ca.sle-tensioni §10 limiti {state.ConcreteStressLimit}, {state.SteelStressLimit}");
            double governing = set == "SLE" ? Math.Max(-state.sigma_cls / limit, state.sigma_acciaio / 360) : -state.sigma_cls / limit;
            Check(Math.Abs(state.Ratio!.Value - governing) <= 1e-9 * governing, $"6 ({engine}): ca.sle-tensioni §10 η {state.Ratio} invece di {governing} dallo stato del solutore");
            values += 4;
        }
        return values;
    }
}
