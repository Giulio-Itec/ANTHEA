using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4ResidualCapture
{
    internal static int Run(string root, string output)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        string target = Path.Combine(output, "residual-before.json");
        if (File.Exists(target)) throw new IOException("Immutable capture already exists.");
        var cases = new JsonArray();
        void Add(string id, string kind, JsonObject input)
        {
            input = (JsonObject)input.DeepClone();
            var row = new JsonObject { ["id"] = id, ["kind"] = kind, ["input"] = input };
            string before = input.ToJsonString(); row["expected"] = Evaluate(row);
            if (input.ToJsonString() != before) throw new Exception("Input changed: " + id);
            cases.Add(row);
        }
        foreach (string shape in new[] { "Rettangolare", "Circolare", "A T" })
        foreach (double cover in new[] { 40d, 70d, 100d })
        {
            var input = SezioneCA.DefaultInput(); input["shape"] = shape; input["cover_mm"] = cover;
            Add(shape + "-" + cover, "section", input);
        }
        foreach (bool hole in new[] { false, true })
        foreach (string shape in new[] { "Rettangolare", "Circolare" })
        {
            var input = SezioneCA.DefaultInput(); input["shape"] = shape;
            input["foro_presente"] = hole; input["inner_diameter_mm"] = 200;
            input["inner_width_mm"] = 100; input["inner_height_mm"] = 100;
            input["second_top_enabled"] = true; input["second_top_count"] = 3;
            input["second_top_diameter"] = 16; input["second_top_gap"] = 35;
            input["second_bottom_enabled"] = true; input["second_bottom_count"] = 3;
            input["second_bottom_diameter"] = 16; input["second_bottom_gap"] = 35;
            input["second_inner_enabled"] = true; input["second_inner_count"] = 12;
            input["second_inner_diameter"] = 20; input["second_inner_gap"] = 40;
            Add(shape + "-layers-hole-" + hole, "section", input);
        }
        foreach (double fck in new[] { 20d, 35d, 50d, 70d, 90d })
        { var input = SezioneCA.DefaultInput(); input["fck_mpa"] = fck; Add("elastic-" + fck, "section", input); }
        foreach (bool enabled in new[] { false, true })
        foreach (string reference in BridgeSection.BottomRebarReferences)
        foreach (double thickness in new[] { 0d, 60d, 249d, 250d })
        {
            var data = BridgeSection.Defaults(); data["predalle"] = enabled; data["h_cls"] = 250;
            data["h_predalle"] = thickness; data["rif_ferri_inf"] = reference;
            Add("slab-" + enabled + "-" + reference + "-" + thickness, "slab", data);
        }
        foreach (double distance in new[] { 0d, 7d, 8d, 45d })
        {
            var data = BridgeSection.Defaults(); data["predalle"] = true; data["h_predalle"] = 60;
            data["rebars_bottom"] = true; data["d_bottom"] = 16; data["cover_bottom"] = distance;
            Add("slab-distance-" + distance, "slab", data);
        }
        foreach (string name in ConcreteStandards.Names)
            Add("standard-" + name, "standard", new JsonObject { ["name"] = name });
        foreach (int angle in new[] { 0, 15, 30 })
        foreach (bool active in new[] { false, true })
        {
            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/geo_micropalo_verticale--regressione-micropalo_igu_0_nessuna-riduzione.json")))!["data"]!.AsObject();
            data["generali"]!["inclinazione"] = angle;
            data["stratigrafie"]![0]![0]!["laterale_attiva"] = active;
            Add("micropile-" + angle + "-" + active, "micropile", data);
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(target, new JsonObject { ["cases"] = cases }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Captured {cases.Count} residual geometry, standards and micropile cases before changes.");
        return 0;
    }
    internal static JsonNode? Evaluate(JsonObject row)
    {
        var input = row["input"]!.AsObject();
        try
        {
            object? result = row.S("kind") switch
            {
                "section" => Section(input),
                "slab" => BridgeSection.SlabLayout(input),
                "standard" => ConcreteStandards.Defaults(input.S("name")),
                "micropile" => Calcolo.Calcola(input, true),
                _ => throw new ArgumentException("Unknown capture kind")
            };
            return new JsonObject { ["result"] = JsonSerializer.SerializeToNode(result) };
        }
        catch (ArgumentException e) { return new JsonObject { ["error"] = e.Message }; }
    }
    static object Section(JsonObject input)
    {
        var section = new SezioneCA(input);
        return new { X = SectionShearGeometry.Derive(section, true), Y = SectionShearGeometry.Derive(section, false), section.NAutomatico };
    }
}
