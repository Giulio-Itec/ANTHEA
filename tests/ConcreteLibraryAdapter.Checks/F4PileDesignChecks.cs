using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4PileDesignChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            var bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        var input=Read("inputs/piles/design-segment.json");var response=ElasticHorizontalPile.CalculateResponse(input);
        string before=input.ToJsonString(),responseBefore=response.ToJsonString();int comparisons=0,cancellations=0;var errors=new List<string>();
        var expected=Read("results/piles/design-segment.json")["value"];
        var result=ElasticHorizontalPile.DesignSegment(input,response,0,[16],[24],[10],[150],maximumParallelism:1);
        Equal(expected,result,"frozen");
        // Capture diagnostics from the retained, unchanged engine before evaluating the new path.
        foreach(var catalog in new[]{(counts:new[]{8,12,16},diameters:new[]{16d,24d},links:new[]{8d,10d},spacings:new[]{150d,200d}),
            (counts:new[]{4,8,16},diameters:new[]{24d,40d},links:new[]{10d},spacings:new[]{100d,150d})})
        {
            var legacy=ElasticHorizontalPile.DesignSegment(input,response,0,catalog.counts,catalog.diameters,catalog.links,catalog.spacings,legacy:true);
            foreach(int workers in new[]{1,2})
            {
                var progress=new Sink();var actual=ElasticHorizontalPile.DesignSegment(input,response,0,catalog.counts,catalog.diameters,catalog.links,catalog.spacings,progress:progress,maximumParallelism:workers);
                Equal(legacy,actual,"catalog/"+workers);if(progress.Items.Count==0)errors.Add("Missing progress");
            }
        }
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{ElasticHorizontalPile.DesignSegment(input,response,0,[16],[24],[10],[150],cancelled.Token);errors.Add("No pre-cancellation");}catch(OperationCanceledException){cancellations++;}}
        using(var cancelled=new CancellationTokenSource())
        {try{ElasticHorizontalPile.DesignSegment(input,response,0,[16],[24],[10],[150],cancelled.Token,new Sink(_=>cancelled.Cancel()));errors.Add("No in-progress cancellation");}catch(OperationCanceledException){cancellations++;}}
        var missing=Read("inputs/piles/design-not-confirmed.json");
        try{ElasticHorizontalPile.DesignSegment(missing,response,0,[16],[24],[10],[150]);errors.Add("Missing confirmations accepted");}catch(ArgumentException ex){if(!ex.Message.StartsWith("Confermare",StringComparison.Ordinal))errors.Add("Wrong confirmation diagnostic");}
        var invalid=(JsonObject)response.DeepClone();invalid["risposta"]!.Array("Points")[0]!["Moment"]=null;
        try{ElasticHorizontalPile.DesignSegment(input,invalid,0,[16],[24],[10],[150]);errors.Add("Missing moment silently zeroed");}catch(ArgumentException){}
        if(input.ToJsonString()!=before||response.ToJsonString()!=responseBefore)errors.Add("Caller mutated");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{comparisons,cancellations,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"F4 pile design: {comparisons} exact comparisons, {cancellations} cancellations, {errors.Count} differences");
        foreach(string error in errors)Console.Error.WriteLine(error);return errors.Count==0?0:1;
        void Equal(JsonNode? first,JsonNode? second,string label){comparisons++;if(!JsonNode.DeepEquals(first,second))errors.Add(label+": proposal differs");}
    }
    sealed class Sink(Action<PileCalculationProgress>? action=null):IProgress<PileCalculationProgress>
    {
        public List<PileCalculationProgress> Items {get;}=[];
        public void Report(PileCalculationProgress value){Items.Add(value);action?.Invoke(value);}
    }
}
