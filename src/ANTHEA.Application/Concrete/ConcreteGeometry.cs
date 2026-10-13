using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

public sealed record ReinforcementBar(double X, double Y, double Area, double Diametro);

/// <summary>Owned geometry for drawing and annotation; contains no solver, material law or lazy calculation.
/// Coordinates, bar order and polygon subdivisions are copied exactly from the existing geometry.</summary>
public sealed class ConcreteGeometry
{
    public JsonObject Input { get; }
    public string Shape { get; }
    public double Radius { get; }
    public double BarRadius { get; }
    public double CentroidY { get; }
    public double AreaCls { get; }
    public double AreaSteel { get; }
    public double Width { get; }
    public double Height { get; }
    public int CircularSides { get; }
    public List<double[]> Outline { get; }
    public List<double[][]> Holes { get; }
    public List<ReinforcementBar> Bars { get; }

    internal ConcreteGeometry(SezioneCA source)
    {
        Input = (JsonObject)source.Input.DeepClone(); Shape = source.Shape;
        Radius = source.Radius; BarRadius = source.BarRadius; CentroidY = source.CentroidY;
        AreaCls = source.AreaCls; AreaSteel = source.AreaSteel; Width = source.Width; Height = source.Height;
        CircularSides = source.CircularSides;
        Outline = source.Outline.Select(p => p.ToArray()).ToList();
        Holes = source.Holes.Select(h => h.Select(p => p.ToArray()).ToArray()).ToList();
        Bars = source.Bars.Select(b => new ReinforcementBar(b.X, b.Y, b.Area, b.Diametro)).ToList();
    }
}
