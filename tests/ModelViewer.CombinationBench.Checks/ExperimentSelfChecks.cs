static class ExperimentSelfChecks
{
    public static void Run()
    {
        var basis=new Dictionary<string,double[]>{["a"]=[-2,3,1,-2,3],["b"]=[4,-1,2,4,-1]};
        var definitions=new Dictionary<string,Definition>{
            ["sum"]=new("sum",0,[new("ST","a",1.5),new("ST","b",-2)]),
            ["env"]=new("env",1,[new("ST","a",1),new("ST","b",1)]),
            ["negative"]=new("negative",0,[new("CBC","env",-2)])};
        var engine=new CombinationExperiment(basis,definitions);
        var sum=engine.Evaluate("sum");var env=engine.Evaluate("env");var negative=engine.Evaluate("negative");
        if(!sum.Min.SequenceEqual(new[]{-11d,6.5,-2.5,-11,6.5})||!sum.Max.SequenceEqual(sum.Min)||
            !env.Min.SequenceEqual(new[]{-2d,-1,1,-2,-1})||!env.Max.SequenceEqual(new[]{4d,3,2,4,3})||
            !negative.Min.SequenceEqual(new[]{-8d,-6,-4,-8,-6})||!negative.Max.SequenceEqual(new[]{4d,2,-2,4,2}))
            throw new InvalidOperationException("Independent hand fixture failed.");
        definitions["cycle"]=new("cycle",0,[new("CBC","cycle",1)]);
        bool cycle=false;try{engine.Evaluate("cycle");}catch(InvalidDataException){cycle=true;}
        if(!cycle)throw new InvalidOperationException("Cycle was accepted.");
    }
}
