using System.Text.Json.Nodes;
using X.Core;

internal static class ConcreteModuleChecks
{
    internal static int Run()
    {
        int count=0;void Check(bool ok,string message){if(!ok)throw new Exception("Nuovo modulo CA: "+message);count++;}
        void Near(double a,double b,double rel,string message)=>Check(Math.Abs(a-b)<=rel*Math.Max(1,Math.Abs(b)),message+$" ({a:G9} / {b:G9})");
        var data=SezioneCA.DefaultData();var w=SectionWorkspace.Prepare(data);var input=data["input"]!.AsObject();
        var options=(JsonObject)w["dominio3d"]!.DeepClone();options["angoli"]="16";options["criterio"]="N costante";
        var engine=new CheckerSection(input,w,options);var domain=engine.Domain3D();var check=domain.Check(new(-500,100,50));
        Check(check.LimitState is not null&&!check.LimitState.IsValueCreated,"Piano limite conservato senza campionamento anticipato");
        var limit=check.LimitState!.Value;
        Near(limit.Response!.EcMin!.Value,check.Response!.EcMin!.Value,1e-10,"Piano del limite identico al riepilogo");
        Check(ReferenceEquals(limit,check.LimitState.Value),"Riutilizzo del dettaglio limite");
        var plane=limit.Native.StrainPlane;
        foreach(var bar in engine.Geometry.Bars.Take(4))Near(plane.GetStrain(bar.X,bar.Y),plane.StrainReferencePoint+plane.ChiX*(bar.X-plane.ReferencePoint.X)+plane.ChiY*(bar.Y-plane.ReferencePoint.Y),1e-10,"Equazione visualizzata del piano");
        var curve=new MomentCurvatureCalculator().Calculate(new(-500,0,10,.8),domain.Check,a=>engine.Stress(a,"CURVA"),engine.Geometry.Fyd/engine.Geometry.Es);
        Check(curve.Points.Count==11,"Undici equilibri nella curva M–χ: "+curve.Status);
        Check(curve.Points.All(p=>double.IsFinite(p.Curvature))&&curve.Points[^1].Curvature>curve.Points[0].Curvature,"Curvatura finita crescente nel caso regolare");
        var zeroCurve=new MomentCurvatureCalculator().Calculate(new(0,0,10,1),domain.Check,a=>engine.Stress(a,"CURVA"),engine.Geometry.Fyd/engine.Geometry.Es);
        Check(zeroCurve.Points.Count>=11&&zeroCurve.UltimateCurvature>0,"Curva completa a N nullo: "+zeroCurve.Status);
        Check(zeroCurve.YieldCurvature<zeroCurve.UltimateCurvature,"Snervamento raffinato distinto dal punto ultimo");
        var elastic=new CheckerSection(input,w,options,"SLV").Domain3D().Check(new(-500,100,50));
        Near(elastic.LimitState!.Value.Response!.SMax!.Value,elastic.Response!.SMax!.Value,1e-10,"Piano del limite elastico identico al riepilogo");
        input["foro_presente"]=true;input["inner_width_mm"]="200";input["inner_height_mm"]="300";
        var hollow=CheckerSection.PrepareModel(input,w);
        Near(hollow.Section.Area,600*800-200*300,1e-10,"Area rettangolare cava nativa");
        Near(hollow.Geometry.Fibers.Sum(f=>f.Area),hollow.Section.Area,1e-10,"Fibre escludono il foro rettangolare");
        Near(hollow.Section.Jxx,(600*Math.Pow(800,3)-200*Math.Pow(300,3))/12,1e-10,"Inerzia rettangolare cava nativa");
        var hstressOptions=(JsonObject)w["sle"]!["SLE_QP"]!.DeepClone();hstressOptions["trazione_cls"]="Sì";
        var hollowEngine=new CheckerSection(input,w,hstressOptions);
        var hs=hollowEngine.Stress(new(-300,0,0),"SLE_QP");Check(hs.ConcreteVertices.All(v=>v.Stress<0),"Equilibrio della sezione cava");
        var invalid=(JsonObject)input.DeepClone();invalid["barre_manuali"]=new JsonArray(J.Obj(("x","0"),("y","0"),("phi","20")));
        try{CheckerSection.PrepareModel(invalid,w);throw new Exception("Barra nel vuoto accettata");}catch(ArgumentException){count++;}
        input["shape"]="Circolare";input["inner_diameter_mm"]="500";
        var ring=CheckerSection.PrepareModel(input,w);
        Check(ring.Geometry.Outline.Count==32&&ring.Geometry.Holes[0].Length==32,"Default circolare: 32 lati esterni e interni");
        foreach(int sides in new[]{32,64,180})
        {
            var configured=(JsonObject)input.DeepClone();configured["circular_sides"]=sides.ToString();
            var model=CheckerSection.PrepareModel(configured,w);double angle=2*Math.PI/sides;
            Check(model.Geometry.Outline.Count==sides&&model.Geometry.Holes[0].Length==sides,"Lati assegnati ai due contorni: "+sides);
            Near(model.Section.Area,sides/2d*Math.Sin(angle)*(500*500-250*250),1e-10,"Area nativa rispetto alla formula del poligono: "+sides);
            Near(model.Section.Jxx,sides/24d*Math.Sin(angle)*(2+Math.Cos(angle))*(Math.Pow(500,4)-Math.Pow(250,4)),1e-10,"Inerzia nativa rispetto alla formula del poligono: "+sides);
        }
        foreach(string invalidSides in new[]{"0","31","32.5","724","non valido"})
        {
            var configured=(JsonObject)input.DeepClone();configured["circular_sides"]=invalidSides;
            try{_ = new SezioneCA(configured);throw new Exception("Lati non validi accettati: "+invalidSides);}catch(ArgumentException){count++;}
        }
        var oldInput=(JsonObject)input.DeepClone();oldInput.Remove("circular_sides");
        Check(new SezioneCA(oldInput).CircularSides==32,"Archivi senza parametro: default 32 lati");
        var sx=SectionShearGeometry.Derive(ring.Geometry,true);var sy=SectionShearGeometry.Derive(ring.Geometry,false);
        Near(sx.Bw,500,1e-12,"bw anello = D−Di");Near(sx.Depth,sy.Depth,1e-8,"d circolare simmetrico nelle due direzioni");
        input["shape"]="Rettangolare";input["foro_presente"]=false;input["staffe_presenti"]="No";
        var noStirrups=new SezioneCA(input);Near(noStirrups.Bars[0].Y,800d/2-70-20d/2,1e-10,"Barre senza staffe: copriferro geometrico coerente");
        var sle=(JsonObject)w["sle"]!["SLE_QP"]!.DeepClone();sle["esposizione"]="XC1";sle["spaziatura_fessure"]="100";
        var tensionEngine=new CheckerSection(input,w,sle);var force=new ActionPoint(300,0,0);var stress=tensionEngine.Stress(force,"SLE_QP");
        var crack=Ntc2018Checks.Cracking(tensionEngine,stress,force,input,w,sle,"SLE_QP");
        Check(crack.Width>0&&crack.Regions.Length==4,"Trazione pura: quattro facce calcolate");
        Near(crack.Width!.Value,crack.Regions.Max(r=>r.Width)!.Value,1e-10,"Apertura governante senza sommare aree di facce distinte");
        Check(crack.Regions.All(r=>r.Area>0&&r.BarIndices.Length>0),"Fasce e barre per ogni faccia");
        var crackedHole=(JsonObject)input.DeepClone();crackedHole["foro_presente"]=true;crackedHole["inner_width_mm"]="200";crackedHole["inner_height_mm"]="300";
        var crackedHoleEngine=new CheckerSection(crackedHole,w,sle);var crackedHoleState=crackedHoleEngine.Stress(force,"SLE_QP");
        var holeCrack=Ntc2018Checks.Cracking(crackedHoleEngine,crackedHoleState,force,crackedHole,w,sle,"SLE_QP");
        Check(holeCrack.Width>0&&holeCrack.Passed!=true&&holeCrack.Status.Contains("superficie del foro"),"Apertura esterna calcolata senza dichiarare verificata la superficie interna");
        var a=new ConcreteAnchorageCalculator().Calculate(new(20,400,2,1.5,true,1000,false,100,0));
        Near(a.Fbd,3,1e-12,"Aderenza 2,25 fctk/γc");Near(a.RequiredLength,2000d/3,1e-12,"Ancoraggio rettilineo analitico");
        var td=new ConcreteDetailingInput(ConcreteMemberKind.Slab,noStirrups,0,false,0,0,0,20,20,10,false,500,150,false,false,false);
        Check(new ConcreteDetailingCalculator().Calculate(td).All(r=>!r.Name.StartsWith("Staffe minime")),"Soletta senza staffatura minima da trave");
        var col=new ConcreteDetailingCalculator().Calculate(td with{Kind=ConcreteMemberKind.Column});Check(col.Any(r=>r.Name=="Diametro staffe"&&r.Passed==false),"Pilastro senza staffe non soddisfatto");
        Check(J.Node(col) is JsonArray,"Dettagli non soddisfatti esportabili senza infinito");
        var thin=(JsonObject)input.DeepClone();thin["foro_presente"]=true;thin["inner_width_mm"]="390";thin["inner_height_mm"]="600";
        var thinChecks=new ConcreteDetailingCalculator().Calculate(td with{Section=new SezioneCA(thin),Kind=ConcreteMemberKind.Beam});
        Check(thinChecks.Any(r=>r.Name=="Margine copriferro barre longitudinali"&&r.Passed==false),"Copriferro insufficiente al bordo del foro");
        var shear=Ntc2018Checks.Shear(0,50,240000,300,450,0,30,17,400,1.5,200,150,90,1);
        var t=new ConcreteTorsionCalculator().Calculate(new(10,new(100000,1300,80),17,400,100,150,1000,1,50,0,shear,shear));
        Near(t.TRcd,68,1e-12,"TRcd = 2 Ak t (0,5 fcd) cot/(1+cot²)");Near(t.TRsd,53.3333333333333,1e-12,"TRsd ramo singolo");
        Near(t.RequiredLongitudinalArea,162.5,1e-12,"Area longitudinale richiesta a torsione");
        var zeroSteel=new ConcreteTorsionCalculator().Calculate(new(10,new(100000,1300,80),17,400,100,150,0,1,50,0,shear,shear));
        Check(!zeroSteel.Passed&&zeroSteel.TorsionRatio is null&&J.Node(zeroSteel) is JsonObject,"Torsione senza As disponibile: fallisce ed è esportabile");
        Near(t.ConcreteCombinedRatio!.Value,10/t.TRcd+50/shear.VRcd,1e-12,"Interazione monoassiale NTC [4.1.40]");
        var rows=new[]{new SectionActionsExcel.Row("Taglio","Torsione",-100,null,null,20,30,-12.34567)};
        Check(SectionActionsExcel.Read(SectionActionsExcel.Write(rows)).Rows.SequenceEqual(rows),"Torsione Excel round trip");
        Console.WriteLine($"Modulo CA ampliato: {count} controlli superati.");return count;
    }
}
