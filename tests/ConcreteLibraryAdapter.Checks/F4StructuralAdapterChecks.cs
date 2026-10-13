using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4StructuralAdapterChecks
{
    internal static int Run(string root, string reference, string output, bool gravityOnly = false)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        int walls=0,numbers=0,shear=0,curvatures=0; double maxError=0;var errors=new List<string>();
        var hashes=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(v=>v!["file"]!.GetValue<string>(),v=>v!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(reference,relative));
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative],StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Frozen reference changed: "+relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        byte[] study=File.ReadAllBytes(Path.Combine(root,"supporto/artefatti/refactoring/f4/wall-sigma-study/independent.json"));
        if(Convert.ToHexString(SHA256.HashData(study))!="4D6837431389ED7D0C71D8206047961B1D81C55EBE580A86A446C2FB223B5F8C")throw new InvalidOperationException("Independent shear expectations changed");
        var shearReference=JsonNode.Parse(study)!["rows"]!.AsArray().OfType<JsonObject>().ToDictionary(r=>(r.S("case"),r.S("combination"),r.S("member"),r.D("position_m")));
        void Compare(JsonNode? a,JsonNode? b,string path)
        {
            if(a is JsonValue av && b is JsonValue bv && av.TryGetValue<double>(out var x) && bv.TryGetValue<double>(out var y))
            { numbers++;double delta=Math.Abs(x-y);maxError=Math.Max(maxError,delta);if(!double.IsFinite(y)||delta>1e-9+1e-9*Math.Max(Math.Abs(x),Math.Abs(y)))errors.Add(path+": "+x+" -> "+y);return; }
            if(a is JsonArray aa && b is JsonArray ba)
            { if(aa.Count!=ba.Count){errors.Add(path+": array count "+aa.Count+" -> "+ba.Count);return;}for(int i=0;i<aa.Count;i++)Compare(aa[i],ba[i],path+"/"+i);return; }
            if(a is JsonObject ao && b is JsonObject bo)
            { if(!ao.Select(p=>p.Key).Order().SequenceEqual(bo.Select(p=>p.Key).Order()))errors.Add(path+": object keys");foreach(var p in ao)Compare(p.Value,bo[p.Key],path+"/"+p.Key);return; }
            if(!JsonNode.DeepEquals(a,b))errors.Add(path+": "+a+" -> "+b);
        }
        foreach(string file in Directory.GetFiles(Path.Combine(reference,"inputs/walls"),"*.json").Order(StringComparer.Ordinal))
        {
            string id=Path.GetFileName(file);var input=Read("inputs/walls/"+id);var captured=Read("results/walls/"+id);
            if(captured.S("status")!="ok"||input.S("family")!=(gravityOnly?"gravity":"cantilever"))continue;
            string before=input.ToJsonString();walls++;
            var actual=JsonSerializer.SerializeToNode(RetainingWall.Calculate(input))!;
            // Derive classified predictions in memory only; never rewrite the captured results.
            var expected=captured["value"]!.DeepClone();
            foreach(var check in expected["Structural"]!.AsArray().OfType<JsonObject>().Where(c=>c.S("Name").EndsWith(" · taglio senza staffe",StringComparison.Ordinal)))
            {
                var independent=shearReference[(Path.GetFileNameWithoutExtension(id),check.S("Combination"),check.S("Member"),check.D("Position"))];
                double? capacity=J.Number(independent["predicted_kn_per_m"]);
                var prediction=RetainingWall.CheckValue(check.S("Name"),check.S("Combination"),check.D("Demand"),capacity,check.S("Unit"));
                check["Resistance"]=capacity;check["Ratio"]=prediction.Ratio;check["Status"]=prediction.Status;shear++;
            }
            if(!gravityOnly) foreach(var c in expected["Cases"]!.AsArray())foreach(var p in c!["Curvatures"]!.AsArray())
            {p!["Curvature"]=p.D("Curvature")*(1+input["materials"].D("creep"));curvatures++;}
            if(gravityOnly)curvatures+=expected["Cases"]!.AsArray().Sum(c=>c!["Curvatures"]!.AsArray().Count);
            Compare(expected,actual,id);
            if(input.ToJsonString()!=before)errors.Add(id+": input mutated");
            Console.WriteLine(id+": "+errors.Count+" differences accumulated");
        }
        var serviceInput=Read("inputs/walls/XC1-C25.json");
        serviceInput["serviceability"]!["displacement"]=true;serviceInput["serviceability"]!["rigid_base"]=true;
        serviceInput["serviceability"]!["horizontal_stiffness"]=100000;
        int shapes=0;
        if(!gravityOnly)
        {
        var old=RetainingWall.Calculate(serviceInput,wallEngine:WallCalculationEngine.Legacy);
        var current=RetainingWall.Calculate(serviceInput);
        foreach(var a in old.Serviceability!.Cases)
        {
            var b=current.Serviceability!.Cases.Single(c=>c.Combination==a.Combination);
            double factor=1+serviceInput["materials"].D("creep");
            Compare(JsonSerializer.SerializeToNode(a.StemDisplacement*factor),JsonSerializer.SerializeToNode(b.StemDisplacement),"service/stem");
            Compare(JsonSerializer.SerializeToNode(a.HeadDisplacement+(factor-1)*a.StemDisplacement),JsonSerializer.SerializeToNode(b.HeadDisplacement),"service/head");
            Compare(JsonSerializer.SerializeToNode(a.Shape.Select(p=>new RetainingWall.DisplacementPoint(p.Y,p.Curvature*factor,p.Rotation*factor,p.DisplacementMm*factor))),
                JsonSerializer.SerializeToNode(b.Shape),"service/shape");shapes++;
        }
        }
        if(gravityOnly)
        {
            var assigned=Read("inputs/walls/gravity-2-e0.json");assigned["gravity_design"]!["elastic_modulus"]="";assigned["gravity_design"]!["effective_height"]=1;
            Compare(JsonSerializer.SerializeToNode(RetainingWall.Calculate(assigned,wallEngine:WallCalculationEngine.Legacy)),
                JsonSerializer.SerializeToNode(RetainingWall.Calculate(assigned)),"assigned-without-E");
            var invalid=Read("inputs/walls/gravity-0-e0.json");invalid["gravity_design"]!["fk"]="";
            Compare(JsonSerializer.SerializeToNode(RetainingWall.Calculate(invalid,wallEngine:WallCalculationEngine.Legacy)),
                JsonSerializer.SerializeToNode(RetainingWall.Calculate(invalid)),"invalid-material");
        }
        int cancellations=0;
        try { RetainingWall.Calculate(serviceInput,new CancellationToken(true));errors.Add("Cancelled calculation completed"); }
        catch(OperationCanceledException){cancellations++;}
        if(gravityOnly?walls!=6||curvatures!=240:walls!=26||shear!=12512||curvatures!=1572||shapes==0)errors.Add("Incomplete structural corpus");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"comparison.json"),JsonSerializer.Serialize(new{walls,numbers,shear,curvatures,shapes,cancellations,maxError,differences=errors.Count,errors=errors.Take(100)},new JsonSerializerOptions{WriteIndented=true}));
        foreach(string e in errors.Take(15))Console.Error.WriteLine(e);
        Console.WriteLine($"F4 structural adapter: {walls} walls, {numbers} numbers, {errors.Count} differences, max {maxError:R}");
        return errors.Count==0?0:1;
    }
}
