using GPC.Checkers.Geotechnics.Piles;
internal static class ViggianiChecks
{
    internal static int Run()
    {
        int count=0;
        void Check(bool b,string name){if(!b)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Near(double a,double b,double tolerance,string name)=>Check(Math.Abs(a-b)<=tolerance*Math.Max(1e-12,Math.Abs(b)),name+$" actual={a:G9}, reference={b:G9}, relative={Math.Abs(a-b)/Math.Max(1e-12,Math.Abs(b)):G6}");
        void Bad(Action a,string name){try{a();throw new Exception("Accepted "+name);}catch(ArgumentException){Check(true,name);}}
        HorizontalSoilAssignment Sand(HorizontalSoilMode mode=HorizontalSoilMode.SandTable145)=>new(){Name="Sabbia",Thickness=20,Mode=mode,Density="Medio",Conditions="Benchmark con parametri espliciti",A=600,UnitWeight=18,SaturatedUnitWeight=20};
        HorizontalSoilProfile Resolve(HorizontalSoilAssignment s,double? water=null)=>ViggianiHorizontalSoil.Resolve(new[]{s},water);
        foreach(var (density,min,max,recommended,dry,wet) in new[]{("Sciolto",100d,300d,200d,2.5,1.5),("Medio",300d,1000d,600d,7.5,5d),("Denso",1000d,3000d,1500d,20d,12d)})
        {
            var row=ViggianiHorizontalSoil.SandTable.Single(r=>r.Density==density);
            Check(row.MinimumA==min&&row.MaximumA==max&&row.RecommendedA==recommended&&row.DryNhNPerCm3==dry&&row.SubmergedNhNPerCm3==wet,"table 14.5 full row "+density);
            var s=Sand();s.Density=density;Near(Resolve(s).Layers[0].Value,dry*1000,1e-14,"dry nh conversion "+density);Near(Resolve(s,0).Layers[0].Value,wet*1000,1e-14,"submerged nh conversion "+density);
        }
        string[] ids={"clay-reese-1956","clay-davisson-1963","organic-peck-1970","organic-davisson-1970","peat-davisson-1970","peat-wilson-1967","loess-bowles-1968"};
        double[] lows={.2,.3,.1,.1,.05,.03,8},highs={3.5,.5,1,.8,.05,.1,10};
        string[] authors={"Reese, Matlock, 1956","Davisson, Prakash, 1963","Peck, Davisson, 1970","Davisson, 1970","Davisson, 1970","Wilson, Hilts, 1967","Bowles, 1968"};
        for(int i=0;i<ids.Length;i++)
        {
            var row=ViggianiHorizontalSoil.CohesiveTable.Single(r=>r.Id==ids[i]);Check(row.MinimumNPerCm3==lows[i]&&row.MaximumNPerCm3==highs[i]&&row.Author==authors[i],"table 14.6 separate source "+ids[i]);
            var s=new HorizontalSoilAssignment{Thickness=20,Mode=HorizontalSoilMode.CohesiveTable146,CohesiveRowId=ids[i],Conditions="Test scelta esplicita"};Bad(()=>Resolve(s),"no automatic midpoint "+ids[i]);s.SelectedNhNPerCm3=highs[i];Near(Resolve(s).Layers[0].Value,highs[i]*1000,1e-14,"table 14.6 selected value "+ids[i]);
        }
        Near(ViggianiHorizontalSoil.ToKnPerM3(1),1000,1e-14,"1 N/cm³ = 1000 kN/m³");
        var correlation=Sand(HorizontalSoilMode.SandCorrelation);var profile=Resolve(correlation,3.17);
        Near(profile.Layers[0].Value,8000,1e-14,"A gamma / 1.35 above water");Near(profile.Layers[1].Value,4528.888888888889,1e-14,"A (gammaSat-gammaW) / 1.35 below water");Near(profile.Determinations[1].AdoptedUnitWeight!.Value,10.19,1e-14,"submerged effective unit weight");Check(profile.Layers.Count==2&&profile.Layers.All(l=>l.OriginalLayer==1),"water splits numerical layer preserves original index");
        correlation.A=null;Bad(()=>Resolve(correlation),"A recommendation never silently assigned");correlation.A=2000;Bad(()=>Resolve(correlation),"A outside selected density range");correlation.A=600;correlation.SaturatedUnitWeight=9;Bad(()=>Resolve(correlation,0),"invalid submerged unit weight");
        var over=Sand();over.OverrideNh=1234;Bad(()=>Resolve(over),"override requires reason");over.OverrideReason="Taratura dichiarata";var overridden=Resolve(over);Near(overridden.Layers[0].Value,1234,1e-14,"manual override adopted");Check(overridden.Determinations[0].BaseValue==7500&&overridden.Determinations[0].Overridden,"override preserves base and traceability");
        ElasticPileInput P(IReadOnlyList<ElasticPileLayer> layers)=>new(){Length=20,Diameter=1,EI=50000,EISource="Benchmark EI",Force=100,Step=.25,Layers=layers};
        var tab=Resolve(Sand());var tabResult=ElasticPile.Calculate(P(tab.Layers));var manual=ElasticPile.Calculate(P(new[]{new ElasticPileLayer{Thickness=20,Law=ElasticSoilLaw.LinearKhReference,Value=7500}}));Near(tabResult.HeadDisplacement,manual.HeadDisplacement,1e-12,"assisted/manual nh equivalent");
        var water=Resolve(Sand(),3.17);var waterResult=ElasticPile.Calculate(P(water.Layers));var sides=waterResult.Points.Where(p=>Math.Abs(p.GroundDepth-3.17)<1e-10).ToArray();Check(sides.Length==2,"mesh node at water discontinuity");Near(sides[0].DistributedStiffness,7500*3.17,1e-12,"global z above water");Near(sides[1].DistributedStiffness,5000*3.17,1e-12,"global z below water no reset");Check(sides.All(p=>p.Layer==1)&&sides[0].Nh==7500&&sides[1].Nh==5000,"nh and original layer exported on both sides");
        var doubled=P(tab.Layers);doubled.Diameter=2;Near(ElasticPile.Calculate(doubled).HeadDisplacement,tabResult.HeadDisplacement,1e-10,"linear nh law has no second diameter factor");
        var p=P(new[]{new ElasticPileLayer{Thickness=30,Law=ElasticSoilLaw.LinearKhReference,Value=5000}});p.Length=30;
        var longPile=ElasticPile.Calculate(p);double nh=5000,ei=50000;
        // Viggiani printed p.467 rounded coefficients, L/lambda > 4; 2% allows the printed approximate constants.
        Near(longPile.HeadDisplacement,2.40*100/(Math.Pow(nh,.6)*Math.Pow(ei,.4)),.02,"book p467 long free head y coefficient 2.40");Near(-longPile.HeadRotation,1.60*100/(Math.Pow(nh,.4)*Math.Pow(ei,.6)),.02,"book p467 long free head theta coefficient 1.60");
        p.FixedHeadRotation=true;var fixedHead=ElasticPile.Calculate(p);Near(fixedHead.HeadDisplacement,.93*100/(Math.Pow(nh,.6)*Math.Pow(ei,.4)),.01,"book p467 fixed rotation y coefficient 0.93");
        p.FixedHeadRotation=false;p.Force=0;p.HeadMoment=-1;var couple=ElasticPile.Calculate(p);Near(couple.HeadDisplacement,1.60/(Math.Pow(nh,.4)*Math.Pow(ei,.6)),.02,"book p467 head couple displacement");Near(-couple.HeadRotation,1.74/(Math.Pow(nh,.2)*Math.Pow(ei,.8)),.02,"book p467 head couple rotation");
        p.Length=.2;p.Force=.001;p.HeadMoment=0;p.Step=.05;var rigid=ElasticPile.Calculate(p);Near(rigid.HeadDisplacement,18*p.Force/(p.Length*p.Length*nh),.001,"book p467 rigid limit displacement");Near(-rigid.HeadRotation,24*p.Force/(Math.Pow(p.Length,3)*nh),.001,"book p467 rigid limit rotation");
        p.Length=30;p.Force=100;p.HeadMoment=-100;p.Step=.5;var coarse=ElasticPile.Calculate(p);var medium=ElasticPile.Calculate(p,2);var fine=ElasticPile.Calculate(p,4);
        foreach(string key in new[]{"y","M","V"}){double A(ElasticPileResult r)=>key=="y"?r.HeadDisplacement:r.Extrema[key].AbsoluteMaximum;double change=Math.Abs(A(fine)-A(medium))/Math.Abs(A(fine));Check(change<.001,$"Reese–Matlock convergence {key}: coarse={A(coarse):G9}, medium={A(medium):G9}, fine={A(fine):G9}, relative={change:G6}");}
        Check(Math.Abs(fine.ForceResidual)<1e-6&&Math.Abs(fine.MomentResidual)<1e-5,"linear nh global equilibrium");
        Console.WriteLine($"VIGGIANI TOTAL {count} passed");return count;
    }
}
