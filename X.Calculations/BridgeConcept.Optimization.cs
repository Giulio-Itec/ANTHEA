using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public enum OptimizationObjective { Cost, Carbon, Balanced }
    public sealed record OptimizationOptions
    {
        public OptimizationObjective Objective { get; init; } = OptimizationObjective.Cost;
        public bool KeepFamily { get; init; }
        public bool KeepSpans { get; init; }
        public bool KeepDepth { get; init; }
        public bool KeepSection { get; init; }
        public bool KeepPier { get; init; }
        public bool KeepFoundation { get; init; }
        public bool KeepContinuity { get; init; } = true;
        public int MinSpans { get; init; } = 1;
        public int MaxSpans { get; init; } = 12;
        public double MaxDepth { get; init; } = 0;
        public double MinDepth { get; init; } = 0;
        public double DepthPercentMin { get; init; } = 100;
        public double DepthPercentMax { get; init; } = 115;
        public double DepthPercentStep { get; init; } = 15;
        public double PilePercentMin { get; init; } = 100;
        public double PilePercentMax { get; init; } = 150;
        public double PilePercentStep { get; init; } = 25;
        public int Alternatives { get; init; } = 10;
    }
    public sealed record OptimizationCandidate(JsonObject Data, Result Result, double Score, bool Pareto, string Changes);
    public sealed record OptimizationResult(Result Baseline, OptimizationCandidate[] Candidates, int Evaluated, int Admissible,
        IReadOnlyDictionary<string, int> Excluded, string[] BaselineExclusions, OptimizationOptions Options)
    {
        public OptimizationTrial[] Trials { get; init; } = [];
        public OptimizationSolution[] Solutions { get; init; } = [];
    }
    public sealed record OptimizationTrial(int Iteration, string Family, int Spans, double? Depth, double? PileLength,
        double? Cost, double? Carbon, string Pier, string Foundation, bool Continuous, string[] Exclusions)
    { public bool Admissible => Exclusions.Length == 0; }
    public sealed record OptimizationSolution(int Rank, OptimizationTrial Trial, JsonObject Data, double Score, bool Pareto);
    public sealed record OptimizationProgress(int Evaluated, int Planned, int Admissible, OptimizationTrial[] NewTrials,
        OptimizationCandidate? Best);
    public const string OptimizationScope = "Migliore soluzione tra le combinazioni esplorate dal modello ANTHEA. Filtri geometrici e soglie orientative; non è un progetto verificato né un ottimo strutturale globale.";

    /// <summary>Deterministic discrete search, no UI and no mutations of the caller's data.</summary>
    public static OptimizationResult Optimize(JsonObject source, OptimizationOptions options, CancellationToken cancellation = default,
        IProgress<OptimizationProgress>? progress = null)
    {
        if (!Enum.IsDefined(options.Objective) || options.MinSpans < 1 || options.MaxSpans > 30 || options.MaxSpans < options.MinSpans
            || !double.IsFinite(options.MaxDepth) || options.MaxDepth < 0 || options.MaxDepth > 15
            || !double.IsFinite(options.MinDepth) || options.MinDepth < 0 || options.MinDepth > 15
            || (options.MaxDepth > 0 && options.MinDepth > options.MaxDepth) || options.Alternatives is < 1 or > 50)
            throw new ArgumentException("Limiti di ricerca non validi: 1–30 campate, altezza massima 0–15 m, 1–50 alternative.");
        if (options.KeepSection && !options.KeepFamily)
            throw new ArgumentException("Per mantenere la sezione occorre mantenere anche la tipologia.");
        var depths = OptimizationRange(options.DepthPercentMin, options.DepthPercentMax, options.DepthPercentStep);
        var piles = OptimizationRange(options.PilePercentMin, options.PilePercentMax, options.PilePercentStep);
        cancellation.ThrowIfCancellationRequested();
        var baselineData = (JsonObject)source.DeepClone(); baselineData.Remove("alternative_a");
        var baseline = Calculate(baselineData); var input = baselineData["input"]!;
        var accepted = new List<(JsonObject Data, Result Result, OptimizationTrial Trial)>();
        var trials = new List<OptimizationTrial>();
        var excluded = new Dictionary<string, int>(); var seen = new HashSet<string>(); int evaluated = 0;
        var families = (options.KeepFamily ? Families.Where(f => f.Id == baseline.Family.Id) : Families).ToArray();
        int[] spans = options.KeepSpans ? [baseline.Spans.Length] : Enumerable.Range(options.MinSpans, options.MaxSpans - options.MinSpans + 1).ToArray();
        bool[] continuity = options.KeepContinuity ? [input.B("continuous")] : [true, false];
        string[] piers = options.KeepPier ? [input.S("pier")] : Piers;
        string[] foundations = options.KeepFoundation ? [baseline.Foundation] : Foundations.Skip(1).ToArray();
        int planned = 1 + families.Sum(f => spans.Count(n => baseline.Length / n >= f.MinSpan && baseline.Length / n <= f.MaxSpan))
            * continuity.Length * piers.Length * (options.KeepDepth ? 1 : depths.Length)
            * foundations.Sum(f => options.KeepFoundation || f == "Plinto diretto" ? 1 : piles.Length);
        if (planned > 50000) throw new ArgumentException($"Griglia troppo estesa ({planned:N0} tentativi): restringere gli intervalli o aumentare il passo. Limite: 50.000.");
        var watch = System.Diagnostics.Stopwatch.StartNew(); long lastReport = -200; int reported = 0;
        double Score(Result r, double minCost, double minCarbon) => options.Objective switch
        {
            OptimizationObjective.Cost => r.TotalCost,
            OptimizationObjective.Carbon => r.Carbon,
            _ => .5 * (r.TotalCost / Math.Max(1, minCost) + r.Carbon / Math.Max(1e-9, minCarbon))
        };
        void Report(bool final = false)
        {
            if (progress is null || (!final && watch.ElapsedMilliseconds - lastReport < 200)) return;
            OptimizationCandidate? best = null;
            if (accepted.Count > 0)
            {
                double mc = accepted.Min(c => c.Result.TotalCost), me = accepted.Min(c => c.Result.Carbon);
                var c = accepted.OrderBy(c => Score(c.Result, mc, me)).ThenBy(c => c.Result.TotalCost).ThenBy(c => c.Result.Carbon)
                    .ThenBy(c => c.Result.Family.Id, StringComparer.Ordinal).ThenBy(c => c.Result.Spans.Length).First();
                best = new(c.Data, c.Result, Score(c.Result, mc, me), false, DescribeChanges(input, c.Data["input"]!));
            }
            progress.Report(new(evaluated, planned, accepted.Count, trials.Skip(reported).ToArray(), best));
            reported = trials.Count; lastReport = watch.ElapsedMilliseconds;
        }
        void Add(JsonObject candidate)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!seen.Add(candidate["input"]!.ToJsonString())) return;
            evaluated++;
            Result? r = null; string[] reasons;
            try
            {
                r = Calculate(candidate); reasons = OptimizationExclusions(candidate, r, options);
            }
            catch (ArgumentException ex)
            {
                reasons = ["Dati incompatibili: " + ex.Message];
            }
            var ci = candidate["input"]!;
            var trial = new OptimizationTrial(evaluated, ci.S("family"), r?.Spans.Length ?? (int)ci.D("spans"), r?.Depth,
                r?.PileLength, r?.TotalCost, r?.Carbon, ci.S("pier"), r?.Foundation ?? ci.S("foundation"), ci.B("continuous"), reasons);
            trials.Add(trial);
            if (reasons.Length == 0 && r is not null) accepted.Add((candidate, r, trial));
            else foreach (var reason in reasons) excluded[reason] = excluded.GetValueOrDefault(reason) + 1;
            Report();
        }
        // Always include the current solution if it satisfies the requested limits.
        Add((JsonObject)baselineData.DeepClone());
        foreach (var family in families)
        foreach (int count in spans)
        {
            cancellation.ThrowIfCancellationRequested();
            // Necessary span-range condition; exact spans and obstacle clearance are checked after calculation.
            if (baseline.Length / count < family.MinSpan || baseline.Length / count > family.MaxSpan) continue;
            foreach (bool continuous in continuity)
            foreach (string pier in piers)
            foreach (string foundation in foundations)
            foreach (double depthFactor in options.KeepDepth ? new[] { 1d } : depths)
            foreach (double pileFactor in options.KeepFoundation || foundation == "Plinto diretto" ? new[] { 1d } : piles)
            {
                var d = (JsonObject)baselineData.DeepClone(); var i = d["input"]!;
                i["family"] = family.Id; i["spans"] = count; i["continuous"] = continuous;
                i["depth"] = options.KeepDepth ? baseline.Depth : 0;
                if (options.KeepSection)
                {
                    i["slab"] = baseline.Slab; i["spacing"] = baseline.Spacing; i["web"] = baseline.Web;
                    i["bottom"] = baseline.Bottom; i["boxes"] = baseline.Family.Id == "steel_box" ? baseline.Girders : input.D("boxes");
                }
                else
                {
                    // Explore the family's standard section, not arbitrary reductions of plate thickness.
                    foreach (var p in Section.Where(p => p.Key is not "depth" and not "fc")) i[p.Key] = p.Default;
                }
                i["pier"] = pier;
                if (!options.KeepPier) i["pier_size"] = 0;
                i["foundation"] = foundation;
                if (options.KeepFoundation)
                {
                    i["pile_length"] = baseline.PileLength;
                    // An automatic count/footing size remains an automatic design rule at each support.
                    // Existing manually imposed dimensions and counts are preserved exactly.
                }
                else
                {
                    i["pile_length"] = foundation == "Plinto diretto" ? 0 : Math.Min(80, new[] { 10d, 15, 22, 30 }[Array.IndexOf(Soils, i.S("soil"))] * pileFactor);
                    i["pile_count"] = 0; i["footing_size"] = 0;
                }
                if (!options.KeepDepth && depthFactor != 1)
                {
                    try { i["depth"] = Math.Ceiling(Calculate(d).Depth * depthFactor * 20) / 20; }
                    catch (ArgumentException) { /* Add records the invalid candidate and its reason. */ }
                }
                Add(d);
            }
        }
        cancellation.ThrowIfCancellationRequested(); Report(true); cancellation.ThrowIfCancellationRequested();
        var baseReasons = OptimizationExclusions(baselineData, baseline, options);
        if (accepted.Count == 0) return new(baseline, [], evaluated, 0, excluded, baseReasons, options) { Trials = trials.ToArray() };
        double minCost = accepted.Min(c => c.Result.TotalCost), minCarbon = accepted.Min(c => c.Result.Carbon);
        var ordered = accepted.OrderBy(c => Score(c.Result, minCost, minCarbon)).ThenBy(c => c.Result.TotalCost).ThenBy(c => c.Result.Carbon)
            .ThenBy(c => c.Result.Family.Id, StringComparer.Ordinal).ThenBy(c => c.Result.Spans.Length).ToArray();
        // Do not fill the shortlist twice with automatic and explicit representations of the same geometry.
        var distinct = ordered.DistinctBy(c => System.Text.Json.JsonSerializer.Serialize(new { c.Result.Family.Id, c.Result.Depth,
            c.Result.PierDepth, c.Result.Slab, c.Result.Spacing, c.Result.Girders, c.Result.Web, c.Result.Bottom, c.Result.Spans,
            c.Result.Supports, c.Result.Quantities, c.Result.Inertia, c.Result.PileDiameter, c.Result.PileLength,
            continuous = c.Data["input"].B("continuous") })).ToArray();
        var pareto = new HashSet<int>(); double previousCarbon = double.PositiveInfinity;
        foreach (var group in distinct.GroupBy(c => c.Result.TotalCost).OrderBy(g => g.Key))
        {
            double carbon = group.Min(c => c.Result.Carbon);
            if (carbon < previousCarbon)
                foreach (var c in group.Where(c => c.Result.Carbon == carbon)) pareto.Add(c.Trial.Iteration);
            previousCarbon = Math.Min(previousCarbon, carbon);
        }
        var candidates = distinct.Take(options.Alternatives).Select(c => new OptimizationCandidate(c.Data, c.Result, Score(c.Result, minCost, minCarbon),
            pareto.Contains(c.Trial.Iteration), DescribeChanges(input, c.Data["input"]!))).ToArray();
        return new(baseline, candidates, evaluated, accepted.Count, excluded, baseReasons, options) {
            Trials = trials.ToArray(), Solutions = distinct.Select((c, k) => new OptimizationSolution(k + 1, c.Trial, c.Data,
                Score(c.Result, minCost, minCarbon), pareto.Contains(c.Trial.Iteration))).ToArray()
        };
    }

    /// <summary>Inclusive percentage grid, with the upper endpoint included even when the step does not divide the interval.</summary>
    public static double[] OptimizationRange(double min, double max, double step)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(step) || min < 100 || max > 200 || min > max || step < 1 || step > 100)
            throw new ArgumentException("Intervalli percentuali: 100 ≤ minimo ≤ massimo ≤ 200; passo da 1 a 100 punti percentuali.");
        if (Math.Ceiling((max - min) / step) + 1 > 11)
            throw new ArgumentException("Massimo 11 valori per intervallo percentuale: aumentare il passo.");
        var values = new List<double>();
        for (int k = 0; min + k * step < max - 1e-9; k++) values.Add((min + k * step) / 100);
        values.Add(max / 100); return values.ToArray();
    }

    public static string[] OptimizationExclusions(JsonObject data, Result r, OptimizationOptions options)
    {
        var reasons = new List<string>(); var i = data["input"]!;
        if (r.Spans.Length < options.MinSpans || r.Spans.Length > options.MaxSpans) reasons.Add("Numero campate fuori dai limiti richiesti");
        if (r.Spans.Min() < r.Family.MinSpan - 1e-8 || r.Spans.Max() > r.Family.MaxSpan + 1e-8) reasons.Add("Luci fuori dal campo usuale della tipologia");
        if (r.Depth < options.MinDepth - 1e-8) reasons.Add("Altezza in campata inferiore al minimo richiesto");
        if (options.MaxDepth > 0 && r.PierDepth > options.MaxDepth + 1e-8) reasons.Add("Altezza massima superata (incluse le pile)");
        double requiredDepth = Math.Max(r.Family.MinDepth, r.Spans.Max() / r.Family.Ratio * (i.B("continuous") ? .95 : 1.1));
        if (r.Depth < requiredDepth - 1e-8) reasons.Add("Altezza inferiore alla regola di predimensionamento");
        if (i.S("obstacle") != "Nessuno" && i.D("obstacle_width") > 0)
        {
            double left = (r.Length - i.D("obstacle_width")) / 2 - 1, right = r.Length - left;
            if (r.Supports.Skip(1).SkipLast(1).Any(s => s.X > left + 1e-8 && s.X < right - 1e-8)) reasons.Add("Pile interferenti con ostacolo e margine di 1 m");
        }
        double DetailValue(string name) => r.Details.Single(d => d.Name == name).Value;
        if (DetailValue("Rapporto carico / riferimento fondazione") > 1 + 1e-8) reasons.Add("Fondazione oltre la soglia assiale indicativa");
        if (DetailValue("Snellezza massima delle pile") > 100 + 1e-8) reasons.Add("Snellezza pile superiore a 100");
        if (DetailValue("Compressione media pile / fc") > .3 + 1e-8) reasons.Add("Compressione media pile superiore a 0,30 fc");
        if (r.Supports.Any(s => s.Piles > 64)) reasons.Add("Più di 64 pali per appoggio");
        if (r.Supports.Any(s => s.Reaction < -1e-8)) reasons.Add("Reazione verso l’alto: dispositivi antisollevamento non dimensionati");
        if (r.PileDiameter > 0 && r.Supports.Any(s => s.FootingSize + .001 < (Math.Ceiling(Math.Sqrt(s.Piles)) - 1) * 3 * r.PileDiameter + 2 * r.PileDiameter)) reasons.Add("Pali non contenuti nel plinto a interasse 3Ø");
        if (r.Family.Id == "psc_u" && i.D("u_top") * r.Girders > r.Width) reasons.Add("Sovrapposizione travi a U");
        if (r.Family.Id == "steel_box")
        {
            double wh = r.Depth - r.Slab - 2 * i.D("flange_mm") / 1000;
            if (r.Width * .4 / r.Girders + 2 * wh * i.D("web_slope") / 4 + i.D("flange_width") > r.Width / r.Girders) reasons.Add("Cassoni metallici oltre la larghezza disponibile");
        }
        if (!double.IsFinite(r.TotalCost) || !double.IsFinite(r.Carbon) || r.TotalCost < 0 || r.Carbon < 0) reasons.Add("Indicatore economico o ambientale non valido");
        return reasons.ToArray();
    }
    private static string DescribeChanges(JsonNode before, JsonNode after)
    {
        var labels = Site.Concat(Layout).Concat(Section).Concat(Substructure).ToDictionary(p => p.Key, p => p.Label.Split('·')[0].Trim());
        foreach (var pair in new[] { ("family", "Tipologia"), ("pier", "Pila"), ("foundation", "Fondazione"), ("continuous", "Continuità") }) labels[pair.Item1] = pair.Item2;
        var keys = after.AsObject().Where(p => !JsonNode.DeepEquals(before[p.Key], p.Value)).Select(p => labels.GetValueOrDefault(p.Key, p.Key)).ToArray();
        return keys.Length == 0 ? "Configurazione corrente" : string.Join(", ", keys);
    }
}
