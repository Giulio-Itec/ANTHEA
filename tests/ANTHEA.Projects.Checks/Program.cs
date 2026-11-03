using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Projects;
using X.Core;

// Checks of src/ANTHEA.Projects (docs/sviluppo/progetto-sistema.md, 3/11/2026): the file of a project with the archive of the results of its
// models. Argument: the output folder (esito.txt).
string output = args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "anthea-projects-checks");
Directory.CreateDirectory(output);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL " + message); checks++; Console.WriteLine("PASS " + message); }

string root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "ANTHEA.sln"))) root = Path.GetDirectoryName(root)!;
var fixture = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "model-viewer-combinations-1.16.2", "progetto-1.16.2.json")))!;
string fixtureId = ProjectModelStore.Models(fixture).Single()["id"]!.GetValue<string>();
var model = ProjectModelStore.Read(fixture, fixtureId)!;

// A project with a model whose results go to the archive (limit of the document 1 byte), saved and opened again.
string folder = Path.Combine(output, "file"); if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder);
var document = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(document, "Ponte");
var archive = new ModelResultArchive(null, document);
string id = ProjectModelStore.Set(project, ModelDocumentEntry.Prepare(model, 1), null, archive);
string path = Path.Combine(folder, "progetto.programma");
var saved = ProjectFile.Save(path, document, archive);
Check(saved.MissingBlocks == 0 && saved.Warning is null && File.Exists(path) && File.Exists(Path.ChangeExtension(path, ModelResultArchive.Extension)),
    "project file: the archive of the results and the document written together, nothing missing");
var opened = ProjectFile.Open(path);
var read = ProjectModelStore.Read(opened.Document["progetti"]!.AsArray().OfType<JsonObject>().Single(), id, opened.Results)!;
Check(read.Fingerprint() == model.Fingerprint() && read.UnavailableResults.Count == 0, "project file: opened again, the model with every result set from the archive");

// The document copied without its archive: opened, the results are not available; saved, the missing result sets are reported.
string copy = Path.Combine(folder, "copia.programma"); File.Copy(path, copy);
var alone = ProjectFile.Open(copy);
var report = ProjectFile.Save(copy, alone.Document, alone.Results);
var entry = ProjectModelStore.Models(alone.Document["progetti"]!.AsArray().OfType<JsonObject>().Single()).Single();
int cited = entry["risultati"]!.AsArray().Select(r => r!["blocco"]!.GetValue<string>()).Append(entry["copia"]!.GetValue<string>()).Distinct().Count();
Check(report.MissingBlocks == cited && report.Warning?.Contains(cited + " insiemi di risultati", StringComparison.Ordinal) == true,
    $"project file: a document without its archive is saved with the warning of the {report.MissingBlocks} result sets that are missing");

File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} checks");
Console.WriteLine($"PASS {checks} checks");
return 0;
