using GPC.Checkers.Geotechnics.Piles;
internal static class SharedChecks
{
    internal static int Run()
    {
        int n=0;void Check(bool b,string s){if(!b)throw new Exception(s);n++;Console.WriteLine("PASS "+s);}
        void Near(double a,double b,string s)=>Check(Math.Abs(a-b)<1e-8*Math.Max(1,Math.Abs(b)),s);
        void Bad(Action action,string s){try{action();throw new Exception(s);}catch(ArgumentException){Check(true,s);}}
        var circle=ElasticPileSection.Concrete(1,30);Near(circle.InertiaMm4,49087385212.34052,"solid circle analytic inertia");Near(circle.ModulusMpa,32836.56803133079,"C30 secant modulus independent EN1992 value");Near(circle.EI,circle.ModulusMpa*49.08738521234052,"N mm² to kN m² conversion");
        var tube=ElasticPileSection.Tube(200,10,210000);Near(tube.InertiaMm4,27009842.839051247,"annulus analytic inertia");Near(tube.EI,5672.066996200762,"steel tube EJ independent reference");
        var section=ElasticPileSection.Concrete(1,30);double original=section.EI;section.Override(50000,"Measured secant");Check(section.EI==50000&&section.BaseEI==original&&section.OverrideReason!="","EJ override trace");Bad(()=>section.Override(50000,""),"EJ override reason");Bad(()=>ElasticPileSection.Tube(200,101,210000),"invalid tube");
        Near(ElasticPileSection.TotalLength(10,2),12,"shared embedded/free convention");Near(ElasticPileSection.HeadCouple(100,3,2),-100,"head moment excludes free lever");Near(ElasticPileSection.GroundEccentricity(100,2,-100,0),3,"legacy eccentricity conversion");
        var p=new ElasticPileInput{Diameter=1,Length=12,FreeLength=2,EI=50000,EISource="Test",Section=section,Force=100,HeadMoment=ElasticPileSection.HeadCouple(100,3,2),Step=.5,Layers=new[]{new ElasticPileLayer{Thickness=10,Value=10000,Law=ElasticSoilLaw.ConstantKh}}};
        var r=ElasticPile.Calculate(p);Near(r.Points.First(x=>x.Depth==2).Moment,300,"moment at ground H times e exactly once");
        Near(r.Nodes.Sum(x=>x.EquivalentSpring),100000,"sum equivalent springs equals integral k dz");Check(r.Nodes.Where(x=>x.TributaryBottom<=2).All(x=>x.EquivalentSpring==0),"free length tributary springs zero");
        Near(r.Nodes.First().EquivalentSpring,0,"free head spring zero");Near(r.Nodes.Last().EquivalentSpring,2500,"tip half tributary length");
        Check(r.SectionDemands.Count==r.Points.Count&&ReferenceEquals(r.Section,section),"section material geometry attached");
        Check(r.SectionDemands.Zip(r.Points).All(pair=>pair.First.Moment==pair.Second.Moment&&pair.First.Shear==pair.Second.Shear&&pair.First.Side==pair.Second.Side),"section demand signed data and side preserved");
        Check(r.SectionDemands.Any(s=>s.ExtremeReferences.Contains("M:abs")),"section maximum references");
        p.FreeLength=0;p.Length=10;p.Layers=new[]{new ElasticPileLayer{Thickness=4,Value=100,Law=ElasticSoilLaw.LinearKhReference},new ElasticPileLayer{Thickness=6,Value=200,Law=ElasticSoilLaw.LinearKhReference}};r=ElasticPile.Calculate(p);
        Near(r.Nodes.Sum(x=>x.EquivalentSpring),9200,"piecewise linear tributary exact integral");Check(r.SectionDemands.Count(s=>s.Depth==4)==2,"section interface two sides");
        var soil=new HorizontalSoilAssignment{Thickness=10,Mode=HorizontalSoilMode.SandCorrelation,Density="Medio",UnitWeight=18,Conditions="test"};
        var d=ViggianiHorizontalSoil.Resolve(new[]{soil}).Determinations[0];Check(d.A==650&&d.InitialMean==650&&d.RecommendedA==600&&d.SelectionOrigin.Contains("software"),"mean recommendation and origin separate");soil.A=700;soil.OverrideNh=9000;soil.OverrideReason="Calibration";d=ViggianiHorizontalSoil.Resolve(new[]{soil}).Determinations[0];Check(d.A==700&&d.AdoptedValue==9000&&d.InitialMean==650&&d.Overridden,"manual values and override retained");
        Console.WriteLine($"SHARED TOTAL {n} passed");return n;
    }
}
