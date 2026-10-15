// Owned values used by wall and global-stability views. Native queries stay internal.
using System.Text.Json.Nodes;
namespace Anthea.Application.Walls;

public static partial class WallModel
{
    public sealed class AppliedAction
    {
        public string Name { get; }
        public string Type { get; }
        public double Value { get; }
        public double Z { get; }
        public double Z0 { get; }
        public double X { get; }
        internal global::Anthea.Calculations.RetainingWall.AppliedAction Source { get; }
        public string Id { get; }
        public double Factor { get; }
        internal AppliedAction(global::Anthea.Calculations.RetainingWall.AppliedAction source)
        {
            Source = source;
            Name = source.Name;
            Type = source.Type;
            Value = source.Value;
            Z = source.Z;
            Z0 = source.Z0;
            X = source.X;

            Id = source.Id;
            Factor = source.Factor;
        }
    }

    public sealed class BarDetail
    {
        public double Spacing { get; }
        internal global::Anthea.Calculations.RetainingWall.BarDetail Source { get; }
        public string Mark { get; }
        public string Zone { get; }
        public string Face { get; }
        public double Diameter { get; }
        public double Count { get; }
        public double Area { get; }
        public double Length { get; }
        public double RequiredAnchor { get; }
        public double Anchor { get; }
        public double RequiredLap { get; }
        public double Lap { get; }
        public double Mandrel { get; }
        public double Fbd { get; }
        public IReadOnlyList<WallModel.BarPoint> Path { get; }
        internal BarDetail(global::Anthea.Calculations.RetainingWall.BarDetail source)
        {
            Source = source;
            Spacing = source.Spacing;

            Mark = source.Mark;
            Zone = source.Zone;
            Face = source.Face;
            Diameter = source.Diameter;
            Count = source.Count;
            Area = source.Area;
            Length = source.Length;
            RequiredAnchor = source.RequiredAnchor;
            Anchor = source.Anchor;
            RequiredLap = source.RequiredLap;
            Lap = source.Lap;
            Mandrel = source.Mandrel;
            Fbd = source.Fbd;
            Path = source.Path.Select(item0 => new WallModel.BarPoint(item0)).ToArray();
        }
    }

    public sealed class BarLeg
    {
        internal global::Anthea.Calculations.RetainingWall.BarLeg Source { get; }
        public string Name { get; }
        public double Length { get; }
        internal BarLeg(global::Anthea.Calculations.RetainingWall.BarLeg source)
        {
            Source = source;
            Name = source.Name;
            Length = source.Length;
        }
    }

    public sealed class BarPoint
    {
        internal global::Anthea.Calculations.RetainingWall.BarPoint Source { get; }
        public double X { get; }
        public double Y { get; }
        internal BarPoint(global::Anthea.Calculations.RetainingWall.BarPoint source)
        {
            Source = source;
            X = source.X;
            Y = source.Y;
        }
    }

    public sealed class BarSchedule
    {
        internal global::Anthea.Calculations.RetainingWall.BarSchedule Source { get; }
        public double PanelLength { get; }
        public double EndCover { get; }
        public double StockLength { get; }
        public IReadOnlyList<WallModel.ScheduledBar> Bars { get; }
        public IReadOnlyList<string> Warnings { get; }
        public double KnownWeight { get; }
        public bool CompleteQuantities { get; }
        internal BarSchedule(global::Anthea.Calculations.RetainingWall.BarSchedule source)
        {
            Source = source;
            PanelLength = source.PanelLength;
            EndCover = source.EndCover;
            StockLength = source.StockLength;
            Bars = source.Bars.Select(item0 => new WallModel.ScheduledBar(item0)).ToArray();
            Warnings = source.Warnings.Select(item0 => item0).ToArray();
            KnownWeight = source.KnownWeight;
            CompleteQuantities = source.CompleteQuantities;
        }
    }

    public sealed class Check
    {
        internal global::Anthea.Calculations.RetainingWall.Check Source { get; }
        public string Name { get; }
        public string Combination { get; }
        public double Demand { get; }
        public double? Resistance { get; }
        public string Unit { get; }
        public double? Ratio { get; }
        public string Status { get; }
        public string? Member { get; }
        public double? Position { get; }
        /// <summary>False when the check has no computed demand (shown as «—»).</summary>
        [System.Text.Json.Serialization.JsonIgnore] public bool HasDemand => Source.HasDemand;
        [System.Text.Json.Serialization.JsonIgnore] public bool IsSatisfied => Source.IsSatisfied;
        [System.Text.Json.Serialization.JsonIgnore] public bool IsNotSatisfied => Source.IsNotSatisfied;
        [System.Text.Json.Serialization.JsonIgnore] public bool IsNotEvaluated => Source.IsNotEvaluated;
        internal Check(global::Anthea.Calculations.RetainingWall.Check source)
        {
            Source = source;
            Name = source.Name;
            Combination = source.Combination;
            Demand = source.Demand;
            Resistance = source.Resistance;
            Unit = source.Unit;
            Ratio = source.Ratio;
            Status = source.Status;
            Member = source.Member;
            Position = source.Position;
        }
    }

    public sealed class Contact
    {
        internal global::Anthea.Calculations.RetainingWall.Contact Source { get; }
        public double Start { get; }
        public double End { get; }
        public double Toe { get; }
        public double Heel { get; }
        public double Peak { get; }
        public bool Valid { get; }
        internal Contact(global::Anthea.Calculations.RetainingWall.Contact source)
        {
            Source = source;
            Start = source.Start;
            End = source.End;
            Toe = source.Toe;
            Heel = source.Heel;
            Peak = source.Peak;
            Valid = source.Valid;
        }
    }

    public sealed class DisplacementPoint
    {
        internal global::Anthea.Calculations.RetainingWall.DisplacementPoint Source { get; }
        public double Y { get; }
        public double Curvature { get; }
        public double Rotation { get; }
        public double DisplacementMm { get; }
        internal DisplacementPoint(global::Anthea.Calculations.RetainingWall.DisplacementPoint source)
        {
            Source = source;
            Y = source.Y;
            Curvature = source.Curvature;
            Rotation = source.Rotation;
            DisplacementMm = source.DisplacementMm;
        }
    }

    public sealed class Family
    {
        internal global::Anthea.Calculations.RetainingWall.Family Source { get; }
        public string Id { get; }
        public string Name { get; }
        public bool Available { get; }
        internal Family(global::Anthea.Calculations.RetainingWall.Family source)
        {
            Source = source;
            Id = source.Id;
            Name = source.Name;
            Available = source.Available;
        }
    }

    public sealed class LoadCase
    {
        public double WallFactor { get; }
        public double SoilFactor { get; }
        public double LiveFactor { get; }
        public double WaterFactor { get; }
        public double X { get; }
        internal global::Anthea.Calculations.RetainingWall.LoadCase Source { get; }
        public string Name { get; }
        public string State { get; }
        public double Kh { get; }
        public double Kv { get; }
        public double Horizontal { get; }
        public double Vertical { get; }
        public double Uplift { get; }
        public double Stabilizing { get; }
        public double Overturning { get; }
        public double Eccentricity { get; }
        public double EffectiveWidth { get; }
        public WallModel.Contact Contact { get; }
        public double SlidingResistance { get; }
        public double OverturningResistance { get; }
        public double? BearingResistance { get; }
        public List<WallModel.PressureSegment> Pressures { get; }
        public List<WallModel.SectionForce> Sections { get; }
        public JsonObject? Factors { get; }
        public List<WallModel.PressureDetail> PressureDetails { get; }
        public List<WallModel.AppliedAction> Actions { get; }
        public JsonObject SoilAudit { get; }
        public List<WallModel.PressureSegment> StemPressures { get; }
        public List<WallModel.PressureSegment> ValleyPressures { get; }
        public List<WallModel.PressureDetail> StemPressureDetails { get; }
        public SeismicBearing? SeismicBearing { get; }
        public string? SeismicBearingError { get; }
        /// <summary>False in the twin rows «b»: variable surcharge in the thrust only, absent from the heel (MUR-1).</summary>
        public bool VariableSurchargeOnHeel => Source.VariableSurchargeOnHeel;
        /// <summary>γR,e of the passive in front (MUR-2).</summary>
        public double PassiveFactor => Source.PassiveFactor;
        /// <summary>Demands of the sliding (kN/m) and overturning (kNm/m) checks, never reduced by the passive (MUR-2).</summary>
        public double SlidingDemand => Source.SlidingDemand;
        public double OverturningDemand => Source.OverturningDemand;
        internal LoadCase(global::Anthea.Calculations.RetainingWall.LoadCase source)
        {
            Source = source;
            WallFactor = source.WallFactor;
            SoilFactor = source.SoilFactor;
            LiveFactor = source.LiveFactor;
            WaterFactor = source.WaterFactor;
            X = source.X;

            Name = source.Name;
            State = source.State;
            Kh = source.Kh;
            Kv = source.Kv;
            Horizontal = source.Horizontal;
            Vertical = source.Vertical;
            Uplift = source.Uplift;
            Stabilizing = source.Stabilizing;
            Overturning = source.Overturning;
            Eccentricity = source.Eccentricity;
            EffectiveWidth = source.EffectiveWidth;
            Contact = new WallModel.Contact(source.Contact);
            SlidingResistance = source.SlidingResistance;
            OverturningResistance = source.OverturningResistance;
            BearingResistance = source.BearingResistance;
            Pressures = source.Pressures.Select(item0 => new WallModel.PressureSegment(item0)).ToList();
            Sections = source.Sections.Select(item0 => new WallModel.SectionForce(item0)).ToList();
            Factors = (JsonObject)source.Factors.DeepClone();
            PressureDetails = source.PressureDetails.Select(item0 => new WallModel.PressureDetail(item0)).ToList();
            Actions = source.Actions.Select(item0 => new WallModel.AppliedAction(item0)).ToList();
            SoilAudit = source.SoilAudit is { } audit ? (JsonObject)audit.DeepClone() : null;
            StemPressures = source.StemPressures.Select(item0 => new WallModel.PressureSegment(item0)).ToList();
            ValleyPressures = source.ValleyPressures.Select(item0 => new WallModel.PressureSegment(item0)).ToList();
            StemPressureDetails = source.StemPressureDetails.Select(item0 => new WallModel.PressureDetail(item0)).ToList();
            SeismicBearing = (source.SeismicBearing is { } valueSeismicBearing ? new SeismicBearing(valueSeismicBearing) : null);
            SeismicBearingError = source.SeismicBearingError;
        }
    }

    public sealed class Parameter
    {
        public double Default { get; }
        public double Min { get; }
        public double Max { get; }
        internal global::Anthea.Calculations.RetainingWall.Parameter Source { get; }
        public string Key { get; }
        public string Label { get; }
        public string Symbol { get; }
        public string Unit { get; }
        internal Parameter(global::Anthea.Calculations.RetainingWall.Parameter source)
        {
            Source = source;
            Default = source.Default;
            Min = source.Min;
            Max = source.Max;

            Key = source.Key;
            Label = source.Label;
            Symbol = source.Symbol;
            Unit = source.Unit;
        }
    }

    public sealed class PressureDetail
    {
        internal global::Anthea.Calculations.RetainingWall.PressureDetail Source { get; }
        public double Z0 { get; }
        public double Z1 { get; }
        public double Phi { get; }
        public double PhiDesign { get; }
        public double K { get; }
        public double Ke { get; }
        public double Sigma0 { get; }
        public double Sigma1 { get; }
        public double Soil0 { get; }
        public double Soil1 { get; }
        public double Surcharge { get; }
        public double Water0 { get; }
        public double Water1 { get; }
        public double Dynamic { get; }
        public double Total0 { get; }
        public double Total1 { get; }
        internal PressureDetail(global::Anthea.Calculations.RetainingWall.PressureDetail source)
        {
            Source = source;
            Z0 = source.Z0;
            Z1 = source.Z1;
            Phi = source.Phi;
            PhiDesign = source.PhiDesign;
            K = source.K;
            Ke = source.Ke;
            Sigma0 = source.Sigma0;
            Sigma1 = source.Sigma1;
            Soil0 = source.Soil0;
            Soil1 = source.Soil1;
            Surcharge = source.Surcharge;
            Water0 = source.Water0;
            Water1 = source.Water1;
            Dynamic = source.Dynamic;
            Total0 = source.Total0;
            Total1 = source.Total1;
        }
    }

    public sealed class PressureSegment
    {
        internal global::Anthea.Calculations.RetainingWall.PressureSegment Source { get; }
        public double Z0 { get; }
        public double Z1 { get; }
        public double P0 { get; }
        public double P1 { get; }
        internal PressureSegment(global::Anthea.Calculations.RetainingWall.PressureSegment source)
        {
            Source = source;
            Z0 = source.Z0;
            Z1 = source.Z1;
            P0 = source.P0;
            P1 = source.P1;
        }
    }

    public sealed class ReinforcementDetails
    {
        internal global::Anthea.Calculations.RetainingWall.ReinforcementDetails Source { get; }
        public IReadOnlyList<WallModel.BarDetail> Bars { get; }
        public IReadOnlyList<WallModel.Check> Checks { get; }
        public double SteelKg { get; }
        public string Note { get; }
        internal ReinforcementDetails(global::Anthea.Calculations.RetainingWall.ReinforcementDetails source)
        {
            Source = source;
            Bars = source.Bars.Select(item0 => new WallModel.BarDetail(item0)).ToArray();
            Checks = source.Checks.Select(item0 => new WallModel.Check(item0)).ToArray();
            SteelKg = source.SteelKg;
            Note = source.Note;
        }
    }

    public sealed class ReinforcementProposal
    {
        public bool Passed { get; }
        internal global::Anthea.Calculations.RetainingWall.ReinforcementProposal Source { get; }
        public JsonObject Input { get; }
        public IReadOnlyList<WallModel.Check> Checks { get; }
        public string Message { get; }
        internal ReinforcementProposal(global::Anthea.Calculations.RetainingWall.ReinforcementProposal source)
        {
            Source = source;
            Passed = source.Passed;

            Input = (JsonObject)source.Input.DeepClone();
            Checks = source.Checks.Select(item0 => new WallModel.Check(item0)).ToArray();
            Message = source.Message;
        }
    }

    public sealed class Result
    {
        public double Area { get; }
        internal global::Anthea.Calculations.RetainingWall.Result Source { get; }
        public JsonObject Input { get; }
        public double Width { get; }
        public double Volume { get; }
        public double SteelKg { get; }
        public List<WallModel.LoadCase> Cases { get; }
        public List<WallModel.Check> Checks { get; }
        public List<WallModel.Check> Structural { get; }
        public SlopeResult? GlobalStability { get; }
        public string? GlobalError { get; }
        public WallModel.ServiceResult? Serviceability { get; }
        public WallModel.ReinforcementDetails? Detailing { get; }
        internal Result(global::Anthea.Calculations.RetainingWall.Result source)
        {
            Source = source;
            Area = source.Area;

            Input = (JsonObject)source.Input.DeepClone();
            Width = source.Width;
            Volume = source.Volume;
            SteelKg = source.SteelKg;
            Cases = source.Cases.Select(item0 => new WallModel.LoadCase(item0)).ToList();
            Checks = source.Checks.Select(item0 => new WallModel.Check(item0)).ToList();
            Structural = source.Structural.Select(item0 => new WallModel.Check(item0)).ToList();
            GlobalStability = (source.GlobalStability is { } valueGlobalStability ? new SlopeResult(valueGlobalStability) : null);
            GlobalError = source.GlobalError;
            Serviceability = (source.Serviceability is { } valueServiceability ? new WallModel.ServiceResult(valueServiceability) : null);
            Detailing = (source.Detailing is { } valueDetailing ? new WallModel.ReinforcementDetails(valueDetailing) : null);
        }
        public JsonObject Json() => Source.Json();
    }

    public sealed class ScheduledBar
    {
        internal global::Anthea.Calculations.RetainingWall.ScheduledBar Source { get; }
        public string Mark { get; }
        public string Zone { get; }
        public string Face { get; }
        public string Kind { get; }
        public double Diameter { get; }
        public int Quantity { get; }
        public double? CutLength { get; }
        public double Spacing { get; }
        public double Mandrel { get; }
        public double Lap { get; }
        public IReadOnlyList<WallModel.BarLeg> Legs { get; }
        public IReadOnlyList<WallModel.BarPoint> Shape { get; }
        public string Note { get; }
        public double? TotalLength { get; }
        public double? Weight { get; }
        internal ScheduledBar(global::Anthea.Calculations.RetainingWall.ScheduledBar source)
        {
            Source = source;
            Mark = source.Mark;
            Zone = source.Zone;
            Face = source.Face;
            Kind = source.Kind;
            Diameter = source.Diameter;
            Quantity = source.Quantity;
            CutLength = source.CutLength;
            Spacing = source.Spacing;
            Mandrel = source.Mandrel;
            Lap = source.Lap;
            Legs = source.Legs.Select(item0 => new WallModel.BarLeg(item0)).ToArray();
            Shape = source.Shape.Select(item0 => new WallModel.BarPoint(item0)).ToArray();
            Note = source.Note;
            TotalLength = source.TotalLength;
            Weight = source.Weight;
        }
    }

    public sealed class SectionForce
    {
        internal global::Anthea.Calculations.RetainingWall.SectionForce Source { get; }
        public string Name { get; }
        public double Position { get; }
        public double Thickness { get; }
        public double N { get; }
        public double M { get; }
        public double V { get; }
        internal SectionForce(global::Anthea.Calculations.RetainingWall.SectionForce source)
        {
            Source = source;
            Name = source.Name;
            Position = source.Position;
            Thickness = source.Thickness;
            N = source.N;
            M = source.M;
            V = source.V;
        }
    }

    public sealed class SeismicDerivation
    {
        public double AgG { get; }
        public double Ss { get; }
        public double St { get; }
        public double AmaxG { get; }
        public double Beta { get; }
        public double BetaOverturning { get; }
        public double KhOverturning { get; }
        public double KvOverturning { get; }
        public string SoilFormula { get; }
        public string TopographyFormula { get; }
        internal global::Anthea.Calculations.RetainingWall.SeismicDerivation Source { get; }
        public double Kh { get; }
        public double Kv { get; }
        public string Description { get; }
        internal SeismicDerivation(global::Anthea.Calculations.RetainingWall.SeismicDerivation source)
        {
            Source = source;
            AgG = source.AgG;
            Ss = source.Ss;
            St = source.St;
            AmaxG = source.AmaxG;
            Beta = source.Beta;
            BetaOverturning = source.BetaOverturning;
            KhOverturning = source.KhOverturning;
            KvOverturning = source.KvOverturning;
            SoilFormula = source.SoilFormula;
            TopographyFormula = source.TopographyFormula;

            Kh = source.Kh;
            Kv = source.Kv;
            Description = source.Description;
        }
    }

    public sealed class SeismicDisplacementCase
    {
        internal global::Anthea.Calculations.RetainingWall.SeismicDisplacementCase Source { get; }
        public string Name { get; }
        public string State { get; }
        public double YieldG { get; }
        public double Scale { get; }
        public double LimitMm { get; }
        public SlidingHistory? History { get; }
        public string Status { get; }
        internal SeismicDisplacementCase(global::Anthea.Calculations.RetainingWall.SeismicDisplacementCase source)
        {
            Source = source;
            Name = source.Name;
            State = source.State;
            YieldG = source.YieldG;
            Scale = source.Scale;
            LimitMm = source.LimitMm;
            History = (source.History is { } valueHistory ? new SlidingHistory(valueHistory) : null);
            Status = source.Status;
        }
    }

    public sealed class ServiceCase
    {
        internal global::Anthea.Calculations.RetainingWall.ServiceCase Source { get; }
        public string Combination { get; }
        public double? ToeSettlement { get; }
        public double? CentreSettlement { get; }
        public double? HeelSettlement { get; }
        public double? FoundationRotation { get; }
        public double? StemDisplacement { get; }
        public double? HeadDisplacement { get; }
        public string Status { get; }
        public IReadOnlyList<SettlementSlice> Slices { get; }
        public IReadOnlyList<WallModel.DisplacementPoint> Shape { get; }
        internal ServiceCase(global::Anthea.Calculations.RetainingWall.ServiceCase source)
        {
            Source = source;
            Combination = source.Combination;
            ToeSettlement = source.ToeSettlement;
            CentreSettlement = source.CentreSettlement;
            HeelSettlement = source.HeelSettlement;
            FoundationRotation = source.FoundationRotation;
            StemDisplacement = source.StemDisplacement;
            HeadDisplacement = source.HeadDisplacement;
            Status = source.Status;
            Slices = source.Slices.Select(item0 => new SettlementSlice(item0)).ToArray();
            Shape = source.Shape.Select(item0 => new WallModel.DisplacementPoint(item0)).ToArray();
        }
    }

    public sealed class ServiceResult
    {
        internal global::Anthea.Calculations.RetainingWall.ServiceResult Source { get; }
        public IReadOnlyList<WallModel.ServiceCase> Cases { get; }
        public IReadOnlyList<WallModel.SeismicDisplacementCase> Earthquakes { get; }
        internal ServiceResult(global::Anthea.Calculations.RetainingWall.ServiceResult source)
        {
            Source = source;
            Cases = source.Cases.Select(item0 => new WallModel.ServiceCase(item0)).ToArray();
            Earthquakes = source.Earthquakes.Select(item0 => new WallModel.SeismicDisplacementCase(item0)).ToArray();
        }
    }

}
public sealed class SeismicBearing
{
    public double Capacity { get; }
    internal global::Anthea.Calculations.Geotechnics.SeismicBearing Source { get; }
    public double? Ratio { get; }
    public double NMax { get; }
    public double SoilInertia { get; }
    public double NBar { get; }
    public double VBar { get; }
    public double MBar { get; }
    public double VerticalLimit { get; }
    public double? Interaction { get; }
    public string Status { get; }
    internal SeismicBearing(global::Anthea.Calculations.Geotechnics.SeismicBearing source)
    {
        Source = source;
        Capacity = source.Capacity;

        Ratio = source.Ratio;
        NMax = source.NMax;
        SoilInertia = source.SoilInertia;
        NBar = source.NBar;
        VBar = source.VBar;
        MBar = source.MBar;
        VerticalLimit = source.VerticalLimit;
        Interaction = source.Interaction;
        Status = source.Status;
    }
}

public sealed class SettlementSlice
{
    internal global::Anthea.Calculations.Geotechnics.SettlementSlice Source { get; }
    public string Soil { get; }
    public double Top { get; }
    public double Bottom { get; }
    public double Stress { get; }
    public double Modulus { get; }
    public double SettlementMm { get; }
    internal SettlementSlice(global::Anthea.Calculations.Geotechnics.SettlementSlice source)
    {
        Source = source;
        Soil = source.Soil;
        Top = source.Top;
        Bottom = source.Bottom;
        Stress = source.Stress;
        Modulus = source.Modulus;
        SettlementMm = source.SettlementMm;
    }
}

public sealed class SlidingHistory
{
    public double PeakVelocity { get; }
    public double PgaG { get; }
    internal global::Anthea.Calculations.Geotechnics.SlidingHistory Source { get; }
    public double DisplacementMm { get; }
    public IReadOnlyList<SlidingState> Points { get; }
    internal SlidingHistory(global::Anthea.Calculations.Geotechnics.SlidingHistory source)
    {
        Source = source;
        PeakVelocity = source.PeakVelocity;
        PgaG = source.PgaG;

        DisplacementMm = source.DisplacementMm;
        Points = source.Points.Select(item0 => new SlidingState(item0)).ToArray();
    }
}

public sealed class SlidingState
{
    internal global::Anthea.Calculations.Geotechnics.SlidingState Source { get; }
    public double Time { get; }
    public double AccelerationG { get; }
    public double Velocity { get; }
    public double DisplacementMm { get; }
    internal SlidingState(global::Anthea.Calculations.Geotechnics.SlidingState source)
    {
        Source = source;
        Time = source.Time;
        AccelerationG = source.AccelerationG;
        Velocity = source.Velocity;
        DisplacementMm = source.DisplacementMm;
    }
}

public sealed class SlipCircle
{
    internal global::Anthea.Calculations.Geotechnics.SlipCircle Source { get; }
    public double X { get; }
    public double Y { get; }
    public double Radius { get; }
    public double Left { get; }
    public double Right { get; }
    internal SlipCircle(global::Anthea.Calculations.Geotechnics.SlipCircle source)
    {
        Source = source;
        X = source.X;
        Y = source.Y;
        Radius = source.Radius;
        Left = source.Left;
        Right = source.Right;
    }
    public double Base(double x) => Source.Base(x);
}

public sealed class SlopeCaseResult
{
    public int GeometricallyValid { get; }
    internal global::Anthea.Calculations.Geotechnics.SlopeCaseResult Source { get; }
    public SlopeFactors Factors { get; }
    public SlopeSurfaceResult? Critical { get; }
    public int Tried { get; }
    public int Solved { get; }
    public int NumericalFailures { get; }
    public bool Boundary { get; }
    public string Status { get; }
    internal SlopeCaseResult(global::Anthea.Calculations.Geotechnics.SlopeCaseResult source)
    {
        Source = source;
        GeometricallyValid = source.GeometricallyValid;

        Factors = new SlopeFactors(source.Factors);
        Critical = (source.Critical is { } valueCritical ? new SlopeSurfaceResult(valueCritical) : null);
        Tried = source.Tried;
        Solved = source.Solved;
        NumericalFailures = source.NumericalFailures;
        Boundary = source.Boundary;
        Status = source.Status;
    }
}

public sealed class SlopeFactors
{
    public double Soil { get; }
    public double Body { get; }
    public bool Undrained { get; }
    internal global::Anthea.Calculations.Geotechnics.SlopeFactors Source { get; }
    public string Name { get; }
    public double MPhi { get; }
    public double MC { get; }
    public double MCu { get; }
    public double R { get; }
    public double Kh { get; }
    public double Kv { get; }
    internal SlopeFactors(global::Anthea.Calculations.Geotechnics.SlopeFactors source)
    {
        Source = source;
        Soil = source.Soil;
        Body = source.Body;
        Undrained = source.Undrained;

        Name = source.Name;
        MPhi = source.MPhi;
        MC = source.MC;
        MCu = source.MCu;
        R = source.R;
        Kh = source.Kh;
        Kv = source.Kv;
    }
}

public sealed class SlopePoint
{
    internal global::Anthea.Calculations.Geotechnics.SlopePoint Source { get; }
    public double X { get; }
    public double Y { get; }
    public SlopePoint(double x, double y) : this(new global::Anthea.Calculations.Geotechnics.SlopePoint(x, y)) { }
    internal SlopePoint(global::Anthea.Calculations.Geotechnics.SlopePoint source)
    {
        Source = source;
        X = source.X;
        Y = source.Y;
    }
}

public sealed class SlopeResult
{
    internal global::Anthea.Calculations.Geotechnics.SlopeResult Source { get; }
    public SlopeSection Section { get; }
    public SlopeCaseResult[] Cases { get; }
    public string[] Notes { get; }
    internal SlopeResult(global::Anthea.Calculations.Geotechnics.SlopeResult source)
    {
        Source = source;
        Section = new SlopeSection(source.Section);
        Cases = source.Cases.Select(item0 => new SlopeCaseResult(item0)).ToArray();
        Notes = source.Notes.Select(item0 => item0).ToArray();
    }
}

public sealed class SlopeSection
{
    public double RequiredLeft { get; }
    public double RequiredRight { get; }
    public double SoilSplitX { get; }
    public double CoveredBottom { get; }
    internal global::Anthea.Calculations.Geotechnics.SlopeSection Source { get; }
    public SlopePoint[] Surface { get; }
    internal SlopeSection(global::Anthea.Calculations.Geotechnics.SlopeSection source)
    {
        Source = source;
        RequiredLeft = source.RequiredLeft;
        RequiredRight = source.RequiredRight;
        SoilSplitX = source.SoilSplitX;
        CoveredBottom = source.CoveredBottom;

        Surface = source.Surface.Select(item0 => new SlopePoint(item0)).ToArray();
    }
}

public sealed class SlopeSlice
{
    public double WeightX { get; }
    public double WeightY { get; }
    public double Vertical { get; }
    public double Driving { get; }
    internal global::Anthea.Calculations.Geotechnics.SlopeSlice Source { get; }
    public int Index { get; }
    public double Left { get; }
    public double Right { get; }
    public double BaseY { get; }
    public double TopY { get; }
    public double Alpha { get; }
    public string Soil { get; }
    public double SoilWeight { get; }
    public double BodyWeight { get; }
    public double VerticalLoad { get; }
    public double HorizontalLoad { get; }
    public double U { get; }
    public double Phi { get; }
    public double Cohesion { get; }
    public double NormalEffective { get; }
    public double Resistance { get; }
    public double Mobilized { get; }
    public double MAlpha { get; }
    internal SlopeSlice(global::Anthea.Calculations.Geotechnics.SlopeSlice source)
    {
        Source = source;
        WeightX = source.WeightX;
        WeightY = source.WeightY;
        Vertical = source.Vertical;
        Driving = source.Driving;

        Index = source.Index;
        Left = source.Left;
        Right = source.Right;
        BaseY = source.BaseY;
        TopY = source.TopY;
        Alpha = source.Alpha;
        Soil = source.Soil;
        SoilWeight = source.SoilWeight;
        BodyWeight = source.BodyWeight;
        VerticalLoad = source.VerticalLoad;
        HorizontalLoad = source.HorizontalLoad;
        U = source.U;
        Phi = source.Phi;
        Cohesion = source.Cohesion;
        NormalEffective = source.NormalEffective;
        Resistance = source.Resistance;
        Mobilized = source.Mobilized;
        MAlpha = source.MAlpha;
    }
}

public sealed class SlopeSurfaceResult
{
    internal global::Anthea.Calculations.Geotechnics.SlopeSurfaceResult Source { get; }
    public SlipCircle Circle { get; }
    public double Factor { get; }
    public double Ratio { get; }
    public int Iterations { get; }
    public double Residual { get; }
    public double Driving { get; }
    public double Resistance { get; }
    public SlopeSlice[] Slices { get; }
    internal SlopeSurfaceResult(global::Anthea.Calculations.Geotechnics.SlopeSurfaceResult source)
    {
        Source = source;
        Circle = new SlipCircle(source.Circle);
        Factor = source.Factor;
        Ratio = source.Ratio;
        Iterations = source.Iterations;
        Residual = source.Residual;
        Driving = source.Driving;
        Resistance = source.Resistance;
        Slices = source.Slices.Select(item0 => new SlopeSlice(item0)).ToArray();
    }
}

