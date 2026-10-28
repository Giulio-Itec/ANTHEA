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
// Frames, node frames, restraint/link/release symbols and messages: the normalization shared with the MIDAS text importer.
GpcBatchNormalization.Apply(snapshot, batch, mapped.Diagnostics);
var boundaries = snapshot.Boundaries!;
snapshot.Name = "Spalla · modello MIDAS completo";
snapshot.Validate();
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(snapshot));
File.WriteAllText(Path.ChangeExtension(output, ".import.json"), JsonSerializer.Serialize(new
{
    Source = directory, Converter = typeof(CivilNxModelProfile).Assembly.GetName().Version?.ToString(),
    Nodes = snapshot.Nodes.Length, Elements = snapshot.Elements.Length, Frames = snapshot.LocalFrames!.Length,
    NodeFrames = snapshot.NodeFrames?.Length,
    Boundaries = boundaries.GroupBy(b => b.Kind).ToDictionary(g => g.Key, g => g.Count()),
    Results = snapshot.Results.Select(r => new { r.Name, Corners = r.Values.Length, Fields = r.Fields?.Select(f => new { f.Family, f.Axes, f.Component, Samples = f.Values.Length }) }),
    Diagnostics = batch.Diagnostics.Select(d => new { d.Code, Severity = d.Severity.ToString(), d.Record, d.Message })
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{snapshot.Nodes.Length} nodes, {snapshot.Elements.Length} elements, {snapshot.LocalFrames.Length} local frames, {boundaries.Length} boundary symbols.");
