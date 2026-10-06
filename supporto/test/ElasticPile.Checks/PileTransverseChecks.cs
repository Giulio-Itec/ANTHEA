using GPC.Checkers.Concrete.Piles;
static class PileTransverseChecks
{
    internal static int Run()
    {
        int count=0;void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        void Near(double a,double b,string name)=>Check(Math.Abs(a-b)<1e-8,name);
        var hoops=PileReinforcement.TransverseDetail(PileSeismicLinks.SingleHoops,0,6,1000,45,10,200,false);
        Check(hoops.Count==30&&hoops.Stations[^1]==5.8,"30 hoops, internal boundary owned by next segment");
        var lower=PileReinforcement.TransverseDetail(PileSeismicLinks.SingleHoops,6,8,1000,45,10,200,true);
        Check(lower.Count==11&&lower.Stations[0]==6&&lower.Stations[^1]==8,"last hoop at toe without duplicated interface");
        Near(hoops.Radius,450,"centreline radius from concrete cover to outside of hoop");
        Near(hoops.GeometricLength,84.82300164692441,"30 independent circles of diameter900mm, excluding hooks");
        var spiral=PileReinforcement.TransverseDetail(PileSeismicLinks.Spiral,0,6,1000,45,10,200,true);
        Check(spiral.Count==1&&spiral.Turns==30,"continuous helix has one run and30 turns, not30 separate hoops");
        Near(spiral.ActualPitch,200,"spiral pitch mm");
        // Unrolling the cylinder yields a right triangle: base30*0.9*pi m, height6 m.
        Near(spiral.GeometricLength*spiral.GeometricLength,7230.94160839414,"helix length squared from independently unrolled cylinder");
        Check(spiral.Projection[0].Depth==0&&spiral.Projection[^1].Depth==6&&spiral.Projection.Any(p=>p.Front)&&spiral.Projection.Any(p=>!p.Front),"spiral endpoints and visible/hidden halves");
        Near(spiral.Projection[12].X,-450,"half turn at opposite side of pile");
        var rounded=PileReinforcement.TransverseDetail(PileSeismicLinks.Spiral,6,7.05,1000,45,10,200,false);
        Check(rounded.Turns==6&&Math.Abs(rounded.ActualPitch-175)<1e-8,"nonmultiple interval adopts whole turns with pitch no larger than input");
        var unknown=PileReinforcement.TransverseDetail(PileSeismicLinks.Unspecified,0,6,1000,45,10,200,true);
        Check(unknown.Count==0&&unknown.Projection.Length==0&&unknown.Stations.Length==0,"unknown reinforcement produces no invented geometry");
        foreach(var bad in new Action[]{()=>PileReinforcement.TransverseDetail(PileSeismicLinks.Spiral,0,6,1000,45,10,0,true),()=>PileReinforcement.TransverseDetail(PileSeismicLinks.Spiral,6,0,1000,45,10,200,true)})
        {try{bad();throw new Exception("invalid geometry accepted");}catch(ArgumentException){Check(true,"invalid transverse detail rejected");}}
        return count;
    }
}
