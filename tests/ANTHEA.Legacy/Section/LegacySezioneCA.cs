// Spostato da X.Calculations/SezioneCA.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: motore storico di RCSectionEngine (dominio
// plastico, resistenza e verifica SLU della sezione), usato solo dalle prove. Codice storico conservato per le prove, fuori dall'installer.
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record PuntoDominio(double axial_force_kn,double moment_knm,double? neutral_axis_depth_mm);

/// <summary>
/// Motore storico di <see cref="SezioneCA"/> come metodi di estensione: le prove scrivono ancora <c>engine.Analyze(…)</c>,
/// <c>engine.Interaction(…)</c>. Corpi invariati salvo la qualificazione dei membri rimasti nel prodotto (<c>engine.</c>, <c>SezioneCA.Hypot</c>).
/// </summary>
public static class LegacySezioneCA
{
    public static (double Ec2,double Ecu,double N) ParametriCls(double fck)
    {
        if(fck<=50)return(.002,.0035,2);double ratio=(90-fck)/100;
        return((2+.085*Math.Pow(fck-50,.53))/1000,(2.6+35*Math.Pow(ratio,4))/1000,1.4+23.4*Math.Pow(ratio,4));
    }
    // Il costruttore di SezioneCA assegnava EpsC2, EpsCu e ParabolaN con ParametriCls(V("fck_mpa")). Ora li calcola il primo uso, una volta
    // per sezione, con la stessa funzione e lo stesso dato (Input non cambia dopo la costruzione nelle prove): valori identici.
    private sealed record Parametri(double Ec2,double Ecu,double N);
    private static readonly ConditionalWeakTable<SezioneCA,Parametri> parametri = new();
    private static Parametri Cls(SezioneCA engine)=>parametri.GetValue(engine,e=>{var (ec2,ecu,n)=ParametriCls(e.V("fck_mpa"));return new(ec2,ecu,n);});
    public static double EpsC2(this SezioneCA engine)=>Cls(engine).Ec2;
    public static double EpsCu(this SezioneCA engine)=>Cls(engine).Ecu;
    public static double ParabolaN(this SezioneCA engine)=>Cls(engine).N;
    public static double ConcreteStress(this SezioneCA engine,double strain)=>strain<=0?0:strain<engine.EpsC2()?engine.Fcd*(1-Math.Pow(1-Math.Clamp(strain/engine.EpsC2(),0,1),engine.ParabolaN())):engine.Fcd;
    public static double SteelStress(this SezioneCA engine,double strain)=>Math.Clamp(engine.Es*strain,-engine.Fyd,engine.Fyd);
    public static double PureCompression(this SezioneCA engine)=>(engine.Fcd*(engine.AreaCls-engine.AreaSteel)+engine.Fyd*engine.AreaSteel)/1000;
    public static double PureTension(this SezioneCA engine)=>-engine.Fyd*engine.AreaSteel/1000;
    public static (double Min,double Max) Supports(this SezioneCA engine,double direction)
    {
        if(engine.Shape=="Circolare")return(-engine.Radius,engine.Radius);double s=Math.Sin(direction),c=Math.Cos(direction);var coords=engine.Outline.Select(p=>p[1]*c-p[0]*s).ToArray();return(coords.Min(),coords.Max());
    }
    public static (double N,double M) Profile(this SezioneCA engine,double neutralDepth,double direction)
    {
        double s=Math.Sin(direction),c=Math.Cos(direction);var (low,high)=engine.Supports(direction);double sectionDepth=Math.Max(high-low,1e-9),depth=Math.Max(neutralDepth,sectionDepth*1e-8),neutral=high-depth,scale=engine.EpsCu()/depth,axial=0,moment=0;
        foreach(var f in engine.Fibers){double u=f.Y*c-f.X*s,strain=scale*(u-neutral),force=engine.ConcreteStress(strain)*f.Area;axial+=force;moment+=force*u;}
        foreach(var b in engine.Bars){double u=b.Y*c-b.X*s,strain=scale*(u-neutral),force=(engine.SteelStress(strain)-engine.ConcreteStress(strain))*b.Area;axial+=force;moment+=force*u;}
        return(axial/1000,Math.Max(moment/1e6,0));
    }
    public static List<PuntoDominio> Interaction(this SezioneCA engine,double direction,int count=321)
    {
        var points=new List<PuntoDominio>{new(engine.PureTension(),0,0)};var (low,high)=engine.Supports(direction);double logMin=Math.Log((high-low)*.0005),logMax=Math.Log((high-low)*1000);
        for(int i=0;i<count;i++){double depth=Math.Exp(logMin+i/(double)Math.Max(count-1,1)*(logMax-logMin));var (n,m)=engine.Profile(depth,direction);points.Add(new(n,m,depth));}
        points.Add(new(engine.PureCompression(),0,null));return points;
    }
    public static (double? Moment,double? Depth) Capacity(IReadOnlyList<PuntoDominio> curve,double target)
    {
        double? best=null,depth=null;
        for(int i=1;i<curve.Count;i++)
        {
            var a=curve[i-1];var b=curve[i];double n1=a.axial_force_kn,n2=b.axial_force_kn;
            if(target<Math.Min(n1,n2)-1e-9||target>Math.Max(n1,n2)+1e-9)continue;
            double m;double? x;
            if(Math.Abs(n2-n1)<1e-12){m=Math.Max(a.moment_knm,b.moment_knm);x=a.neutral_axis_depth_mm;}
            else
            {
                double t=(target-n1)/(n2-n1);m=a.moment_knm+t*(b.moment_knm-a.moment_knm);
                x=a.neutral_axis_depth_mm is double ax&&b.neutral_axis_depth_mm is double bx?ax+t*(bx-ax):a.neutral_axis_depth_mm??b.neutral_axis_depth_mm;
            }
            m=Math.Max(m,0);if(best is null||m>best){best=m;depth=x;}
        }
        return(best,depth);
    }
    private static bool Inside(IReadOnlyList<PuntoDominio> curve,double n,double m)=>Capacity(curve,n).Moment is double cap&&m<=cap+1e-8;
    private static double LoadFactor(IReadOnlyList<PuntoDominio> curve,double n,double m)
    {
        if(Math.Abs(n)<1e-12&&Math.Abs(m)<1e-12)return double.PositiveInfinity;double low=0,high=1;int i;
        for(i=0;i<48;i++){if(!Inside(curve,high*n,high*m))break;low=high;high*=2;}if(i==48)return double.PositiveInfinity;
        for(i=0;i<64;i++){double mid=(low+high)/2;if(Inside(curve,mid*n,mid*m))low=mid;else high=mid;}return low;
    }
    public static JsonObject Analyze(this SezioneCA engine,double axial,double mx,double my)
    {
        if(!new[]{axial,mx,my}.All(double.IsFinite))throw new ArgumentException("Azioni non finite.");
        double demand=SezioneCA.Hypot(mx,my),direction=demand>1e-12?Math.Atan2(my,mx):0,effective=demand;var warnings=new List<string>();
        if(engine.Input.B("apply_minimum_eccentricity")&&axial>0){double min=axial*engine.V("minimum_eccentricity_mm")/1000;if(min>effective){effective=min;warnings.Add($"Applicato il momento minimo NEd x e_min = {min:F1} kNm.");}}
        var curve=engine.Interaction(direction);var (resistance,depth)=Capacity(curve,axial);double factor=LoadFactor(curve,axial,effective),ratio=double.IsInfinity(factor)?0:1/Math.Max(factor,1e-15);
        double reinforcement=engine.AreaSteel/engine.AreaCls,minRatio=engine.Input.B("apply_pile_requirements")?(engine.Input.B("dissipative_zone")?.01:.003):0,spacing=double.PositiveInfinity;
        for(int i=0;i<engine.Bars.Count;i++)for(int j=i+1;j<engine.Bars.Count;j++)spacing=Math.Min(spacing,SezioneCA.Hypot(engine.Bars[i].X-engine.Bars[j].X,engine.Bars[i].Y-engine.Bars[j].Y)-(engine.Bars[i].Diametro+engine.Bars[j].Diametro)/2);
        bool ok=Inside(curve,axial,effective),axialOk=engine.PureTension()-1e-9<=axial&&axial<=engine.PureCompression()+1e-9;
        var checks=new JsonArray();void Check(string name,bool pass,string detail)=>checks.Add(new JsonArray(name,pass,detail));
        Check("Pressoflessione SLU",ok,$"eta = {ratio:F2}; lambda = "+(double.IsInfinity(factor)?"infinito":$"{factor:F2}"));Check("Campo di resistenza assiale",axialOk,$"{engine.PureTension():F1} <= NEd <= {engine.PureCompression():F1} kN");
        if(engine.Input.B("apply_pile_requirements"))
        {
            Check("Armatura longitudinale minima palo",reinforcement+1e-12>=minRatio,$"rho = {100*reinforcement:F2}% (min {100*minRatio:F1}%)");
            Check("Diametro armatura trasversale palo",engine.V("transverse_bar_diameter_mm")>=8,$"phi_t = {engine.V("transverse_bar_diameter_mm"):F0} mm (min 8 mm)");
            double maxSpacing=(engine.Input.B("dissipative_zone")?6:8)*engine.Bars.Min(b=>b.Diametro);Check("Passo armatura trasversale palo",engine.V("transverse_spacing_mm")<=maxSpacing+1e-9,$"s = {engine.V("transverse_spacing_mm"):F0} mm (max {maxSpacing:F0} mm)");
        }
        else Check("Rapporto geometrico di armatura",true,$"rho = {100*reinforcement:F2}% (solo informativo)");
        Check("Spaziatura libera indicativa",spacing>0,$"distanza libera minima = {spacing:F1} mm");
        if(engine.Input.B("apply_pile_requirements")&&engine.Input.B("dissipative_zone"))Check("Tensione normale media in zona dissipativa",axial*1000/engine.AreaCls<=.45*engine.Fcd+1e-9,$"sigma_m = {axial*1000/engine.AreaCls:F2} MPa; 0.45 fcd = {.45*engine.Fcd:F2} MPa");
        if(engine.V("fck_mpa")>50)warnings.Add("Calcestruzzo ad alta resistenza: deformazioni ed esponente del diagramma adeguati automaticamente.");
        warnings.Add("Verifica di sezione del primo ordine: effetti del secondo ordine, taglio, fessurazione e interazione terreno-palo non inclusi.");
        bool passed=ok&&checks.All(c=>c![0]!.ToString()=="Spaziatura libera indicativa"||c[1]!.GetValue<bool>());
        return J.Obj(("fcd_mpa",engine.Fcd),("fyd_mpa",engine.Fyd),("concrete_area_mm2",engine.AreaCls),("steel_area_mm2",engine.AreaSteel),("reinforcement_ratio",reinforcement),("bar_radius_mm",engine.BarRadius),("section_outline",engine.Outline),("bars",engine.Bars.Select(b=>new[]{b.X,b.Y,b.Area,b.Diametro}).ToArray()),("section_width_mm",engine.Width),("section_height_mm",engine.Height),("minimum_steel_ratio",minRatio),("maximum_axial_kn",engine.PureCompression()),("minimum_axial_kn",engine.PureTension()),("demand_moment_knm",demand),("effective_moment_knm",effective),("resistance_moment_knm",resistance??0),("utilization",ratio),("load_factor",double.IsFinite(factor)?(object)factor:"Infinity"),("direction_rad",direction),("neutral_axis_depth_mm",depth),("interaction_curve",curve),("checks",checks),("warnings",warnings),("passed",passed));
    }
}
