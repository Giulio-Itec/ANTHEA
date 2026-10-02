using System.Text.Json.Nodes;
using Anthea.Calculations;
var data = JsonNode.Parse(File.ReadAllText(args[0]))!["dati"]!.AsObject();
var ws = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
var options = ws["sle"]!["SLE"]!.AsObject();
Console.WriteLine(options.ToJsonString());
foreach (double phi in new[] {15d, 0d, 15/(101.6653863811558/16)-1})
{
    options["phi"] = phi;
    var checker = new CheckerSection(input, ws, options);
    var s = checker.Stress(new(-2500,500,250), "SLE");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {phi, s.sigma_cls,s.sigma_acciaio,s.tensioni_barre, s.Response}));
}
input["cover_mm"] = "29";
var exact = new CheckerSection(input, ws, options);
var matched = exact.Stress(new(-2500,500,250), "SLE");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { caseName="n15-bars50mm", matched.sigma_cls,matched.sigma_acciaio, matched.Response, bars=new SezioneCA(input).Bars }));
