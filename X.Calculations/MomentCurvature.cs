using NativeCurve = GPC.Checkers.Concrete.Response;

namespace Anthea.Calculations;

public sealed record MomentCurvatureRequest(double AxialKn, double DirectionDegrees, int Steps, double EndFraction = 1, bool QuadraticSampling = true, double AxialToleranceKn = 1, int YieldRefinementSteps = 12)
{
    /// <summary>Tolleranza assegnata dall'utente quando AxialToleranceKn è quella effettiva, cioè il massimo con la regola della libreria (SLU-3).</summary>
    public double? AssignedAxialToleranceKn { get; init; }
}
public sealed record MomentCurvaturePoint(double Moment, double Mx, double My, double Curvature, double GradientX,
    double GradientY, double Epsilon0, double ConcreteCompressionStrain, double SteelStrain, bool Yielded, bool Limit);
/// <summary>
/// Duttilità in curvatura μφ (NTC 2018 §4.1.2.3.4.2, bilineare della Circolare 2019 Fig. C4.1.12) della libreria (GPCChecker.Concrete 0.35.0.0,
/// variante generica), in kNm e 1/m: primo snervamento convenzionale M'yd, φ'yd (barra più tesa a εyd o calcestruzzo a εc2), MRd del punto limite,
/// φyd = (MRd/M'yd) φ'yd, massimo del ramo a curvatura controllata, φu (0,85 Mmax dopo il picco o punto limite) e μφ = φu/φyd.
/// </summary>
public sealed record MomentCurvatureDuctility(double FirstYieldMoment, double FirstYieldCurvature, bool FirstYieldInSteel, double ResistingMoment,
    double YieldCurvature, double MaximumMoment, double MaximumMomentCurvature, double LimitCurvature, double UltimateCurvature, bool UltimateAfterPeak,
    double CurvatureDuctility);
public sealed record MomentCurvatureResult(IReadOnlyList<MomentCurvaturePoint> Points, double LimitMoment, double? YieldCurvature,
    double? UltimateCurvature, string Status)
{
    public double LimitAxialKn { get; init; }
    public double AxialResidualKn { get; init; }
    /// <summary>
    /// μφ calcolata a richiesta della scheda «Momento–curvatura» (decisione dell'utente del 1/11/2026: nella scheda, non nel report); null altrimenti.
    /// Esclusa dalla serializzazione: esportazioni e report restano quelli di prima, byte per byte.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore] public MomentCurvatureDuctility? Ductility { get; init; }
    /// <summary>Motivo per cui la duttilità richiesta non è calcolata; null se calcolata o non richiesta. Esclusa dalla serializzazione.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public string? DuctilityMessage { get; init; }
}
/// <summary>Native equilibrium is injected. Portable orchestration, no UI or file dependencies.
/// L'interfaccia IMomentCurvatureCalculator, senza utilizzatori, è in tests/ANTHEA.Legacy dalla decisione E5 del 15/10/2026 (F2.11c).</summary>
public sealed class MomentCurvatureCalculator
{
    public MomentCurvatureResult Calculate(MomentCurvatureRequest p, Func<ActionPoint,DomainCheck> resistance,
        Func<ActionPoint,CheckerStressState> response, double steelYieldStrain, CancellationToken token=default)
        => Calculate(p, resistance, response, steelYieldStrain, null, token);

    /// <summary>
    /// La curva e, con <paramref name="ductility"/>, la duttilità in curvatura della libreria sulla variante generica (ramo a curvatura controllata
    /// con le forze dei piani della sezione, in kN e kNm negli assi locali); la curva è la stessa, bit per bit.
    /// </summary>
    public MomentCurvatureResult Calculate(MomentCurvatureRequest p, Func<ActionPoint,DomainCheck> resistance,
        Func<ActionPoint,CheckerStressState> response, double steelYieldStrain, NativeCurve.MomentCurvatureDuctilitySection? ductility,
        CancellationToken token=default)
    {
        // The native strains use 1/mm and absolute strain; only output units are converted.
        NativeCurve.MomentCurvatureStrains Strains(CheckerStressState state)
        {
            var plane = state.Native.StrainPlane;
            // ε0 di ogni riga nell'origine degli assi locali (SLU-4 del 15/10/2026), come MomentCurvatureStrains.From della libreria: il piano del
            // punto limite viene dal dominio con il riferimento nel vertice pivot e la riga riportava −3,5 ‰. Nel punto di riferimento del piano
            // stesso (le risposte) il valore memorizzato, bit per bit.
            var origin = state.StrainReference;
            bool own = origin is null || plane.ReferencePoint is { } r && r.X == origin.X && r.Y == origin.Y;
            return new(plane.ChiX, plane.ChiY, own ? plane.StrainReferencePoint : plane.GetStrain(origin!),
                state.BarStrains.Select(Math.Abs).DefaultIfEmpty(0).Max()/1000,
                state.ConcreteVertices.Min(v => v.Strain)/1000);
        }
        NativeCurve.MomentCurvatureLimit Limit(double n, double x, double y)
        {
            var check = resistance(new(n,x,y));
            // N fuori dal campo del dominio (SLU-2 del 15/10/2026): nessun punto limite a quell'N. Il punto della libreria è a eccentricità
            // costante: senza questo rifiuto la curva lo scarterebbe per il residuo su N, con un motivo fuorviante.
            if (check.AxialForceOutsideRange is { } range)
                throw new ArgumentException("Punto limite non disponibile: " + ConcreteLibraryMapping.AxialForceOutsideRange(range.Compression, range.Tension) + ".");
            if (check.Resistance is not ActionPoint rd || check.LimitState is null) return null!;
            return new(rd.N,rd.Mx,rd.My,() => Strains(check.LimitState.Value));
        }
        NativeCurve.MomentCurvatureResult r;
        try
        {
            var request = new NativeCurve.MomentCurvatureRequest(p.AxialKn,p.DirectionDegrees,p.Steps,p.EndFraction,p.QuadraticSampling,p.AxialToleranceKn,p.YieldRefinementSteps);
            var units = new NativeCurve.MomentCurvatureUnits("kN","kNm");
            r = ductility is null
                ? NativeCurve.MomentCurvatureAnalysis.Calculate(request, Limit, (n,x,y) => Strains(response(new(n,x,y))), steelYieldStrain, units, token)
                : NativeCurve.MomentCurvatureAnalysis.Calculate(request, Limit, (n,x,y) => Strains(response(new(n,x,y))), steelYieldStrain, units, ductility,
                    new NativeCurve.MomentCurvatureDuctilityRequest(), token);
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
            status+$" N del punto limite = {r.LimitAxialForce:G9} kN; residuo = {r.AxialResidual:G6} kN ({Tolerance(p)}). χ è il modulo del gradiente [1/m]; la direzione del momento è imposta, quella della curvatura può differire nelle sezioni asimmetriche. Nessun ramo post-picco.")
        { LimitAxialKn=r.LimitAxialForce, AxialResidualKn=r.AxialResidual, Ductility=Ductility(r.Ductility),
          DuctilityMessage=ductility is null || r.Ductility is not null ? null : DuctilityMessage(r.DuctilityMessage) };
    }
    /// <summary>Curvature della libreria in 1/mm, momenti già in kNm: curvature in 1/m come i punti della curva.</summary>
    static MomentCurvatureDuctility? Ductility(NativeCurve.MomentCurvatureDuctility? d) => d is null ? null
        : new(d.FirstYieldMoment, d.FirstYieldCurvature*1000, d.FirstYield == NativeCurve.MomentCurvatureFirstYield.TensionSteel, d.ResistingMoment,
            d.YieldCurvature*1000, d.MaximumMoment, d.MaximumMomentCurvature*1000, d.LimitCurvature*1000, d.UltimateCurvature*1000,
            d.Ultimate == NativeCurve.MomentCurvatureUltimate.PostPeakReduction, d.CurvatureDuctility);
    static string DuctilityMessage(string? message) => message switch
    {
        "Curvature ductility: the curve did not reach the limit point." => "la curva non arriva al punto limite (frazione finale minore di 1 o curva interrotta).",
        "Curvature ductility: no conventional first yield before the limit point." => "nessun primo snervamento convenzionale prima del punto limite.",
        "Curvature ductility: only with the concrete in tension of the responses neglected." => "solo con «CLS teso nelle risposte» trascurato.",
        _ => "ramo a curvatura controllata non risolto (" + message + ")."
    };
    static string Tolerance(MomentCurvatureRequest p) => p.AssignedAxialToleranceKn is double assigned && assigned != p.AxialToleranceKn
        ? $"tolleranza {p.AxialToleranceKn:G6} kN dalla regola della libreria per legame e dimensioni della sezione; assegnata {assigned:G6} kN"
        : $"tolleranza assegnata {p.AxialToleranceKn:G6} kN";
}
