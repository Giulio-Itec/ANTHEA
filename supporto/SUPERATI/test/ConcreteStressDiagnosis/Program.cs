using System.Text.Json.Nodes;
using Anthea.Calculations;
var data = JsonNode.Parse(File.ReadAllText(args[0]))!["dati"]!.AsObject();
// The desktop file may be edited between runs. Reconstruct the input read on 2 October explicitly.
if(args.Contains("--original"))
{
    var original=data["input"]!.AsObject();
    foreach(var (key,value) in new[]{("width_mm","400"),("height_mm","700"),("cover_mm","30"),
        ("top_bar_count","6"),("bottom_bar_count","6"),("side_bar_count_per_side","2"),
        ("top_bar_diameter_mm","22"),("bottom_bar_diameter_mm","22"),("side_bar_diameter_mm","22"),("transverse_bar_diameter_mm","10")})original[key]=value;
}
var ws = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
var options = ws["sle"]!["SLE"]!.AsObject();
double ec = ConcreteMaterials.Concrete(input).E;
Console.WriteLine(options.ToJsonString());
foreach (double phi in new[] {15d, 0d, 15*ec/input.D("steel_modulus_mpa")-1})
{
    options["phi"] = phi;
    var checker = new CheckerSection(input, ws, options);
    var s = checker.Stress(new(-2500,500,250), "SLE");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {phi, s.sigma_cls,s.sigma_acciaio,s.tensioni_barre, s.Response}));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(IndependentElastic.Solve(new SezioneCA(input),input.D("steel_modulus_mpa")*(1+phi)/ec,true,s.sigma_cls,s.sigma_acciaio)));
}
input["cover_mm"] = "29";
var exact = new CheckerSection(input, ws, options);
var matched = exact.Stress(new(-2500,500,250), "SLE");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { caseName="n15-cover29mm", matched.sigma_cls,matched.sigma_acciaio, matched.Response, bars=new SezioneCA(input).Bars }));
foreach(bool deduct in new[]{true,false})Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(IndependentElastic.Solve(new SezioneCA(input),15,deduct,matched.sigma_cls,matched.sigma_acciaio)));
Console.WriteLine("PASS: independent equilibrium and native stress comparisons; input file unchanged.");
