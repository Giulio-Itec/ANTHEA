// Owned presentation data. All derived engineering values are copied from the original calculation.
using System.Text.Json.Nodes;
using Native = Anthea.Calculations.BridgeConcept;
namespace Anthea.Application.BridgeDesign;

public static partial class BridgeDesignModel
{
    public sealed class StructuralLine
    {
        public string Kind { get; }
        public double X1 { get; }
        public double Z1 { get; }
        public double X2 { get; }
        public double Z2 { get; }
        public int Count { get; }
        public double Area { get; }
        public double Force { get; }
        public double Length { get; }
        public double Mass { get; }
        internal StructuralLine(Native.StructuralLine source)
        {
            Kind = source.Kind;
            X1 = source.X1;
            Z1 = source.Z1;
            X2 = source.X2;
            Z2 = source.Z2;
            Count = source.Count;
            Area = source.Area;
            Force = source.Force;
            Length = source.Length;
            Mass = source.Mass;
        }
    }

    public sealed class AdvancedGeometry
    {
        public bool Orthotropic { get; }
        public bool GlobalBeam { get; }
        public double MainSpan { get; }
        public double Rise { get; }
        public double TowerHeight { get; }
        public double DeckSteel { get; }
        public double StructureSteel { get; }
        public double CableSteel { get; }
        public double AnchorVolume { get; }
        public double TowerVolume { get; }
        public double HorizontalForce { get; }
        public StructuralLine[] Members { get; }
        public TechnicalItem[] Dimensions { get; }
        public SupportGeometry[] FoundationSchedule { get; }
        internal AdvancedGeometry(Native.AdvancedGeometry source)
        {
            Orthotropic = source.Orthotropic;
            GlobalBeam = source.GlobalBeam;
            MainSpan = source.MainSpan;
            Rise = source.Rise;
            TowerHeight = source.TowerHeight;
            DeckSteel = source.DeckSteel;
            StructureSteel = source.StructureSteel;
            CableSteel = source.CableSteel;
            AnchorVolume = source.AnchorVolume;
            TowerVolume = source.TowerVolume;
            HorizontalForce = source.HorizontalForce;
            Members = source.Members.Select(item => new StructuralLine(item)).ToArray();
            Dimensions = source.Dimensions.Select(item => new TechnicalItem(item)).ToArray();
            FoundationSchedule = source.FoundationSchedule.Select(item => new SupportGeometry(item)).ToArray();
        }
    }

    public sealed class Parameter
    {
        public string Key { get; }
        public string Label { get; }
        public string Unit { get; }
        public double Default { get; }
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }
        internal Parameter(Native.Parameter source)
        {
            Key = source.Key;
            Label = source.Label;
            Unit = source.Unit;
            Default = source.Default;
            Min = source.Min;
            Max = source.Max;
            Step = source.Step;
        }
    }

    public sealed class Family
    {
        public string Id { get; }
        public string Name { get; }
        public double MinSpan { get; }
        public double MaxSpan { get; }
        public double Ratio { get; }
        public double MinDepth { get; }
        public double Slab { get; }
        public double Spacing { get; }
        public bool Steel { get; }
        public bool Prestressed { get; }
        internal Family(Native.Family source)
        {
            Id = source.Id;
            Name = source.Name;
            MinSpan = source.MinSpan;
            MaxSpan = source.MaxSpan;
            Ratio = source.Ratio;
            MinDepth = source.MinDepth;
            Slab = source.Slab;
            Spacing = source.Spacing;
            Steel = source.Steel;
            Prestressed = source.Prestressed;
        }
    }

    public sealed class Quantity
    {
        public string Group { get; }
        public string Item { get; }
        public double Amount { get; }
        public string Unit { get; }
        public double Rate { get; }
        public double Cost { get; }
        public double Carbon { get; }
        internal Quantity(Native.Quantity source)
        {
            Group = source.Group;
            Item = source.Item;
            Amount = source.Amount;
            Unit = source.Unit;
            Rate = source.Rate;
            Cost = source.Cost;
            Carbon = source.Carbon;
        }
    }

    public sealed class Detail
    {
        public string Name { get; }
        public double Value { get; }
        public string Unit { get; }
        public string Rule { get; }
        internal Detail(Native.Detail source)
        {
            Name = source.Name;
            Value = source.Value;
            Unit = source.Unit;
            Rule = source.Rule;
        }
    }

    public sealed class Station
    {
        public int Span { get; }
        public double X { get; }
        public double Moment { get; }
        public double Shear { get; }
        public double DeflectionMm { get; }
        public double? MomentMinimum { get; }
        public double? ShearMinimum { get; }
        public double? DeflectionMinimumMm { get; }
        internal Station(Native.Station source)
        {
            Span = source.Span;
            X = source.X;
            Moment = source.Moment;
            Shear = source.Shear;
            DeflectionMm = source.DeflectionMm;
            MomentMinimum = source.MomentMinimum; ShearMinimum = source.ShearMinimum; DeflectionMinimumMm = source.DeflectionMinimumMm;
        }
    }

    public sealed class Support
    {
        public int Index { get; }
        public double X { get; }
        public string Type { get; }
        public double Reaction { get; }
        public double? MinimumReaction { get; }
        public double PierHeight { get; }
        public double PierSize { get; }
        public int Columns { get; }
        public double FootingSize { get; }
        public double FootingWidth { get; }
        public int Piles { get; }
        internal Support(Native.Support source)
        {
            Index = source.Index;
            X = source.X;
            Type = source.Type;
            Reaction = source.Reaction; MinimumReaction = source.MinimumReaction;
            PierHeight = source.PierHeight;
            PierSize = source.PierSize;
            Columns = source.Columns;
            FootingSize = source.FootingSize;
            FootingWidth = source.FootingWidth;
            Piles = source.Piles;
        }
    }

    public sealed record TrafficInfo(string Model, string Reference, int Lanes, double UniformKnPerMetre, double TandemKn,
        double GammaG, double GammaQ, string Scope)
    {
        internal TrafficInfo(Native.TrafficInfo t) : this(t.Model, t.Reference, t.Lanes, t.UniformKnPerMetre, t.TandemKn, t.GammaG, t.GammaQ, t.Scope) { }
    }
    public sealed class Result
    {
        public TrafficInfo? Traffic { get; }
        public Family Family { get; }
        public double Width { get; }
        public double Depth { get; }
        public double PierDepth { get; }
        public double Slab { get; }
        public double Spacing { get; }
        public int Girders { get; }
        public double Web { get; }
        public double Bottom { get; }
        public double[] Spans { get; }
        public Support[] Supports { get; }
        public string Foundation { get; }
        public double PileDiameter { get; }
        public double PileLength { get; }
        public double Concrete { get; }
        public double Steel { get; }
        public double Rebar { get; }
        public double Prestress { get; }
        public double Inertia { get; }
        public double DirectCost { get; }
        public double TotalCost { get; }
        public string? PriceRevision { get; }
        public string? EnvironmentRevision { get; }
        public double CostLow { get; }
        public double CostHigh { get; }
        public double Carbon { get; }
        public double Duration { get; }
        public Quantity[] Quantities { get; }
        public Detail[] Details { get; }
        public Station[] Stations { get; }
        public string[] Warnings { get; }
        public AdvancedGeometry? Advanced { get; }
        public double Length { get; }
        public double Area { get; }
        internal Native.Result Source { get; }
        public JsonObject Json() => Source.Json();
        internal Result(Native.Result source)
        {
            Source = source; Traffic = source.Traffic is { } traffic ? new TrafficInfo(traffic) : null;
            Family = new Family(source.Family);
            Width = source.Width;
            Depth = source.Depth;
            PierDepth = source.PierDepth;
            Slab = source.Slab;
            Spacing = source.Spacing;
            Girders = source.Girders;
            Web = source.Web;
            Bottom = source.Bottom;
            Spans = source.Spans.ToArray();
            Supports = source.Supports.Select(item => new Support(item)).ToArray();
            Foundation = source.Foundation;
            PileDiameter = source.PileDiameter;
            PileLength = source.PileLength;
            Concrete = source.Concrete;
            Steel = source.Steel;
            Rebar = source.Rebar;
            Prestress = source.Prestress;
            Inertia = source.Inertia;
            DirectCost = source.DirectCost;
            TotalCost = source.TotalCost;
            PriceRevision = source.PriceRevision;
            EnvironmentRevision = source.EnvironmentRevision;
            CostLow = source.CostLow;
            CostHigh = source.CostHigh;
            Carbon = source.Carbon;
            Duration = source.Duration;
            Quantities = source.Quantities.Select(item => new Quantity(item)).ToArray();
            Details = source.Details.Select(item => new Detail(item)).ToArray();
            Stations = source.Stations.Select(item => new Station(item)).ToArray();
            Warnings = source.Warnings.ToArray();
            Advanced = source.Advanced is { } valueAdvancedGeometry ? new AdvancedGeometry(valueAdvancedGeometry) : null;
            Length = source.Length;
            Area = source.Area;
        }
    }

    public enum OptimizationObjective { Cost = 0, Carbon = 1, Balanced = 2 }

    public sealed record OptimizationOptions
    {
        public OptimizationObjective Objective { get; init; }
        public bool KeepFamily { get; init; }
        public bool KeepSpans { get; init; }
        public bool KeepDepth { get; init; }
        public bool KeepSection { get; init; }
        public bool KeepPier { get; init; }
        public bool KeepFoundation { get; init; }
        public bool KeepContinuity { get; init; }
        public int MinSpans { get; init; }
        public int MaxSpans { get; init; }
        public double MaxDepth { get; init; }
        public double MinDepth { get; init; }
        public double DepthPercentMin { get; init; }
        public double DepthPercentMax { get; init; }
        public double DepthPercentStep { get; init; }
        public double PilePercentMin { get; init; }
        public double PilePercentMax { get; init; }
        public double PilePercentStep { get; init; }
        public int Alternatives { get; init; }
        public OptimizationOptions() : this(new Native.OptimizationOptions()) { }
        internal OptimizationOptions(Native.OptimizationOptions source)
        {
            Objective = (OptimizationObjective)source.Objective;
            KeepFamily = source.KeepFamily;
            KeepSpans = source.KeepSpans;
            KeepDepth = source.KeepDepth;
            KeepSection = source.KeepSection;
            KeepPier = source.KeepPier;
            KeepFoundation = source.KeepFoundation;
            KeepContinuity = source.KeepContinuity;
            MinSpans = source.MinSpans;
            MaxSpans = source.MaxSpans;
            MaxDepth = source.MaxDepth;
            MinDepth = source.MinDepth;
            DepthPercentMin = source.DepthPercentMin;
            DepthPercentMax = source.DepthPercentMax;
            DepthPercentStep = source.DepthPercentStep;
            PilePercentMin = source.PilePercentMin;
            PilePercentMax = source.PilePercentMax;
            PilePercentStep = source.PilePercentStep;
            Alternatives = source.Alternatives;
        }
        internal Native.OptimizationOptions ToNative() => new()
        {
            Objective = (Native.OptimizationObjective)Objective,
            KeepFamily = KeepFamily,
            KeepSpans = KeepSpans,
            KeepDepth = KeepDepth,
            KeepSection = KeepSection,
            KeepPier = KeepPier,
            KeepFoundation = KeepFoundation,
            KeepContinuity = KeepContinuity,
            MinSpans = MinSpans,
            MaxSpans = MaxSpans,
            MaxDepth = MaxDepth,
            MinDepth = MinDepth,
            DepthPercentMin = DepthPercentMin,
            DepthPercentMax = DepthPercentMax,
            DepthPercentStep = DepthPercentStep,
            PilePercentMin = PilePercentMin,
            PilePercentMax = PilePercentMax,
            PilePercentStep = PilePercentStep,
            Alternatives = Alternatives,
        };
    }

    public sealed class OptimizationCandidate
    {
        public JsonObject Data { get; }
        public Result Result { get; }
        public double Score { get; }
        public bool Pareto { get; }
        public string Changes { get; }
        internal OptimizationCandidate(Native.OptimizationCandidate source)
        {
            Data = (JsonObject)source.Data.DeepClone();
            Result = new Result(source.Result);
            Score = source.Score;
            Pareto = source.Pareto;
            Changes = source.Changes;
        }
    }

    public sealed class OptimizationResult
    {
        public Result Baseline { get; }
        public OptimizationCandidate[] Candidates { get; }
        public int Evaluated { get; }
        public int Admissible { get; }
        public IReadOnlyDictionary<string, int> Excluded { get; }
        public string[] BaselineExclusions { get; }
        public OptimizationOptions Options { get; }
        public OptimizationTrial[] Trials { get; }
        public OptimizationSolution[] Solutions { get; }
        internal OptimizationResult(Native.OptimizationResult source)
        {
            Baseline = new Result(source.Baseline);
            Candidates = source.Candidates.Select(item => new OptimizationCandidate(item)).ToArray();
            Evaluated = source.Evaluated;
            Admissible = source.Admissible;
            Excluded = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(source.Excluded.ToDictionary(pair => pair.Key, pair => pair.Value));
            BaselineExclusions = source.BaselineExclusions.ToArray();
            Options = new OptimizationOptions(source.Options);
            Trials = source.Trials.Select(item => new OptimizationTrial(item)).ToArray();
            Solutions = source.Solutions.Select(item => new OptimizationSolution(item)).ToArray();
        }
    }

    public sealed class OptimizationTrial
    {
        public int Iteration { get; }
        public string Family { get; }
        public int Spans { get; }
        public double? Depth { get; }
        public double? PileLength { get; }
        public double? Cost { get; }
        public double? Carbon { get; }
        public string Pier { get; }
        public string Foundation { get; }
        public bool Continuous { get; }
        public string[] Exclusions { get; }
        public bool Admissible { get; }
        internal OptimizationTrial(Native.OptimizationTrial source)
        {
            Iteration = source.Iteration;
            Family = source.Family;
            Spans = source.Spans;
            Depth = source.Depth;
            PileLength = source.PileLength;
            Cost = source.Cost;
            Carbon = source.Carbon;
            Pier = source.Pier;
            Foundation = source.Foundation;
            Continuous = source.Continuous;
            Exclusions = source.Exclusions.ToArray();
            Admissible = source.Admissible;
        }
    }

    public sealed class OptimizationSolution
    {
        public int Rank { get; }
        public OptimizationTrial Trial { get; }
        public JsonObject Data { get; }
        public double Score { get; }
        public bool Pareto { get; }
        internal OptimizationSolution(Native.OptimizationSolution source)
        {
            Rank = source.Rank;
            Trial = new OptimizationTrial(source.Trial);
            Data = (JsonObject)source.Data.DeepClone();
            Score = source.Score;
            Pareto = source.Pareto;
        }
    }

    public sealed class OptimizationProgress
    {
        public int Evaluated { get; }
        public int Planned { get; }
        public int Admissible { get; }
        public OptimizationTrial[] NewTrials { get; }
        public OptimizationCandidate? Best { get; }
        internal OptimizationProgress(Native.OptimizationProgress source)
        {
            Evaluated = source.Evaluated;
            Planned = source.Planned;
            Admissible = source.Admissible;
            NewTrials = source.NewTrials.Select(item => new OptimizationTrial(item)).ToArray();
            Best = source.Best is { } valueOptimizationCandidate ? new OptimizationCandidate(valueOptimizationCandidate) : null;
        }
    }

    public sealed class TechnicalItem
    {
        public string Component { get; }
        public string Symbol { get; }
        public double Value { get; }
        public string Unit { get; }
        public string Origin { get; }
        public string Note { get; }
        internal TechnicalItem(Native.TechnicalItem source)
        {
            Component = source.Component;
            Symbol = source.Symbol;
            Value = source.Value;
            Unit = source.Unit;
            Origin = source.Origin;
            Note = source.Note;
        }
    }

    public sealed class SpanGeometry
    {
        public int Number { get; }
        public double Start { get; }
        public double End { get; }
        public double Length { get; }
        public int Elements { get; }
        public double DevelopedLength { get; }
        internal SpanGeometry(Native.SpanGeometry source)
        {
            Number = source.Number;
            Start = source.Start;
            End = source.End;
            Length = source.Length;
            Elements = source.Elements;
            DevelopedLength = source.DevelopedLength;
        }
    }

    public sealed class SupportGeometry
    {
        public int Number { get; }
        public string Type { get; }
        public double X { get; }
        public double Height { get; }
        public int Columns { get; }
        public double Size { get; }
        public double WallWidth { get; }
        public double CapLength { get; }
        public double CapWidth { get; }
        public double CapThickness { get; }
        public double FootingLength { get; }
        public double FootingWidth { get; }
        public double FootingThickness { get; }
        public int Piles { get; }
        public double PileDiameter { get; }
        public double PileLength { get; }
        internal SupportGeometry(Native.SupportGeometry source)
        {
            Number = source.Number;
            Type = source.Type;
            X = source.X;
            Height = source.Height;
            Columns = source.Columns;
            Size = source.Size;
            WallWidth = source.WallWidth;
            CapLength = source.CapLength;
            CapWidth = source.CapWidth;
            CapThickness = source.CapThickness;
            FootingLength = source.FootingLength;
            FootingWidth = source.FootingWidth;
            FootingThickness = source.FootingThickness;
            Piles = source.Piles;
            PileDiameter = source.PileDiameter;
            PileLength = source.PileLength;
        }
    }

}
