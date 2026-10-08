namespace Anthea.Calculations;

// DTO della torsione della sezione (refactoring F2.5): contratto dell'adattatore ConcreteShearTorsionAdapter e del JSON 'torsione'
// di ConcreteAnalysis, con le unità di ANTHEA (kNm, kN, mm, mm², MPa). I nomi delle proprietà sono le chiavi del JSON: non cambiarli.
// Spostati senza modifiche da ConcreteTorsion.cs, che resta il motore legacy.
public sealed record TorsionGeometry(double Area, double Perimeter, double Thickness);
public sealed record TorsionInput(double TorqueKnM, TorsionGeometry Geometry, double Fcd, double Fyd,
    double StirrupLegArea, double Spacing, double AvailableLongitudinalArea, double CotTheta,
    double VxKn, double VyKn, Ntc2018Checks.ShearResult ShearX, Ntc2018Checks.ShearResult ShearY);
public sealed record TorsionResult(double TRcd, double TRsd, double TRld, double TRd, double? TorsionRatio,
    double? ConcreteCombinedRatio, double? SteelCombinedRatio, double RequiredLongitudinalArea, bool Passed, string Status)
{
    public TorsionGeometry? Geometry { get; init; }
    public double CotTheta { get; init; }
}
