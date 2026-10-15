using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record Fibra(double X,double Y,double Area);
public sealed record Barra(double X,double Y,double Area,double Diametro);

/// <summary>Trascrizione di RCSectionEngine: mm, MPa, kN, kNm. Compressione positiva; Mx=ΣF*y, My=-ΣF*x.</summary>
/// <remarks>Qui restano geometria, fibre, barre e materiali. Il motore storico (PuntoDominio, ParametriCls, EpsC2/EpsCu/ParabolaN,
/// ConcreteStress…Analyze) è in tests/ANTHEA.Legacy/Section/LegacySezioneCA.cs (decisione D3 del 15/10/2026).</remarks>
public sealed class SezioneCA
{
    public JsonObject Input { get; }
    public string Shape { get; }
    public double Fcd { get; } public double Fyd { get; } public double Es { get; }
    public double Radius { get; } public double BarRadius { get; private set; } public double CentroidY { get; private set; }
    public double AreaCls { get; private set; } public double AreaSteel { get; }
    public double Width { get; } public double Height { get; }
    public int CircularSides { get; private set; } = 32;
    public List<double[]> Outline { get; } = [];
    public List<double[][]> Holes { get; } = [];
    public List<Fibra> Fibers { get; } = [];
    public List<Barra> Bars { get; } = [];
    public double NAutomatico => GPC.Checkers.Concrete.Serviceability.Homogenization.ModularRatio(Es,
        GPC.Model.Materials.EuropeanConcreteElasticity.SecantModulusMpa(ConcreteMaterials.Concrete(Input).Fcm), 0);
    public static JsonObject DefaultInput() => J.Obj(
        ("shape","Rettangolare"),("diameter_mm","1000"),("circular_sides","32"),("width_mm","600"),("height_mm","800"),("flange_width_mm","1200"),("web_width_mm","400"),("flange_thickness_mm","250"),("cover_mm","70"),
        ("longitudinal_bar_count","16"),("longitudinal_bar_diameter_mm","24"),("top_bar_count","4"),("top_bar_diameter_mm","20"),("bottom_bar_count","6"),("bottom_bar_diameter_mm","24"),("side_bar_count_per_side","2"),("side_bar_diameter_mm","16"),("transverse_bar_diameter_mm","10"),("transverse_spacing_mm","200"),
        ("apply_pile_requirements",false),("dissipative_zone",false),("fck_mpa","35"),("fyk_mpa","450"),("alpha_cc","0.85"),("gamma_c","1.50"),("gamma_s","1.15"),("steel_modulus_mpa","200000"),
        ("axial_force_kn","2500"),("moment_x_knm","500"),("moment_y_knm","250"),("apply_minimum_eccentricity",false),("minimum_eccentricity_mm","0"),("classe_cls","C35/45"),("n",""));
    public static JsonObject DefaultCatalogInput()
    {
        var input = DefaultInput();
        foreach (var material in new[] { ConcreteMaterialCatalog.Concrete().Single(m => m.S("nome") == "C35/45"), ConcreteMaterialCatalog.Steel(false, "NTC 2018").Single(m => m.S("nome") == "B450C") })
            foreach (var (key, value) in material.Where(p => p.Key != "nome")) input[key] = value?.DeepClone();
        return input;
    }
    public static JsonObject DefaultData()
    {
        var input = DefaultCatalogInput();
        var combos=new JsonObject();foreach(var limit in new[]{"SLU","SLV","SLE"})combos[limit]=new JsonArray(J.Obj(("nome","Combo 1"),("azioni",new[]{"2500","500","250"})));
        return J.Obj(("versione_sezione",2),("input",input),("n_automatico",true),("combinazioni",combos),("workspace_ca",J.Obj(("normativa","NTC 2018"))));
    }
    // internal (prima private): lo legge anche il motore storico LegacySezioneCA in ANTHEA.Legacy (decisione D3 del 15/10/2026).
    internal double V(string key)=>key=="transverse_bar_diameter_mm"&&Input.S("staffe_presenti","Sì")=="No"?0:Input.D(key);
    public SezioneCA(JsonObject input,int radialDivisions=28,int angularDivisions=96)
    {
        Input=DefaultInput();foreach(var (k,v) in input)Input[k]=v?.DeepClone();
        Shape=Input.S("shape").Trim().ToLowerInvariant().Replace('-',' ') switch {"circolare"=>"Circolare","rettangolare"=>"Rettangolare","a t" or "t"=>"A T",_=>Input.S("shape")};
        Validate();Fcd=V("alpha_cc")*V("fck_mpa")/V("gamma_c");Fyd=V("fyk_mpa")/V("gamma_s");Es=V("steel_modulus_mpa");
        Radius=Shape=="Circolare"?V("diameter_mm")/2:0;
        Geometry(Math.Max(12,radialDivisions),Math.Max(36,angularDivisions));
        if(Input.B("foro_presente"))BuildHole(Math.Max(12,radialDivisions),Math.Max(36,angularDivisions));
        Width=Outline.Max(p=>p[0])-Outline.Min(p=>p[0]);Height=Outline.Max(p=>p[1])-Outline.Min(p=>p[1]);
        BuildBars();AreaSteel=LegacyNumerics.Sum(Bars.Select(b=>b.Area));
        if(Holes.Count>0&&Bars.Any(b=>SectionRegions.BarCover(this,b)<-1e-8))throw new ArgumentException("Una barra interseca il foro o il contorno della sezione.");
        if(AreaSteel>=AreaCls)throw new ArgumentException("L'area delle armature deve essere inferiore all'area lorda della sezione.");
        for(int i=0;i<Bars.Count;i++)for(int j=i+1;j<Bars.Count;j++)if(Hypot(Bars[i].X-Bars[j].X,Bars[i].Y-Bars[j].Y)<=(Bars[i].Diametro+Bars[j].Diametro)/2)throw new ArgumentException("La disposizione scelta provoca la sovrapposizione di due barre.");
    }
    public static double Hypot(double x,double y)=>double.Hypot(x,y);
    private void BuildHole(int nr,int na)
    {
        if(Shape=="Circolare")
        {
            double ri=Input.Required("inner_diameter_mm",strict:true)/2;
            if(ri>=Radius)throw new ArgumentException("Il diametro del foro deve essere minore di quello esterno.");
            Holes.Add(Enumerable.Range(0,CircularSides).Select(i=>new[]{ri*Math.Cos(-2*Math.PI*i/CircularSides),ri*Math.Sin(-2*Math.PI*i/CircularSides)}).ToArray());
            Fibers.Clear();
            for(int ir=0;ir<nr;ir++)
            {
                double r1=ri+(Radius-ri)*ir/nr,r2=ri+(Radius-ri)*(ir+1)/nr,r=Math.Sqrt((r1*r1+r2*r2)/2),area=Math.PI*(r2*r2-r1*r1)/na;
                for(int ia=0;ia<na;ia++){double angle=2*Math.PI*(ia+.5)/na;Fibers.Add(new(r*Math.Cos(angle),r*Math.Sin(angle),area));}
            }
            AreaCls=Math.PI*(Radius*Radius-ri*ri);
        }
        else if(Shape=="Rettangolare")
        {
            double bi=Input.Required("inner_width_mm",strict:true),hi=Input.Required("inner_height_mm",strict:true),b=V("width_mm"),h=V("height_mm");
            if(bi>=b||hi>=h)throw new ArgumentException("Il foro deve essere contenuto nel rettangolo esterno.");
            Holes.Add([[-bi/2,-hi/2],[-bi/2,hi/2],[bi/2,hi/2],[bi/2,-hi/2]]);
            Fibers.Clear();
            RectangleFibers(-b/2,b/2,hi/2,h/2,2*nr,Math.Max(6,na/4));RectangleFibers(-b/2,b/2,-h/2,-hi/2,2*nr,Math.Max(6,na/4));
            RectangleFibers(-b/2,-bi/2,-hi/2,hi/2,Math.Max(6,nr/2),na/2);RectangleFibers(bi/2,b/2,-hi/2,hi/2,Math.Max(6,nr/2),na/2);
            AreaCls=b*h-bi*hi;
        }
        else throw new ArgumentException("Foro centrale disponibile per sezione rettangolare o circolare.");
    }
    private void Validate()
    {
        _ = ConcreteCalculationSettings.HasStirrups(Input);
        if(Shape.StartsWith("Generica"))throw new ArgumentException("Sezione generica: predisposizione salvabile; definizione del contorno e delle armature ancora da implementare. Calcolo non disponibile.");
        if(Shape is not ("Circolare" or "Rettangolare" or "A T"))throw new ArgumentException("Tipo di sezione non riconosciuto.");
        string[] common=["cover_mm","transverse_bar_diameter_mm","transverse_spacing_mm","fck_mpa","fyk_mpa","alpha_cc","gamma_c","gamma_s","steel_modulus_mpa","minimum_eccentricity_mm"];
        foreach(var k in common)if(Input.S("staffe_presenti","Sì")!="No"||k is not("transverse_bar_diameter_mm" or "transverse_spacing_mm"))Input.Required(k,strict:k is not ("cover_mm" or "minimum_eccentricity_mm"));
        void Count(string k,int min){double value=Input.Required(k,min);if(value!=Math.Truncate(value)||value>int.MaxValue)throw new ArgumentException("Il numero delle barre deve essere intero.");}
        bool automaticBars = Input["barre_manuali"] is not JsonArray;
        if(Shape=="Circolare")
        {
            Input.Required("diameter_mm",strict:true);
            if (automaticBars) { Count("longitudinal_bar_count",4);Input.Required("longitudinal_bar_diameter_mm",strict:true); }
            CircularSides=SectionWorkspace.Subdivisions(Input.S("circular_sides"),"Lati del contorno circolare",12,720);
            // Keep vertices on both coordinate axes, preserving the assigned diameter in x and y.
            if(CircularSides%4!=0)throw new ArgumentException("Lati del contorno circolare: inserire un multiplo di 4 fra 12 e 720.");
        }
        else
        {
            Input.Required("height_mm",strict:true);if(Shape=="Rettangolare")Input.Required("width_mm",strict:true);
            else
            {
                if (automaticBars && Input.ContainsKey("flange_bottom_count")) { Count("flange_bottom_count", 0); if (Input.D("flange_bottom_count") == 1) throw new ArgumentException("Fila intradosso: indicare 0 oppure almeno 2 barre."); }
                Input.Required("flange_width_mm",strict:true);Input.Required("web_width_mm",strict:true);Input.Required("flange_thickness_mm",strict:true);
                if(V("web_width_mm")>V("flange_width_mm")||V("flange_thickness_mm")>=V("height_mm"))throw new ArgumentException("Geometria a T non valida.");
            }
            if (automaticBars)
            {
                Count("top_bar_count",2);Count("bottom_bar_count",2);Count("side_bar_count_per_side",0);
                foreach(var k in new[]{"top_bar_diameter_mm","bottom_bar_diameter_mm"})Input.Required(k,strict:true);
                if (V("side_bar_count_per_side") > 0) Input.Required("side_bar_diameter_mm",strict:true);
            }
        }
        if(V("fck_mpa")<12||V("fck_mpa")>90)throw new ArgumentException("fck deve essere compreso tra 12 e 90 MPa.");
        foreach (string layer in Shape == "Circolare" ? new[] { "inner" } : Shape == "Rettangolare" ? new[] { "top", "bottom" } : Array.Empty<string>())
            if (automaticBars && Input.B("second_" + layer + "_enabled"))
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
            for(int i=0;i<CircularSides;i++)Outline.Add([Radius*Math.Cos(2*Math.PI*i/CircularSides),Radius*Math.Sin(2*Math.PI*i/CircularSides)]);
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
}
