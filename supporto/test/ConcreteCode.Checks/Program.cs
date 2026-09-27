using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

int count=0;
void Check(bool value,string message) { if(!value)throw new Exception(message); count++; }
void Near(double value,double expected,string message,double tolerance=1e-9) => Check(double.IsFinite(value)&&Math.Abs(value-expected)<=tolerance*Math.Max(1,Math.Abs(expected)), $"{message}: {value:G17} != {expected:G17}");
void Reject(Action action,string message) { try { action(); } catch(ArgumentException) { count++; return; } throw new Exception(message); }
try
{
    var refs=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"reference.json")))!;
    foreach(var row in refs["shear"]!.AsArray())
    {
        var p=row!["input"]!.Deserialize<ConcreteCodeChecks.ShearInput>()!;
        var r=ConcreteCodeChecks.Shear(p);
        Near(r.VRd,row.D("expected"),"fib shear "+p.Standard);
        if(J.Number(row["steel"]) is double rs)Near(r.VRsd,rs,"fib VRds");
        if(J.Number(row["concrete"]) is double rc)Near(r.VRcd,rc,"fib VRdmax");
        Check(r.Details.Length>0&&r.Reference.Length>0,"Traccia normative assente");
        var reversed=ConcreteCodeChecks.Shear(p with{V=-p.V,M=-p.M});
        Near(r.VRd,reversed.VRd,"Inversione azioni");
    }
    foreach(var row in refs["cracks"]!.AsArray())
    {
        var p=row!["input"]!.AsArray(); double D(int i)=>p[i]!.GetValue<double>();
        double w=ConcreteCodeChecks.CrackWidth("EN 1992-1-1",D(0),D(1),D(2),D(3),D(4),D(5),D(6),D(7),D(8),p[9]!.GetValue<bool>(),p[10]!.GetValue<bool>(),D(11));
        Near(w,row.D("expected"),"fib wk");
    }
    var din=new ConcreteCodeChecks.ShearInput("DIN EN 1992-1-1",0,343.25,0,150000,300,450,1933.5,30,17,500/1.15,1.5,200000,128.4,100,90,1,384d/450);
    Near(ConcreteCodeChecks.Shear(din).VRcd,734.4,"SOFiSTiK DCE-EN6 puntone DIN");
    Reject(()=>ConcreteCodeChecks.Shear(din with{CotTheta=2}),"DIN accetta cot oltre limite dipendente dal taglio");
    var ds=din with{Standard="DS EN 1992-1-1",CotTheta=1,LeverFactor=.9};
    Near(ConcreteCodeChecks.Shear(ds).VRcd,405*300*(.7-30d/200)*17/2/1000,"DS 5.103 NA");
    var ns=din with{Standard="NS EN 1992-1-1",Asw=0,Aggregate=8};
    Check(ConcreteCodeChecks.Shear(ns).VRd<ConcreteCodeChecks.Shear(ns with{Aggregate=20}).VRd,"NS granulometria non considerata");
    Reject(()=>ConcreteCodeChecks.Shear(ns with{Asw=100,N=1000,CotTheta=2}),"NS forte trazione senza limite cot");
    foreach(string norm in ConcreteStandards.Names.Where(n=>n!="CNR-DT 204/2006"&&n!="CS-TR34"))
    {
        foreach(var shape in new[]{"Rettangolare","A T","Circolare","Rettangolare cava","Circolare cava"})
        {
            var data=SezioneCA.DefaultData();var input=data["input"]!.AsObject();var settings=SectionWorkspace.Prepare(data);
            settings["normativa"]=norm; settings["coefficienti"]=ConcreteStandards.Defaults(norm);
            foreach(var(k,v) in ConcreteCalculationSettings.CommonCoefficients)input[k]=settings["coefficienti"]![v]!.DeepClone();
            input["shape"]=shape.Replace(" cava","");
            if(shape.EndsWith("cava")){input["foro_presente"]=true;input["inner_width_mm"]="200";input["inner_height_mm"]="300";input["inner_diameter_mm"]="400";}
            ConcreteCalculationSettings.Prepare(input,settings);
            var o=settings["taglio"]!.AsObject();o["modello_circolare"]="Parametri assegnati";o["z_d"]="0.75";ConcreteCalculationSettings.UpdateAutomaticShear(input,o);o["ancoraggio"]="Confermato";
            var loads=J.Obj(("N","-300"),("Mx","60"),("My","25"),("Vx","50"),("Vy","80"),("T","0"));
            var s=ConcreteShearAnalysis.Calculate(input,settings,o,loads);
            Check(s.Shear.All(r=>r.VRd>0&&r.Ratio.HasValue),norm+" taglio "+shape);
            var stressOptions=settings["sle"]!["SLE_QP"]!.AsObject();stressOptions["esposizione"]="XC3";stressOptions["limite_fessure"]="0.3";stressOptions["spaziatura_fessure"]="150";
            var engine=new CheckerSection(input,settings,stressOptions);var force=new ActionPoint(0,180,20);var state=engine.Stress(force,"SLE_QP");
            var crack=Ntc2018Checks.Cracking(engine,state,force,input,settings,stressOptions,"SLE_QP");
            Check(crack.Width>=0&&crack.Details.Length>0,norm+" fessure "+shape+": "+crack.Status);
            Check(crack.Regions.Length>0,norm+" regioni "+shape);
            var compressed=engine.Stress(new(-300,0,0),"SLE_QP");
            var uncracked=Ntc2018Checks.Cracking(engine,compressed,new(-300,0,0),input,settings,stressOptions,"SLE_QP");
            Check(uncracked.Width==0&&uncracked.Passed==true,norm+" compressione "+shape);
        }
    }
    foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1})
        Reject(()=>ConcreteCodeChecks.Shear(din with{Bw=invalid}),"bw non valido accettato");
    Reject(()=>ConcreteCodeChecks.CrackWidth("CNR-DT 204/2006",200,200000,33000,3,.01,20,30,100,400,true,true,.5),"FRC calcolato implicitamente");
    var options=J.Obj(("esposizione","XD3"));
    Check(ConcreteCodeChecks.CrackRequirement("NS EN 1992-1-1","SLE_FREQ",options).Limit==.3,"NS XD3 frequente");
    Check(ConcreteCodeChecks.CrackRequirement("NS EN 1992-1-1","SLE_QP",options).Limit is null,"NS famiglia errata");
    Check(ConcreteCodeChecks.CrackRequirement("Model Code 2010","SLE_QP",options).Limit is null,"MC limite inventato");
    // Closed-form benchmarks independent of the section mesh and of the C# helpers.
    var uni=din with{Standard="UNI EN 1992-1-1",N=-500,CotTheta=1,LeverFactor=.9};
    Near(ConcreteCodeChecks.Shear(uni).VRcd,405*300*.5*17/2/1000,"UNI ordinary concrete alpha_cw=1, official NA p.83");
    Near(ConcreteCodeChecks.Shear(uni with{N=0}).VRcd,ConcreteCodeChecks.Shear(uni).VRcd,"UNI compression does not introduce prestress enhancement");
    var zero=ConcreteCodeChecks.Shear(din with{V=0,CotTheta=null});
    Near(zero.Ratio!.Value,0,"Zero action gives zero utilization");
    foreach(var norm in new[]{"EN 1992-1-1","DIN EN 1992-1-1","DS EN 1992-1-1","NS EN 1992-1-1","UNI EN 1992-1-1","Model Code 2010"})
        foreach(double alpha in new[]{45d,60d,90d})
        {
            var p=din with{Standard=norm,Alpha=alpha,CotTheta=null,N=0,V=50};
            var auto=ConcreteCodeChecks.Shear(p); double sampled=0;
            for(int i=0;i<=500;i++)
            {
                double c=.4+i*.006;
                try { sampled=Math.Max(sampled,ConcreteCodeChecks.Shear(p with{CotTheta=c}).VRd); } catch(ArgumentException) { }
            }
            Check(auto.VRd>=sampled-1e-7,"Auto cot misses maximum: "+norm+" alpha "+alpha);
        }
    double Crack(string norm,bool shortTerm=true,double spacing=100) => ConcreteCodeChecks.CrackWidth(norm,200,200000,33000,3,.01,20,35,spacing,350,shortTerm,true,.5);
    Near(Crack("DIN EN 1992-1-1"), (200*20/(3.6*3))*.6*200/200000,"DIN hand calculation near bars, stress cap governs");
    Near(ConcreteCodeChecks.CrackWidth("DIN EN 1992-1-1",200,200000,33000,3,.01,20,35,500,200,true,true,.5), 1.3*200*.6*200/200000,"DIN sparse bars equation 7.14");
    Near(Crack("DIN EN 1992-1-1",false),Crack("DIN EN 1992-1-1"),"DIN kt=0.4 both durations");
    Near(Crack("DS EN 1992-1-1"), (3.4*Math.Pow(25d/35,2d/3)*35+.8*.5*.425*20/.01)*.6*200/200000,"DS national k3");
    Near(Crack("Model Code 2010"),2*(35+20/(4*1.8*.01))*.4*200/200000,"MC short hand calculation");
    Check(Crack("Model Code 2010",false)>Crack("Model Code 2010"),"MC duration affects transfer length and strain");
    foreach(var norm in ConcreteStandards.OrdinaryNames)
        Near(ConcreteCodeChecks.CrackWidth(norm,0,200000,33000,3,.01,20,35,100,350,false,true,1),0,"Zero stress gives zero crack "+norm);
    var geometryInput=SezioneCA.DefaultData()["input"]!.AsObject();geometryInput["shape"]="Rettangolare";
    var rectangular=new SezioneCA(geometryInput);
    Near(ConcreteCodeChecks.EffectiveCrackDepth("DS EN 1992-1-1",rectangular,0,1,rectangular.Outline.Max(p=>p[1]),rectangular.Height,50,300,false),100,"DK centroid rule 2(h-d) on rectangle");
    Near(ConcreteCodeChecks.EffectiveCrackDepth("EN 1992-1-1",rectangular,0,1,rectangular.Outline.Max(p=>p[1]),600,50,150,false),50,"EC neutral-axis bound");
    Near(ConcreteCodeChecks.EffectiveCrackDepth("DIN EN 1992-1-1",rectangular,0,1,rectangular.Outline.Max(p=>p[1]),600,50,450,true),160,"DIN depth-dependent coefficient");
    foreach(var norm in ConcreteStandards.OrdinaryNames)
    {
        var data=SezioneCA.DefaultData();var input=data["input"]!.AsObject();input["shape"]="Rettangolare";
        var settings=SectionWorkspace.Prepare(data);settings["normativa"]=norm;settings["coefficienti"]=ConcreteStandards.Defaults(norm);
        ConcreteCalculationSettings.Prepare(input,settings);
        var opt=settings["sle"]!["SLE_QP"]!.AsObject();opt["esposizione"]="XC3";opt["limite_fessure"]="0.3";opt["spaziatura_fessure"]="150";
        var engine=new CheckerSection(input,settings,opt);var force=new ActionPoint(100,0,0);
        var r=Ntc2018Checks.Cracking(engine,engine.Stress(force,"SLE_QP"),force,input,settings,opt,"SLE_QP");
        Check(r.Width>0&&r.Passed.HasValue&&r.Regions.Length>=4,"Uniform tension faces "+norm);
        if(norm.StartsWith("DS")) Check(r.Regions.Any(v=>v.Name=="Sistema grossolano DS"),"DK coarse crack system absent");
        input["foro_presente"]=true;input["inner_width_mm"]="200";input["inner_height_mm"]="300";
        var externalBars=new SezioneCA(input).Bars;
        input["barre_manuali"]=new JsonArray(externalBars.Select(b=>(JsonNode)J.Obj(("x",b.X),("y",b.Y),("phi",b.Diametro))).ToArray());
        foreach(var(x,y) in new[]{(0d,180d),(0d,-180d),(130d,0d),(-130d,0d)})
            input["barre_manuali"]!.AsArray().Add(J.Obj(("x",x),("y",y),("phi",16)));
        engine=new CheckerSection(input,settings,opt);
        r=Ntc2018Checks.Cracking(engine,engine.Stress(force,"SLE_QP"),force,input,settings,opt,"SLE_QP");
        Check(r.Regions.Count(v=>v.Name.StartsWith("Parete interna"))==4,"Four inner wall bands "+norm);
        Check(r.Passed.HasValue&&r.Regions.Where(v=>v.Name.StartsWith("Parete interna")).All(v=>v.Width.HasValue),"Inner reinforced faces calculable "+norm+": "+r.Status);
        Near(CrackCalculationSummary.Values(r).Single(v=>v.Symbol=="wk").Value!.Value,r.Width!.Value,"Governing inner/outer width in summary "+norm);
    }
    Console.WriteLine($"PASS · {count} controlli normative CA (riferimenti fib, benchmark DIN, formule DS/NS, integrazione catalogo).");
    return 0;
}
catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
