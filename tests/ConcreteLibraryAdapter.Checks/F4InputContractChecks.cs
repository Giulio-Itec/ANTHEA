using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4InputContractChecks
{
    internal static int Run(string root,string output)
    {
        CultureInfo.CurrentCulture=CultureInfo.CurrentUICulture=CultureInfo.GetCultureInfo("it-IT");
        var reference=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/input-contracts-reference.json")))!;
        byte[] bytes=File.ReadAllBytes(Path.Combine(root,reference.S("file")));
        if(Convert.ToHexString(SHA256.HashData(bytes))!=reference.S("sha256"))throw new InvalidOperationException("Before-input capture changed.");
        var cases=JsonNode.Parse(bytes)!["cases"]!.AsArray().OfType<JsonObject>().ToArray();
        var errors=new List<string>();var stats=new Stats("F4 input contracts");int covers=0,ned=0;
        foreach(var row in cases)
        {
            var expected=row["expected"]!.DeepClone();string before=row.ToJsonString();
            if(row.S("kind")=="ned")
            {
                ned++;
                // Classified input correction: the UI must use the same action union already captured for design.
                expected["ui"]=expected["design"]!.DeepClone();
                var data=(JsonObject)row["input"]!.DeepClone();var settings=SectionWorkspace.Prepare(data);
                var actions=row["actions"]!.AsArray().OfType<JsonObject>().ToArray();
                // F4-44 adds the governing combination to the result; rows without "stato" are ultimate.
                var retained=JsonSerializer.SerializeToNode(ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(),settings,actions,legacyActions:true));
                Compare(row["expected"]!["ui"],Governing(retained,"SLU · SLU","retained-legacy-input"),"retained-legacy-input");
                settings["taglio"]!["azioni"]![0]!["N"]="-20000";
                var significant=ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(),settings,actions);
                var minimum=significant.Checks.Single(c=>c.Name=="Armatura longitudinale minima");
                if(significant.MaximumCompression!=20000||minimum.Passed!=false||minimum.Limit is not double required||Math.Abs(required-5111.111111111111)>1e-9)
                    errors.Add("Governing shear axial load does not drive the minimum steel requirement.");
                settings["taglio"]!["azioni"]![0]!["N"]="6000";
                if(ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(),settings,actions).MaximumCompression!=100)errors.Add("Tension treated as compression.");
                settings["taglio"]!["azioni"]![0]!["N"]="not-a-number";
                try{ConcreteDetailingAnalysis.Calculate(data["input"]!.AsObject(),settings,actions);errors.Add("Invalid axial force silently ignored.");}catch(ArgumentException){}
            }
            else covers++;
            var actual=F4InputContractsCapture.Evaluate(row);
            if(row.S("kind")=="ned")
            {
                // Equal compressions (-6000 N): design families win, then the first row. The UI sees the shear row; the design call
                // also passes it among the actions without "stato", so the ultimate copy comes first.
                actual!["ui"]=Governing(actual["ui"],"Taglio con compressione massima · taglio",row.S("id")+".ui");
                actual["design"]=Governing(actual["design"],"Taglio con compressione massima · SLU",row.S("id")+".design");
            }
            Compare(expected,actual,row.S("id"));
            if(row.ToJsonString()!=before)errors.Add(row.S("id")+": source mutated");
        }
        if(covers!=12||ned!=1)errors.Add("Incomplete contract corpus.");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{covers,ned,numbers=stats.Numbers,errors},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(stats.Line());foreach(string error in errors.Take(20))Console.Error.WriteLine(error);return errors.Count==0?0:1;
        void Compare(JsonNode? a,JsonNode? b,string name)=>Json.Compare(Json.Sort(a),Json.Sort(b),name,stats,errors,1e-9);
        // Fields added by F4-44 after the capture: checked against the expected combination, then removed for the frozen comparison.
        JsonNode Governing(JsonNode? result,string source,string name)
        {
            var copy=result!.DeepClone().AsObject();
            if(copy["CompressionSource"]?.GetValue<string>()!=source||copy["CompressionFromServiceability"]?.GetValue<bool>()!=false)
                errors.Add($"{name}: NEd from '{copy["CompressionSource"]}', expected '{source}' (not serviceability).");
            copy.Remove("CompressionSource");copy.Remove("CompressionFromServiceability");return copy;
        }
    }
}
