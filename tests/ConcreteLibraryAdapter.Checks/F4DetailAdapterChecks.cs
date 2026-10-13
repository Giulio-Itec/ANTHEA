using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class F4DetailAdapterChecks
{
    internal static int Run(string root,string reference,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        int models=0,schedules=0,numbers=0;double maxError=0;var errors=new List<string>();
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        void Compare(JsonNode? a,JsonNode? b,string path)
        {
            if(a is JsonValue av && b is JsonValue bv && av.TryGetValue<double>(out var x) && bv.TryGetValue<double>(out var y))
            { numbers++;double error=Math.Abs(x-y);maxError=Math.Max(maxError,error);if(!double.IsFinite(y)||error>1e-9+1e-9*Math.Max(Math.Abs(x),Math.Abs(y))) errors.Add(path+": "+x+" -> "+y);return; }
            if(a is JsonArray aa && b is JsonArray ba)
            { if(aa.Count!=ba.Count){errors.Add(path+": array count");return;}for(int i=0;i<aa.Count;i++)Compare(aa[i],ba[i],path+"/"+i);return; }
            if(a is JsonObject ao && b is JsonObject bo)
            { if(!ao.Select(p=>p.Key).Order().SequenceEqual(bo.Select(p=>p.Key).Order()))errors.Add(path+": object keys");foreach(var p in ao)Compare(p.Value,bo[p.Key],path+"/"+p.Key);return; }
            if(!JsonNode.DeepEquals(a,b))errors.Add(path+": "+a+" -> "+b);
        }
        foreach(string scope in new[]{"walls","schedules"})
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/"+scope),"*.json").Order(StringComparer.Ordinal))
        {
            string id=scope+"/"+Path.GetFileName(file);var d=Read("inputs/"+id);var expected=Read("results/"+id);
            if(expected.S("status")!="ok"||d.S("family")!="cantilever")continue;
            string before=d.ToJsonString();models++;
            Compare(JsonSerializer.SerializeToNode(RetainingWall.CalculateReinforcementDetails(d,WallCalculationEngine.Legacy)),
                JsonSerializer.SerializeToNode(RetainingWall.CalculateReinforcementDetails(d)),id+"/details");
            if(scope=="schedules")
            {
                schedules++;var a=RetainingWall.CalculateBarSchedule(d);
                Compare(expected["value"]!["schedule"],JsonSerializer.SerializeToNode(a),id+"/schedule");
                if(ReportRetainingWall.BarScheduleCsv(a)!=expected["value"]!.S("csv")) errors.Add(id+": CSV differs");
            }
            if(d.ToJsonString()!=before)errors.Add(id+": caller input changed");
        }
        int refusals=0;
        foreach(var edit in new Action<JsonObject>[] {
            d=>d["geometry"]!["height"]="non numerico",
            d=>d["detailing"]!["life"]=99,
            d=>d["detailing"]!["cover_method"]="profilo inesistente" })
        {
            var bad=Read("inputs/walls/XC1-C25.json");edit(bad);string before=bad.ToJsonString();
            try { RetainingWall.CalculateReinforcementDetails(bad);errors.Add("Invalid detail input was accepted"); }
            catch(ArgumentException){refusals++;}
            if(bad.ToJsonString()!=before)errors.Add("Invalid input was modified");
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{models,schedules,numbers,refusals,maxError,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"F4 details adapter: {models} models, {schedules} schedules/CSV, {numbers} numbers, {errors.Count} differences, max {maxError:R}");
        foreach(string e in errors.Take(20))Console.Error.WriteLine(e);
        return errors.Count==0?0:1;
    }
}
