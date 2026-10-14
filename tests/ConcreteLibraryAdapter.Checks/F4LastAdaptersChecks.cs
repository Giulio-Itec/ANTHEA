using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4LastAdaptersChecks
{
    internal static int Run(string root, string output, bool capture)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        Directory.CreateDirectory(output);
        var rows = new JsonArray();
        if (capture)
        {
            foreach (string standard in ConcreteStandards.OrdinaryNames)
            foreach (string shape in new[] { "Rettangolare", "Circolare", "Anello" })
            foreach (int cover in new[] { 40, 70 })
            {
                var data = SezioneCA.DefaultData(); var input = data["input"]!.AsObject();
                input["shape"] = shape == "Anello" ? "Circolare" : shape; input["foro_presente"] = shape == "Anello";
                input["inner_diameter_mm"] = 200; input["cover_mm"] = cover;
                input["steel_eps_u"] = 75; input["steel_fu_mpa"] = 600;
                var ws = SectionWorkspace.Prepare(data); ws["normativa"] = standard;
                var options = ws["taglio"]!.AsObject(); ConcreteCalculationSettings.UpdateAutomaticShear(input, options);
                options["ancoraggio"] = "Confermato"; options["modello_circolare"] = standard == "NTC 2018" ? "Pile" : "Parametri assegnati";
                rows.Add(new JsonObject { ["id"] = standard + "/" + shape + "/" + cover, ["kind"] = "shear", ["input"] = data });
            }
            foreach (string standard in BridgeSection.Standards)
                rows.Add(new JsonObject { ["id"] = standard, ["kind"] = "bridge", ["input"] = J.Obj(("normativa", standard), ("gamma_c", "1.47"), ("gamma_s", "1.16")) });
            foreach (var row in rows.OfType<JsonObject>()) row["expected"] = Evaluate(row);
            string target = Path.Combine(output, "last-adapters-before.json");
            if (File.Exists(target)) throw new IOException("Immutable capture already exists.");
            File.WriteAllText(target, rows.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Captured {rows.Count} successful shear and standard transitions.");
            return 0;
        }
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f4/last-adapters-reference.json")))!;
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, manifest["file"]!.GetValue<string>()));
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(manifest["sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) throw new Exception("Immutable capture changed.");
        rows = JsonNode.Parse(bytes)!.AsArray(); var stats = new Stats("Last adapters"); var errors = new List<string>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            string before = row.ToJsonString(); Json.Compare(Json.Sort(row["expected"]), Json.Sort(Evaluate(row)), row.S("id"), stats, errors, 1e-10);
            if (before != row.ToJsonString()) errors.Add("Input mutated: " + row.S("id"));
        }
        if (rows.Count != 44) errors.Add("Incomplete corpus.");
        File.WriteAllText(Path.Combine(output, "comparison.json"), JsonSerializer.Serialize(new { cases = rows.Count, stats.Numbers, stats.MaxRelative, errors }));
        Console.WriteLine(stats.Line()); foreach (string error in errors.Take(20)) Console.Error.WriteLine(error);
        return errors.Count == 0 ? 0 : 1;
    }

    private static JsonNode? Evaluate(JsonObject row)
    {
        var data = (JsonObject)row["input"]!.DeepClone();
        if (row.S("kind") == "bridge") { BridgeSection.ApplyStandardDefaults(data); return data; }
        var ws = data["workspace_ca"]!.AsObject();
        return JsonSerializer.SerializeToNode(ConcreteShearAnalysis.Calculate(data["input"]!.AsObject(), ws, ws["taglio"]!.AsObject(),
            J.Obj(("N", "100"), ("Vx", "50"), ("Vy", "40"), ("T", "0"), ("Mx", "100"), ("My", "70"))));
    }
}
