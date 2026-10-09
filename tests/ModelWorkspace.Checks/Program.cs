using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;
using System.Text.Json.Nodes;
using X.Core;
using Anthea.Calculations;

static class Program
{
    static int checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message) { try { action(); } catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException) { Check(true, message); return; } throw new Exception("Accepted: " + message); }
    static async Task<int> Main(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "supporto/artefatti/model-viewer/checks"); Directory.CreateDirectory(output);
        try
        {
            var model = Sample.Model(); model.Validate();
            var clone = ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
            Check(clone.Fingerprint() == model.Fingerprint(), "snapshot survives round trip, including offsets and per-element result values");
            Check(clone.Results[0].Values.Where(v => v.Node == 2).Select(v => v.Values[1]).SequenceEqual([20.0, -200.0]), "shared node retains the discontinuity between elements");
            clone.Elements[0].Nodes[0] = 999; Reject(clone.Validate, "dangling node rejected");
            Reject(() => ModelSnapshot.FromJson("{\"Nodes\":null}"), "null collections rejected");
            clone = Sample.Model(); clone.Results[0].Values[0].Values[0] = double.NaN; Reject(clone.Validate, "non-finite results rejected");
            clone = Sample.Model(); clone.Results[0].Values[0] = new(12, 1, [1, 2, 3, 4, 5, 6, 7, 8]); Reject(clone.Validate, "plate results cannot be attached to a beam");

            var doc = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(doc, "Progetto prova");
            var phase = ProjectDocuments.AddSection(project, "Fase"); var sub = ProjectDocuments.AddSection(phase, "Sottofase");
            var sheet = ProjectDocuments.AddSheet(sub, "str_palo", "Verifica 1"); var second = ProjectDocuments.AddSheet(sub, "mat_calcestruzzo", "Materiale 1");
            string input = sheet["dati"]!.ToJsonString();
            string id = ProjectModelStore.Set(sub, model), secondId = ProjectModelStore.Set(sub, Sample.Model());
            ProjectModelStore.Link(sub, sheet, id, 10, "SLU 01", "Myy");
            Check(sheet["dati"]!.ToJsonString() == input && second[ProjectModelStore.Binding] == null, "link changes neither engineering data nor other sheets");
            Check(ReferenceEquals(ProjectModelStore.FindOwner(sheet), sub), "sheet resolves its model through the actual hierarchy");
            Reject(() => ProjectModelStore.Link(sub, sheet, id, 12, "SLU 01", "Myy"), "beam without imported results cannot be linked");
            string other = ProjectModelStore.Set(project, Sample.Model()); ProjectModelStore.Link(project, second, other, 10, "SLU 01", "Myy");
            Check(ReferenceEquals(ProjectModelStore.FindOwner(second), project), "subphase sheet can refer to a project-level model");
            ProjectModelStore.Rename(sub, id, "Modello locale A");
            Check(ProjectModelStore.LinkStatus(sub, sheet) == "Riferimento aggiornato", "rename does not invalidate engineering reference");
            var changed = Sample.Model(); changed.Plates[0] = changed.Plates[0] with { Offset = .45 }; ProjectModelStore.Set(sub, changed, id);
            Check(ProjectModelStore.LinkStatus(sub, sheet) == "Riferimento da aggiornare" && sheet["dati"]!.ToJsonString() == input, "replacement marks stale references without applying loads");
            Check(ProjectModelStore.Read(sub, secondId)!.Plates[0].Offset == -.7, "replacing one model leaves the second model unchanged");
            string archive = Path.Combine(output, "progetto.programma"); Archivio.Scrivi(archive, doc); var reopened = Archivio.Leggi(archive);
            var reopenedSub = ProjectRevisions.Find(reopened, sub.S("id"))!;
            Check(ProjectModelStore.Models(reopenedSub).Count() == 2 && ProjectModelStore.Read(reopenedSub, id)!.Fingerprint() == changed.Fingerprint(), "real ANTHEA archive persists multiple models per subphase");
            ProjectRevisions.NewRevision(doc, sub, "Con modello");
            changed.Plates[0] = changed.Plates[0] with { Offset = .9 }; ProjectModelStore.Set(sub, changed, id);
            ProjectRevisions.NewRevision(doc, sub, "Offset modificato");
            Archivio.Scrivi(archive, doc); reopened = Archivio.Leggi(archive);
            reopenedSub = ProjectRevisions.Find(reopened, sub.S("id"))!;
            var history = reopenedSub.Array("revisioni").OfType<JsonObject>().First();
            var historical = ProjectRevisions.Snapshot(reopened, history);
            var historicalSub = ProjectModelStore.Containers(historical).Single(c => c.S("id") == sub.S("id"));
            Check(ProjectModelStore.Read(historicalSub, id)!.Plates[0].Offset == .45, "archived revision preserves its model independently of later imports");
            var copy = ProjectRevisions.Duplicate(sub); ProjectModelStore.ReidentifyCopy(copy);
            Check(!ProjectModelStore.Models(copy).Any(m => m.S("id") == id) && ProjectModelStore.LinkStatus(copy, ProjectModelStore.DescendantSheets(copy).First()) == "Riferimento da aggiornare", "duplicate gets independent identities and preserves stale-state meaning");
            var tampered = (JsonObject)sub.DeepClone(); tampered[ProjectModelStore.Property]![0]!["snapshot"]!["Name"] = "Tampered";
            Reject(() => ProjectModelStore.Read(tampered, id), "stored fingerprint detects altered snapshot");

            var service = new FakeServices { Next = Sample.Model() }; int imports = 0, links = 0;
            using var vm = new ModelViewerViewModel(null, [new("sheet", "Foglio", "Non collegato")], false, service, _ => imports++, (_, _, _, _) => links++);
            int changes = 0; vm.SceneChanged += (_, _) => changes++;
            await vm.ImportCommand.ExecuteAsync(null);
            Check(imports == 1 && vm.SelectedCase == "SLU 01" && !vm.IsBusy && changes == 1, "async import commits once and publishes one coherent scene state");
            Check(vm.Minimum == "-200" && vm.Maximum == "40", "range covers imported extrema without nodal averaging");
            vm.QueryCommand.Execute(null); Check(vm.SelectionInformation.Contains("10") && vm.SelectionInformation.Contains("offset"), "query exposes source properties and values");
            vm.LinkCommand.Execute(null); Check(links == 1, "viewmodel delegates explicit sheet link");
            service.Fail = true; await vm.ImportCommand.ExecuteAsync(null);
            Check(imports == 1 && vm.Snapshot != null && vm.Status.Contains("non completata"), "failed import retains the previously displayed model");
            using var readOnly = new ModelViewerViewModel(Sample.Model(), [new("sheet", "Foglio", "")], true, service, _ => imports++, (_, _, _, _) => links++);
            Check(!readOnly.ImportCommand.CanExecute(null) && !readOnly.LinkCommand.CanExecute(null) && readOnly.QueryCommand.CanExecute(null), "revision preview permits inspection but blocks mutations");
            var delayed = new FakeServices { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            var closing = new ModelViewerViewModel(null, [], false, delayed, _ => imports++, (_, _, _, _) => { });
            Task pending = closing.ImportCommand.ExecuteAsync(null); closing.Dispose(); delayed.Pending.SetResult(Sample.Model()); await pending;
            Check(imports == 1, "closing the view prevents a late import from mutating the document");

            if (args.Length > 1)
            {
                var real = MidasSnapshotImporter.ReadDirectory(args[1]);
                Check(real.Nodes.Length == 3755 && real.Elements.Length == 3616 && real.Results.Single().Values.Length == 13718, "laboratory geometry and result row counts are preserved");
                Check(real.Plates.Single(p => p.Id == 3).Offset == -.7 && real.Plates.Single(p => p.Id == 6).Offset == .45 && Math.Abs(real.Plates.Single(p => p.Id == 7).Offset - .35) < 1e-14, "laboratory offsets -0.70, +0.45, +0.35 m preserved");
                var demo = ProjectDocuments.CreateArchive(); var p = ProjectDocuments.AddProject(demo, "Studio · Modelli e verifiche");
                var f = ProjectDocuments.AddSection(p, "Fase · Progetto strutturale"); var sf = ProjectDocuments.AddSection(f, "Sottofase · Spalla");
                var check = ProjectDocuments.AddSheet(sf, "str_palo", "Verifica parete · riferimento modello"); ProjectDocuments.AddSheet(sf, "mat_calcestruzzo", "Materiale della spalla");
                real.Name = "Spalla · modello del laboratorio"; string key = ProjectModelStore.Set(sf, real);
                ProjectModelStore.Link(sf, check, key, real.Results[0].Values[0].Element, real.Results[0].Name, "Myy");
                ProjectModelStore.Set(sf, Sample.Model());
                Archivio.Scrivi(Path.Combine(output, "demo-modelli.programma"), demo);
                File.WriteAllText(Path.Combine(output, "spalla.antheamodel"), JsonSerializer.Serialize(real));
            }
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); File.WriteAllText(Path.Combine(output, "errore.txt"), ex.ToString()); return 1; }
    }

    sealed class FakeServices : IModelViewerServices
    {
        public ModelSnapshot? Next; public bool Fail; public TaskCompletionSource<ModelSnapshot?>? Pending;
        public Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken) => Pending?.Task ?? (Fail ? Task.FromException<ModelSnapshot?>(new InvalidDataException("Bad source")) : Task.FromResult(Next));
        public void FitView() { }
        public void SaveImage() { }
    }
}
