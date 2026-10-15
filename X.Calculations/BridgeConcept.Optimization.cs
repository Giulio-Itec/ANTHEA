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
}
