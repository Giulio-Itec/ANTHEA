using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4WallDesignChecks
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
        var errors=new List<string>();var stats=new Stats("F4 wall design");int walls=0;
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/wall-design"),"catalog-*.json").Order(StringComparer.Ordinal))
        {
            string id=Path.GetFileName(file);var input=Read("inputs/wall-design/"+id);string before=input.ToJsonString();
            var frozen=Read("results/wall-design/"+id)["value"]!;
            var legacy=RetainingWall.DesignReinforcement(input,legacy:true); // search on the current, already verified GPC section adapter
            var result=RetainingWall.DesignReinforcement(input);walls++;
            Compare(frozen["Input"],JsonSerializer.SerializeToNode(result.Input),id+"/frozen-input");
            Compare(frozen["Message"],JsonSerializer.SerializeToNode(result.Message),id+"/frozen-message");
            Compare(frozen["Passed"],JsonSerializer.SerializeToNode(result.Passed),id+"/frozen-status");
            Compare(JsonSerializer.SerializeToNode(legacy),JsonSerializer.SerializeToNode(result),id+"/complete-current-engines");
            if(input.ToJsonString()!=before)errors.Add(id+": caller mutated");
        }
        int refused=0;
        foreach(string id in new[]{"invalid-count.json","unsupported-family.json"})
        {
            var input=Read("inputs/wall-design/"+id);
            try{RetainingWall.DesignReinforcement(input);errors.Add(id+": accepted invalid input");}catch(ArgumentException){refused++;}
        }
        try{RetainingWall.DesignReinforcement(Read("inputs/wall-design/cancelled.json"),new CancellationToken(true));errors.Add("No cancellation");}catch(OperationCanceledException){refused++;}
        if(walls!=12||refused!=3)errors.Add("Incomplete corpus");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{walls,refused,numbers=stats.Numbers,maxRelative=stats.MaxRelative,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(stats.Line());Console.WriteLine($"{walls} walls, {refused} refusals, {errors.Count} differences");foreach(string error in errors.Take(20))Console.Error.WriteLine(error);
        return errors.Count==0?0:1;
        void Compare(JsonNode? a,JsonNode? b,string name)=>Json.Compare(Json.Sort(a),Json.Sort(b),name,stats,errors,1e-9);
    }
}
