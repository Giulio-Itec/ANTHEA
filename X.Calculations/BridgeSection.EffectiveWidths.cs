namespace Anthea.Calculations;
public static partial class BridgeSection
{
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy) => HBridgeSection.EffectiveWidths(g, stress, fy);
    public static BridgePlate InternalPlate(double b, double t, double startStress, double endStress, double fy) => HBridgeSection.InternalPlate(b, t, startStress, endStress, fy);
}
