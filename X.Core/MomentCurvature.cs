namespace X.Core;

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
    public MomentCurvatureResult Calculate(MomentCurvatureRequest p,Func<ActionPoint,DomainCheck> resistance,
        Func<ActionPoint,CheckerStressState> response,double steelYieldStrain,CancellationToken token=default)
    {
        if(!double.IsFinite(p.AxialKn+p.DirectionDegrees+p.EndFraction+p.AxialToleranceKn+steelYieldStrain)||p.Steps<10||p.Steps>500||p.EndFraction<=0||p.EndFraction>1||steelYieldStrain<=0||p.AxialToleranceKn<=0||p.YieldRefinementSteps<0||p.YieldRefinementSteps>30)
            throw new ArgumentException("M–χ: 10–500 passi, frazione finale > 0 e ≤ 1; N e direzione finiti.");
        double angle=p.DirectionDegrees*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
        token.ThrowIfCancellationRequested();var check=resistance(new(p.AxialKn,c,s));
        if(check.Resistance is not ActionPoint rd || check.LimitState is null)throw new ArgumentException("Punto limite non disponibile al N assegnato.");
        if(Math.Abs(rd.N-p.AxialKn)>p.AxialToleranceKn)throw new ArgumentException($"Residuo N al limite superiore alla tolleranza {p.AxialToleranceKn:G6} kN: N richiesto = {p.AxialKn:G9} kN; N limite = {rd.N:G9} kN.");
        double mr=rd.Mx*c+rd.My*s;
        if(mr<=0||!double.IsFinite(mr))throw new ArgumentException("Momento limite non positivo nella direzione richiesta.");
        var points=new List<MomentCurvaturePoint>();double? yield=null,ultimate=null;
        MomentCurvaturePoint Point(CheckerStressState state,double moment,bool limit)
        {
            var plane=state.Native.StrainPlane;double curvature=double.Hypot(plane.ChiX,plane.ChiY)*1000;
            double strain=state.BarStrains.Select(Math.Abs).DefaultIfEmpty(0).Max()/1000;
            if(!double.IsFinite(curvature+strain))throw new ArgumentException("Risposta non finita.");
            return new(moment,moment*c,moment*s,curvature,plane.ChiX*1000,plane.ChiY*1000,plane.StrainReferencePoint*1000,
                Math.Max(0,-state.ConcreteVertices.Min(v=>v.Strain)),strain*1000,strain>=steelYieldStrain,limit);
        }
        string status="Ramo a momento crescente, N costante. Leggi e coefficienti dei materiali della sezione.";
        for(int i=0;i<=p.Steps;i++)
        {
            token.ThrowIfCancellationRequested();double f=(double)i/p.Steps;if(p.QuadraticSampling)f*=f;f*=p.EndFraction;
            bool limit=i==p.Steps&&p.EndFraction==1;double m=mr*f;
            try
            {
                var state=limit?check.LimitState.Value:response(new(p.AxialKn,m*c,m*s));
                var point=Point(state,m,limit);
                if(point.Yielded&&yield is null)yield=point.Curvature;
                if(limit)ultimate=point.Curvature;
                points.Add(point);
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {status+=$" Interrotta al passo {i}: {ex.Message}";break;}
        }
        int firstYield=points.FindIndex(v=>v.Yielded);
        if(firstYield>0&&p.YieldRefinementSteps>0)
        {
            var high=points[firstYield];double low=points[firstYield-1].Moment;
            try
            {
                for(int i=0;i<p.YieldRefinementSteps;i++)
                {token.ThrowIfCancellationRequested();double m=(low+high.Moment)/2;var candidate=Point(response(new(p.AxialKn,m*c,m*s)),m,false);if(candidate.Yielded)high=candidate;else low=m;}
                yield=high.Curvature;if(high.Moment<points[firstYield].Moment)points.Insert(firstYield,high);
                status+=$" Primo snervamento raffinato con {p.YieldRefinementSteps} bisezioni, My = {high.Moment:G7} kNm.";
            }
            catch(Exception ex) when(ex is not OperationCanceledException){status+=" Snervamento riferito al primo campione: raffinamento interrotto, "+ex.Message;}
        }
        return new(points,mr,yield,ultimate,status+$" N del punto limite = {rd.N:G9} kN; residuo = {rd.N-p.AxialKn:G6} kN (tolleranza assegnata {p.AxialToleranceKn:G6} kN). χ è il modulo del gradiente [1/m]; la direzione del momento è imposta, quella della curvatura può differire nelle sezioni asimmetriche. Nessun ramo post-picco."){LimitAxialKn=rd.N,AxialResidualKn=rd.N-p.AxialKn};
    }
}
