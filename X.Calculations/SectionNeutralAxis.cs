using GPC.Checkers.Concrete.Results;
using GPC.Checkers.CompositeBridge;
using GPC.Geometry;

namespace Anthea.Calculations;

/// <summary>Adapter for native section results; no equilibrium analysis is repeated.</summary>
public sealed record SectionNeutralAxis(Line2d? Line, string Caption, string Explanation)
{
    public static SectionNeutralAxis Concrete(StrainPlane plane) => new(plane.GetNeutralAxis(),
        "Asse neutro · ε = 0", "Piano delle deformazioni della combinazione selezionata.");

    public static SectionNeutralAxis Bridge(BridgeStage stage)
    {
        if (stage.GetHistory() is { } history)
        {
            var p = history.State.TotalPlane;
            var plane = new StrainPlane(0, -p.Curvature, new Point2d(0, 0), p.AxialStrain);
            return new(plane.GetNeutralAxis(), "Asse neutro · ε totale = 0",
                "Piano totale dello storico. Ritiro, attivazione e plasticità possono separare questo asse dagli zeri delle tensioni dei materiali.");
        }
        return new(stage.SteelNeutralAxis is { } y && double.IsFinite(y)
                ? new Line2d(new Point2d(0, y), new Point2d(1, y)) : null,
            "Asse neutro · σa = 0", "Zero delle tensioni cumulate nell’acciaio; non è lo zero delle tensioni nella soletta.");
    }
}
