using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Discrete reinforcement search over the existing section services. Never mutates its source.
/// No domain mesh is generated. Candidates retain the full trace of every applicable combination.</summary>
public static partial class ConcreteReinforcementDesign
{
    private sealed record Layout(int Top, int Bottom, int Side, double TopPhi, double BottomPhi, double SidePhi,
        bool Stirrups, double StirrupPhi, double Spacing);
    public static ConcreteDesignResult Optimize(JsonObject source, ConcreteDesignOptions options,
        CancellationToken token = default, IProgress<ConcreteDesignProgress>? progress = null)
    {
        options.Validate(); token.ThrowIfCancellationRequested();
        var data = (JsonObject)source.DeepClone(); var workspace = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        ValidateScope(data, workspace);
        ConcreteCalculationSettings.ValidateStirrups(input, workspace["taglio"]!.AsObject());
        var rows = SectionWorkspace.Sets.ToDictionary(k => k, k => data["combinazioni"]!.Array(k).OfType<JsonObject>()
            .Select(r => J.Obj(("nome", r.S("nome")), ("N", r["azioni"]?[0]), ("Mx", r["azioni"]?[1]), ("My", r["azioni"]?[2]))).ToArray());
        foreach (var row in rows.Values.SelectMany(r => r)) _ = ConcreteAnalysisSession.ReadAction(row);
        var shearRows = workspace["taglio"]!.Array("azioni").OfType<JsonObject>().ToArray();
        // Shear/torsion rows also carry N/M: the design must not ignore those simultaneous actions.
        rows["SLU"] = rows["SLU"].Concat(shearRows.Select(r => J.Obj(("nome", "Taglio · " + r.S("nome")),
            ("N", r.S("N", "0")), ("Mx", r.S("Mx", "0")), ("My", r.S("My", "0")), ("__locali", true)))).ToArray();
        foreach (var row in rows["SLU"]) _ = ConcreteAnalysisSession.ReadAction(row);
        bool torsion = shearRows.Any(r => SectionWorkspace.Number(r.S("T", "0"), "T") != 0);
        if (torsion && !options.TorsionLayoutConfirmed) throw new ArgumentException("Torsione: confermare nella ricerca lo schema periferico con staffe chiuse e barre di spigolo.");
        var accepted = new List<ConcreteDesignCandidate>(); var exclusions = new Dictionary<string, int>();
        var exclusionExamples = new Dictionary<string, ConcreteDesignCheck>(); var exampleIds = new Dictionary<string, int>();
        var prepared = new ConcurrentBag<PreparedDesign>(); var gate = new object(); var timers = new DesignTimers();
        int evaluated = 0, preliminaryChecked = 0, nativeCalculations = 0, reuses = 0, probeSections = 0;
        var watch = Stopwatch.StartNew(); long lastReport = -500; double? firstSolution = null;
        ConcreteDesignCandidate? best = null;
        string phase = "Controlli preliminari: dettagli, ancoraggi, taglio";
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.Workers, CancellationToken = token };
        var plans = Layouts(input, options).OrderBy(p => ApproximateScore(p, input, options)).ToArray();
        // A stable cheap order prioritises the selected objective; final ranking uses exact section metrics.
        ConcreteDesignMetrics? baseline = null;
        try { baseline = Metrics(new SezioneCA(input), workspace["taglio"]!.AsObject(), workspace["dettagli_costruttivi"]!.AsObject(), options); } catch (ArgumentException) { }
        void Complete(int id, ConcreteDesignCandidate? candidate = null, string? reason = null, ConcreteDesignCheck? example = null)
        {
            lock (gate)
            {
                evaluated++;
                if (candidate is not null)
                {
                    accepted.Add(candidate); firstSolution ??= watch.Elapsed.TotalSeconds;
                    if (best is null || candidate.Score < best.Score || candidate.Score == best.Score && candidate.Id < best.Id) best = candidate;
                }
                if (reason is not null)
                {
                    exclusions[reason] = exclusions.GetValueOrDefault(reason) + 1;
                    if (example is not null && (!exampleIds.TryGetValue(reason, out int previous) || id < previous))
                    { exampleIds[reason] = id; exclusionExamples[reason] = example; }
                }
                Report();
            }
        }
        void Report(bool force = false)
        {
            if (progress is null || !force && watch.ElapsedMilliseconds - lastReport < 300) return;
            progress.Report(new(evaluated, plans.Length, accepted.Count, best)
                { Phase = phase, Workers = options.Workers, Seconds = watch.Elapsed.TotalSeconds, PreliminaryChecked = preliminaryChecked });
            lastReport = watch.ElapsedMilliseconds;
        }
        void PrepareCandidate(int index)
        {
            token.ThrowIfCancellationRequested();
            var plan = plans[index];
            int id = index + 1; // Stable identity regardless of worker scheduling and search order.
            try
            {
                var ci = CandidateInput(input, plan); var cw = (JsonObject)workspace.DeepClone();
                var so = cw["taglio"]!.AsObject(); var details = cw["dettagli_costruttivi"]!.AsObject();
                details["barre_trattenute"] = "Da confermare"; details["ancoraggio_appoggi"] = "Da confermare";
                so["modello"] = plan.Stirrups ? "Con staffe" : "Senza staffe";
                // Derived Asl/d must follow each candidate; explicit circular parameters remain explicit.
                if (ci.S("shape") != "Circolare") so["parametri"] = "Automatici da sezione";
                var section = timers.Measure(0, () => ConcreteCalculationSettings.UpdateAutomaticShear(ci, so));
                so["ancoraggio"] = options.AnchorageConfirmed ? "Confermato" : "Da verificare";
                so["chiusura_torsione"] = options.TorsionLayoutConfirmed ? "Confermato" : "Da confermare";
                if (torsion) so["as_torsione"] = section.AreaSteel * options.TorsionReservePercent / 100;
                var dc = timers.Measure(1, () => ConcreteDetailingAnalysis.Calculate(ci, cw, rows["SLU"].Concat(rows["SLV"])));
                if (dc.DurabilityError is not null) throw new ArgumentException(dc.DurabilityError);
                var checks = new List<ConcreteDesignCheck>();
                if (details.S("elemento") is "Soletta piena" or "Parete")
                    SelectSecondary(section, so, details, dc.Checks, options);
                if (details.S("elemento") is "Soletta piena" or "Parete") dc = timers.Measure(1, () => ConcreteDetailingAnalysis.Calculate(ci, cw, rows["SLU"].Concat(rows["SLV"])));
                foreach (var check in dc.Checks)
                    checks.Add(new("dettagli", check.Name, "Sezione proposta", null, 1, check.Passed,
                        $"{check.Actual:0.###} / {check.Limit:0.###} {check.Unit}. {check.Explanation} {check.Reference}"));
                // Any numeric failure excludes the candidate, while non-section executable details stay pending.
                var failed = checks.FirstOrDefault(c => c.Passed == false);
                if (failed is not null) { Complete(id, reason: failed.Name, example: failed); return; }
                var metrics = Metrics(section, so, details, options);
                if (metrics.Diameters > options.MaxDiameterKinds) { Complete(id, reason: "Troppi diametri distinti"); return; }
                // These checks are cheaper than native resistance/stress solvers and independent of their results.
                timers.Measure(2, () => AnchorageChecks(ci, cw, options, checks));
                failed = checks.FirstOrDefault(c => c.Passed == false);
                if (failed is not null) { Complete(id, reason: failed.Name, example: failed); return; }
                foreach (var row in shearRows)
                {
                    token.ThrowIfCancellationRequested();
                    var r = timers.Measure(3, () => ConcreteShearAnalysis.Calculate(ci, cw, so, row));
                    AddRatio(checks, "vx", row.S("nome"), r.Shear[0].Ratio, options, $"VRd,x = {r.Shear[0].VRd:0.###} kN; cotθ = {r.Shear[0].CotTheta:0.###}");
                    AddRatio(checks, "vy", row.S("nome"), r.Shear[1].Ratio, options, $"VRd,y = {r.Shear[1].VRd:0.###} kN; cotθ = {r.Shear[1].CotTheta:0.###}");
                    if (r.Torsion is { } t)
                    {
                        AddRatio(checks, "torsione", row.S("nome"), t.TorsionRatio, options, $"TRd = {t.TRd:0.###} kNm; As,T richiesta = {t.RequiredLongitudinalArea:0.###} mm²; riserva {options.TorsionReservePercent:0.#}% per barra.");
                        AddRatio(checks, "vt_cls", row.S("nome"), t.ConcreteCombinedRatio, options, t.Status);
                        AddRatio(checks, "vt_staffe", row.S("nome"), t.SteelCombinedRatio, options, t.Status);
                        CheckTorsionDistribution(section, t, options, checks, row.S("nome"));
                    }
                    failed = checks.FirstOrDefault(c => c.Passed == false);
                    if (failed is not null) { Complete(id, reason: failed.Name, example: failed); return; }
                }
                // Keep spacing-specific checks; compute longitudinal response once per identical section.
                var keyInput = (JsonObject)ci.DeepClone(); keyInput.Remove("transverse_spacing_mm");
                // JsonNode retains its parent: do not retain an entire workspace for every surviving proposal.
                cw.Remove("taglio"); cw.Remove("dettagli_costruttivi");
                prepared.Add(new(id, ci, so, details, metrics, checks, Score(metrics, options), keyInput.ToJsonString()));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { Complete(id, reason: ex.Message); }
            finally { lock (gate) { preliminaryChecked++; Report(); } }
        }
        void CalculateGroups(IEnumerable<PreparedDesign[]> groups, string name, bool probe = false)
        {
            phase = name;
            lock (gate) Report(true);
            Parallel.ForEach(groups, parallel, group =>
            {
                token.ThrowIfCancellationRequested();
                var representative = group[0];
                try
                {
                    var cw = (JsonObject)workspace.DeepClone();
                    cw["taglio"] = representative.Shear.DeepClone(); cw["dettagli_costruttivi"] = representative.Detailing.DeepClone();
                    // A solver belongs to exactly one group/worker. The native mesh-construction gate remains in CheckerSection.
                    Interlocked.Increment(ref nativeCalculations);
                    if (probe) Interlocked.Increment(ref probeSections);
                    var longitudinal = timers.Measure(4, () => LongitudinalChecks(representative.Input, cw, rows, torsion, options, token));
                    Interlocked.Add(ref reuses, group.Length - 1);
                    var failed = longitudinal.FirstOrDefault(c => c.Passed == false);
                    foreach (var c in group)
                    {
                        if (failed is not null) Complete(c.Id, reason: failed.Name, example: failed);
                        else Complete(c.Id, new(c.Id, c.Input, c.Shear, c.Detailing, Describe(c.Input, c.Detailing), c.Metrics,
                            c.Checks.Concat(longitudinal).ToArray(), c.Score));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { foreach (var c in group) Complete(c.Id, reason: ex.Message); }
            });
        }
        try
        {
            Report(true);
            Parallel.ForEach(SearchIndices(plans.Length, options.MaxEvaluations), parallel, PrepareCandidate);
            var groups = prepared.GroupBy(c => c.Key).Select(g => g.OrderBy(c => c.Score).ThenBy(c => c.Id).ToArray())
                .OrderBy(g => g[0].Score).ThenBy(g => g[0].Id).ToArray();
            var probes = ProbeGroups(groups);
            CalculateGroups(probes, "Prove iniziali: estremi e valori intermedi", true);
            var done = probes.Select(g => g[0].Key).ToHashSet();
            if (best is not null)
            {
                // Try lighter/cheaper neighbours of the best successful probe. No interval is excluded on this evidence.
                var reference = best;
                var neighbours = groups.Where(g => !done.Contains(g[0].Key) && g[0].Score <= reference.Score)
                    .OrderBy(g => Math.Abs(g[0].Metrics.SteelArea / reference.Metrics.SteelArea - 1) +
                        Math.Abs((double)g[0].Metrics.Bars / reference.Metrics.Bars - 1)).ThenBy(g => g[0].Score).Take(16).ToArray();
                CalculateGroups(neighbours, "Affinamento attorno alle prime soluzioni");
                done.UnionWith(neighbours.Select(g => g[0].Key));
            }
            CalculateGroups(groups.Where(g => !done.Contains(g[0].Key)), "Completamento: resistenza SLU/SLV e SLE");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        var ordered = Rank(accepted, options);
        bool completed = evaluated == plans.Length && !token.IsCancellationRequested;
        Report(true);
        return new(ordered, evaluated, plans.Length, completed, exclusions, baseline,
        [ "Migliore tra le configurazioni calcolate; ricerca discreta, nessuna garanzia di minimo fuori dagli elenchi assegnati.",
          "Geometria, materiali, copriferro e sollecitazioni mantenuti. Barre su una fila per faccia / un anello; barre laterali con il diametro inferiore. Le armature manuali e i secondi strati vengono sostituiti solo con Applica.",
          "Peso per metro: acciaio 7850 kg/m³, staffe con sviluppo geometrico e maggiorazione ganci impostata; escluse sovrapposizioni, ancoraggi longitudinali, sfridi e armature locali.",
          "Numero barre: pezzi equivalenti per metro, con una barra longitudinale continua contata come un pezzo, più staffe, bracci aggiunti e barre ortogonali. Compromesso: p_peso·kg/m + p_barre·pezzi/m + p_diametri·numero diametri. I pesi sono coefficienti assegnati dall’utente, senza normalizzazione automatica.",
          "Dettagli NTC capitolo 4 / EC2: i controlli numerici sono obbligatori. Le verifiche esecutive non deducibili dalla sezione restano da completare.",
          "Solette / pareti: rete ortogonale uguale sulle due facce; per ogni disposizione principale si conserva la rete ammissibile migliore per l’obiettivo. La verifica della direzione ortogonale sotto proprie azioni richiede un’altra sezione.",
          torsion ? "Torsione: quota uniforme di area riservata su OGNI barra; distribuzione controllata per lunghezza tributaria periferica. Resistenza N–Mx–My calcolata solo con l’area residua, alle stesse coordinate. SLE usa l’area totale." : "Nessuna torsione nelle azioni assegnate.",
          "Tutte le combinazioni compilate sono considerate, anche se nascoste nei grafici. Non si aggiungono azioni, effetti del secondo ordine o verifiche sismiche non presenti nel foglio.",
          "Ricerca completa: tutte le disposizioni ammesse. Con limite tentativi, il campione copre l’intero elenco ordinato per obiettivo, comprese le disposizioni più armate; nessuna conclusione sulle configurazioni non calcolate.",
          "Ordine: dettagli, ancoraggi, taglio/torsione, poi resistenza e SLE. Dopo il primo controllo negativo la configurazione è esclusa. Le prove su estremi e valori intermedi delle sezioni sopravvissute orientano solo l’ordine, senza eliminare intervalli. Risultati longitudinali riusati per i passi staffa compatibili." ])
          { ExclusionExamples = exclusionExamples,
            Performance = new(watch.Elapsed.TotalSeconds, firstSolution, options.Workers, nativeCalculations, reuses, probeSections, timers.Results()) };
    }

    /// <summary>In a limited run, sample across the entire sorted grid instead of exhausting the lightest prefix.</summary>
    private static IEnumerable<int> SearchIndices(int planned, int budget)
    {
        int count = Math.Min(planned, budget);
        if (count == 1) { yield return planned - 1; yield break; }
        for (int i = 0; i < count; i++) yield return (int)((long)i * (planned - 1) / (count - 1));
    }

    private static void ValidateScope(JsonObject data, JsonObject workspace)
    {
        if (workspace.S("normativa") != "NTC 2018") throw new ArgumentException("Calcola armature: dettagli costruttivi disponibili per NTC 2018 con integrazioni EC2.");
        if (workspace.Array("trefoli").Count > 0) throw new ArgumentException("Calcola armature: attualmente disponibile per armatura ordinaria senza trefoli.");
        if (data["input"].S("shape") is not ("Rettangolare" or "Circolare" or "A T")) throw new ArgumentException("Ricerca disponibile per rettangoli, sezioni a T e circolari, anche con foro centrale.");
        if (workspace["dettagli_costruttivi"].S("elemento") == "Da scegliere") throw new ArgumentException("Scegliere trave, pilastro, soletta o parete nei dettagli costruttivi.");
        if (workspace["dettagli_costruttivi"].S("zona_sovrapposizione") == "Sì") throw new ArgumentException("Ricerca sulla sezione fuori dalle giunzioni: le barre sovrapposte richiedono una disposizione locale esplicita.");
        if (workspace["sle_comuni"].S("esposizione", "Da scegliere") == "Da scegliere") throw new ArgumentException("Scegliere l’esposizione SLE per il copriferro di durabilità.");
        if (!SectionWorkspace.Sets.Any(k => data["combinazioni"]!.Array(k).Count > 0) && workspace["taglio"]!.Array("azioni").Count == 0)
            throw new ArgumentException("Inserire almeno una combinazione di sollecitazioni nel foglio.");
    }

    private static IEnumerable<Layout> Layouts(JsonObject input, ConcreteDesignOptions o)
    {
        var stirrups = o.Stirrups switch
        {
            "Mantieni" => new[] { (input.S("staffe_presenti", "Sì") == "Sì", input.D("transverse_bar_diameter_mm"), input.D("transverse_spacing_mm")) },
            "Senza staffe" => new[] { (false, 0d, 0d) },
            _ => o.StirrupDiameters.Distinct().Order().SelectMany(d => o.StirrupSpacings.Distinct().Order().Select(s => (true, d, s)))
                .Concat(o.Stirrups == "Con e senza staffe" ? [(false, 0d, 0d)] : []).ToArray()
        };
        var list = new List<Layout>();
        void Add(Layout l) { list.Add(l); if (list.Count > 100000) throw new ArgumentException("Più di 100.000 configurazioni: ridurre gli elenchi o usare la disposizione simmetrica."); }
        foreach (var (present, phi, spacing) in stirrups)
        foreach (double topPhi in o.Diameters.Distinct().Order())
        {
            if (input.S("shape") == "Circolare")
            { foreach (int n in o.CircularCounts.Distinct().Order()) Add(new(n, 0, 0, topPhi, topPhi, topPhi, present, phi, spacing)); continue; }
            foreach (int top in o.TopCounts.Distinct().Order())
            foreach (int bottom in o.BottomCounts.Distinct().Order())
            foreach (double bottomPhi in o.Diameters.Distinct().Order())
            foreach (int side in o.SideCounts.Distinct().Order())
            {
                if (o.Symmetric && (top != bottom || topPhi != bottomPhi)) continue;
                Add(new(top, bottom, side, topPhi, bottomPhi, bottomPhi, present, phi, spacing));
            }
        }
        if (list.Count == 0) throw new ArgumentException("Nessuna disposizione: controllare gli elenchi, soprattutto i numeri comuni con simmetria attiva.");
        return list;
    }
    private static JsonObject CandidateInput(JsonObject source, Layout p)
    {
        var i = (JsonObject)source.DeepClone(); i.Remove("barre_manuali");
        foreach (string k in new[] { "top", "bottom", "inner" }) i["second_" + k + "_enabled"] = false;
        i["flange_bottom_count"] = "0";
        i["top_bar_count"] = p.Top; i["bottom_bar_count"] = p.Bottom; i["side_bar_count_per_side"] = p.Side;
        i["top_bar_diameter_mm"] = p.TopPhi; i["bottom_bar_diameter_mm"] = p.BottomPhi; i["side_bar_diameter_mm"] = p.SidePhi;
        i["longitudinal_bar_count"] = p.Top; i["longitudinal_bar_diameter_mm"] = p.TopPhi;
        i["staffe_presenti"] = p.Stirrups ? "Sì" : "No";
        i["transverse_bar_diameter_mm"] = p.Stirrups ? p.StirrupPhi : source.D("transverse_bar_diameter_mm", 8);
        i["transverse_spacing_mm"] = p.Stirrups ? p.Spacing : source.D("transverse_spacing_mm", 200);
        return i;
    }
    private static double ApproximateScore(Layout p, JsonObject input, ConcreteDesignOptions o)
    {
        double area = Math.PI / 4 * (p.Top * p.TopPhi * p.TopPhi + p.Bottom * p.BottomPhi * p.BottomPhi + 2 * p.Side * p.SidePhi * p.SidePhi);
        double perimeter = input.S("shape") == "Circolare" ? Math.PI * input.D("diameter_mm") : 2 * (input.D("height_mm") + input.D("width_mm"));
        var d = new[] { p.TopPhi, p.BottomPhi, p.SidePhi }.Concat(p.Stirrups ? [p.StirrupPhi] : []).Distinct().Count();
        return Score(new(area, area * .00785, p.Stirrups ? Math.PI * p.StirrupPhi * p.StirrupPhi / 4 * perimeter / p.Spacing * .00785 : 0, p.Top + p.Bottom + 2 * p.Side, d, p.Top + p.Bottom + 2 * p.Side + (p.Stirrups ? 1000 / p.Spacing : 0)), o);
    }
    public static double Score(ConcreteDesignMetrics m, ConcreteDesignOptions o) => o.Objective switch
    {
        "Peso" => m.KgPerM,
        "Numero barre" => m.PiecesPerM,
        "Numero diametri" => m.Diameters,
        _ => o.WeightCost * m.KgPerM + o.BarCost * m.PiecesPerM + o.DiameterCost * m.Diameters
    };
    public static ConcreteDesignCandidate[] Rank(IEnumerable<ConcreteDesignCandidate> source, ConcreteDesignOptions o)
    {
        var rows = source.Select(c => c with { Score = Score(c.Metrics, o) }).OrderBy(c => c.Score).ThenBy(c => c.Metrics.KgPerM)
            .ThenBy(c => c.Metrics.PiecesPerM).ThenBy(c => c.Metrics.Diameters).ThenBy(c => c.Id).ToArray();
        // Weight-ordered sweep; dominated points never need to remain in the frontier.
        var frontier = new List<ConcreteDesignMetrics>(); var pareto = new HashSet<int>();
        foreach (var c in rows.OrderBy(c => c.Metrics.KgPerM).ThenBy(c => c.Metrics.PiecesPerM).ThenBy(c => c.Metrics.Diameters))
        {
            var m = c.Metrics;
            if (frontier.Any(b => b.KgPerM <= m.KgPerM && b.PiecesPerM <= m.PiecesPerM && b.Diameters <= m.Diameters &&
                (b.KgPerM < m.KgPerM || b.PiecesPerM < m.PiecesPerM || b.Diameters < m.Diameters))) continue;
            pareto.Add(c.Id);
            if (!frontier.Any(b => b.KgPerM == m.KgPerM && b.PiecesPerM == m.PiecesPerM && b.Diameters == m.Diameters)) frontier.Add(m);
        }
        return rows.Select(c => c with { Pareto = pareto.Contains(c.Id) }).ToArray();
    }
    private static string Describe(JsonObject i, JsonObject d) => (i.S("shape") == "Circolare" ? $"{i.S("longitudinal_bar_count")}Ø{i.S("longitudinal_bar_diameter_mm")}" :
        $"Sup. {i.S("top_bar_count")}Ø{i.S("top_bar_diameter_mm")} · inf. {i.S("bottom_bar_count")}Ø{i.S("bottom_bar_diameter_mm")} · laterali {i.S("side_bar_count_per_side")}Ø{i.S("side_bar_diameter_mm")}/lato") +
        (i.S("staffe_presenti") == "Sì" ? $" · staffe Ø{i.S("transverse_bar_diameter_mm")}/{i.S("transverse_spacing_mm")}" : " · senza staffe") +
        (d.S("elemento") is "Soletta piena" or "Parete" ? $" · ortogonali Ø{d.S("diametro_secondaria")}/{d.S("passo_secondaria")} per faccia" : "");
}
