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
    Capture("region/" + id++, () => SectionRegions.Region(section, "test", 1, 0, 0, indices));
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
        numbers++; double error = Math.Abs(x - y), scale = Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
        if (error > 1e-9 * scale) throw new Exception($"{path}: {x:R} != {y:R}");
        if (x != y) rounded++;
        maxRelative = Math.Max(maxRelative, error / scale);
    }
    else if (!JsonNode.DeepEquals(a, b)) throw new Exception(path + ": " + a + " != " + b);
}
Compare(expected, snapshot, "cases");
// Hand calculation: fbd=2.25*2/1.5=3 MPa; lb=16*300/(4*3)=400 mm;
// NTC straight bar minimum 320 mm, lap with 100% has alpha6=1.5 and l0=600 mm.
var anchor = new ConcreteAnchorageCalculator().Calculate(new(16, 300, 2, 1.5, true, 400, false, 100, 0));
if (anchor.Fbd != 3 || anchor.BasicLength != 400 || anchor.RequiredLength != 400 || !anchor.Passed) throw new Exception("Independent straight anchorage");
var lapResult = new ConcreteAnchorageCalculator().Calculate(new(16, 300, 2, 1.5, true, 600, true, 100, 64));
if (lapResult.RequiredLength != 600 || lapResult.Alpha6 != 1.5 || !lapResult.Passed) throw new Exception("Independent lap");
Console.WriteLine($"PASS: {rows.Count} captured cases; {numbers} numbers, {rounded} rounding differences, max relative {maxRelative:R}; 2 independent anchorage cases.");
