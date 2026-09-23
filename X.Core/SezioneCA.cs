using System.Text.Json.Nodes;

namespace X.Core;

public sealed record Fibra(double X,double Y,double Area);
public sealed record Barra(double X,double Y,double Area,double Diametro);
public sealed record PuntoDominio(double axial_force_kn,double moment_knm,double? neutral_axis_depth_mm);

/// <summary>Trascrizione di RCSectionEngine: mm, MPa, kN, kNm. Compressione positiva; Mx=ΣF*y, My=-ΣF*x.</summary>
public sealed class SezioneCA
{
    public JsonObject Input { get; }
    public string Shape { get; }
    public double Fcd { get; } public double Fyd { get; } public double Es { get; }
    public double EpsC2 { get; } public double EpsCu { get; } public double ParabolaN { get; }
    public double Radius { get; } public double BarRadius { get; private set; } public double CentroidY { get; private set; }
    public double AreaCls { get; private set; } public double AreaSteel { get; }
    public double Width { get; } public double Height { get; }
    public List<double[]> Outline { get; } = [];
    public List<Fibra> Fibers { get; } = [];
    public List<Barra> Bars { get; } = [];
    public double NAutomatico => Es/(22000*Math.Pow((Input.D("fck_mpa")+8)/10,.3));
    public static JsonObject DefaultInput() => J.Obj(
        ("shape","Rettangolare"),("diameter_mm","1000"),("width_mm","600"),("height_mm","800"),("flange_width_mm","1200"),("web_width_mm","400"),("flange_thickness_mm","250"),("cover_mm","70"),
        ("longitudinal_bar_count","16"),("longitudinal_bar_diameter_mm","24"),("top_bar_count","4"),("top_bar_diameter_mm","20"),("bottom_bar_count","6"),("bottom_bar_diameter_mm","24"),("side_bar_count_per_side","2"),("side_bar_diameter_mm","16"),("transverse_bar_diameter_mm","10"),("transverse_spacing_mm","200"),
        ("apply_pile_requirements",false),("dissipative_zone",false),("fck_mpa","35"),("fyk_mpa","450"),("alpha_cc","0.85"),("gamma_c","1.50"),("gamma_s","1.15"),("steel_modulus_mpa","200000"),
        ("axial_force_kn","2500"),("moment_x_knm","500"),("moment_y_knm","250"),("apply_minimum_eccentricity",false),("minimum_eccentricity_mm","0"),("classe_cls","C35/45"),("n",""));
    public static JsonObject DefaultData()
    {
        var input = DefaultInput();
        foreach (var material in new[] { ConcreteMaterialCatalog.Concrete().Single(m => m.S("nome") == "C35/45"), ConcreteMaterialCatalog.Steel(false, "NTC 2018").Single(m => m.S("nome") == "B450C") })
            foreach (var (key, value) in material.Where(p => p.Key != "nome")) input[key] = value?.DeepClone();
        var combos=new JsonObject();foreach(var limit in new[]{"SLU","SLV","SLE"})combos[limit]=new JsonArray(J.Obj(("nome","Combo 1"),("azioni",new[]{"2500","500","250"})));
        return J.Obj(("versione_sezione",2),("input",input),("n_automatico",true),("combinazioni",combos));
    }
    public static (double Ec2,double Ecu,double N) ParametriCls(double fck)
    {
        if(fck<=50)return(.002,.0035,2);double ratio=(90-fck)/100;
        return((2+.085*Math.Pow(fck-50,.53))/1000,(2.6+35*Math.Pow(ratio,4))/1000,1.4+23.4*Math.Pow(ratio,4));
    }
    private double V(string key)=>Input.D(key);
    public SezioneCA(JsonObject input,int radialDivisions=28,int angularDivisions=96)
    {
        Input=DefaultInput();foreach(var (k,v) in input)Input[k]=v?.DeepClone();
        Shape=Input.S("shape").Trim().ToLowerInvariant().Replace('-',' ') switch {"circolare"=>"Circolare","rettangolare"=>"Rettangolare","a t" or "t"=>"A T",_=>Input.S("shape")};
        Validate();Fcd=V("alpha_cc")*V("fck_mpa")/V("gamma_c");Fyd=V("fyk_mpa")/V("gamma_s");Es=V("steel_modulus_mpa");
        (EpsC2,EpsCu,ParabolaN)=ParametriCls(V("fck_mpa"));Radius=Shape=="Circolare"?V("diameter_mm")/2:0;
        Geometry(Math.Max(12,radialDivisions),Math.Max(36,angularDivisions));Width=Outline.Max(p=>p[0])-Outline.Min(p=>p[0]);Height=Outline.Max(p=>p[1])-Outline.Min(p=>p[1]);
        BuildBars();AreaSteel=J.Sum(Bars.Select(b=>b.Area));
        if(AreaSteel>=AreaCls)throw new ArgumentException("L'area delle armature deve essere inferiore all'area lorda della sezione.");
        for(int i=0;i<Bars.Count;i++)for(int j=i+1;j<Bars.Count;j++)if(Hypot(Bars[i].X-Bars[j].X,Bars[i].Y-Bars[j].Y)<=(Bars[i].Diametro+Bars[j].Diametro)/2)throw new ArgumentException("La disposizione scelta provoca la sovrapposizione di due barre.");
    }
    public static double Hypot(double x,double y)=>double.Hypot(x,y);
    private void Validate()
    {
        if(Shape.StartsWith("Generica"))throw new ArgumentException("Sezione generica: predisposizione salvabile; definizione del contorno e delle armature ancora da implementare. Calcolo non disponibile.");
        if(Shape is not ("Circolare" or "Rettangolare" or "A T"))throw new ArgumentException("Tipo di sezione non riconosciuto.");
        string[] common=["cover_mm","transverse_bar_diameter_mm","transverse_spacing_mm","fck_mpa","fyk_mpa","alpha_cc","gamma_c","gamma_s","steel_modulus_mpa","minimum_eccentricity_mm"];
        foreach(var k in common)Input.Required(k,strict:k is not ("cover_mm" or "minimum_eccentricity_mm"));
        void Count(string k,int min){double value=Input.Required(k,min);if(value!=Math.Truncate(value)||value>int.MaxValue)throw new ArgumentException("Il numero delle barre deve essere intero.");}
        if(Shape=="Circolare") {Input.Required("diameter_mm",strict:true);Count("longitudinal_bar_count",4);Input.Required("longitudinal_bar_diameter_mm",strict:true);}
        else
        {
            Input.Required("height_mm",strict:true);if(Shape=="Rettangolare")Input.Required("width_mm",strict:true);
            else
            {
                if (Input.ContainsKey("flange_bottom_count")) { Count("flange_bottom_count", 0); if (Input.D("flange_bottom_count") == 1) throw new ArgumentException("Fila intradosso: indicare 0 oppure almeno 2 barre."); }
                Input.Required("flange_width_mm",strict:true);Input.Required("web_width_mm",strict:true);Input.Required("flange_thickness_mm",strict:true);
                if(V("web_width_mm")>V("flange_width_mm")||V("flange_thickness_mm")>=V("height_mm"))throw new ArgumentException("Geometria a T non valida.");
            }
            Count("top_bar_count",2);Count("bottom_bar_count",2);Count("side_bar_count_per_side",0);
            foreach(var k in new[]{"top_bar_diameter_mm","bottom_bar_diameter_mm","side_bar_diameter_mm"})Input.Required(k,strict:true);
        }
        if(V("fck_mpa")<12||V("fck_mpa")>90)throw new ArgumentException("fck deve essere compreso tra 12 e 90 MPa.");
        foreach (string layer in Shape == "Circolare" ? new[] { "inner" } : Shape == "Rettangolare" ? new[] { "top", "bottom" } : Array.Empty<string>())
            if (Input.B("second_" + layer + "_enabled"))
            {
                Count("second_" + layer + "_count", layer == "inner" ? 4 : 2);
                Input.Required("second_" + layer + "_diameter", strict: true);
                Input.Required("second_" + layer + "_gap", strict: true);
            }
    }
    private void RectangleFibers(double xmin,double xmax,double ymin,double ymax,int nx,int ny,double shift=0)
    {
        double dx=(xmax-xmin)/nx,dy=(ymax-ymin)/ny,area=dx*dy;
        for(int iy=0;iy<ny;iy++)for(int ix=0;ix<nx;ix++)Fibers.Add(new(xmin+(ix+.5)*dx,ymin+(iy+.5)*dy-shift,area));
    }
    private void Geometry(int nr,int na)
    {
        if(Shape=="Circolare")
        {
            for(int i=0;i<180;i++)Outline.Add([Radius*Math.Cos(2*Math.PI*i/180),Radius*Math.Sin(2*Math.PI*i/180)]);
            for(int ir=0;ir<nr;ir++)
            {
                double r1=Radius*ir/nr,r2=Radius*(ir+1)/nr,r=Math.Sqrt((r1*r1+r2*r2)/2),area=Math.PI*(r2*r2-r1*r1)/na;
                for(int ia=0;ia<na;ia++){double angle=2*Math.PI*(ia+.5)/na;Fibers.Add(new(r*Math.Cos(angle),r*Math.Sin(angle),area));}
            }
            AreaCls=Math.PI*Radius*Radius;return;
        }
        double h=V("height_mm");int nx=Math.Max(16,2*nr),ny=Math.Max(16,na/2);
        if(Shape=="Rettangolare")
        {
            double b=V("width_mm");Outline.AddRange([[-b/2,-h/2],[b/2,-h/2],[b/2,h/2],[-b/2,h/2]]);RectangleFibers(-b/2,b/2,-h/2,h/2,nx,ny);AreaCls=b*h;return;
        }
        double bf=V("flange_width_mm"),bw=V("web_width_mm"),hf=V("flange_thickness_mm"),af=bf*hf,hw=h-hf,aw=bw*hw,yf=h/2-hf/2,yw=-h/2+hw/2;
        CentroidY=(af*yf+aw*yw)/(af+aw);
        double[][] raw=[[-bf/2,h/2],[bf/2,h/2],[bf/2,h/2-hf],[bw/2,h/2-hf],[bw/2,-h/2],[-bw/2,-h/2],[-bw/2,h/2-hf],[-bf/2,h/2-hf]];
        Outline.AddRange(raw.Select(p=>new[]{p[0],p[1]-CentroidY}));
        RectangleFibers(-bf/2,bf/2,h/2-hf,h/2,nx,Math.Max(6,(int)Math.Round(ny*hf/h)),CentroidY);
        RectangleFibers(-bw/2,bw/2,-h/2,h/2-hf,Math.Max(12,(int)Math.Round(nx*bw/bf)),Math.Max(8,(int)Math.Round(ny*hw/h)),CentroidY);AreaCls=af+aw;
    }
    private void Layer(int count,double halfWidth,double y,double diameter)
    {
        if(halfWidth<=0)throw new ArgumentException("Copriferro e diametro barre non lasciano spazio nello strato.");
        double area=Math.PI*diameter*diameter/4;for(int i=0;i<count;i++)Bars.Add(new(-halfWidth+2*halfWidth*i/(count-1),y,area,diameter));
    }
    private void BuildBars()
    {
        if (Input["barre_manuali"] is JsonArray manual)
        {
            if (manual.Count == 0) throw new ArgumentException("Inserire almeno una barra.");
            foreach (var row in manual)
            {
                double x = SectionWorkspace.Number(row.S("x"), "x barra"), y = SectionWorkspace.Number(row.S("y"), "y barra"), diameter = row!.Required("phi", strict: true);
                Bars.Add(new(x, y, Math.PI * diameter * diameter / 4, diameter));
            }
            return;
        }
        double cover=V("cover_mm"),transverse=V("transverse_bar_diameter_mm");
        if(Shape=="Circolare")
        {
            double diameter=V("longitudinal_bar_diameter_mm"),area=Math.PI*diameter*diameter/4;int count=(int)V("longitudinal_bar_count");BarRadius=Radius-cover-transverse-diameter/2;
            if(BarRadius<=0)throw new ArgumentException("Copriferro e diametri delle barre non lasciano spazio all'armatura longitudinale.");
            for(int i=0;i<count;i++)Bars.Add(new(BarRadius*Math.Cos(2*Math.PI*i/count),BarRadius*Math.Sin(2*Math.PI*i/count),area,diameter));
            if (Input.B("second_inner_enabled"))
            {
                double d = V("second_inner_diameter"), r = BarRadius - (diameter + d) / 2 - V("second_inner_gap");
                int n = (int)V("second_inner_count");
                if (r <= d / 2) throw new ArgumentException("Secondo anello: distanza libera e diametro non lasciano spazio sufficiente.");
                for (int i = 0; i < n; i++) Bars.Add(new(r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n), Math.PI * d * d / 4, d));
            }
            return;
        }
        double h=V("height_mm"),tp=V("top_bar_diameter_mm"),bp=V("bottom_bar_diameter_mm");
        double tw=(Shape=="Rettangolare"?V("width_mm"):V("flange_width_mm"))/2-cover-transverse-tp/2;
        double halfSide=(Shape=="Rettangolare"?V("width_mm"):V("web_width_mm"))/2,bw=halfSide-cover-transverse-bp/2,sideTop=Shape=="Rettangolare"?h/2:h/2-V("flange_thickness_mm");
        double ty=h/2-cover-transverse-tp/2-CentroidY,by=-h/2+cover+transverse+bp/2-CentroidY;
        if(ty<=by)throw new ArgumentException("Copriferro e armature non lasciano una distanza utile tra gli strati.");
        Layer((int)V("top_bar_count"),tw,ty,tp);Layer((int)V("bottom_bar_count"),bw,by,bp);
        if (Shape == "Rettangolare")
        {
            double upper = ty, lower = by;
            foreach (string layer in new[] { "top", "bottom" }) if (Input.B("second_" + layer + "_enabled"))
            {
                double d = V("second_" + layer + "_diameter"), gap = V("second_" + layer + "_gap");
                double y = layer == "top" ? ty - (tp + d) / 2 - gap : by + (bp + d) / 2 + gap;
                if (layer == "top") upper = y - d / 2; else lower = y + d / 2;
                if (upper <= lower) throw new ArgumentException("Secondi strati: distanza libera incompatibile con l'altezza della sezione.");
                Layer((int)V("second_" + layer + "_count"), V("width_mm") / 2 - cover - transverse - d / 2, y, d);
            }
        }
        if (Shape == "A T" && Input.D("flange_bottom_count") > 0)
        {
            double number = Input.Required("flange_bottom_count", 2);
            if (number != Math.Truncate(number) || number > 1000) throw new ArgumentException("Numero barre intradosso ala non valido.");
            int count = (int)number;
            double diameter = Input.Required("flange_bottom_diameter_mm", strict: true);
            double flangeOffset = Input.Required("flange_bottom_offset_mm", strict: true);
            if (flangeOffset < cover + transverse + diameter / 2 || flangeOffset + diameter / 2 >= V("flange_thickness_mm"))
                throw new ArgumentException("Fila intradosso soletta: offset all’asse incompatibile con copriferro o spessore ala.");
            Layer(count, V("flange_width_mm") / 2 - cover - transverse - diameter / 2,
                h / 2 - V("flange_thickness_mm") + flangeOffset - CentroidY, diameter);
        }
        int sides=(int)V("side_bar_count_per_side");if(sides==0)return;
        double phi=V("side_bar_diameter_mm"),offset=cover+transverse+phi/2,sx=halfSide-offset,low=-h/2+offset-CentroidY,high=(Shape == "A T" ? h/2 : sideTop)-offset-CentroidY;
        if(sx<=0||high<=low)throw new ArgumentException("Non c'è spazio sufficiente per le barre laterali indicate.");
        double sa=Math.PI*phi*phi/4;for(int i=0;i<sides;i++){double y=low+(i+1)*(high-low)/(Shape == "A T" ? sides : sides+1);Bars.Add(new(-sx,y,sa,phi));Bars.Add(new(sx,y,sa,phi));}
    }
    public double ConcreteStress(double strain)=>strain<=0?0:strain<EpsC2?Fcd*(1-Math.Pow(1-Math.Clamp(strain/EpsC2,0,1),ParabolaN)):Fcd;
    public double SteelStress(double strain)=>Math.Clamp(Es*strain,-Fyd,Fyd);
    public double PureCompression()=>(Fcd*(AreaCls-AreaSteel)+Fyd*AreaSteel)/1000;
    public double PureTension()=>-Fyd*AreaSteel/1000;
    public (double Min,double Max) Supports(double direction)
    {
        if(Shape=="Circolare")return(-Radius,Radius);double s=Math.Sin(direction),c=Math.Cos(direction);var coords=Outline.Select(p=>p[1]*c-p[0]*s).ToArray();return(coords.Min(),coords.Max());
    }
    public (double N,double M) Profile(double neutralDepth,double direction)
    {
        double s=Math.Sin(direction),c=Math.Cos(direction);var (low,high)=Supports(direction);double sectionDepth=Math.Max(high-low,1e-9),depth=Math.Max(neutralDepth,sectionDepth*1e-8),neutral=high-depth,scale=EpsCu/depth,axial=0,moment=0;
        foreach(var f in Fibers){double u=f.Y*c-f.X*s,strain=scale*(u-neutral),force=ConcreteStress(strain)*f.Area;axial+=force;moment+=force*u;}
        foreach(var b in Bars){double u=b.Y*c-b.X*s,strain=scale*(u-neutral),force=(SteelStress(strain)-ConcreteStress(strain))*b.Area;axial+=force;moment+=force*u;}
        return(axial/1000,Math.Max(moment/1e6,0));
    }
    public List<PuntoDominio> Interaction(double direction,int count=321)
    {
        var points=new List<PuntoDominio>{new(PureTension(),0,0)};var (low,high)=Supports(direction);double logMin=Math.Log((high-low)*.0005),logMax=Math.Log((high-low)*1000);
        for(int i=0;i<count;i++){double depth=Math.Exp(logMin+i/(double)Math.Max(count-1,1)*(logMax-logMin));var (n,m)=Profile(depth,direction);points.Add(new(n,m,depth));}
        points.Add(new(PureCompression(),0,null));return points;
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
    public JsonObject Analyze(double axial,double mx,double my)
    {
        if(!new[]{axial,mx,my}.All(double.IsFinite))throw new ArgumentException("Azioni non finite.");
        double demand=Hypot(mx,my),direction=demand>1e-12?Math.Atan2(my,mx):0,effective=demand;var warnings=new List<string>();
        if(Input.B("apply_minimum_eccentricity")&&axial>0){double min=axial*V("minimum_eccentricity_mm")/1000;if(min>effective){effective=min;warnings.Add($"Applicato il momento minimo NEd x e_min = {min:F1} kNm.");}}
        var curve=Interaction(direction);var (resistance,depth)=Capacity(curve,axial);double factor=LoadFactor(curve,axial,effective),ratio=double.IsInfinity(factor)?0:1/Math.Max(factor,1e-15);
        double reinforcement=AreaSteel/AreaCls,minRatio=Input.B("apply_pile_requirements")?(Input.B("dissipative_zone")?.01:.003):0,spacing=double.PositiveInfinity;
        for(int i=0;i<Bars.Count;i++)for(int j=i+1;j<Bars.Count;j++)spacing=Math.Min(spacing,Hypot(Bars[i].X-Bars[j].X,Bars[i].Y-Bars[j].Y)-(Bars[i].Diametro+Bars[j].Diametro)/2);
        bool ok=Inside(curve,axial,effective),axialOk=PureTension()-1e-9<=axial&&axial<=PureCompression()+1e-9;
        var checks=new JsonArray();void Check(string name,bool pass,string detail)=>checks.Add(new JsonArray(name,pass,detail));
        Check("Pressoflessione SLU",ok,$"eta = {ratio:F2}; lambda = "+(double.IsInfinity(factor)?"infinito":$"{factor:F2}"));Check("Campo di resistenza assiale",axialOk,$"{PureTension():F1} <= NEd <= {PureCompression():F1} kN");
        if(Input.B("apply_pile_requirements"))
        {
            Check("Armatura longitudinale minima palo",reinforcement+1e-12>=minRatio,$"rho = {100*reinforcement:F2}% (min {100*minRatio:F1}%)");
            Check("Diametro armatura trasversale palo",V("transverse_bar_diameter_mm")>=8,$"phi_t = {V("transverse_bar_diameter_mm"):F0} mm (min 8 mm)");
            double maxSpacing=(Input.B("dissipative_zone")?6:8)*Bars.Min(b=>b.Diametro);Check("Passo armatura trasversale palo",V("transverse_spacing_mm")<=maxSpacing+1e-9,$"s = {V("transverse_spacing_mm"):F0} mm (max {maxSpacing:F0} mm)");
        }
        else Check("Rapporto geometrico di armatura",true,$"rho = {100*reinforcement:F2}% (solo informativo)");
        Check("Spaziatura libera indicativa",spacing>0,$"distanza libera minima = {spacing:F1} mm");
        if(Input.B("apply_pile_requirements")&&Input.B("dissipative_zone"))Check("Tensione normale media in zona dissipativa",axial*1000/AreaCls<=.45*Fcd+1e-9,$"sigma_m = {axial*1000/AreaCls:F2} MPa; 0.45 fcd = {.45*Fcd:F2} MPa");
        if(V("fck_mpa")>50)warnings.Add("Calcestruzzo ad alta resistenza: deformazioni ed esponente del diagramma adeguati automaticamente.");
        warnings.Add("Verifica di sezione del primo ordine: effetti del secondo ordine, taglio, fessurazione e interazione terreno-palo non inclusi.");
        bool passed=ok&&checks.All(c=>c![0]!.ToString()=="Spaziatura libera indicativa"||c[1]!.GetValue<bool>());
        return J.Obj(("fcd_mpa",Fcd),("fyd_mpa",Fyd),("concrete_area_mm2",AreaCls),("steel_area_mm2",AreaSteel),("reinforcement_ratio",reinforcement),("bar_radius_mm",BarRadius),("section_outline",Outline),("bars",Bars.Select(b=>new[]{b.X,b.Y,b.Area,b.Diametro}).ToArray()),("section_width_mm",Width),("section_height_mm",Height),("minimum_steel_ratio",minRatio),("maximum_axial_kn",PureCompression()),("minimum_axial_kn",PureTension()),("demand_moment_knm",demand),("effective_moment_knm",effective),("resistance_moment_knm",resistance??0),("utilization",ratio),("load_factor",double.IsFinite(factor)?(object)factor:"Infinity"),("direction_rad",direction),("neutral_axis_depth_mm",depth),("interaction_curve",curve),("checks",checks),("warnings",warnings),("passed",passed));
    }
}
