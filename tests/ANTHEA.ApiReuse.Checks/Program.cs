using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

// Before/after evidence, not independent expected values. --capture refuses to overwrite.
// The independent checks below have explicit hand-calculated expectations.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var rows = new SortedDictionary<string, JsonNode?>();
void Capture(string key, Func<object?> run)
{
    try { rows.Add(key, JsonSerializer.SerializeToNode(run())); }
    catch (ArgumentException e) { rows.Add(key, JsonSerializer.SerializeToNode(new { Error = e.Message, Type = e.GetType().FullName })); }
}
int id = 0;
foreach (double fck in new[] { 25d, 50, 60, 90 })
foreach (double diameter in new[] { 16d, 32, 40, 132 })
foreach (double eta in new[] { 1d, .7 })
    Capture("bond/" + id++, () => ConcreteBond.Calculate(fck, diameter, eta, .85, 1.5));
foreach (double diameter in new[] { 16d, 40, 132 })
foreach (bool lap in new[] { false, true })
foreach (double percent in new[] { 25d, 50, 100, double.NaN })
foreach (double clear in new[] { 0d, 50, 200 })
    Capture("anchorage/" + id++, () => new ConcreteAnchorageCalculator().Calculate(new(diameter, 300, 2, 1.5, true, 600, lap, percent, clear)));
foreach (string shape in new[] { "Rettangolare", "Circolare", "A T" })
foreach (ConcreteMemberKind kind in Enum.GetValues<ConcreteMemberKind>())
foreach (bool lap in new[] { false, true })
foreach (bool links in new[] { false, true })
{
    var input = SezioneCA.DefaultInput(); input["shape"] = shape;
    var section = new SezioneCA(input);
    Capture("detailing/" + id++, () => new ConcreteDetailingCalculator().Calculate(new(kind, section, 500, links, 10, 200, 2, 20, 25, 10, lap, 350, 200, true, false, false)));
    var indices = Enumerable.Range(0, section.Bars.Count).ToArray();
    Capture("spacing/" + id++, () => new TensionBarSpacing().Maximum(section, indices));
    Capture("region/" + id++, () => LegacySectionRegions.Region(section, "test", 1, 0, 0, indices));
}
foreach (string diagram in new[] { "Elastoplastico", "Incrudente" })
foreach (double fy in new[] { 215d, 450, 600 })
foreach (double gamma in new[] { 1d, 1.15, 2 })
{
    var input = RebarMaterial.Defaults()["input"]!.DeepClone().AsObject();
    input["fyk_mpa"] = fy; input["steel_fu_mpa"] = 1.2 * fy; input["gamma_s"] = gamma;
    input["steel_diagramma"] = diagram;
    Capture("steel/" + id++, () => new { Values = RebarMaterial.Evaluate(input), Curve = RebarMaterial.Curve(input) });
}
var snapshot = JsonSerializer.SerializeToNode(rows)!;
if (args.Length == 2 && args[0] == "--capture")
{
    using var output = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write);
    JsonSerializer.Serialize(output, rows, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine($"CAPTURE: {rows.Count} cases; no existing expected file overwritten.");
    return;
}
var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "before.json");
var expected = JsonNode.Parse(File.ReadAllText(expectedPath))!;
int numbers = 0, rounded = 0; double maxRelative = 0;
void Compare(JsonNode? a, JsonNode? b, string path)
{
    if (a is JsonObject oa && b is JsonObject ob)
    {
        if (!oa.Select(x => x.Key).Order().SequenceEqual(ob.Select(x => x.Key).Order())) throw new Exception(path + ": fields differ");
        foreach (var (key, value) in oa) Compare(value, ob[key], path + "/" + key);
    }
    else if (a is JsonArray aa && b is JsonArray ab)
    {
        if (aa.Count != ab.Count) throw new Exception(path + ": lengths differ");
        for (int i = 0; i < aa.Count; i++) Compare(aa[i], ab[i], path + "/" + i);
    }
    else if (a is JsonValue va && b is JsonValue vb && va.TryGetValue<double>(out var x) && vb.TryGetValue<double>(out var y))
    {
        numbers++; double error = Math.Abs(x - y), scale = Math.Max(Math.Abs(x), Math.Abs(y));
        if (error > 1e-12 * scale) throw new Exception($"{path}: {x:R} != {y:R}");
        if (x != y) rounded++;
        if (scale > 0) maxRelative = Math.Max(maxRelative, error / scale);
    }
    else if (!JsonNode.DeepEquals(a, b)) throw new Exception(path + ": " + a + " != " + b);
}
// DET-1 (analisi dei bug del 15/10/2026, GPCChecker.Concrete 8f95242c): l'interasse di solette e pareti si misura lungo gli strati, non più fra
// le barre allineate delle due facce. Differenza ammessa una per una: parete dalla sezione predefinita 600×800 (Width < Height: strati alle facce
// x = ±212), «Interasse verticale sulla sezione» da 424 (prima: max fra strati e attraverso lo spessore) a 208 mm, atteso a mano
// (800 − 2·(70 + 10 + 8))/3 = 208: le due barre laterali Ø16 di ogni faccia fra i centri a ±312 mm; la verifica (≤ 400 mm) diventa soddisfatta.
// Ogni altro campo resta identico; la cattura before.json non si rigenera.
int det1 = 0;
foreach (var (key, node) in snapshot.AsObject())
{
    if (!key.StartsWith("detailing/") || node is not JsonArray now || expected[key] is not JsonArray before || now.Count != before.Count) continue;
    for (int i = 0; i < now.Count; i++)
    {
        if (now[i] is not JsonObject n || before[i] is not JsonObject b || (string?)n["Name"] != "Interasse verticale sulla sezione") continue;
        if (JsonNode.DeepEquals(n, b)) continue;
        double hand = (800 - 2 * (70 + 10 + 8)) / 3.0, limit = (double)n["Limit"]!;
        bool sameOtherwise = n.Where(p => p.Key is not ("Actual" or "Passed")).All(p => JsonNode.DeepEquals(p.Value, b[p.Key]));
        if (!sameOtherwise || (double)b["Actual"]! != 424 || (bool?)b["Passed"] != false || Math.Abs((double)n["Actual"]! - hand) > 1e-12 * hand || (bool?)n["Passed"] != (hand <= limit))
            throw new Exception($"cases/{key}/{i}: differenza fuori dalla categoria DET-1: {b.ToJsonString()} -> {n.ToJsonString()}");
        n["Actual"] = b["Actual"]!.DeepClone(); n["Passed"] = b["Passed"]!.DeepClone(); det1++;
    }
}
if (det1 != 4) throw new Exception($"DET-1: {det1} voci classificate invece di 4 (pareti rettangolari 600×800, con e senza sovrapposizione e staffe)");
Compare(expected, snapshot, "cases");
LibraryCalls.Check();
ProductionPaths.Check();
// API-02: independent geometry: max(2*pi*144.000005/8, 2*pi*110.000005/6).
// The frozen historical captures are preserved; 452.3893578248936 was the legacy defect.
var ringInput = SezioneCA.DefaultInput();
foreach (var (key, value) in new[] { ("shape", "Circolare"), ("diameter_mm", "400"), ("longitudinal_bar_count", "8"),
    ("longitudinal_bar_diameter_mm", "16"), ("cover_mm", "39.999995"), ("transverse_bar_diameter_mm", "8"),
    ("second_inner_count", "6"), ("second_inner_diameter", "12"), ("second_inner_gap", "20") }) ringInput[key] = value;
ringInput["second_inner_enabled"] = true;
var ringSection = new SezioneCA(ringInput);
double ringSpacing = new TensionBarSpacing().Maximum(ringSection, Enumerable.Range(0, ringSection.Bars.Count).ToArray())!.Value;
if (Math.Abs(ringSpacing - 115.19173586761349) > 1e-12 * ringSpacing) throw new Exception("API-02: incorrect circular spacing");
// Hand calculation: fbd=2.25*2/1.5=3 MPa; lb=16*300/(4*3)=400 mm;
// NTC straight bar minimum 320 mm, lap with 100% has alpha6=1.5 and l0=600 mm.
var anchor = new ConcreteAnchorageCalculator().Calculate(new(16, 300, 2, 1.5, true, 400, false, 100, 0));
if (anchor.Fbd != 3 || anchor.BasicLength != 400 || anchor.RequiredLength != 400 || !anchor.Passed) throw new Exception("Independent straight anchorage");
var lapResult = new ConcreteAnchorageCalculator().Calculate(new(16, 300, 2, 1.5, true, 600, true, 100, 64));
if (lapResult.RequiredLength != 600 || lapResult.Alpha6 != 1.5 || !lapResult.Passed) throw new Exception("Independent lap");
Console.WriteLine($"PASS: {rows.Count} captured cases; {numbers} numbers, {rounded} rounding differences, max relative {maxRelative:R}; 2 independent anchorage cases; DET-1 {det1} wall spacings classified.");
