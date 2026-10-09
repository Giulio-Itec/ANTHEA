using System.Text.Json;
using ANTHEA.ModelWorkspace;
using GPC.Converter.CivilNx;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Attributes;
using GPC.Converter;

if (args.Length != 2) throw new ArgumentException("Arguments: source directory, output .antheamodel");
string directory = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
if (File.Exists(output)) throw new IOException("Choose a new package name to preserve prior evidence.");
var snapshot = MidasSnapshotImporter.ReadDirectory(directory);
var tables = CivilNxModelProfile.RequiredTables.Concat(CivilNxModelProfile.OptionalTables).Distinct()
    .Where(t => File.Exists(Path.Combine(directory, t + ".json")))
    .ToDictionary(t => t, t => new CivilNxResponse("db/" + t, File.ReadAllText(Path.Combine(directory, t + ".json"))));
var batch = new CivilNxModelProfile().Read(new CivilNxSnapshot(tables),
    new AnalysisSource { Program = CivilNxModelProfile.Program, SolverVersion = "acquisizione di prova", ModelRevision = snapshot.Fingerprint(), AnalysisId = "snapshot" }, CancellationToken.None);
var mapped = ModelMapper.Map(batch);
if (mapped.Model == null) throw new InvalidDataException("GPC Model ha rifiutato la copia: " + string.Join("; ", mapped.Diagnostics.Select(d => d.Code + ": " + d.Message)));
ModelDirection Direction(Vector3d v) => new(v.X, v.Y, v.Z);
snapshot.LocalFrames = batch.Shells.Select(s => new ModelLocalFrame(int.Parse(s.Id), Direction(s.CoordinateSystem.V1), Direction(s.CoordinateSystem.V2), Direction(s.CoordinateSystem.V3)))
    .Concat(batch.Beams.Select(b => new ModelLocalFrame(int.Parse(b.Id), Direction(b.CoordinateSystem.V3), Direction(b.CoordinateSystem.V1), Direction(b.CoordinateSystem.V2)))).ToArray();
string[] dofs = ["DX", "DY", "DZ", "RX", "RY", "RZ"];
string Flags(IEnumerable<bool> flags) => string.Join(", ", flags.Select((active, i) => active ? dofs[i] : null).OfType<string>());
var boundaries = batch.NodeRestrains.Select(r => new ModelBoundary(r.Record, "RESTRAIN", [int.Parse(r.NodeId)], null, Flags(r.FixedDofs))).ToList();
boundaries.AddRange(batch.NodeLinks.Select(r => new ModelBoundary(r.Record, r.RigidDofs == null ? "LINK" : "CONSTRAINT",
    [int.Parse(r.I), int.Parse(r.J)], null, r.RigidDofs == null ? "Matrice elastica" : Flags(r.RigidDofs))));
foreach (var r in batch.BeamReleases)
{
    var beam = batch.Beams.Single(b => b.Id == r.BeamId);
    foreach (var (node, connections, end) in new[] { (beam.I, r.Release.I, "I"), (beam.J, r.Release.J, "J") })
        if (connections.Any(c => c.Kind != BeamConnectionKind.Continuous))
            boundaries.Add(new(r.Record + "/" + end, "RELEASE", [int.Parse(node)], int.Parse(r.BeamId), string.Join(", ", connections.Select((c, i) => dofs[i] + ": " + c.Kind))));
}
snapshot.Boundaries = boundaries.ToArray();
snapshot.ImportMessages = mapped.Diagnostics.Select(d => new ModelImportMessage(d.Code, d.Severity.ToString(), d.Record, d.Message)).ToArray();
snapshot.Name = "Spalla · modello MIDAS completo";
snapshot.Validate();
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(snapshot));
File.WriteAllText(Path.ChangeExtension(output, ".import.json"), JsonSerializer.Serialize(new
{
    Source = directory, Converter = typeof(CivilNxModelProfile).Assembly.GetName().Version?.ToString(),
    Nodes = snapshot.Nodes.Length, Elements = snapshot.Elements.Length, Frames = snapshot.LocalFrames.Length,
    Boundaries = boundaries.GroupBy(b => b.Kind).ToDictionary(g => g.Key, g => g.Count()),
    Results = snapshot.Results.Select(r => new { r.Name, Corners = r.Values.Length, Fields = r.Fields?.Select(f => new { f.Family, f.Axes, f.Component, Samples = f.Values.Length }) }),
    Diagnostics = batch.Diagnostics.Select(d => new { d.Code, Severity = d.Severity.ToString(), d.Record, d.Message })
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{snapshot.Nodes.Length} nodes, {snapshot.Elements.Length} elements, {snapshot.LocalFrames.Length} local frames, {boundaries.Count} boundary symbols.");
