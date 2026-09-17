using System.Text.Json.Nodes;

namespace X.Core;

public sealed record StatoElastico(double[] piano,double lunghezza_mm,double sigma_cls,double sigma_acciaio,double[] tensioni_barre,double residuo_relativo);

/// <summary>Metodo n fessurato; porting di sezione_elastica.py. CLS non teso. SLV al primo |σs|=fyd.</summary>
public static class SezioneElastica
{
    public const double KnToN=1000, KnmToNmm=1e6;
    public static double[] Risolvi3(double[,] matrix,double[] rhs)
    {
        double[,] a=new double[3,4];double scale=0;
        for(int i=0;i<3;i++){for(int j=0;j<3;j++){a[i,j]=matrix[i,j];scale=Math.Max(scale,Math.Abs(a[i,j]));}a[i,3]=rhs[i];}
        for(int i=0;i<3;i++)
        {
            int pivot=i;for(int j=i+1;j<3;j++)if(Math.Abs(a[j,i])>Math.Abs(a[pivot,i]))pivot=j;
            if(Math.Abs(a[pivot,i])<=Math.Max(scale*1e-13,1e-30))throw new ArgumentException("Sezione elastica singolare: controllare geometria e armature.");
            for(int j=0;j<4;j++)(a[i,j],a[pivot,j])=(a[pivot,j],a[i,j]);double divisor=a[i,i];for(int j=0;j<4;j++)a[i,j]/=divisor;
            for(int j=0;j<3;j++)if(j!=i){double factor=a[j,i];for(int k=0;k<4;k++)a[j,k]-=factor*a[i,k];}
        }
        return[a[0,3],a[1,3],a[2,3]];
    }
    public static StatoElastico Tensioni(SezioneCA engine,double n,double axial,double mx,double my)
    {
        if(!new[]{n,axial,mx,my}.All(double.IsFinite)||n<=0)throw new ArgumentException("Metodo n: input finiti e coefficiente n positivo richiesti.");
        double length=Math.Max(engine.Width,engine.Height);double[] target=[axial*KnToN,mx*KnmToNmm/length,my*KnmToNmm/length];
        var points=engine.Fibers.Select(f=>(f.Area,U:new[]{1.0,f.Y/length,-f.X/length},Steel:false)).Concat(engine.Bars.Select(b=>(b.Area,U:new[]{1.0,b.Y/length,-b.X/length},Steel:true))).ToArray();
        (double[] Force,double[,] Stiffness) Response(double[] q,bool uncracked=false)
        {
            double[] force=new double[3];double[,] stiffness=new double[3,3];
            foreach(var (area,u,steel) in points)
            {
                double stress=J.Sum(q.Zip(u,(v,w)=>v*w));bool active=stress>0||uncracked;double factor=steel?n-(active?1:0):active?1:0;
                for(int i=0;i<3;i++){force[i]+=area*factor*stress*u[i];for(int j=0;j<3;j++)stiffness[i,j]+=area*factor*u[i]*u[j];}
            }
            return(force,stiffness);
        }
        var (_,initial)=Response([0,0,0],true);double[] q=Risolvi3(initial,target);double norm=Math.Max(1,Math.Sqrt(J.Sum(target.Select(v=>v*v)))),error=0;int iteration;
        for(iteration=0;iteration<80;iteration++)
        {
            var (force,stiffness)=Response(q);var residual=target.Zip(force,(v,w)=>v-w).ToArray();error=Math.Sqrt(J.Sum(residual.Select(v=>v*v)))/norm;if(error<1e-9)break;
            var delta=Risolvi3(stiffness,residual);bool improved=false;
            for(int step=0;step<20;step++)
            {
                var candidate=q.Zip(delta,(v,w)=>v+w*Math.Pow(.5,step)).ToArray();var (trial,_)=Response(candidate);double trialError=Math.Sqrt(J.Sum(target.Zip(trial,(v,w)=>(v-w)*(v-w))))/norm;
                if(trialError<error){q=candidate;improved=true;break;}
            }
            if(!improved)throw new ArgumentException("Metodo n: mancata convergenza dell’equilibrio.");
        }
        if(iteration==80)throw new ArgumentException("Metodo n: superato il limite di 80 iterazioni.");
        double[] steelStresses=points.Where(p=>p.Steel).Select(p=>n*J.Sum(q.Zip(p.U,(v,w)=>v*w))).ToArray();
        double cls=Math.Max(0,engine.Outline.Max(p=>q[0]+q[1]*p[1]/length-q[2]*p[0]/length));
        return new(q,length,cls,steelStresses.Select(Math.Abs).DefaultIfEmpty(0).Max(),steelStresses,error);
    }
    public static (double Moment,StatoElastico State) Resistenza(SezioneCA engine,double n,double axial,double direction)
    {
        StatoElastico State(double moment)=>Tensioni(engine,n,axial,moment*Math.Cos(direction),moment*Math.Sin(direction));
        var baseline=State(0);if(baseline.sigma_acciaio>=engine.Fyd)return(0,baseline);
        double low=0,high=Math.Max(1,engine.Fyd*engine.AreaSteel*engine.Height/KnmToNmm);int i;
        for(i=0;i<32;i++){if(State(high).sigma_acciaio>=engine.Fyd)break;high*=2;}if(i==32)throw new ArgumentException("Limite elastico non individuato.");
        for(i=0;i<42;i++){double middle=(low+high)/2;if(State(middle).sigma_acciaio<engine.Fyd)low=middle;else high=middle;}
        return(low,State(low));
    }
}

/// <summary>Profili e intersezioni campionati; stessa discretizzazione e interpolazione della fonte.</summary>
public static class Domini
{
    public static List<double[]> Profili(SezioneCA engine,string mode="Plastico",double n=15,double angle=0,int samples=121)
    {
        double sx=Math.Sin(angle),cy=Math.Cos(angle);var (lo,hi)=engine.Supports(angle);double depth=hi-lo;bool elastic=mode=="Elastico";
        if(elastic&&(!double.IsFinite(n)||n<=0))throw new ArgumentException("n elastico non valido.");
        var concrete=engine.Fibers.Select(f=>(U:f.Y*cy-f.X*sx,f.X,f.Y,f.Area)).ToArray();var bars=engine.Bars.Select(b=>(U:b.Y*cy-b.X*sx,b.X,b.Y,b.Area)).ToArray();
        double[] Response(double neutral)
        {
            double factor;
            if(elastic){double maximum=bars.Max(b=>Math.Abs(b.U-neutral));if(maximum<=1e-12)throw new ArgumentException("Braccio delle armature nullo.");factor=engine.Fyd/(n*maximum);}
            else factor=engine.EpsCu/Math.Max(hi-neutral,depth*1e-8);
            double C(double u)=>elastic?Math.Max(0,(u-neutral)*factor):engine.ConcreteStress((u-neutral)*factor);
            double S(double u)=>elastic?n*(u-neutral)*factor:engine.SteelStress((u-neutral)*factor);
            double nn=0,mx=0,my=0;
            foreach(var p in concrete){double force=C(p.U)*p.Area;nn+=force;mx+=force*p.Y;my-=force*p.X;}
            foreach(var p in bars){double force=(S(p.U)-C(p.U))*p.Area;nn+=force;mx+=force*p.Y;my-=force*p.X;}
            return[nn/1000,mx/1e6,my/1e6];
        }
        var points=new List<double[]>();
        if(elastic)for(int i=0;i<samples;i++)points.Add(Response(hi+depth*Math.Pow(10,3-6.0*i/(samples-1))));else points.Add([engine.PureTension(),0,0]);
        for(int i=0;i<samples;i++)points.Add(Response(hi-depth*Math.Exp(Math.Log(.0005)+i/(double)(samples-1)*Math.Log(1000/.0005))));
        if(mode=="Plastico")points.Add([engine.PureCompression(),0,0]);return points;
    }
    public static List<double[]> NM(SezioneCA engine,string mode,double n)=>Profili(engine,mode,n,0).Select(p=>new[]{p[1],p[0]}).Concat(Profili(engine,mode,n,Math.PI).AsEnumerable().Reverse().Select(p=>new[]{p[1],p[0]})).ToList();
    public static List<double[]> MM(SezioneCA engine,string mode,double n,double axial,int angles=36)
    {
        var contour=new List<double[]>();
        for(int i=0;i<angles;i++)
        {
            double angle=2*Math.PI*i/angles;var curve=Profili(engine,mode,n,angle);var candidates=new List<double[]>();
            for(int j=1;j<curve.Count;j++){var p=curve[j-1];var q=curve[j];if(Math.Min(p[0],q[0])<=axial&&axial<=Math.Max(p[0],q[0])&&Math.Abs(q[0]-p[0])>1e-12){double t=(axial-p[0])/(q[0]-p[0]);candidates.Add([p[1]+t*(q[1]-p[1]),p[2]+t*(q[2]-p[2])]);}}
            if(candidates.Count>0)contour.Add(candidates.MaxBy(p=>p[0]*Math.Cos(angle)+p[1]*Math.Sin(angle))!);
        }
        return contour;
    }
}

public static class CalcoloSezione
{
    public static JsonObject Calcola(JsonObject data)
    {
        try
        {
            if(data["input"] is not JsonObject original)throw new ArgumentException("Input della sezione mancanti.");
            if(data.D("versione_sezione",1) is not (1 or 2))throw new ArgumentException("Versione della sezione non supportata.");
            var input=(JsonObject)original.DeepClone();var warnings=new List<string>();
            if(new[]{"apply_pile_requirements","dissipative_zone","apply_minimum_eccentricity"}.Any(k=>input.B(k)))warnings.Add("Archivio aggiornato: rimossi requisiti specifici ed eccentricità minima, come richiesto.");
            foreach(var k in new[]{"apply_pile_requirements","dissipative_zone","apply_minimum_eccentricity"})input[k]=false;input["minimum_eccentricity_mm"]="0";
            var engine=new SezioneCA(input);double elasticN=engine.NAutomatico;var results=new JsonObject();var combinations=data["combinazioni"] as JsonObject;
            combinations??=J.Obj(("SLU",new JsonArray(J.Obj(("nome","Combo 1"),("azioni",new[]{input.S("axial_force_kn","0"),input.S("moment_x_knm","0"),input.S("moment_y_knm","0")})))));
            foreach(var limit in new[]{"SLU","SLV","SLE"})
            {
                var rows=combinations.Array(limit);var names=rows.Select(r=>r.S("nome")).ToArray();
                for(int i=0;i<rows.Count;i++)
                {
                    var row=rows[i]!;string name=row.S("nome"),label=limit+" · "+name;
                    try
                    {
                        if(string.IsNullOrWhiteSpace(name)||names.Count(n=>n==name)>1)throw new ArgumentException("Nome combinazione vuoto o duplicato.");
                        var forces=row.Array("azioni");if(forces.Count!=3)throw new ArgumentException("Azioni: richiesti N, Mx, My.");
                        var aa=forces.Select(J.Number).ToArray();if(aa.Any(v=>v is null))throw new ArgumentException("Azioni: inserire numeri finiti.");
                        double axial=aa[0]!.Value,mx=aa[1]!.Value,my=aa[2]!.Value,moment=SezioneCA.Hypot(mx,my),direction=moment!=0?Math.Atan2(my,mx):0;JsonObject result;
                        if(limit=="SLE")result=J.Obj(("stato",SezioneElastica.Tensioni(engine,data.B("n_automatico",true)?elasticN:input.Required("n",strict:true),axial,mx,my)));
                        else if(limit=="SLU")
                        {
                            result=engine.Analyze(axial,mx,my);
                            try{result["resistance_elastic_knm"]=SezioneElastica.Resistenza(engine,elasticN,axial,result.D("direction_rad")).Moment;}
                            catch(ArgumentException ex){warnings.Add(label+", confronto elastico: "+ex.Message);}
                        }
                        else
                        {
                            var (resistance,state)=SezioneElastica.Resistenza(engine,elasticN,axial,direction);var demand=SezioneElastica.Tensioni(engine,elasticN,axial,mx,my);
                            double ratio=Math.Max(demand.sigma_acciaio/engine.Fyd,resistance!=0?moment/resistance:moment!=0?double.PositiveInfinity:0);
                            result=J.Obj(("stato",state),("domanda",demand),("resistance_elastic_knm",resistance),("resistance_moment_knm",engine.Analyze(axial,mx,my).D("resistance_moment_knm")),("utilization",double.IsFinite(ratio)?(object)ratio:"Infinity"),("passed",ratio<=1),("cls_supera_fcd",state.sigma_cls>engine.Fcd));
                            if(state.sigma_cls>engine.Fcd)warnings.Add(label+": σc > fcd al limite acciaio.");
                        }
                        result["azioni"]=J.Node(new[]{axial,mx,my});results[label]=result;
                    }
                    catch(ArgumentException ex){results[label+" ["+(i+1)+"]"]=J.Error(ex.Message);warnings.Add($"{limit} {i+1}: {ex.Message}");}
                }
            }
            warnings.Add("Primo ordine; secondo ordine, taglio, fessurazione e deformabilità non compresi. Le tensioni SLE non equivalgono alla verifica completa di esercizio.");
            return J.Obj(("errore",""),("risultati",results),("n_automatico",elasticN),("fcd",engine.Fcd),("fyd",engine.Fyd),("area_cls_mm2",engine.AreaCls),("area_acciaio_mm2",engine.AreaSteel),("section_outline",engine.Outline),("bars",engine.Bars.Select(b=>new[]{b.X,b.Y,b.Area,b.Diametro}).ToArray()),("avvisi",warnings));
        }
        catch(ArgumentException ex){return J.Error(ex.Message);}
    }
}
