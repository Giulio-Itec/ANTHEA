using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public sealed record TechnicalItem(string Component, string Symbol, double Value, string Unit, string Origin, string Note);
    public sealed record SpanGeometry(int Number, double Start, double End, double Length, int Elements, double DevelopedLength);
    public sealed record SupportGeometry(int Number, string Type, double X, double Height, int Columns, double Size,
        double WallWidth, double CapLength, double CapWidth, double CapThickness, double FootingLength, double FootingWidth,
        double FootingThickness, int Piles, double PileDiameter, double PileLength);
}
