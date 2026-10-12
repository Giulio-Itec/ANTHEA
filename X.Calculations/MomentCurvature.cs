using NativeCurve = GPC.Checkers.Concrete.Response;

namespace Anthea.Calculations;

public sealed record MomentCurvatureRequest(double AxialKn, double DirectionDegrees, int Steps, double EndFraction = 1, bool QuadraticSampling = true, double AxialToleranceKn = 1, int YieldRefinementSteps = 12);
public sealed record MomentCurvaturePoint(double Moment, double Mx, double My, double Curvature, double GradientX,
    double GradientY, double Epsilon0, double ConcreteCompressionStrain, double SteelStrain, bool Yielded, bool Limit);
public sealed record MomentCurvatureResult(IReadOnlyList<MomentCurvaturePoint> Points, double LimitMoment, double? YieldCurvature,
    double? UltimateCurvature, string Status)
{
    public double LimitAxialKn { get; init; }
    public double AxialResidualKn { get; init; }
}
/// <summary>Native equilibrium is injected. Portable orchestration, no UI or file dependencies.</summary>
public interface IMomentCurvatureCalculator
{
    MomentCurvatureResult Calculate(MomentCurvatureRequest request, Func<ActionPoint,DomainCheck> resistance,
        Func<ActionPoint,CheckerStressState> response, double steelYieldStrain, CancellationToken token=default);
}
public sealed class MomentCurvatureCalculator : IMomentCurvatureCalculator
{
    public MomentCurvatureResult Calculate(MomentCurvatureRequest p, Func<ActionPoint,DomainCheck> resistance,
        Func<ActionPoint,CheckerStressState> response, double steelYieldStrain, CancellationToken token=default)
    {
        // The native strains use 1/mm and absolute strain; only output units are converted.
        NativeCurve.MomentCurvatureStrains Strains(CheckerStressState state)
        {
            var plane = state.Native.StrainPlane;
            return new(plane.ChiX, plane.ChiY, plane.StrainReferencePoint,
                state.BarStrains.Select(Math.Abs).DefaultIfEmpty(0).Max()/1000,
                state.ConcreteVertices.Min(v => v.Strain)/1000);
        }
        NativeCurve.MomentCurvatureLimit Limit(double n, double x, double y)
        {
            var check = resistance(new(n,x,y));
            if (check.Resistance is not ActionPoint rd || check.LimitState is null) return null!;
            return new(rd.N,rd.Mx,rd.My,() => Strains(check.LimitState.Value));
        }
        NativeCurve.MomentCurvatureResult r;
        try
        {
            r = NativeCurve.MomentCurvatureAnalysis.Calculate(
                new(p.AxialKn,p.DirectionDegrees,p.Steps,p.EndFraction,p.QuadraticSampling,p.AxialToleranceKn,p.YieldRefinementSteps),
                Limit, (n,x,y) => Strains(response(new(n,x,y))), steelYieldStrain,
                new NativeCurve.MomentCurvatureUnits("kN","kNm"), token);
        }
        catch (NativeCurve.MomentCurvatureException e)
        {
            throw new ArgumentException(e.Reason switch
            {
                NativeCurve.MomentCurvatureRejection.InvalidRequest => "M–χ: 10–500 passi, frazione finale > 0 e ≤ 1; N e direzione finiti.",
                NativeCurve.MomentCurvatureRejection.LimitPointNotAvailable => "Punto limite non disponibile al N assegnato.",
                NativeCurve.MomentCurvatureRejection.AxialResidual => $"Residuo N al limite superiore alla tolleranza {e.AxialTolerance:G6} kN: N richiesto = {e.AxialForce:G9} kN; N limite = {e.LimitAxialForce:G9} kN.",
                NativeCurve.MomentCurvatureRejection.NonPositiveLimitMoment => "Momento limite non positivo nella direzione richiesta.",
                _ => e.Message
            });
        }
        string Message(string message) => message == "Response not finite." ? "Risposta non finita." : message;
        string status = "Ramo a momento crescente, N costante. Leggi e coefficienti dei materiali della sezione.";
        if (r.InterruptedAtStep is int step) status += $" Interrotta al passo {step}: {Message(r.InterruptionMessage)}";
        if (r.YieldRefinement is {} refinement)
            status += refinement.Applied
                ? $" Primo snervamento raffinato con {refinement.Bisections} bisezioni, My = {refinement.Moment:G7} kNm."
                : " Snervamento riferito al primo campione: raffinamento interrotto, " + Message(refinement.InterruptionMessage);
        var points = r.Points.Select(v => new MomentCurvaturePoint(v.Moment,v.Mx,v.My,v.Curvature*1000,
            v.GradientX*1000,v.GradientY*1000,v.ReferenceStrain*1000,v.ConcreteCompressionStrain*1000,v.SteelStrain*1000,v.Yielded,v.Limit)).ToArray();
        return new(points,r.LimitMoment,r.YieldCurvature*1000,r.UltimateCurvature*1000,
            status+$" N del punto limite = {r.LimitAxialForce:G9} kN; residuo = {r.AxialResidual:G6} kN (tolleranza assegnata {p.AxialToleranceKn:G6} kN). χ è il modulo del gradiente [1/m]; la direzione del momento è imposta, quella della curvatura può differire nelle sezioni asimmetriche. Nessun ramo post-picco.")
        { LimitAxialKn=r.LimitAxialForce, AxialResidualKn=r.AxialResidual };
    }
}
