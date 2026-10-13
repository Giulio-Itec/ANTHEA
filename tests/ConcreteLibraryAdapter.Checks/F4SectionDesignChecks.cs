using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthea.Calculations;

internal static class F4SectionDesignChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        var errors=new List<string>();var stats=new Stats("F4 section design");int cases=0,refused=0,candidates=0;
        var deserialize=new JsonSerializerOptions{NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals|JsonNumberHandling.AllowReadingFromString};
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/section-design"),"*.json").Order(StringComparer.Ordinal))
        {
            string id=Path.GetFileName(file);var captured=Read("inputs/section-design/"+id);var input=captured["document"]!.AsObject();
            string before=input.ToJsonString();var options=captured["configuration"]!.Deserialize<ConcreteDesignOptions>(deserialize)!;
            var expected=Read("results/section-design/"+id);cases++;
            try
            {
                var result=ConcreteReinforcementDesign.Optimize(input,options,id=="cancelled.json"?new CancellationToken(true):default);
                if(expected.S("status")!="ok"){errors.Add(id+": expected refusal");continue;}
                var actual=JsonSerializer.SerializeToNode(result)!.AsObject();actual.Remove("Performance");
                Compare(expected["value"],actual,id);candidates+=result.Candidates.Count;
                var old=ConcreteReinforcementDesign.Optimize(input,options,legacy:true);
                var previous=JsonSerializer.SerializeToNode(old)!.AsObject();previous.Remove("Performance");
                Compare(previous,actual,id+"/legacy-search");
                if(old.Performance!.LongitudinalCalculations!=result.Performance!.LongitudinalCalculations||old.Performance.LongitudinalReuses!=result.Performance.LongitudinalReuses||old.Performance.ProbeSections!=result.Performance.ProbeSections)
                    errors.Add(id+": search reuse counters differ");
                foreach(var c in result.Candidates)
                {
                    var section=new SezioneCA(c.Input);
                    Compare(JsonSerializer.SerializeToNode(ConcreteReinforcementDesign.Metrics(section,c.Shear,c.Detailing,options,legacy:true)),JsonSerializer.SerializeToNode(c.Metrics),id+"/metrics-legacy/"+c.Id);
                }
                foreach(string objective in new[]{"Peso","Numero barre","Numero diametri","Compromesso"})
                {
                    var choice=options with {Objective=objective};
                    Compare(JsonSerializer.SerializeToNode(ConcreteReinforcementDesign.Rank(result.Candidates,choice,legacy:true)),
                        JsonSerializer.SerializeToNode(ConcreteReinforcementDesign.Rank(result.Candidates,choice)),id+"/rank/"+objective);
                }
            }
            catch(Exception ex) when(ex is ArgumentException or OperationCanceledException)
            {
                if(expected.S("status")!="refused"||expected["value"].S("type")!=ex.GetType().FullName)errors.Add(id+": unexpected "+ex);
                else refused++;
            }
            if(input.ToJsonString()!=before)errors.Add(id+": caller mutated");
        }
        if(cases!=9||refused!=3||candidates==0)errors.Add("Incomplete corpus");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{cases,refused,candidates,numbers=stats.Numbers,maxRelative=stats.MaxRelative,errors=errors.Take(100),differences=errors.Count},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(stats.Line());Console.WriteLine($"{cases} cases, {refused} refusals, {candidates} candidates, {errors.Count} differences");foreach(string error in errors.Take(20))Console.Error.WriteLine(error);
        return errors.Count==0?0:1;
        void Compare(JsonNode? a,JsonNode? b,string name)=>Json.Compare(Json.Sort(a),Json.Sort(b),name,stats,errors,1e-9);
    }
}
