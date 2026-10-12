using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

// R7: compare cold shared sections and independent sections with serial solves.
// Captures are evidence, never overwritten as reference by a later run.
string output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
string[] shapes = ["Rettangolare", "A T", "Circolare"];
var cases = (from linear in new[] { "Lineare", "Non lineare" }
             from phi in new[] { "0", "2" }
             from load in new[] { new ActionPoint(-300, 30, 5), new ActionPoint(-100, -15, 10), new ActionPoint(100, 25, -5) }
             select (linear, phi, load)).ToArray();
var captured = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (string shape in shapes)
{
    var data = SezioneCA.DefaultData();
    data["input"]!["shape"] = shape;
    var workspace = SectionWorkspace.Prepare(data);
    string inputJson = data["input"]!.ToJsonString(), workspaceJson = workspace.ToJsonString();
    (JsonObject Input, JsonObject Workspace) Inputs() => (JsonNode.Parse(inputJson)!.AsObject(), JsonNode.Parse(workspaceJson)!.AsObject());
    CheckerSectionModel Model() { var v = Inputs(); return CheckerSection.PrepareModel(v.Input, v.Workspace); }
    string Solve(CheckerSectionModel model, int index)
    {
        var v = Inputs(); var c = cases[index];
        var options = J.Obj(("assi", "Locali"), ("criterio", "N costante"), ("modello", c.linear), ("phi", c.phi));
        var engine = new CheckerSection(model, v.Input, v.Workspace, options);
        var result = engine.Stress(c.load, "SLE");
        var mesh = model.Section.Mesh;
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        {
            // Preserve numbering, order and every floating-point bit.
            foreach (var vertex in mesh.GetVertices()) { writer.Write(vertex.Id); writer.Write(vertex.Point.X); writer.Write(vertex.Point.Y); writer.Write(vertex.Point.Z); }
            foreach (var face in mesh.GetFaces()) { writer.Write(face.Id); writer.Write(face.A); writer.Write(face.B); writer.Write(face.C); writer.Write(face.D); }
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray())) + "\n" + J.Node(result)!.ToJsonString();
    }
    var serial = Model();
    var expected = Enumerable.Range(0, cases.Length).Select(i => Solve(serial, i)).ToArray();
    for (int i = 0; i < expected.Length; i++) captured[shape + "/" + i] = expected[i];
    for (int round = 0; round < 2; round++)
    {
        var shared = Model(); // no mesh access before concurrent construction
        foreach (bool independent in new[] { false, true })
        {
            var results = new string[cases.Length * 2];
            Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 },
                i => results[i] = Solve(independent ? Model() : shared, i % cases.Length));
            for (int i = 0; i < results.Length; i++)
                Check(results[i] == expected[i % cases.Length], $"{shape}, round {round}, independent={independent}, case {i}: mesh or stress differs from serial");
        }
    }
    Console.WriteLine($"PASS {shape}: shared cold/warm mesh, independent models, linear/nonlinear and creep, same bits as serial");
}
string json = JsonSerializer.Serialize(captured, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(output, "states.json"), json);
int refIndex = Array.IndexOf(args, "--reference");
if (refIndex >= 0)
{
    string reference = Path.GetFullPath(args[refIndex + 1]);
    var previous = JsonSerializer.Deserialize<SortedDictionary<string, string>>(File.ReadAllText(Path.Combine(reference, "states.json")))!;
    Check(previous.Count == captured.Count, "before/after same case count");
    foreach (var pair in captured) Check(previous.TryGetValue(pair.Key, out var value) && value == pair.Value, "before/after " + pair.Key);
    // Reuse the existing 80-section capture rather than a second catalogue.
    string[] Rows(string directory) => File.ReadAllLines(Path.Combine(directory, "meshes", "mesh-fingerprint.csv")).Where(l => !l.StartsWith('#')).ToArray();
    Check(Rows(reference).SequenceEqual(Rows(output)), "80 usual section meshes: identical coordinates/connectivity/order and solver results");
}
File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new { checks, cases = captured.Count, processors = Environment.ProcessorCount }));
Console.WriteLine($"PASS {checks} concurrency and before/after checks");
