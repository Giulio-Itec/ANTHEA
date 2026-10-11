using System.Numerics;
using System.Text.Json;

// Laboratory experiment only. Not referenced by ANTHEA or GPC production code.
// Scope: componentwise additive combinations and independent min/max envelopes of static result fields.
sealed record Term(string Analysis, string Name, double Factor);
sealed record Definition(string Name, int Type, Term[] Terms);
sealed record Bounds(double[] Min, double[] Max);
sealed class CombinationExperiment(Dictionary<string,double[]> basis, Dictionary<string,Definition> definitions)
{
    readonly Dictionary<string,Bounds> cache = new(StringComparer.Ordinal);
    readonly HashSet<string> visiting = new(StringComparer.Ordinal);
    public void Clear() { cache.Clear(); visiting.Clear(); }
    public Bounds Evaluate(string name)
    {
        if(cache.TryGetValue(name,out var existing))return existing;
        if(!definitions.TryGetValue(name,out var definition))throw new InvalidDataException("Missing combination: "+name);
        if(!visiting.Add(name))throw new InvalidDataException("Combination cycle: "+name);
        if(definition.Type is not (0 or 1) || definition.Terms.Length==0)throw new InvalidDataException("Unsupported/empty combination: "+name);
        var count=basis.First().Value.Length;
        var low=new double[count]; var high=new double[count];
        if(definition.Type==1) { Array.Fill(low,double.PositiveInfinity); Array.Fill(high,double.NegativeInfinity); }
        foreach(var term in definition.Terms)
        {
            Bounds source=term.Analysis switch {
                "ST" when basis.TryGetValue(term.Name,out var field) => new(field,field),
                "CBC" => Evaluate(term.Name),
                _ => throw new InvalidDataException("Missing or unsupported dependency: "+term.Analysis+"/"+term.Name)
            };
            if(!double.IsFinite(term.Factor))throw new InvalidDataException("Non-finite factor.");
            var a=term.Factor>=0?source.Min:source.Max; var b=term.Factor>=0?source.Max:source.Min;
            Apply(low,high,a,b,term.Factor,definition.Type==1);
        }
        visiting.Remove(name);
        if(low.Any(v=>!double.IsFinite(v)) || high.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Non-finite combination output.");
        return cache[name]=new(low,high);
    }
    static void Apply(double[] low,double[] high,double[] a,double[] b,double factor,bool envelope)
    {
        int i=0, width=Vector<double>.Count; var f=new Vector<double>(factor);
        for(;i<=low.Length-width;i+=width)
        {
            var x=new Vector<double>(a,i)*f; var y=new Vector<double>(b,i)*f;
            (envelope?Vector.Min(new Vector<double>(low,i),x):new Vector<double>(low,i)+x).CopyTo(low,i);
            (envelope?Vector.Max(new Vector<double>(high,i),y):new Vector<double>(high,i)+y).CopyTo(high,i);
        }
        for(;i<low.Length;i++) { low[i]=envelope?Math.Min(low[i],factor*a[i]):low[i]+factor*a[i]; high[i]=envelope?Math.Max(high[i],factor*b[i]):high[i]+factor*b[i]; }
    }
    public static Dictionary<string,Definition> Definitions(string path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("LCOM-CONC").EnumerateObject().Select(p=>p.Value).Select(p=>new Definition(p.GetProperty("NAME").GetString()!,p.GetProperty("iTYPE").GetInt32(),
            p.GetProperty("vCOMB").EnumerateArray().Select(t=>new Term(t.GetProperty("ANAL").GetString()!,t.GetProperty("LCNAME").GetString()!,t.GetProperty("FACTOR").GetDouble())).ToArray())).ToDictionary(d=>d.Name,StringComparer.Ordinal);
    }
}
