using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4ResidualChecks
{
    internal static int Run(string root, string output)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/residual-reference.json")))!;
        byte[] bytes = File.ReadAllBytes(Path.Combine(root,manifest["file"]!.GetValue<string>()));
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(manifest["sha256"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase)) throw new Exception("Immutable residual capture changed.");
        var errors = new List<string>();var stats = new Stats("F4 residual adapters"); int cases=0, rejected=0;
        foreach(var row in JsonNode.Parse(bytes)!["cases"]!.AsArray().OfType<JsonObject>())
        {
            string before=row["input"]!.ToJsonString();var actual=F4ResidualCapture.Evaluate(row);
            Json.Compare(Json.Sort(row["expected"]),Json.Sort(actual),row.S("id"),stats,errors,1e-10);
            if(row["input"]!.ToJsonString()!=before) errors.Add(row.S("id")+": input mutated");
            if(row["expected"]?["error"] is not null)rejected++;
            cases++;
        }
        if(cases!=53||rejected!=6)errors.Add("Incomplete frozen corpus");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{cases,rejected,stats.Numbers,stats.MaxRelative,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(stats.Line()); foreach(string error in errors.Take(20))Console.Error.WriteLine(error);
        return errors.Count==0?0:1;
    }
}
