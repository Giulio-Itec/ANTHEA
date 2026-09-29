using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

string root = Path.GetFullPath(args[0]);
foreach (string path in Directory.GetFiles(root, "preparazione-input.json", SearchOption.AllDirectories))
{
    var native = JsonNode.Parse(File.ReadAllText(path))!; string folder = Path.GetDirectoryName(path)!, id = native.S("id");
    var d = RetainingWall.Example(id.StartsWith("03-") ? "gravity" : "cantilever");
    var geometry = d["geometry"]!;
    geometry["height"] = native.D("height"); geometry["stem_base"] = native.D("top"); geometry["stem_top"] = native.D("top");
    geometry["toe"] = 1; geometry["heel"] = .5; geometry["slab"] = .5;
    double gamma = native.D("gamma") * .00980665, sat = (native.D("gamma_sat") > 0 ? native.D("gamma_sat") : 2000) * .00980665, phi = native.D("phi");
    d["materials"]!["gamma"] = 2500 * .00980665; d["materials"]!["fck"] = 25;
    d["foundation"]!["gamma"] = gamma; d["foundation"]!["gamma_sat"] = sat; d["foundation"]!["phi"] = phi; d["foundation"]!["delta"] = native.D("delta");
    var layer = d["layers"]![0]!; layer["gamma"] = gamma; layer["gamma_sat"] = sat; layer["phi"] = phi; layer["thickness"] = 30;
    foreach (var action in d.Array("actions")) { action!["enabled"] = false; action["value"] = 0; }
    RetainingWall.PrepareGlobalProfile(d); var global = d["global_stability"]!;
    global["enabled"] = true; global["profile_confirmed"] = true; global["depth_min"] = .01; global["depth_max"] = 10;
    global["layers"]![0]!["c"] = native.D("cohesion") * .00980665;
    var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = d.DeepClone();
    string model = Path.Combine(folder, "modello.anthea"); Archivio.Scrivi(model, doc);
    if (Archivio.Leggi(model)["dati"]!.ToJsonString() != d.ToJsonString()) throw new Exception("Round-trip ANTHEA non riuscito");
    var result = RetainingWall.Calculate(d);
    File.WriteAllText(Path.Combine(folder, "risultati-anthea.json"), result.Json().ToJsonString(J.Options));
    File.WriteAllText(Path.Combine(folder, "conci-anthea.csv"), ReportRetainingWall.GlobalCsv(result.GlobalStability!));
    Archivio.ScriviAtomico(Path.Combine(folder, "relazione-anthea.docx"), ReportRetainingWall.Create(id, result));
    Console.WriteLine(id + ": " + string.Join("; ", result.GlobalStability!.Cases.Select(c => $"{c.Factors.Name}: F={c.Critical?.Factor:G10}, {c.Status}")));
}
