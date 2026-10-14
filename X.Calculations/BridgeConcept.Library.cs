using System.Text.Json.Nodes;
using NativeBridge = GPC.Design.Bridges.BridgeConcept;
using GPC.Design.Bridges;

namespace Anthea.Calculations;

// Archive/UI boundary only. Engineering rules, search and numerical validation belong to GPC.Design.
public static partial class BridgeConcept
{
    public static Result Calculate(JsonObject data, bool legacy = false) => legacy ? LegacyCalculate(data) : WithEnvironment(FromNative(NativeBridge.Calculate(NativeInput(data))), data);
    private static Result WithEnvironment(Result result, JsonObject data) => data["environment"] is null ? result
        : result with { EnvironmentRevision = Anthea.Serialization.BridgeEnvironmentData.Revision(WithAdvancedDefaults(data)) };
    public static (double[] Reactions, Station[] Stations) Analyze(double[] spans, double q, double ei, bool continuous)
    { var r = NativeBridge.Analyze(spans, q, ei, continuous); return (r.Reactions, r.Stations.Select(FromNative).ToArray()); }
    public static TechnicalItem[] TechnicalSchedule(JsonObject data, Result result) => NativeBridge.TechnicalSchedule(NativeInput(data), ToNative(result)).Select(FromNative).ToArray();
    public static SupportGeometry[] SupportSchedule(JsonObject data, Result result) => NativeBridge.SupportSchedule(NativeInput(data), ToNative(result)).Select(FromNative).ToArray();
    public static SpanGeometry[] SpanSchedule(Result result) => NativeBridge.SpanSchedule(ToNative(result)).Select(FromNative).ToArray();
    public static double[] OptimizationRange(double min, double max, double step) => NativeBridge.OptimizationRange(min, max, step);
    public static string[] OptimizationExclusions(JsonObject data, Result result, OptimizationOptions options) => NativeBridge.OptimizationExclusions(NativeInput(data), ToNative(result), ToNative(options));
    public static double ReferenceDepth(Family family, double maxSpan, bool continuous) => NativeBridge.ReferenceDepth(ToNative(family), maxSpan, continuous);
    public static OptimizationResult Optimize(JsonObject source, OptimizationOptions options, CancellationToken cancellation = default,
        IProgress<OptimizationProgress>? progress = null, bool legacy = false)
    {
        if (legacy) return LegacyOptimize(source, options, cancellation, progress);
        var template = (JsonObject)source.DeepClone(); template.Remove("alternative_a");
        // A trial published in progress is the same presentation object in the completed trace.
        var trials = new Dictionary<NativeBridge.OptimizationTrial, OptimizationTrial>(ReferenceEqualityComparer.Instance);
        OptimizationTrial Trial(NativeBridge.OptimizationTrial value)
        { if (!trials.TryGetValue(value, out var mapped)) trials.Add(value, mapped = FromNative(value)); return mapped; }
        OptimizationCandidate Candidate(NativeBridge.OptimizationCandidate c) => new(ArchiveInput(c.Data, template), WithEnvironment(FromNative(c.Result), template), c.Score, c.Pareto, c.Changes);
        var r = NativeBridge.Optimize(NativeInput(template), ToNative(options), cancellation,
            progress is null ? null : new BridgeProgress(p => progress.Report(new(p.Evaluated, p.Planned, p.Admissible, p.NewTrials.Select(Trial).ToArray(), p.Best is null ? null : Candidate(p.Best)))));
        return new(WithEnvironment(FromNative(r.Baseline), template), r.Candidates.Select(Candidate).ToArray(), r.Evaluated, r.Admissible,
            new Dictionary<string, int>(r.Excluded), r.BaselineExclusions.ToArray(), options)
        { Trials = r.Trials.Select(Trial).ToArray(), Solutions = r.Solutions.Select(s => new OptimizationSolution(s.Rank, Trial(s.Trial), ArchiveInput(s.Data, template), s.Score, s.Pareto)).ToArray() };
    }
    private sealed class BridgeProgress(Action<NativeBridge.OptimizationProgress> report) : IProgress<NativeBridge.OptimizationProgress>
    { public void Report(NativeBridge.OptimizationProgress value) => report(value); }
    internal static BridgeModel NativeInput(JsonObject source)
    {
        var data = WithAdvancedDefaults(source); ValidateShape(data);
        double Number(string group, string key) => J.Number(data[group]?[key]) is double n && double.IsFinite(n) ? n : throw new ArgumentException($"Dato numerico richiesto: {group}.{key}.");
        string Text(string key) => data["input"]?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new ArgumentException("Scelta richiesta: " + key);
        bool Flag(string key) => data["input"]?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new ArgumentException("Opzione non valida: " + key);
        var input = new BridgeInput
        {
            Length = Number("input", "length"),
            Height = Number("input", "height"),
            ObstacleWidth = Number("input", "obstacle_width"),
            Lanes = Number("input", "lanes"),
            LaneWidth = Number("input", "lane_width"),
            Shoulder = Number("input", "shoulder"),
            Median = Number("input", "median"),
            BarrierWidth = Number("input", "barrier_width"),
            Spans = Number("input", "spans"),
            Depth = Number("input", "depth"),
            Slab = Number("input", "slab"),
            Spacing = Number("input", "spacing"),
            Web = Number("input", "web"),
            Bottom = Number("input", "bottom"),
            BottomRatio = Number("input", "bottom_ratio"),
            Cells = Number("input", "cells"),
            Boxes = Number("input", "boxes"),
            FlangeWidth = Number("input", "flange_width"),
            FlangeMm = Number("input", "flange_mm"),
            WebMm = Number("input", "web_mm"),
            WebSlope = Number("input", "web_slope"),
            Haunch = Number("input", "haunch"),
            UTop = Number("input", "u_top"),
            UBottom = Number("input", "u_bottom"),
            Fc = Number("input", "fc"),
            ArchRise = Number("input", "arch_rise"),
            TowerHeight = Number("input", "tower_height"),
            CableSag = Number("input", "cable_sag"),
            SuspenderSpacing = Number("input", "suspender_spacing"),
            TrussRatio = Number("input", "truss_ratio"),
            EmbeddedCover = Number("input", "embedded_cover"),
            DeckPlateMm = Number("input", "deck_plate_mm"),
            RibHeight = Number("input", "rib_height"),
            RibTop = Number("input", "rib_top"),
            RibBottom = Number("input", "rib_bottom"),
            RibMm = Number("input", "rib_mm"),
            RibSpacing = Number("input", "rib_spacing"),
            PileLength = Number("input", "pile_length"),
            PierSize = Number("input", "pier_size"),
            PileCount = Number("input", "pile_count"),
            FootingSize = Number("input", "footing_size"),
            FcSub = Number("input", "fc_sub"),
            DeckType = Text("deck_type"),
            Family = Text("family"),
            Obstacle = Text("obstacle"),
            Soil = Text("soil"),
            Pier = Text("pier"),
            Foundation = Text("foundation"),
            Continuous = Flag("continuous"),
            StartPier = Flag("start_pier"),
            EndPier = Flag("end_pier"),
            LowCarbon = Flag("low_carbon"),
            Recycled = Flag("recycled"),
        };
        var rates = new BridgeRates
        {
            ConcreteDeck = Number("rates", "concrete_deck"),
            ConcreteSub = Number("rates", "concrete_sub"),
            Rebar = Number("rates", "rebar"),
            Prestress = Number("rates", "prestress"),
            Steel = Number("rates", "steel"),
            SteelBox = Number("rates", "steel_box"),
            Formwork = Number("rates", "formwork"),
            Pile1 = Number("rates", "pile_1"),
            Pile15 = Number("rates", "pile_15"),
            Bearing = Number("rates", "bearing"),
            Joint = Number("rates", "joint"),
            Barrier = Number("rates", "barrier"),
            Surfacing = Number("rates", "surfacing"),
            SteelOrtho = Number("rates", "steel_ortho"),
            Cables = Number("rates", "cables"),
            ErectionSpecial = Number("rates", "erection_special"),
        };
        var assumptions = new BridgeAssumptions
        {
            Prelims = Number("assumptions", "prelims"),
            Contingency = Number("assumptions", "contingency"),
            Uncertainty = Number("assumptions", "uncertainty"),
            Co2Concrete = Number("assumptions", "co2_concrete"),
            Co2Rebar = Number("assumptions", "co2_rebar"),
            Co2Steel = Number("assumptions", "co2_steel"),
            Co2Pt = Number("assumptions", "co2_pt"),
            Co2Site = Number("assumptions", "co2_site"),
            LowCarbonFactor = Number("assumptions", "low_carbon_factor"),
            RecycledFactor = Number("assumptions", "recycled_factor"),
            RebarRc = Number("assumptions", "rebar_rc"),
            RebarPsc = Number("assumptions", "rebar_psc"),
            RebarSub = Number("assumptions", "rebar_sub"),
            RebarFound = Number("assumptions", "rebar_found"),
            PtDensity = Number("assumptions", "pt_density"),
            WearLoad = Number("assumptions", "wear_load"),
            TrafficLoad = Number("assumptions", "traffic_load"),
            GammaG = Number("assumptions", "gamma_g"),
            GammaQ = Number("assumptions", "gamma_q"),
            SoilPressure = Number("assumptions", "soil_pressure"),
            PileSkin = Number("assumptions", "pile_skin"),
            PileBase = Number("assumptions", "pile_base"),
            MonthsBase = Number("assumptions", "months_base"),
            MonthsSpan = Number("assumptions", "months_span"),
            MonthsPier = Number("assumptions", "months_pier"),
            ConceptTension = Number("assumptions", "concept_tension"),
            ConceptCompression = Number("assumptions", "concept_compression"),
            ConceptCable = Number("assumptions", "concept_cable"),
            ConceptTower = Number("assumptions", "concept_tower"),
            AnchorFriction = Number("assumptions", "anchor_friction"),
            SpecialConnections = Number("assumptions", "special_connections"),
            Co2Cable = Number("assumptions", "co2_cable"),
        };
        return new(input, rates, assumptions) { Prices = BridgePricing.Read(data) };
    }
    private static JsonObject ArchiveInput(BridgeModel model, JsonObject source)
    {
        var result = (JsonObject)source.DeepClone();
        result["input"]!["length"] = model.Input.Length;
        result["input"]!["height"] = model.Input.Height;
        result["input"]!["obstacle_width"] = model.Input.ObstacleWidth;
        result["input"]!["lanes"] = model.Input.Lanes;
        result["input"]!["lane_width"] = model.Input.LaneWidth;
        result["input"]!["shoulder"] = model.Input.Shoulder;
        result["input"]!["median"] = model.Input.Median;
        result["input"]!["barrier_width"] = model.Input.BarrierWidth;
        result["input"]!["spans"] = model.Input.Spans;
        result["input"]!["depth"] = model.Input.Depth;
        result["input"]!["slab"] = model.Input.Slab;
        result["input"]!["spacing"] = model.Input.Spacing;
        result["input"]!["web"] = model.Input.Web;
        result["input"]!["bottom"] = model.Input.Bottom;
        result["input"]!["bottom_ratio"] = model.Input.BottomRatio;
        result["input"]!["cells"] = model.Input.Cells;
        result["input"]!["boxes"] = model.Input.Boxes;
        result["input"]!["flange_width"] = model.Input.FlangeWidth;
        result["input"]!["flange_mm"] = model.Input.FlangeMm;
        result["input"]!["web_mm"] = model.Input.WebMm;
        result["input"]!["web_slope"] = model.Input.WebSlope;
        result["input"]!["haunch"] = model.Input.Haunch;
        result["input"]!["u_top"] = model.Input.UTop;
        result["input"]!["u_bottom"] = model.Input.UBottom;
        result["input"]!["fc"] = model.Input.Fc;
        result["input"]!["arch_rise"] = model.Input.ArchRise;
        result["input"]!["tower_height"] = model.Input.TowerHeight;
        result["input"]!["cable_sag"] = model.Input.CableSag;
        result["input"]!["suspender_spacing"] = model.Input.SuspenderSpacing;
        result["input"]!["truss_ratio"] = model.Input.TrussRatio;
        result["input"]!["embedded_cover"] = model.Input.EmbeddedCover;
        result["input"]!["deck_plate_mm"] = model.Input.DeckPlateMm;
        result["input"]!["rib_height"] = model.Input.RibHeight;
        result["input"]!["rib_top"] = model.Input.RibTop;
        result["input"]!["rib_bottom"] = model.Input.RibBottom;
        result["input"]!["rib_mm"] = model.Input.RibMm;
        result["input"]!["rib_spacing"] = model.Input.RibSpacing;
        result["input"]!["pile_length"] = model.Input.PileLength;
        result["input"]!["pier_size"] = model.Input.PierSize;
        result["input"]!["pile_count"] = model.Input.PileCount;
        result["input"]!["footing_size"] = model.Input.FootingSize;
        result["input"]!["fc_sub"] = model.Input.FcSub;
        result["input"]!["deck_type"] = model.Input.DeckType;
        result["input"]!["family"] = model.Input.Family;
        result["input"]!["obstacle"] = model.Input.Obstacle;
        result["input"]!["soil"] = model.Input.Soil;
        result["input"]!["pier"] = model.Input.Pier;
        result["input"]!["foundation"] = model.Input.Foundation;
        result["input"]!["continuous"] = model.Input.Continuous;
        result["input"]!["start_pier"] = model.Input.StartPier;
        result["input"]!["end_pier"] = model.Input.EndPier;
        result["input"]!["low_carbon"] = model.Input.LowCarbon;
        result["input"]!["recycled"] = model.Input.Recycled;
        result["rates"]!["concrete_deck"] = model.Rates.ConcreteDeck;
        result["rates"]!["concrete_sub"] = model.Rates.ConcreteSub;
        result["rates"]!["rebar"] = model.Rates.Rebar;
        result["rates"]!["prestress"] = model.Rates.Prestress;
        result["rates"]!["steel"] = model.Rates.Steel;
        result["rates"]!["steel_box"] = model.Rates.SteelBox;
        result["rates"]!["formwork"] = model.Rates.Formwork;
        result["rates"]!["pile_1"] = model.Rates.Pile1;
        result["rates"]!["pile_15"] = model.Rates.Pile15;
        result["rates"]!["bearing"] = model.Rates.Bearing;
        result["rates"]!["joint"] = model.Rates.Joint;
        result["rates"]!["barrier"] = model.Rates.Barrier;
        result["rates"]!["surfacing"] = model.Rates.Surfacing;
        result["rates"]!["steel_ortho"] = model.Rates.SteelOrtho;
        result["rates"]!["cables"] = model.Rates.Cables;
        result["rates"]!["erection_special"] = model.Rates.ErectionSpecial;
        result["assumptions"]!["prelims"] = model.Assumptions.Prelims;
        result["assumptions"]!["contingency"] = model.Assumptions.Contingency;
        result["assumptions"]!["uncertainty"] = model.Assumptions.Uncertainty;
        result["assumptions"]!["co2_concrete"] = model.Assumptions.Co2Concrete;
        result["assumptions"]!["co2_rebar"] = model.Assumptions.Co2Rebar;
        result["assumptions"]!["co2_steel"] = model.Assumptions.Co2Steel;
        result["assumptions"]!["co2_pt"] = model.Assumptions.Co2Pt;
        result["assumptions"]!["co2_site"] = model.Assumptions.Co2Site;
        result["assumptions"]!["low_carbon_factor"] = model.Assumptions.LowCarbonFactor;
        result["assumptions"]!["recycled_factor"] = model.Assumptions.RecycledFactor;
        result["assumptions"]!["rebar_rc"] = model.Assumptions.RebarRc;
        result["assumptions"]!["rebar_psc"] = model.Assumptions.RebarPsc;
        result["assumptions"]!["rebar_sub"] = model.Assumptions.RebarSub;
        result["assumptions"]!["rebar_found"] = model.Assumptions.RebarFound;
        result["assumptions"]!["pt_density"] = model.Assumptions.PtDensity;
        result["assumptions"]!["wear_load"] = model.Assumptions.WearLoad;
        result["assumptions"]!["traffic_load"] = model.Assumptions.TrafficLoad;
        result["assumptions"]!["gamma_g"] = model.Assumptions.GammaG;
        result["assumptions"]!["gamma_q"] = model.Assumptions.GammaQ;
        result["assumptions"]!["soil_pressure"] = model.Assumptions.SoilPressure;
        result["assumptions"]!["pile_skin"] = model.Assumptions.PileSkin;
        result["assumptions"]!["pile_base"] = model.Assumptions.PileBase;
        result["assumptions"]!["months_base"] = model.Assumptions.MonthsBase;
        result["assumptions"]!["months_span"] = model.Assumptions.MonthsSpan;
        result["assumptions"]!["months_pier"] = model.Assumptions.MonthsPier;
        result["assumptions"]!["concept_tension"] = model.Assumptions.ConceptTension;
        result["assumptions"]!["concept_compression"] = model.Assumptions.ConceptCompression;
        result["assumptions"]!["concept_cable"] = model.Assumptions.ConceptCable;
        result["assumptions"]!["concept_tower"] = model.Assumptions.ConceptTower;
        result["assumptions"]!["anchor_friction"] = model.Assumptions.AnchorFriction;
        result["assumptions"]!["special_connections"] = model.Assumptions.SpecialConnections;
        result["assumptions"]!["co2_cable"] = model.Assumptions.Co2Cable;
        return result;
    }
    private static Parameter FromNative(NativeBridge.Parameter v) => new(v.Key, v.Label, v.Unit, v.Default, v.Min, v.Max, v.Step);
    private static NativeBridge.Parameter ToNative(Parameter v) => new(v.Key, v.Label, v.Unit, v.Default, v.Min, v.Max, v.Step);
    private static Family FromNative(NativeBridge.Family v) => new(v.Id, v.Name, v.MinSpan, v.MaxSpan, v.Ratio, v.MinDepth, v.Slab, v.Spacing, v.Steel, v.Prestressed);
    private static NativeBridge.Family ToNative(Family v) => new(v.Id, v.Name, v.MinSpan, v.MaxSpan, v.Ratio, v.MinDepth, v.Slab, v.Spacing, v.Steel, v.Prestressed);
    private static Quantity FromNative(NativeBridge.Quantity v) => new(v.Group, v.Item, v.Amount, v.Unit, v.Rate, v.Cost, v.Carbon);
    private static NativeBridge.Quantity ToNative(Quantity v) => new(v.Group, v.Item, v.Amount, v.Unit, v.Rate, v.Cost, v.Carbon);
    private static Detail FromNative(NativeBridge.Detail v) => new(v.Name, v.Value, v.Unit, v.Rule);
    private static NativeBridge.Detail ToNative(Detail v) => new(v.Name, v.Value, v.Unit, v.Rule);
    private static Station FromNative(NativeBridge.Station v) => new(v.Span, v.X, v.Moment, v.Shear, v.DeflectionMm);
    private static NativeBridge.Station ToNative(Station v) => new(v.Span, v.X, v.Moment, v.Shear, v.DeflectionMm);
    private static Support FromNative(NativeBridge.Support v) => new(v.Index, v.X, v.Type, v.Reaction, v.PierHeight, v.PierSize, v.Columns, v.FootingSize, v.FootingWidth, v.Piles);
    private static NativeBridge.Support ToNative(Support v) => new(v.Index, v.X, v.Type, v.Reaction, v.PierHeight, v.PierSize, v.Columns, v.FootingSize, v.FootingWidth, v.Piles);
    private static Result FromNative(NativeBridge.Result v) => new(FromNative(v.Family), v.Width, v.Depth, v.PierDepth, v.Slab, v.Spacing, v.Girders, v.Web, v.Bottom, v.Spans.ToArray(), v.Supports.Select(FromNative).ToArray(), v.Foundation, v.PileDiameter, v.PileLength, v.Concrete, v.Steel, v.Rebar, v.Prestress, v.Inertia, v.DirectCost, v.TotalCost, v.CostLow, v.CostHigh, v.Carbon, v.Duration, v.Quantities.Select(FromNative).ToArray(), v.Details.Select(FromNative).ToArray(), v.Stations.Select(FromNative).ToArray(), v.Warnings.ToArray()) { Advanced = v.Advanced is null ? null : FromNative(v.Advanced), PriceRevision = v.PriceRevision };
    private static NativeBridge.Result ToNative(Result v) => new(ToNative(v.Family), v.Width, v.Depth, v.PierDepth, v.Slab, v.Spacing, v.Girders, v.Web, v.Bottom, v.Spans.ToArray(), v.Supports.Select(ToNative).ToArray(), v.Foundation, v.PileDiameter, v.PileLength, v.Concrete, v.Steel, v.Rebar, v.Prestress, v.Inertia, v.DirectCost, v.TotalCost, v.CostLow, v.CostHigh, v.Carbon, v.Duration, v.Quantities.Select(ToNative).ToArray(), v.Details.Select(ToNative).ToArray(), v.Stations.Select(ToNative).ToArray(), v.Warnings.ToArray()) { Advanced = v.Advanced is null ? null : ToNative(v.Advanced) };
    private static TechnicalItem FromNative(NativeBridge.TechnicalItem v) => new(v.Component, v.Symbol, v.Value, v.Unit, v.Origin, v.Note);
    private static NativeBridge.TechnicalItem ToNative(TechnicalItem v) => new(v.Component, v.Symbol, v.Value, v.Unit, v.Origin, v.Note);
    private static SpanGeometry FromNative(NativeBridge.SpanGeometry v) => new(v.Number, v.Start, v.End, v.Length, v.Elements, v.DevelopedLength);
    private static NativeBridge.SpanGeometry ToNative(SpanGeometry v) => new(v.Number, v.Start, v.End, v.Length, v.Elements, v.DevelopedLength);
    private static SupportGeometry FromNative(NativeBridge.SupportGeometry v) => new(v.Number, v.Type, v.X, v.Height, v.Columns, v.Size, v.WallWidth, v.CapLength, v.CapWidth, v.CapThickness, v.FootingLength, v.FootingWidth, v.FootingThickness, v.Piles, v.PileDiameter, v.PileLength);
    private static NativeBridge.SupportGeometry ToNative(SupportGeometry v) => new(v.Number, v.Type, v.X, v.Height, v.Columns, v.Size, v.WallWidth, v.CapLength, v.CapWidth, v.CapThickness, v.FootingLength, v.FootingWidth, v.FootingThickness, v.Piles, v.PileDiameter, v.PileLength);
    private static StructuralLine FromNative(NativeBridge.StructuralLine v) => new(v.Kind, v.X1, v.Z1, v.X2, v.Z2, v.Count, v.Area, v.Force);
    private static NativeBridge.StructuralLine ToNative(StructuralLine v) => new(v.Kind, v.X1, v.Z1, v.X2, v.Z2, v.Count, v.Area, v.Force);
    private static AdvancedGeometry FromNative(NativeBridge.AdvancedGeometry v) => new(v.Orthotropic, v.GlobalBeam, v.MainSpan, v.Rise, v.TowerHeight, v.DeckSteel, v.StructureSteel, v.CableSteel, v.AnchorVolume, v.TowerVolume, v.HorizontalForce, v.Members.Select(FromNative).ToArray(), v.Dimensions.Select(FromNative).ToArray()) { FoundationSchedule = v.FoundationSchedule.Select(FromNative).ToArray() };
    private static NativeBridge.AdvancedGeometry ToNative(AdvancedGeometry v) => new(v.Orthotropic, v.GlobalBeam, v.MainSpan, v.Rise, v.TowerHeight, v.DeckSteel, v.StructureSteel, v.CableSteel, v.AnchorVolume, v.TowerVolume, v.HorizontalForce, v.Members.Select(ToNative).ToArray(), v.Dimensions.Select(ToNative).ToArray()) { FoundationSchedule = v.FoundationSchedule.Select(ToNative).ToArray() };
    private static OptimizationTrial FromNative(NativeBridge.OptimizationTrial v) => new(v.Iteration, v.Family, v.Spans, v.Depth, v.PileLength, v.Cost, v.Carbon, v.Pier, v.Foundation, v.Continuous, v.Exclusions.ToArray());
    private static NativeBridge.OptimizationTrial ToNative(OptimizationTrial v) => new(v.Iteration, v.Family, v.Spans, v.Depth, v.PileLength, v.Cost, v.Carbon, v.Pier, v.Foundation, v.Continuous, v.Exclusions.ToArray());
    private static NativeBridge.OptimizationOptions ToNative(OptimizationOptions v) => new()
    {
        Objective = (NativeBridge.OptimizationObjective)v.Objective,
        KeepFamily = v.KeepFamily,
        KeepSpans = v.KeepSpans,
        KeepDepth = v.KeepDepth,
        KeepSection = v.KeepSection,
        KeepPier = v.KeepPier,
        KeepFoundation = v.KeepFoundation,
        KeepContinuity = v.KeepContinuity,
        MinSpans = v.MinSpans,
        MaxSpans = v.MaxSpans,
        MaxDepth = v.MaxDepth,
        MinDepth = v.MinDepth,
        DepthPercentMin = v.DepthPercentMin,
        DepthPercentMax = v.DepthPercentMax,
        DepthPercentStep = v.DepthPercentStep,
        PilePercentMin = v.PilePercentMin,
        PilePercentMax = v.PilePercentMax,
        PilePercentStep = v.PilePercentStep,
        Alternatives = v.Alternatives,
    };
}
