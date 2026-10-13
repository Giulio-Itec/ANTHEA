using Anthea.Application.Concrete;
using GPC.Checkers.CompositeBridge;
namespace Anthea.Application.Presentation;

public sealed record SectionPoint(double X, double Y);
public sealed record SectionLine(SectionPoint Start, SectionPoint End);
public sealed record NeutralAxisPresentation(SectionLine? Line, string Caption, string Explanation)
{
    public static NeutralAxisPresentation Concrete(ConcreteResults.StressState state) => Project(SectionNeutralAxis.Concrete(state.Source.Native.StrainPlane));
    internal static NeutralAxisPresentation Bridge(BridgeStage stage) => Project(SectionNeutralAxis.Bridge(stage));
    public static NeutralAxisPresentation Bridge(Anthea.Application.Bridge.BridgeStage stage) => stage.NeutralAxis;
    private static NeutralAxisPresentation Project(SectionNeutralAxis value) => new(value.Line is { } line
        ? new(new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y)) : null, value.Caption, value.Explanation);
}
