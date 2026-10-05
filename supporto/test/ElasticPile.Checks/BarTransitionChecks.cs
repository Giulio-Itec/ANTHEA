using GPC.Checkers.Concrete.Piles;

static class BarTransitionChecks
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
        void Near(double a,double b,string label)=>Check(Math.Abs(a-b)<1e-8,label);
        PileReinforcement Service(int n=20,double phi=20,double angle=0,double stock=12,double outside=0,double percent=100)
        {
            var p=ReinforcementChecks.Parameters(n,phi,angle);p.StockLength=stock;p.HeadAnchorage=p.ToeAnchorage=outside;p.LapPercent=percent;
            return new PileReinforcement(p);
        }
        PileDetailSegment S(string id,double a,double b,PileReinforcement service)=>new(){Id=id,Start=a,End=b,Service=service,Checks=new[]{new PileSectionCheck{Action=new PileAction{Depth=(a+b)/2},CotTheta=2}}};
        var service=Service();
        var runs=PileReinforcement.SegmentRuns(new[]{S("T1",0,12,service),S("T2",12,18,service),S("T3",18,20,service)},20);
        Check(runs.Count==3,"three user segments stay three physical groups even with identical reinforcement");
        double[] starts={0,10.8,16.8},ends={12,18,20},lengths={12,7.2,3.2};
        for(int i=0;i<3;i++)
        {
            Near(runs[i].Start,starts[i],"user example actual start "+i);Near(runs[i].End,ends[i],"user example exact cutting end "+i);
            Check(runs[i].Pieces.Count==1,"no extra cuts when one stock bar fits group "+i);
            Near(runs[i].Pieces[0].CuttingLength,lengths[i],"independent expected bar length "+i);
        }
        Near(runs.Sum(r=>r.CuttingLength),20*22.4,"steel quantity net length plus two real overlaps exactly once");
        var joints=runs.SelectMany(r=>r.Joints).Distinct().ToArray();
        Check(joints.Length==2&&joints.All(j=>j.LengthPassed&&j.ArrangementPassed),"two real overlaps available at the assigned boundaries");
        foreach(var j in joints){Near(j.ActualLength,1.2,"one-sided joint length 1.20m");Near(j.End,j.Boundary,"joint ends at the assigned cutting boundary");}
        var outside=PileReinforcement.SegmentRuns(new[]{S("A",0,12,Service(outside:3)),S("B",12,20,Service(outside:3))},20);
        Near(outside[0].Start,0,"legacy external head allowance never lengthens first bar");Near(outside[1].End,20,"legacy external toe allowance never lengthens last bar");
        Check(outside.All(r=>r.Status.Contains("precedente schema")),"unused legacy exterior allowances remain explicitly traceable");
        var changed=PileReinforcement.SegmentRuns(new[]{S("A",0,12,Service(20,20)),S("B",12,18,Service(12,16)),S("C",18,20,Service(12,16))},20);
        Near(changed[1].Start,10.8,"diameter change uses larger adopted lap of adjacent sections");Near(changed[2].Start,17,"next phi16 joint uses 1.00m initial lap");
        Check(changed.Select(r=>r.Count).SequenceEqual(new[]{20,12,12}),"no splitting or merging of user cage quantities");
        Check(changed[0].Joints.Single().Count==12&&changed[0].Joints.Single().MatchedCount==4&&!changed[0].Joints.Single().ArrangementPassed,"20 to12 ring layouts preserve exact cuts but expose four of twelve aligned pairs");
        Check(changed[2].Joints.Single().MatchedCount==12,"identical lower rings have twelve aligned pairs");
        var longRuns=PileReinforcement.SegmentRuns(new[]{S("A",0,15,service),S("B",15,30,service)},30);
        Check(longRuns.All(r=>r.Pieces.Count==2),"long user segments split internally only for commercial length");
        Near(longRuns[0].Pieces[0].Start,0,"first internal piece starts at head");Near(longRuns[0].Pieces.Last().End,15,"first group still ends exactly at15");
        Near(longRuns[1].Pieces[0].Start,13.8,"second long group starts before15 by1.20");Near(longRuns[1].Pieces.Last().End,30,"last internal piece ends exactly at toe");
        Check(longRuns.SelectMany(r=>r.Pieces).All(p=>p.CuttingLength<=p.StockLength+1e-9&&p.StockLength<=12),"all pieces respect actual commercial bars");
        foreach(var r in longRuns)Near(r.Pieces[0].End-r.Pieces[1].Start,1.2,"internal stock overlap is counted once");
        Near(longRuns.Sum(r=>r.CuttingLength),20*(30+3*1.2),"quantity includes boundary overlap and two internal stock overlaps");
        var boundaryStock=PileReinforcement.SegmentRuns(new[]{S("A",0,12,service),S("B",12,24,service)},24);
        Check(boundaryStock[0].Pieces.Count==1&&boundaryStock[1].Pieces.Count==2,"12m net following segment needs a cut because its incoming lap exceeds12");
        var shortRuns=PileReinforcement.SegmentRuns(new[]{S("A",0,.4,service),S("B",.4,.8,service)},.8);
        Check(!shortRuns[0].Joints.Single().LengthPassed&&shortRuns[1].Start==0,"insufficient space is flagged without drawing bars outside the pile");
        var bad=new PileSectionCheck{Action=new PileAction{Depth=.4},Status="Soddisfatto",Message="",MRdPositive=100};
        PileReinforcement.ApplyDevelopment(shortRuns,new[]{bad});Check(bad.UsableMRdPositive==null,"assigned cuts cannot manufacture usable resistance when development fails");
        var lowerPercent=PileReinforcement.SegmentRuns(new[]{S("A",0,12,Service(percent:25)),S("B",12,20,Service(percent:25))},20);
        Near(lowerPercent[0].Joints.Single().RequiredLength,joints[0].RequiredLength,"unstaggered grouped joints still calculated at100percent");
        Check(lowerPercent.All(r=>r.Status.Contains("100%")),"unimplemented staggering remains visible");
        try{Service(percent:double.NaN);throw new Exception("Invalid percentage accepted");}catch(ArgumentException){Check(true,"invalid requested lap percentage rejected");}
        var overlap=PileReinforcement.SegmentRuns(new[]{S("A",0,8,service),S("B",8,8.5,service),S("C",8.5,12,service)},12);
        Check(overlap.SelectMany(r=>r.Joints).Any(j=>!j.ArrangementPassed),"overlapping boundary joints remain explicitly invalid");
        return count;
    }
}
