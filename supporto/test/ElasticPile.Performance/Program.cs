using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Geotechnics.Piles;

// Isolated timing diagnostic: uses the production FEM and RC orchestration without altering the application.
var inputPath=Path.GetFullPath(args.Length>0?args[0]:"supporto/artefatti/palo-armature/input.json");
var outputPath=Path.GetFullPath(args.Length>1?args[1]:"supporto/artefatti/palo-prestazioni/misure.json");
var root=JsonNode.Parse(File.ReadAllText(inputPath))!.AsObject();
ElasticHorizontalPile.PrepareShared(root);
if(args.Contains("--compare"))
{
    var measurements=new List<object>();
    foreach(double step in new[]{.5,.25})
    {
        root["elastico"]!["passo"]=step;var sw=Stopwatch.StartNew();var response=ElasticHorizontalPile.CalculateResponse(root);double fem=sw.Elapsed.TotalMilliseconds;
        var cache=new ElasticPileVerificationCache();
        foreach(int degree in new[]{1,0,0})
        {
            sw.Restart();var value=ElasticHorizontalPile.CompleteReinforcement(root,response,degree==1?new():cache,maximumParallelism:degree);double elapsed=sw.Elapsed.TotalMilliseconds;
            if(value["armature"]!.Array("tratti").Any(t=>t.S("errore")!=""))throw new Exception(value["armature"]!.ToJsonString());
            var item=new{step,degree,femMs=fem,verificationMs=elapsed,points=response["risposta"]!.Array("Points").Count,newN=value["armature"]!.Array("tratti").Sum(t=>t.D("nuovi_valori_N")),timings=value["armature"]!.Array("tratti").Select(t=>t!["tempi_ms"]!.DeepClone()).ToArray()};measurements.Add(item);Console.WriteLine(JsonSerializer.Serialize(item));
        }
    }
    File.WriteAllText(outputPath,JsonSerializer.Serialize(new{processors=Environment.ProcessorCount,measurements},new JsonSerializerOptions{WriteIndented=true}));return;
}
var reinforcement=typeof(ElasticHorizontalPile).GetMethod("CalculateReinforcement",BindingFlags.Static|BindingFlags.NonPublic)!;
var samples=new List<object>();
foreach(var scenario in new[]{(Step:.5,Weight:true),(Step:.25,Weight:true),(Step:.5,Weight:false)})
{
    for(int trial=0;trial<3;trial++)
    {
        var copy=(JsonObject)root.DeepClone();copy["elastico"]!["passo"]=scenario.Step;
        var input=ElasticHorizontalPile.SharedInput(copy);
        input["N"]=copy["generali"]!["azione_assiale"]!.DeepClone();
        input["peso_lineare"]=scenario.Weight?PileSegments.ConcreteWeight(copy["generali"]!.D("diametro"),copy["sezione"]!.D("gamma_ca",25)):0;
        input["quote_verifica"]=J.Node(ElasticHorizontalPile.ReadSegments(copy).SelectMany(s=>new[]{s.Start,s.End}).Distinct().ToArray());
        var watch=Stopwatch.StartNew();var result=ElasticHorizontalPile.Calculate(input);double fem=watch.Elapsed.TotalMilliseconds;
        watch.Restart();var rc=(JsonObject)reinforcement.Invoke(null,new object[]{copy,result})!;double concrete=watch.Elapsed.TotalMilliseconds;
        var points=result["risposta"]!.Array("Points");int distinct=points.Select(p=>p!.D("AxialForce")).Distinct().Count();
        if(rc.Array("tratti").Any(t=>t!.S("errore")!=""))throw new Exception("RC diagnostic returned an error.");
        var row=new{step=scenario.Step,weight=scenario.Weight,trial,femAndJsonMs=fem,reinforcementMs=concrete,elements=result["risposta"]!.D("Elements"),points=points.Count,distinctN=distinct,expectedDomainSolves=distinct*2};
        samples.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));
    }
}
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath,JsonSerializer.Serialize(new{input=inputPath,note="Release diagnostic; trial 0 includes warm-up. Weight=false is only a counterfactual to isolate the exact-N cache, never a proposed physical simplification.",samples},new JsonSerializerOptions{WriteIndented=true}));
