namespace Anthea.Calculations;

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
public interface IConcreteTorsionCalculator { TorsionResult Calculate(TorsionInput input); }
public sealed class ConcreteTorsionCalculator : IConcreteTorsionCalculator
{
    public static TorsionGeometry Geometry(SezioneCA s)
    {
        if(s.Shape is not ("Rettangolare" or "Circolare"))throw new ArgumentException("Torsione: profilo periferico automatico per rettangolare o circolare.");
        double outerPerimeter=s.Shape=="Circolare"?Math.PI*s.Width:2*(s.Width+s.Height);
        double axisCover=s.Input.D("cover_mm")+s.Input.D("transverse_bar_diameter_mm")+s.Bars.Max(b=>b.Diametro)/2;
        double t=s.AreaCls/outerPerimeter;
        if(s.Input.B("foro_presente"))t=s.Shape=="Circolare"?(s.Width-s.Input.D("inner_diameter_mm"))/2:Math.Min((s.Width-s.Input.D("inner_width_mm"))/2,(s.Height-s.Input.D("inner_height_mm"))/2);
        else t=Math.Max(t,2*axisCover);
        if(t<2*axisCover||t<=0||t>=Math.Min(s.Width,s.Height))throw new ArgumentException("Torsione: spessore resistente insufficiente per contenere le armature periferiche.");
        return s.Shape=="Circolare"?new(Math.PI*Math.Pow((s.Width-t)/2,2),Math.PI*(s.Width-t),t):new((s.Width-t)*(s.Height-t),2*(s.Width+s.Height-2*t),t);
    }
    public TorsionResult Calculate(TorsionInput p)
    {
        if(new[]{p.Geometry.Area,p.Geometry.Perimeter,p.Geometry.Thickness,p.Fcd,p.Fyd,p.StirrupLegArea,p.Spacing}.Any(v=>!double.IsFinite(v)||v<=0)||!double.IsFinite(p.TorqueKnM+p.VxKn+p.VyKn+p.AvailableLongitudinalArea+p.CotTheta)||p.AvailableLongitudinalArea<0||p.CotTheta<1||p.CotTheta>2.5)
            throw new ArgumentException("Torsione: controllare geometria, armature, materiali e 1 ≤ cot θ ≤ 2,5.");
        double t=Math.Abs(p.TorqueKnM),cot=p.CotTheta,a=p.Geometry.Area;
        double rc=2*a*p.Geometry.Thickness*.5*p.Fcd*cot/(1+cot*cot)/1e6;
        double rs=2*a*p.StirrupLegArea/p.Spacing*p.Fyd*cot/1e6;
        double rl=2*a*p.AvailableLongitudinalArea/p.Geometry.Perimeter*p.Fyd/cot/1e6;
        double rd=Math.Min(rc,Math.Min(rs,rl));
        double Ratio(double action,double capacity)=>Math.Abs(action)<1e-12?0:capacity>0?Math.Abs(action)/capacity:double.PositiveInfinity;
        if((Math.Abs(p.VxKn)>0&&Math.Abs(p.ShearX.CotTheta-cot)>1e-8)||(Math.Abs(p.VyKn)>0&&Math.Abs(p.ShearY.CotTheta-cot)>1e-8))throw new ArgumentException("Taglio e torsione devono usare lo stesso cot θ.");
        double concrete=Ratio(t,rc)+Ratio(p.VxKn,p.ShearX.VRcd)+Ratio(p.VyKn,p.ShearY.VRcd);
        double steel=Ratio(t,rs)+Math.Max(Ratio(p.VxKn,p.ShearX.VRsd),Ratio(p.VyKn,p.ShearY.VRsd));
        double eta=Ratio(t,rd),al=t*1e6*p.Geometry.Perimeter*cot/(2*a*p.Fyd);
        bool pass=eta<=1&&concrete<=1&&steel<=1;
        double? Finite(double v)=>double.IsFinite(v)?v:null;
        return new(rc,rs,rl,rd,Finite(eta),Finite(concrete),Finite(steel),al,pass,pass?"Torsione e interazione soddisfatte nel modello assegnato":!double.IsFinite(eta)?"Torsione non soddisfatta: resistenza nulla; tasso non definito":"Torsione / interazione non soddisfatta") {Geometry=p.Geometry,CotTheta=cot};
    }
}
