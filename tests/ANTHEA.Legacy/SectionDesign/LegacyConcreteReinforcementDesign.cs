// Spostato da X.Calculations/ConcreteReinforcementDesign.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: ricerca precedente
// Optimize(…, legacy: true) con Layouts e ApproximateScore, rami legacy di Score e Rank (H1) e ramo mai raggiungibile di SearchIndices
// (H2). Codice storico conservato per le prove, fuori dall'installer.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Parti storiche di <see cref="ConcreteReinforcementDesign"/> (classe partial: nome nuovo, corpi invariati). Le chiamate che nel
/// prodotto andavano ai rami nativi (AddRatio, SelectSecondary, CheckTorsionDistribution, SearchIndices, Metrics, Score, Rank senza
/// <c>legacy</c>) restano qualificate con <see cref="ConcreteReinforcementDesign"/>: la ricerca precedente usa le stesse verifiche native.</summary>
public static partial class LegacyConcreteReinforcementDesign
{
    /// <summary>Ricerca precedente al porting in GPC.Design (prima ConcreteReinforcementDesign.Optimize con <c>legacy: true</c>).
    /// Autonoma: nessun percorso del prodotto la chiama, le prove la chiamano direttamente.</summary>
    public static ConcreteDesignResult Optimize(JsonObject source, ConcreteDesignOptions options,
        CancellationToken token = default, IProgress<ConcreteDesignProgress>? progress = null)
    {
        var context=ConcreteReinforcementDesign.PrepareDesignInput(source,options,token);
        var data=context.Data;var workspace=context.Workspace;var input=context.Input;
        var rows=context.Rows;var shearRows=context.ShearRows;bool torsion=context.Torsion;
        var accepted = new List<ConcreteDesignCandidate>(); var exclusions = new Dictionary<string, int>();
        var exclusionExamples = new Dictionary<string, ConcreteDesignCheck>(); var exampleIds = new Dictionary<string, int>();
        var prepared = new ConcurrentBag<PreparedDesign>(); var gate = new object(); var timers = new ConcreteReinforcementDesign.DesignTimers();
        int evaluated = 0, preliminaryChecked = 0, nativeCalculations = 0, reuses = 0, probeSections = 0;
        var watch = Stopwatch.StartNew(); long lastReport = -500; double? firstSolution = null;
        ConcreteDesignCandidate? best = null;
        string phase = "Controlli preliminari: dettagli, ancoraggi, taglio";
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = options.Workers, CancellationToken = token };
        var plans = Layouts(input, options).OrderBy(p => ApproximateScore(p, input, options)).ToArray();
        // A stable cheap order prioritises the selected objective; final ranking uses exact section metrics.
        ConcreteDesignMetrics? baseline = null;
        try { baseline = ConcreteReinforcementDesign.Metrics(new SezioneCA(input), workspace["taglio"]!.AsObject(), workspace["dettagli_costruttivi"]!.AsObject(), options); } catch (ArgumentException) { }
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
                var ci = ConcreteReinforcementDesign.CandidateInput(input, plan); var cw = (JsonObject)workspace.DeepClone();
                var so = cw["taglio"]!.AsObject(); var details = cw["dettagli_costruttivi"]!.AsObject();
                details["barre_trattenute"] = "Da confermare"; details["ancoraggio_appoggi"] = "Da confermare";
                so["modello"] = plan.Stirrups ? "Con staffe" : "Senza staffe";
                // Derived Asl/d must follow each candidate; explicit circular parameters remain explicit.
                if (ci.S("shape") != "Circolare") so["parametri"] = "Automatici da sezione";
                var section = timers.Measure(0, () => ConcreteCalculationSettings.UpdateAutomaticShear(ci, so));
                so["ancoraggio"] = options.AnchorageConfirmed ? "Confermato" : "Da verificare";
                so["chiusura_torsione"] = options.TorsionLayoutConfirmed ? "Confermato" : "Da confermare";
                if (torsion) so["as_torsione"] = section.AreaSteel * options.TorsionReservePercent / 100;
                var dc = timers.Measure(1, () => ConcreteDetailingAnalysis.Calculate(ci, cw, ConcreteDetailingAnalysis.AllRows(rows)));
                if (dc.DurabilityError is not null) throw new ArgumentException(dc.DurabilityError);
                var checks = new List<ConcreteDesignCheck>();
                if (details.S("elemento") is "Soletta piena" or "Parete")
                    ConcreteReinforcementDesign.SelectSecondary(section, so, details, dc.Checks, options);
                if (details.S("elemento") is "Soletta piena" or "Parete") dc = timers.Measure(1, () => ConcreteDetailingAnalysis.Calculate(ci, cw, ConcreteDetailingAnalysis.AllRows(rows)));
                foreach (var check in dc.Checks)
                    checks.Add(new("dettagli", check.Name, "Sezione proposta", null, 1, check.Passed,
                        $"{check.Actual:0.###} / {check.Limit:0.###} {check.Unit}. {check.Explanation} {check.Reference}"));
                // Any numeric failure excludes the candidate, while non-section executable details stay pending.
                var failed = checks.FirstOrDefault(c => c.Passed == false);
                if (failed is not null) { Complete(id, reason: failed.Name, example: failed); return; }
                var metrics = ConcreteReinforcementDesign.Metrics(section, so, details, options);
                if (metrics.Diameters > options.MaxDiameterKinds) { Complete(id, reason: "Troppi diametri distinti"); return; }
                // These checks are cheaper than native resistance/stress solvers and independent of their results.
                timers.Measure(2, () => ConcreteReinforcementDesign.AnchorageChecks(ci, cw, options, checks));
                failed = checks.FirstOrDefault(c => c.Passed == false);
                if (failed is not null) { Complete(id, reason: failed.Name, example: failed); return; }
                foreach (var row in shearRows)
                {
                    token.ThrowIfCancellationRequested();
                    var r = timers.Measure(3, () => ConcreteShearAnalysis.Calculate(ci, cw, so, row));
                    ConcreteReinforcementDesign.AddRatio(checks, "vx", row.S("nome"), r.Shear[0].Ratio, options, $"VRd,x = {r.Shear[0].VRd:0.###} kN; cotθ = {r.Shear[0].CotTheta:0.###}");
                    ConcreteReinforcementDesign.AddRatio(checks, "vy", row.S("nome"), r.Shear[1].Ratio, options, $"VRd,y = {r.Shear[1].VRd:0.###} kN; cotθ = {r.Shear[1].CotTheta:0.###}");
                    if (r.Torsion is { } t)
                    {
                        ConcreteReinforcementDesign.AddRatio(checks, "torsione", row.S("nome"), t.TorsionRatio, options, $"TRd = {t.TRd:0.###} kNm; As,T richiesta = {t.RequiredLongitudinalArea:0.###} mm²; riserva {options.TorsionReservePercent:0.#}% per barra.");
                        ConcreteReinforcementDesign.AddRatio(checks, "vt_cls", row.S("nome"), t.ConcreteCombinedRatio, options, t.Status);
                        ConcreteReinforcementDesign.AddRatio(checks, "vt_staffe", row.S("nome"), t.SteelCombinedRatio, options, t.Status);
                        ConcreteReinforcementDesign.CheckTorsionDistribution(section, t, options, checks, row.S("nome"));
                    }
                    failed = checks.FirstOrDefault(c => c.Passed == false);
                    if (failed is not null) { Complete(id, reason: failed.Name, example: failed); return; }
                }
                // Keep spacing-specific checks; compute longitudinal response once per identical section.
                var keyInput = (JsonObject)ci.DeepClone(); keyInput.Remove("transverse_spacing_mm");
                // JsonNode retains its parent: do not retain an entire workspace for every surviving proposal.
                cw.Remove("taglio"); cw.Remove("dettagli_costruttivi");
                prepared.Add(new(id, ci, so, details, metrics, checks, ConcreteReinforcementDesign.Score(metrics, options), keyInput.ToJsonString()));
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
                    var longitudinal = timers.Measure(4, () => ConcreteReinforcementDesign.LongitudinalChecks(representative.Input, cw, rows, torsion, options, token));
                    Interlocked.Add(ref reuses, group.Length - 1);
                    var failed = longitudinal.FirstOrDefault(c => c.Passed == false);
                    foreach (var c in group)
                    {
                        if (failed is not null) Complete(c.Id, reason: failed.Name, example: failed);
                        else Complete(c.Id, new(c.Id, c.Input, c.Shear, c.Detailing, ConcreteReinforcementDesign.Describe(c.Input, c.Detailing), c.Metrics,
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
            Parallel.ForEach(ConcreteReinforcementDesign.SearchIndices(plans.Length, options.MaxEvaluations), parallel, PrepareCandidate);
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
        var ordered = ConcreteReinforcementDesign.Rank(accepted, options);
        bool completed = evaluated == plans.Length && !token.IsCancellationRequested;
        Report(true);
        return new(ordered, evaluated, plans.Length, completed, exclusions, baseline,
        ConcreteReinforcementDesign.DesignNotes(torsion))
          { ExclusionExamples = exclusionExamples,
            Performance = new(watch.Elapsed.TotalSeconds, firstSolution, options.Workers, nativeCalculations, reuses, probeSections, timers.Results()) };
    }

    /// <summary>Ramo legacy di ConcreteReinforcementDesign.SearchIndices: nessun chiamante passava <c>legacy: true</c>.
    /// In a limited run, sample across the entire sorted grid instead of exhausting the lightest prefix.</summary>
    internal static IEnumerable<int> SearchIndices(int planned, int budget)
    {
        int count = Math.Min(planned, budget);
        if (count == 1) { yield return planned - 1; yield break; }
        for (int i = 0; i < count; i++) yield return (int)((long)i * (planned - 1) / (count - 1));
    }

    /// <summary>Griglia delle disposizioni della ricerca precedente (la ricerca nativa usa SectionDesignCatalogue).</summary>
    private static IEnumerable<ConcreteReinforcementDesign.Layout> Layouts(JsonObject input, ConcreteDesignOptions o)
    {
        var stirrups = o.Stirrups switch
        {
            "Mantieni" => new[] { (input.S("staffe_presenti", "Sì") == "Sì", input.D("transverse_bar_diameter_mm"), input.D("transverse_spacing_mm")) },
            "Senza staffe" => new[] { (false, 0d, 0d) },
            _ => o.StirrupDiameters.Distinct().Order().SelectMany(d => o.StirrupSpacings.Distinct().Order().Select(s => (true, d, s)))
                .Concat(o.Stirrups == "Con e senza staffe" ? [(false, 0d, 0d)] : []).ToArray()
        };
        var list = new List<ConcreteReinforcementDesign.Layout>();
        void Add(ConcreteReinforcementDesign.Layout l) { list.Add(l); if (list.Count > 100000) throw new ArgumentException("Più di 100.000 configurazioni: ridurre gli elenchi o usare la disposizione simmetrica."); }
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

    /// <summary>Ordinamento economico preliminare della ricerca precedente.</summary>
    private static double ApproximateScore(ConcreteReinforcementDesign.Layout p, JsonObject input, ConcreteDesignOptions o)
    {
        double area = Math.PI / 4 * (p.Top * p.TopPhi * p.TopPhi + p.Bottom * p.BottomPhi * p.BottomPhi + 2 * p.Side * p.SidePhi * p.SidePhi);
        double perimeter = input.S("shape") == "Circolare" ? Math.PI * input.D("diameter_mm") : 2 * (input.D("height_mm") + input.D("width_mm"));
        var d = new[] { p.TopPhi, p.BottomPhi, p.SidePhi }.Concat(p.Stirrups ? [p.StirrupPhi] : []).Distinct().Count();
        return ConcreteReinforcementDesign.Score(new(area, area * .00785, p.Stirrups ? Math.PI * p.StirrupPhi * p.StirrupPhi / 4 * perimeter / p.Spacing * .00785 : 0, p.Top + p.Bottom + 2 * p.Side, d, p.Top + p.Bottom + 2 * p.Side + (p.Stirrups ? 1000 / p.Spacing : 0)), o);
    }

    /// <summary>Ramo legacy di ConcreteReinforcementDesign.Score (prima con <c>legacy: true</c>).</summary>
    public static double Score(ConcreteDesignMetrics m, ConcreteDesignOptions o)
    {
        return o.Objective switch
        {
            "Peso" => m.KgPerM,
            "Numero barre" => m.PiecesPerM,
            "Numero diametri" => m.Diameters,
            _ => o.WeightCost * m.KgPerM + o.BarCost * m.PiecesPerM + o.DiameterCost * m.Diameters
        };
    }

    /// <summary>Ramo legacy di ConcreteReinforcementDesign.Rank (prima con <c>legacy: true</c>). Come in origine il punteggio viene dal
    /// ramo nativo di Score.</summary>
    public static ConcreteDesignCandidate[] Rank(IEnumerable<ConcreteDesignCandidate> source, ConcreteDesignOptions o)
    {
        var rows = source.Select(c => c with { Score = ConcreteReinforcementDesign.Score(c.Metrics, o) }).OrderBy(c => c.Score).ThenBy(c => c.Metrics.KgPerM)
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
}
