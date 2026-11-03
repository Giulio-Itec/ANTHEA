using System.Text.Json.Nodes;
using Anthea.Application.Documents;
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

// The lock of a document (step 2d): taken, held against a second opening, released (Windows deletes the file), a lock left over taken over.
string locked = Path.Combine(folder, "bloccato.programma");
File.WriteAllText(locked, "{}");
var rossi = new DocumentLockOwner("rossi", "PC-07", new DateTimeOffset(2026, 11, 3, 14, 32, 0, TimeSpan.Zero), 4242);
var first = DocumentLock.Acquire(locked, rossi);
string lockFile = DocumentLock.PathFor(locked);
Check(first.Lock is not null && first.Holder is null && first.Lock.Path == lockFile && Path.GetFileName(lockFile) == "~$bloccato.programma"
    && File.Exists(lockFile) && File.GetAttributes(lockFile).HasFlag(FileAttributes.Hidden), "lock: taken, the hidden file «~$name» beside the document");
var second = DocumentLock.Acquire(locked);
Check(second.Lock is null && second.Holder == rossi && !second.Holder.IsSameUserHere && second.Holder.Description.StartsWith("da rossi sul computer PC-07 dalle ", StringComparison.Ordinal),
    "lock: a second opening gets who holds it: " + second.Holder?.Description);
first.Lock!.Dispose();
Check(!File.Exists(lockFile), "lock: released, its file deleted");
var mine = DocumentLock.Acquire(locked);
var again = DocumentLock.Acquire(locked);
Check(mine.Lock is not null && again.Holder is { IsSameUserHere: true } && again.Holder.Description.StartsWith("in un'altra finestra di ANTHEA su questo computer", StringComparison.Ordinal),
    "lock: held by the same user on this computer: " + again.Holder?.Description);
mine.Lock!.Dispose();
File.WriteAllText(lockFile, "non è un blocco");
var takenOver = DocumentLock.Acquire(locked);
Check(takenOver.Lock is not null && DocumentLock.Read(lockFile)?.Process == Environment.ProcessId, "lock: a lock file nobody holds open (power cut, copy) taken over");
File.Delete(locked);
Check(DocumentLock.Acquire(locked).Holder?.Description == DocumentLock.Read(lockFile)?.Description, "lock: held while the document file is deleted");
takenOver.Lock!.Dispose();
var nowhere = DocumentLock.Acquire(Path.Combine(folder, "cartella-assente", "x.programma"));
Check(nowhere.Lock is null && nowhere.Holder is null, "lock: a folder where the lock cannot be written gives no lock and no holder");

// The stamp of a file: equal while the file does not change, different after a write.
string stamped = Path.Combine(folder, "impronta.programma");
File.WriteAllText(stamped, "{\"a\":1}");
var before = FileStamp.Of(stamped);
Check(before is not null && FileStamp.Of(stamped) == before && FileStamp.Of(Path.Combine(folder, "assente.programma")) is null, "stamp: the same file, no stamp for a missing file");
File.WriteAllText(stamped, "{\"a\":2}");
File.SetLastWriteTimeUtc(stamped, before!.Value.WrittenUtc.AddSeconds(2));
Check(FileStamp.Of(stamped) != before, "stamp: a file written again has another stamp");

// The frozen document (steps 2c and 2e): the history freezes the project with its archived model; thawed on another thread it is the same
// document, and saved it gives the same file as the live one.
var history = new ProjectHistory();
history.Reset(document);
var snapshot = history.Snapshot(document);
var thawed = Task.Run(snapshot.Thaw).GetAwaiter().GetResult();
Check(JsonNode.DeepEquals(thawed, document) && !ReferenceEquals(thawed, document), "frozen document: thawed on another thread, equal to the live one");
string live = Path.Combine(folder, "vivo.programma"), frozenFile = Path.Combine(folder, "congelato.programma");
DocumentService.Save(live, document); DocumentService.Save(frozenFile, thawed);
Check(File.ReadAllBytes(live).AsSpan().SequenceEqual(File.ReadAllBytes(frozenFile)), "frozen document: saved, the same bytes as the live document");
ProjectDocuments.AddProject(document, "Viadotto");
Check(!JsonNode.DeepEquals(snapshot.Thaw(), document) && JsonNode.DeepEquals(history.Snapshot(document).Thaw(), document),
    "frozen document: a change after the freeze is not in it, a new snapshot has it");

// The recovery copy (step 2e): a project with a model whose results are not saved yet; the copy holds the document and those results; a
// copy in use is not left over; abandoned (a crash), it is found at the next start and taken up with its results; deleted, it is gone.
string recoveryRoot = Path.Combine(output, "recupero");
if (Directory.Exists(recoveryRoot)) Directory.Delete(recoveryRoot, true);
var store = new RecoveryStore(recoveryRoot);
var unsaved = ProjectDocuments.CreateArchive(); var unsavedProject = ProjectDocuments.AddProject(unsaved, "Non salvato");
var unsavedResults = new ModelResultArchive(null, unsaved);
string unsavedId = ProjectModelStore.Set(unsavedProject, ModelDocumentEntry.Prepare(model, 1), null, unsavedResults);
var unsavedHistory = new ProjectHistory(); unsavedHistory.Reset(unsaved);
var info = new RecoveryInfo(null, "Nuovo progetto", DateTimeOffset.Now, null, true);
var slot = store.Create()!;
slot.Write(unsavedHistory.Snapshot(unsaved), info, unsavedResults);
slot.Write(unsavedHistory.Snapshot(unsaved), info, unsavedResults);
int blocks = ModelResultArchive.References(unsaved).Count;
Check(slot.Writes == 2 && Directory.GetFiles(slot.Folder, "risultati-*").Length == 1 && store.LeftOver().Count == 0,
    $"recovery: written twice, the {blocks} result sets once, a copy in use not left over");
slot.Abandon();
var left = store.LeftOver();
Check(left.Count == 1 && left[0].Info == info && left[0].Folder == slot.Folder, "recovery: abandoned (a crash), found at the next start");
left[0].Dispose();
var again2 = store.LeftOver();
var recovered = again2.Single().ReadDocument();
var recoveredResults = new ModelResultArchive(null, recovered);
var failures = again2.Single().ResultArchives.Select(recoveredResults.AddArchive).OfType<string>().ToArray();
var recoveredModel = ProjectModelStore.Read(recovered["progetti"]!.AsArray().OfType<JsonObject>().Single(), unsavedId, recoveredResults)!;
Check(failures.Length == 0 && JsonNode.DeepEquals(recovered, unsaved) && recoveredModel.Fingerprint() == model.Fingerprint() && recoveredModel.UnavailableResults.Count == 0,
    "recovery: «Più tardi» keeps it; taken up, the document and every result set of its model");
again2.Single().Delete();
Check(!Directory.Exists(slot.Folder) && store.LeftOver().Count == 0, "recovery: deleted after it was taken up");
var discarded = store.Create()!;
discarded.Write(unsavedHistory.Snapshot(unsaved), info, unsavedResults);
discarded.Delete();
discarded.Write(unsavedHistory.Snapshot(unsaved), info, unsavedResults);
Check(!Directory.Exists(discarded.Folder) && store.LeftOver().Count == 0, "recovery: a document saved or closed deletes its copy, a late write writes nothing");

File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} checks");
Console.WriteLine($"PASS {checks} checks");
return 0;
