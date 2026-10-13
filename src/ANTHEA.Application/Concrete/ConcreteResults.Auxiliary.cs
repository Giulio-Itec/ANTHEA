using System.Text.Json.Nodes;
using Native = Anthea.Calculations;
namespace Anthea.Application.Concrete;

/// <summary>Owned display data. Derived values are copied from the original result.</summary>
public static partial class ConcreteResults
{
    public sealed class ShearBatchResult
    {
        internal Native.ConcreteShearResult Source { get; }
        public ShearResult[] Shear { get; }
        public TorsionResult? Torsion { get; }
        internal ShearBatchResult(Native.ConcreteShearResult value)
        {
            Source = value;
            Shear = value.Shear.Select(item0 => new ShearResult(item0)).ToArray();
            Torsion = value.Torsion is null ? null : new TorsionResult(value.Torsion);
        }
    }

    public sealed class ShearResult
    {
        internal Native.Ntc2018Checks.ShearResult Source { get; }
        public double VRsd { get; }
        public double VRcd { get; }
        public double VRd { get; }
        public double? Ratio { get; }
        public double CotTheta { get; }
        public string Status { get; }
        public string Reference { get; }
        public string Model { get; }
        public Detail[] Details { get; }
        internal ShearResult(Native.Ntc2018Checks.ShearResult value)
        {
            Source = value;
            VRsd = value.VRsd;
            VRcd = value.VRcd;
            VRd = value.VRd;
            Ratio = value.Ratio;
            CotTheta = value.CotTheta;
            Status = value.Status;
            Reference = value.Reference;
            Model = value.Model;
            Details = value.Details.Select(item0 => new Detail(item0)).ToArray();
        }
    }

    public sealed class TorsionGeometry
    {
        internal Native.TorsionGeometry Source { get; }
        public double Area { get; }
        public double Perimeter { get; }
        public double Thickness { get; }
        internal TorsionGeometry(Native.TorsionGeometry value)
        {
            Source = value;
            Area = value.Area;
            Perimeter = value.Perimeter;
            Thickness = value.Thickness;
        }
    }

    public sealed class TorsionResult
    {
        internal Native.TorsionResult Source { get; }
        public double TRcd { get; }
        public double TRsd { get; }
        public double TRld { get; }
        public double TRd { get; }
        public double? TorsionRatio { get; }
        public double? ConcreteCombinedRatio { get; }
        public double? SteelCombinedRatio { get; }
        public double RequiredLongitudinalArea { get; }
        public bool Passed { get; }
        public string Status { get; }
        public TorsionGeometry? Geometry { get; }
        public double CotTheta { get; }
        internal TorsionResult(Native.TorsionResult value)
        {
            Source = value;
            TRcd = value.TRcd;
            TRsd = value.TRsd;
            TRld = value.TRld;
            TRd = value.TRd;
            TorsionRatio = value.TorsionRatio;
            ConcreteCombinedRatio = value.ConcreteCombinedRatio;
            SteelCombinedRatio = value.SteelCombinedRatio;
            RequiredLongitudinalArea = value.RequiredLongitudinalArea;
            Passed = value.Passed;
            Status = value.Status;
            Geometry = value.Geometry is null ? null : new TorsionGeometry(value.Geometry);
            CotTheta = value.CotTheta;
        }
    }

    public sealed class Detail
    {
        internal Native.CrackCalculationDetail Source { get; }
        public string Symbol { get; }
        public double? Value { get; }
        public string Unit { get; }
        public string Expression { get; }
        public string Note { get; }
        internal Detail(Native.CrackCalculationDetail value)
        {
            Source = value;
            Symbol = value.Symbol;
            Value = value.Value;
            Unit = value.Unit;
            Expression = value.Expression;
            Note = value.Note;
        }
        public string Format() => Source.Format();
    }

    public sealed class CurvaturePoint
    {
        internal Native.MomentCurvaturePoint Source { get; }
        public double Moment { get; }
        public double Mx { get; }
        public double My { get; }
        public double Curvature { get; }
        public double GradientX { get; }
        public double GradientY { get; }
        public double Epsilon0 { get; }
        public double ConcreteCompressionStrain { get; }
        public double SteelStrain { get; }
        public bool Yielded { get; }
        public bool Limit { get; }
        internal CurvaturePoint(Native.MomentCurvaturePoint value)
        {
            Source = value;
            Moment = value.Moment;
            Mx = value.Mx;
            My = value.My;
            Curvature = value.Curvature;
            GradientX = value.GradientX;
            GradientY = value.GradientY;
            Epsilon0 = value.Epsilon0;
            ConcreteCompressionStrain = value.ConcreteCompressionStrain;
            SteelStrain = value.SteelStrain;
            Yielded = value.Yielded;
            Limit = value.Limit;
        }
    }

    public sealed class CurvatureResult
    {
        internal Native.MomentCurvatureResult Source { get; }
        public IReadOnlyList<CurvaturePoint> Points { get; }
        public double LimitMoment { get; }
        public double? YieldCurvature { get; }
        public double? UltimateCurvature { get; }
        public string Status { get; }
        public double LimitAxialKn { get; }
        public double AxialResidualKn { get; }
        internal CurvatureResult(Native.MomentCurvatureResult value)
        {
            Source = value;
            Points = value.Points.Select(item0 => new CurvaturePoint(item0)).ToArray();
            LimitMoment = value.LimitMoment;
            YieldCurvature = value.YieldCurvature;
            UltimateCurvature = value.UltimateCurvature;
            Status = value.Status;
            LimitAxialKn = value.LimitAxialKn;
            AxialResidualKn = value.AxialResidualKn;
        }
    }

    public sealed class PrincipalResistance
    {
        internal Native.PrincipalMomentResistance Source { get; }
        public string Direction { get; }
        public double? Moment { get; }
        public Action? Resistance { get; }
        public string Status { get; }
        public SectionState? Section { get; }
        internal PrincipalResistance(Native.PrincipalMomentResistance value)
        {
            Source = value;
            Direction = value.Direction;
            Moment = value.Moment;
            Resistance = value.Resistance is { } item0 ? new Action(item0) : null;
            Status = value.Status;
            Section = value.Section is null ? null : new SectionState(value.Section);
        }
    }

    public sealed class DesignCandidate
    {
        internal Native.ConcreteDesignCandidate Source { get; }
        public int Id { get; }
        public JsonObject Input { get; }
        public JsonObject Shear { get; }
        public JsonObject Detailing { get; }
        public string Description { get; }
        public DesignMetrics Metrics { get; }
        public IReadOnlyList<DesignCheck> Checks { get; }
        public double Score { get; }
        public bool Pareto { get; }
        public bool NeedsReview { get; }
        internal DesignCandidate(Native.ConcreteDesignCandidate value)
        {
            Source = value;
            Id = value.Id;
            Input = (JsonObject)value.Input.DeepClone();
            Shear = (JsonObject)value.Shear.DeepClone();
            Detailing = (JsonObject)value.Detailing.DeepClone();
            Description = value.Description;
            Metrics = new DesignMetrics(value.Metrics);
            Checks = value.Checks.Select(item0 => new DesignCheck(item0)).ToArray();
            Score = value.Score;
            Pareto = value.Pareto;
            NeedsReview = value.NeedsReview;
        }
    }

    public sealed class DesignCheck
    {
        internal Native.ConcreteDesignCheck Source { get; }
        public string Key { get; }
        public string Name { get; }
        public string Combination { get; }
        public double? Ratio { get; }
        public double Target { get; }
        public bool? Passed { get; }
        public string Detail { get; }
        internal DesignCheck(Native.ConcreteDesignCheck value)
        {
            Source = value;
            Key = value.Key;
            Name = value.Name;
            Combination = value.Combination;
            Ratio = value.Ratio;
            Target = value.Target;
            Passed = value.Passed;
            Detail = value.Detail;
        }
    }

    public sealed class DesignMetrics
    {
        internal Native.ConcreteDesignMetrics Source { get; }
        public double SteelArea { get; }
        public double LongitudinalKgPerM { get; }
        public double TransverseKgPerM { get; }
        public int Bars { get; }
        public int Diameters { get; }
        public double PiecesPerM { get; }
        public double KgPerM { get; }
        internal DesignMetrics(Native.ConcreteDesignMetrics value)
        {
            Source = value;
            SteelArea = value.SteelArea;
            LongitudinalKgPerM = value.LongitudinalKgPerM;
            TransverseKgPerM = value.TransverseKgPerM;
            Bars = value.Bars;
            Diameters = value.Diameters;
            PiecesPerM = value.PiecesPerM;
            KgPerM = value.KgPerM;
        }
    }

    public sealed class DesignProgress
    {
        internal Native.ConcreteDesignProgress Source { get; }
        public int Evaluated { get; }
        public int Planned { get; }
        public int Accepted { get; }
        public DesignCandidate? Best { get; }
        public string Phase { get; }
        public double Seconds { get; }
        public int Workers { get; }
        public int PreliminaryChecked { get; }
        internal DesignProgress(Native.ConcreteDesignProgress value)
        {
            Source = value;
            Evaluated = value.Evaluated;
            Planned = value.Planned;
            Accepted = value.Accepted;
            Best = value.Best is null ? null : new DesignCandidate(value.Best);
            Phase = value.Phase;
            Seconds = value.Seconds;
            Workers = value.Workers;
            PreliminaryChecked = value.PreliminaryChecked;
        }
    }

    public sealed class DesignPerformance
    {
        internal Native.ConcreteDesignPerformance Source { get; }
        public double Seconds { get; }
        public double? FirstSolutionSeconds { get; }
        public int Workers { get; }
        public int LongitudinalCalculations { get; }
        public int LongitudinalReuses { get; }
        public int ProbeSections { get; }
        public IReadOnlyList<DesignStage> Stages { get; }
        internal DesignPerformance(Native.ConcreteDesignPerformance value)
        {
            Source = value;
            Seconds = value.Seconds;
            FirstSolutionSeconds = value.FirstSolutionSeconds;
            Workers = value.Workers;
            LongitudinalCalculations = value.LongitudinalCalculations;
            LongitudinalReuses = value.LongitudinalReuses;
            ProbeSections = value.ProbeSections;
            Stages = value.Stages.Select(item0 => new DesignStage(item0)).ToArray();
        }
    }

    public sealed class DesignStage
    {
        internal Native.ConcreteDesignStage Source { get; }
        public string Name { get; }
        public int Calls { get; }
        public double Seconds { get; }
        internal DesignStage(Native.ConcreteDesignStage value)
        {
            Source = value;
            Name = value.Name;
            Calls = value.Calls;
            Seconds = value.Seconds;
        }
    }

    public sealed class DesignResult
    {
        internal Native.ConcreteDesignResult Source { get; }
        public IReadOnlyList<DesignCandidate> Candidates { get; }
        public int Evaluated { get; }
        public int Planned { get; }
        public bool Completed { get; }
        public IReadOnlyDictionary<string, int> Exclusions { get; }
        public DesignMetrics? Baseline { get; }
        public string[] Notes { get; }
        public IReadOnlyDictionary<string, DesignCheck> ExclusionExamples { get; }
        public DesignPerformance? Performance { get; }
        internal DesignResult(Native.ConcreteDesignResult value)
        {
            Source = value;
            Candidates = value.Candidates.Select(item0 => new DesignCandidate(item0)).ToArray();
            Evaluated = value.Evaluated;
            Planned = value.Planned;
            Completed = value.Completed;
            Exclusions = value.Exclusions.ToDictionary(item0 => item0.Key, item0 => item0.Value);
            Baseline = value.Baseline is null ? null : new DesignMetrics(value.Baseline);
            Notes = value.Notes.Select(item0 => item0).ToArray();
            ExclusionExamples = value.ExclusionExamples.ToDictionary(item0 => item0.Key, item0 => new DesignCheck(item0.Value));
            Performance = value.Performance is null ? null : new DesignPerformance(value.Performance);
        }
    }

    public sealed class Action
    {
        internal Native.ActionPoint Source { get; }
        public double N { get; }
        public double Mx { get; }
        public double My { get; }
        internal Action(Native.ActionPoint value)
        {
            Source = value;
            N = value.N;
            Mx = value.Mx;
            My = value.My;
        }
    }

    public sealed class SectionState
    {
        internal Native.SectionResponse Source { get; }
        public double? CMin { get; }
        public double? CMax { get; }
        public double? SMin { get; }
        public double? SMax { get; }
        public double? PMin { get; }
        public double? PMax { get; }
        public double? EcMin { get; }
        public double? EcMax { get; }
        public double? EsMin { get; }
        public double? EsMax { get; }
        public double? EpMin { get; }
        public double? EpMax { get; }
        public double? UsefulDepth { get; }
        public double? NeutralDistance { get; }
        public double? NeutralAngle { get; }
        internal SectionState(Native.SectionResponse value)
        {
            Source = value;
            CMin = value.CMin;
            CMax = value.CMax;
            SMin = value.SMin;
            SMax = value.SMax;
            PMin = value.PMin;
            PMax = value.PMax;
            EcMin = value.EcMin;
            EcMax = value.EcMax;
            EsMin = value.EsMin;
            EsMax = value.EsMax;
            EpMin = value.EpMin;
            EpMax = value.EpMax;
            UsefulDepth = value.UsefulDepth;
            NeutralDistance = value.NeutralDistance;
            NeutralAngle = value.NeutralAngle;
        }
    }

}
