using GPC.Checkers.Concrete.Piles;

static class PileSeismicChecks
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        void Near(double actual,double expected,string name)=>Check(Math.Abs(actual-expected)<1e-6,name);
        void Bad(Action action,string name){try{action();throw new Exception("Accepted "+name);}catch(ArgumentException){Check(true,name);}}
        var p=ReinforcementChecks.Parameters(24,24);p.LinkSpacing=144;p.Fcd=20;p.PileMinimumRequirements=false;
        var service=new PileReinforcement(p);
        var settings=new PileSeismicSettings{Enabled=true,SeismicActionsConfirmed=true,ElasticMomentConfirmed=true,Links=PileSeismicLinks.SingleHoops};
        PileSectionCheck Section(double x=2,double n=1000,double v=100,double m=100)=>new(){Action=new(){Depth=x,N=n,V=v,M=m},VRd=130,MRdPositive=200,MRdNegative=-150};
        PileSeismicResult Run(double a=0,double b=12,params PileSectionCheck[] points)=>service.CheckHeadSeismic(settings,a,b,20,points.Length==0?[Section(),Section(10),Section(12)]:points);
        PileSeismicRule Rule(PileSeismicResult r,string key)=>r.Checks.Single(c=>c.Key==key);
        var initial=Run();Near(initial.HeadEnd,10,"NTC head zone10D with D1m");
        var displayed=PileReinforcement.DescribeSeismicHead(800,20,12);Near(displayed.NominalLength,8,"graphic10D metadata uses structural diameter converted from mm");Near(displayed.AdoptedEnd,12,"graphic assigned zone differs from minimum");Check(displayed.MeetsMinimum,"longer graphic zone satisfies minimum");
        displayed=PileReinforcement.DescribeSeismicHead(1000,6,5);Near(displayed.MinimumEnd,6,"graphic minimum clipped only at physical toe");Check(!displayed.MeetsMinimum,"graphic metadata flags inadequate user zone");
        Near(Rule(initial,"SeismicSteelArea").Limit!.Value,7853.98163397448,"independent1percent full circular area mm2");
        Near(Rule(initial,"SeismicSteelArea").Actual!.Value,10857.3442108063,"independent24phi24 steel area");
        Check(Rule(initial,"SeismicLinkSpacing").Passed==true,"6phi equality passes for links144mm and phi24");
        Near(Rule(initial,"SeismicCompression").Actual!.Value,1.27323954473516,"1000kN over1m pile pressure inMPa");
        Near(Rule(initial,"SeismicCompression").Limit!.Value,9,"0.45fcd for fcd20MPa");
        Check(Rule(initial,"SeismicShear").Passed==true,"VRd130 equals1.3V100 passes");
        Near(Rule(initial,"SeismicElasticMoment").Limit!.Value,300,"positive moment uses positive MRd at same N");
        var negative=Run(0,12,Section(m:-224.9));Check(Rule(negative,"SeismicElasticMoment").Passed==true,"negative branch uses150kNm resistance");
        Check(Rule(Run(0,12,Section(m:-225)),"SeismicElasticMoment").Passed==false,"strict elastic moment equality1.5MRd rejected");
        Check(Rule(Run(0,12,Section(v:-100.01)),"SeismicShear").Passed==false,"shear sign uses magnitude and1.3margin");
        Check(Rule(Run(0,12,Section(n:7100)),"SeismicCompression").Passed==false,"independent7100kN exceeds0.45fcd");
        Check(Rule(Run(0,12,Section(n:7000)),"SeismicCompression").Passed==true,"independent7000kN below0.45fcd");
        Check(Rule(Run(0,12,Section(n:-1000)),"SeismicCompression").Actual==0,"tension is not positive compressive stress");
        settings.SeismicActionsConfirmed=false;var pending=Run();Check(Rule(pending,"SeismicShear").Passed==null&&Rule(pending,"SeismicCompression").Passed==null,"unconfirmed seismic actions never give pass");
        Check(Rule(pending,"SeismicSteelArea").Passed==true,"geometry can be checked without seismic actions");
        settings.SeismicActionsConfirmed=true;settings.ElasticMomentConfirmed=false;Check(Rule(Run(),"SeismicElasticMoment").Passed==null,"generic elastic FEM is not proof of q1actions");settings.ElasticMomentConfirmed=true;
        settings.StandardNtc2018=false;Check(Rule(Run(),"SeismicStandard").Passed==null&&Rule(Run(),"SeismicShear").Passed==null,"custom coefficients cannot certify NTC seismic demands");settings.StandardNtc2018=true;
        double gamma=p.Standard.GammaC;p.Standard.GammaC=1;Check(Rule(Run(),"SeismicStandard").Passed==null,"actual modified gamma detected independently of UI flag");p.Standard.GammaC=gamma;
        settings.Links=PileSeismicLinks.Spiral;Check(Rule(Run(),"SeismicSingleHoops").Passed==false,"spiral forbidden as single hoop in dissipative head");
        settings.Links=PileSeismicLinks.Unspecified;Check(Rule(Run(),"SeismicSingleHoops").Passed==null,"unknown transverse type remains pending");settings.Links=PileSeismicLinks.SingleHoops;
        var below=Run(12,20,Section(12,n:10000),Section(20));Check(!below.IntersectsHeadZone&&!below.Checks.Any(c=>c.Key=="SeismicCompression"),"0.45fcd applies only inside dissipative zone");
        Near(Rule(below,"SeismicSteelArea").Limit!.Value,2356.19449019234,"outside head zone uses0.3percent");
        Near(Rule(below,"SeismicLinkSpacing").Limit!.Value,192,"outside head zone uses8phi");
        Check(below.Checks.Any(c=>c.Key=="SeismicShear")&&below.Checks.Any(c=>c.Key=="SeismicElasticMoment"),"shear and elastic moment safeguards cover whole pile");
        Check(Run(6,12,Section(6),Section(10),Section(12)).IntersectsHeadZone,"zone crosses independent reinforcement segments");
        Check(!Run(10,20,Section(10),Section(20)).IntersectsHeadZone,"segment beginning exactly at zone end is outside");
        var critical=Run(0,12,Section(2,n:1000),Section(10,n:7100),Section(12,n:9000));Near(Rule(critical,"SeismicCompression").Depth!.Value,10,"critical compression at zone boundary excludes deeper points");
        settings.HeadLength=8;Check(Rule(Run(),"SeismicHeadLength").Passed==false,"user cannot silently shorten10D zone");settings.HeadLength=12;Near(Run().HeadEnd,12,"longer assigned zone respected");settings.HeadLength=null;
        Near(PileReinforcement.SeismicHeadEnd(1000,6),6,"short pile entirely detailed without fictitious extra length");
        var shortPile=service.CheckHeadSeismic(settings,0,6,6,[Section(),Section(6)]);Check(Rule(shortPile,"SeismicHeadLength").Passed==true&&shortPile.Scope.Contains("più corto"),"short-pile convention explicit");
        var invalid=Section();invalid.MRdPositive=null;invalid.MRdNegative=null;Check(Rule(Run(0,12,invalid),"SeismicElasticMoment").Passed==null,"out of domain leaves seismic moment unverified");
        invalid=Section();invalid.VRd=null;Check(Rule(Run(0,12,invalid),"SeismicShear").Passed==null,"missing VRd leaves shear unverified");
        invalid=Section();invalid.DevelopmentAvailable=false;Check(Run(0,12,invalid).Checks.Any(c=>c.Key=="SeismicBarDevelopment"&&c.Passed==null),"nominal resistances do not certify unanchored bars");
        var sparse=service.CheckHeadSeismic(settings,0,12,20,[]);Check(Rule(sparse,"SeismicShear").Passed==null,"empty actions never produce pass");
        var weak=ReinforcementChecks.Parameters(20,20);weak.LinkSpacing=150;weak.LinkDiameter=6;var weakResult=new PileReinforcement(weak).CheckHeadSeismic(settings,0,12,20,[Section()]);
        Check(Rule(weakResult,"SeismicSteelArea").Passed==false&&Rule(weakResult,"SeismicLinkSpacing").Passed==false&&Rule(weakResult,"SeismicLinkDiameter").Passed==false,"20phi20 and6mm links150 fail head minima independently of ordinary toggle");
        settings.Enabled=false;Check(!Run().Active&&Run().Checks.Count==0,"legacy inactive mode does not alter verifications");settings.Enabled=true;
        Bad(()=>PileReinforcement.SeismicHeadEnd(0,20),"invalid pile diameter rejected");Bad(()=>PileReinforcement.SeismicHeadEnd(1000,20,double.NaN),"invalid head zone rejected");
        Bad(()=>Run(0,12,Section(n:double.NaN)),"nonfinite seismic action rejected");
        return count;
    }
}
