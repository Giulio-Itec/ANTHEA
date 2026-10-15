using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Native = Anthea.Calculations;
namespace Anthea.Application.Concrete;

public static partial class ConcreteResults
{
    public sealed class StressState
    {
        internal Native.CheckerStressState Source { get; }
        public double sigma_cls { get; }
        public double sigma_acciaio { get; }
        public double[] tensioni_barre { get; }
        public double[] FiberStresses { get; }
        public double? Ratio { get; }
        public string Status { get; }
        public SectionState? Response { get; }
        public double ConcreteCompressionStrength { get; }
        public double ConcreteTensionStrength { get; }
        public double? ConcreteStressLimit { get; }
        public double SteelStressLimit { get; }
        public StressPoint[] ConcreteVertices { get; }
        public double[] BarStrains { get; }
        public double[] BarStrengths { get; }
        public double[] FiberStrains { get; }
        private readonly Lazy<StressRaster?> raster;
        public StressRaster? Raster => raster.Value;
        public bool IsRasterCreated => Source.IsRasterCreated;
        internal StressState(Native.CheckerStressState value)
        {
            Source = value;
            sigma_cls = value.sigma_cls;
            sigma_acciaio = value.sigma_acciaio;
            tensioni_barre = value.tensioni_barre.Select(item0 => item0).ToArray();
            FiberStresses = value.FiberStresses.Select(item0 => item0).ToArray();
            Ratio = value.Ratio;
            Status = value.Status;
            Response = value.Response is null ? null : new SectionState(value.Response);
            ConcreteCompressionStrength = value.ConcreteCompressionStrength;
            ConcreteTensionStrength = value.ConcreteTensionStrength;
            ConcreteStressLimit = value.ConcreteStressLimit;
            SteelStressLimit = value.SteelStressLimit;
            ConcreteVertices = value.ConcreteVertices.Select(item0 => new StressPoint(item0)).ToArray();
            BarStrains = value.BarStrains.Select(item0 => item0).ToArray();
            BarStrengths = value.BarStrengths.Select(item0 => item0).ToArray();
            FiberStrains = value.FiberStrains.Select(item0 => item0).ToArray();
            raster = new(() => value.Raster is { } sampled ? new StressRaster(sampled) : null);
        }
    }

    public sealed class CrackResult
    {
        internal Native.Ntc2018Checks.CrackResult Source { get; }
        public double? Width { get; }
        public double? Limit { get; }
        public double? Ratio { get; }
        public bool? Passed { get; }
        public string Status { get; }
        public double? EffectiveArea { get; }
        public double? EffectiveSteel { get; }
        public double? BarSpacing { get; }
        public string? SpacingSource { get; }
        public Detail[] Details { get; }
        public EffectiveRegion[] Regions { get; }
        internal CrackResult(Native.Ntc2018Checks.CrackResult value)
        {
            Source = value;
            Width = value.Width;
            Limit = value.Limit;
            Ratio = value.Ratio;
            Passed = value.Passed;
            Status = value.Status;
            EffectiveArea = value.EffectiveArea;
            EffectiveSteel = value.EffectiveSteel;
            BarSpacing = value.BarSpacing;
            SpacingSource = value.SpacingSource;
            Details = value.Details.Select(item0 => new Detail(item0)).ToArray();
            Regions = value.Regions.Select(item0 => new EffectiveRegion(item0)).ToArray();
        }
    }

    public sealed class EffectiveRegion
    {
        internal Native.ConcreteEffectiveRegion Source { get; }
        public string Name { get; }
        public double Qx { get; }
        public double Qy { get; }
        public double Level { get; }
        public double Area { get; }
        public int[] BarIndices { get; }
        public double SteelArea { get; }
        public double? Width { get; }
        public double[][] Outline { get; }
        public double[][][] Holes { get; }
        internal EffectiveRegion(Native.ConcreteEffectiveRegion value)
        {
            Source = value;
            Name = value.Name;
            Qx = value.Qx;
            Qy = value.Qy;
            Level = value.Level;
            Area = value.Area;
            BarIndices = value.BarIndices.Select(item0 => item0).ToArray();
            SteelArea = value.SteelArea;
            Width = value.Width;
            Outline = value.Outline.Select(item0 => item0.Select(item1 => item1).ToArray()).ToArray();
            Holes = value.Holes.Select(item0 => item0.Select(item1 => item1.Select(item2 => item2).ToArray()).ToArray()).ToArray();
        }
    }

    public sealed class StressPoint
    {
        internal Native.StressPoint Source { get; }
        public string Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Stress { get; }
        public double Strain { get; }
        internal StressPoint(Native.StressPoint value)
        {
            Source = value;
            Id = value.Id;
            X = value.X;
            Y = value.Y;
            Stress = value.Stress;
            Strain = value.Strain;
        }
    }

    public sealed class StressRaster
    {
        internal Native.StressRaster Source { get; }
        public int Size { get; }
        public double XMin { get; }
        public double YMin { get; }
        public double XMax { get; }
        public double YMax { get; }
        public double[] Stresses { get; }
        public double[] Strains { get; }
        internal StressRaster(Native.StressRaster value)
        {
            Source = value;
            Size = value.Size;
            XMin = value.XMin;
            YMin = value.YMin;
            XMax = value.XMax;
            YMax = value.YMax;
            Stresses = value.Stresses.Select(item0 => item0).ToArray();
            Strains = value.Strains.Select(item0 => item0).ToArray();
        }
    }

    public sealed class Segment
    {
        internal Native.DomainSegment Source { get; }
        public Action A { get; }
        public Action B { get; }
        internal Segment(Native.DomainSegment value)
        {
            Source = value;
            A = new Action(value.A);
            B = new Action(value.B);
        }
    }

    public sealed class Check
    {
        internal Native.DomainCheck Source { get; }
        public double? Utilization { get; }
        public Action? Resistance { get; }
        public string Status { get; }
        public SectionState? Response { get; }
        public Lazy<StressState>? LimitState { get; }
        internal Check(Native.DomainCheck value)
        {
            Source = value;
            Utilization = value.Utilization;
            Resistance = value.Resistance is { } item0 ? new Action(item0) : null;
            Status = value.Status;
            Response = value.Response is null ? null : new SectionState(value.Response);
            LimitState = value.LimitState is { } lazy ? new(() => new StressState(lazy.Value)) : null;
        }
    }

    public sealed class Mesh
    {
        internal Native.SectionDomainMesh Source { get; }
        public List<Action> Vertices { get; }
        public List<int> Triangles { get; }
        public string Mode { get; }
        public Action Scale { get; }
        internal Mesh(Native.SectionDomainMesh value)
        {
            Source = value;
            Vertices = value.Vertices.Select(item0 => new Action(item0)).ToList();
            Triangles = value.Triangles.Select(item0 => item0).ToList();
            Mode = value.Mode;
            Scale = new Action(value.Scale);
        }
    }

    public sealed class Domain2D
    {
        internal Native.CheckerDomain2D Source { get; }
        public IReadOnlyList<Segment> Segments { get; }
        internal Domain2D(Native.CheckerDomain2D source)
        { Source = source; Segments = source.Segments.Select(s => new Segment(s)).ToArray(); }
        internal double[] Project(Action point) => Source.Project(point.Source);
        /// <summary>Punto del grafico di un'azione: quello verificato, con la proiezione sul meridiano di θ se attiva (CheckerDomain2D.ProjectAction).</summary>
        internal double[] ProjectAction(Action point) => Source.ProjectAction(point.Source);
        internal Action LocalAction(Action point) => new(Source.LocalAction(point.Source));
    }
    public sealed class Domain3D
    {
        internal Native.CheckerDomain3D Source { get; }
        private readonly ConditionalWeakTable<Native.SectionDomainMesh, Mesh> meshes = new();
        internal Domain3D(Native.CheckerDomain3D source) => Source = source;
        public bool IsMeshCreated => Source.IsMeshCreated;
        public Mesh Mesh => DisplayMesh(Source.Section.Options);
        public Mesh DisplayMesh(JsonObject options) => meshes.GetValue(Source.DisplayMesh(options), mesh => new(mesh));
        internal Action LocalAction(Action point) => new(Native.CheckerSection.Point(Source.Section.Force(point.Source)));
    }
}

/// <summary>One projection cache per analysis session; it never samples a mesh or stress raster on its own.</summary>
internal sealed class ConcreteProjection
{
    private readonly ConditionalWeakTable<Native.CheckerDomain3D, ConcreteResults.Domain3D> three = new();
    private readonly ConditionalWeakTable<Native.CheckerDomain2D, ConcreteResults.Domain2D> two = new();
    private readonly ConditionalWeakTable<Native.CheckerStressState, ConcreteResults.StressState> states = new();
    internal ConcreteResults.Domain3D? Read(Native.CheckerDomain3D? value) => value is null ? null : three.GetValue(value, v => new(v));
    internal ConcreteResults.Domain2D? Read(Native.CheckerDomain2D? value) => value is null ? null : two.GetValue(value, v => new(v));
    internal ConcreteResults.StressState Read(Native.CheckerStressState value) => states.GetValue(value, v => new(v));
}
