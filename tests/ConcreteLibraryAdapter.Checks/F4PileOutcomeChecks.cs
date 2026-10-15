using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4PileOutcomeChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        int models=0,segments=0,checks=0,numbers=0;var errors=new List<string>();
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/piles"),"*.json").Order(StringComparer.Ordinal))
        {
            string id=Path.GetFileName(file);if(!id.StartsWith("law-",StringComparison.Ordinal)&&!id.StartsWith("segments-",StringComparison.Ordinal))continue;
            var input=Read("inputs/piles/"+id);var expected=Read("results/piles/"+id)["value"]!["result"]!;string before=input.ToJsonString();
            var result=ElasticHorizontalPile.CompleteReinforcement(input,ElasticHorizontalPile.CalculateResponse(input),maximumParallelism:1);models++;
            var old=expected["armature"]!["tratti"]!.AsArray();var current=result["armature"]!["tratti"]!.AsArray();
            if(old.Count!=current.Count){errors.Add(id+": segment count");continue;}
            for(int i=0;i<old.Count;i++)
            {
                segments++;var a=old[i]!;var b=current[i]!.AsObject();
                foreach(string field in new[]{"stato","da_completare","riepilogo"})
                    if(!JsonNode.DeepEquals(a[field],b[field]))errors.Add(id+"/"+i+"/"+field);
                if(!JsonNode.DeepEquals(LegacyElasticHorizontalPile.ReinforcementSummary(b),b["riepilogo"]))errors.Add(id+": legacy summary differs");
                var originalChecks=a["verifiche"]!.AsArray();var actualChecks=b["verifiche"]!.AsArray();
                if(originalChecks.Count!=actualChecks.Count){errors.Add(id+": section check count");continue;}
                for(int j=0;j<originalChecks.Count;j++)
                {
                    checks++;var x=originalChecks[j]!;var y=actualChecks[j]!;
                    if(x.S("Status")!=y.S("Status"))errors.Add(id+": status differs");
                    foreach(string key in new[]{"MRdPositive","MRdNegative","BendingRatio","ShearRatio","VRd","GoverningRatio"})
                    {
                        numbers++;double? first=J.Number(x[key]),second=J.Number(y[key]);
                        if(first.HasValue!=second.HasValue||first.HasValue&&Math.Abs(first.Value-second!.Value)>1e-9+1e-9*Math.Max(Math.Abs(first.Value),Math.Abs(second.Value)))errors.Add(id+": numeric "+key);
                    }
                }
                var translated=(JsonObject)b.DeepClone();
                foreach(var issue in translated["da_completare_tipizzati"]!.AsArray())issue!["Text"]="Translated or arbitrary display label";
                translated["da_completare"]=new JsonArray("Soddisfatto");
                foreach(var check in translated["verifiche"]!.AsArray())check!["Status"]="Soddisfatto";
                if(!JsonNode.DeepEquals(ElasticHorizontalPile.ReinforcementSummary(translated),b["riepilogo"]))errors.Add(id+": display strings control verdict");
            }
            if(input.ToJsonString()!=before)errors.Add(id+": caller mutated");
        }
        var unknown=J.Obj(("verifiche",new JsonArray(J.Obj(("Status","Soddisfatto")))),("da_completare",new JsonArray("Non eseguiti: SLE")));
        if(ElasticHorizontalPile.ReinforcementSummary(unknown).B("eseguiti_soddisfatti"))errors.Add("Legacy labels certified an untyped result");
        if(models!=20||segments==0||checks==0)errors.Add("Incomplete pile corpus");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{models,segments,checks,numbers,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"F4 pile outcomes: {models} models, {segments} segments, {checks} checks, {numbers} numbers, {errors.Count} differences");
        foreach(string error in errors.Take(15))Console.Error.WriteLine(error);return errors.Count==0?0:1;
    }
}
