using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    /// <summary>1.15.0, offscreen: a MIDAS Civil .mct and a Straus7 model imported through the production panel of the viewer. Straus7 runs the
    /// real worker when GPC_STRAUS7_ELEMENTS is set (licensed R3 x64); otherwise a substitute source returns the synthetic GPC model.</summary>
    static void ImporterRenderingChecks(ModelViewerControl control, Viewport3DX viewport, string output)
    {
        // The 3D scene follows the control's own view model: its services are replaced for the test and restored at the end.
        var state = (ModelViewerViewModel)control.DataContext;
        var field = typeof(ModelViewerViewModel).GetField("services", Flags)!; var production = field.GetValue(state);
        static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        static void Await(Task task, int seconds) { for (int i = 0; i < seconds * 20 && !task.IsCompleted; i++) Wait(50); if (!task.IsCompleted) throw new TimeoutException("Operazione non conclusa in " + seconds + " s"); task.GetAwaiter().GetResult(); }
        void Busy(ModelViewerViewModel vm, int seconds) { Wait(60); for (int i = 0; i < seconds * 20 && vm.IsBusy; i++) Wait(50); if (vm.IsBusy) throw new TimeoutException("Importazione non conclusa in " + seconds + " s"); }
        string root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "ANTHEA.sln"))) root = Path.GetDirectoryName(root)!;
        string directory = Path.Combine(output, "importatori"); Directory.CreateDirectory(directory);

        // ---- MIDAS Civil .mct through the file source ----
        // The substitute services fit the production renderer, as WpfModelViewerServices does after an import.
        void Fit() { if (control.GetType().GetField("renderer", Flags)?.GetValue(control) is { } renderer) Call(renderer, "Fit", false); }
        var files = new ImporterUiServices { File = Path.Combine(root, "tests", "fixtures", "model-viewer-midas-text", "coverage.mct"), Fit = Fit };
        {
            var vm = state; field.SetValue(vm, files); SetAppearance("Light"); Wait(120);
            vm.OpenImportOptionsCommand.Execute(null); Wait(100);
            var panel = Children<ModelImportOptionsView>(control).Single();
            Check(Children<TextBlock>(panel).Any(t => t.IsVisible && t.Text.Contains(".mct") && t.Text.Contains(".mgt")) && vm.ImportActionLabel == "Scegli il file e importa…",
                "file source of the production panel names the MIDAS Civil and Gen text models");
            Invoke(Children<Button>(panel).Single(b => b.Command == vm.ImportCommand)); Busy(vm, 60);
            Check(vm.Snapshot is { Elements.Length: 7 } s && s.Source.StartsWith("MIDAS Civil · file .mct", StringComparison.Ordinal) && !vm.HasImportFailure,
                "MIDAS .mct imported into the viewer through the file source");
            if (vm.ClearRevisionCommand.CanExecute(null)) vm.ClearRevisionCommand.Execute(null);
            vm.ShowImportOptions = false; vm.ShowInspector = false; vm.SelectedRepresentation = 1; vm.ShowLocalAxes = true; vm.ShowRestraints = true; vm.ShowLinks = true; vm.ShowReleases = true; Wait(400);
            Capture(control, viewport, Path.Combine(directory, "midas-mct-coverage-volumi.png"));
            vm.ShowLoads = true; vm.SelectedRepresentation = 0; Wait(300);
            Capture(control, viewport, Path.Combine(directory, "midas-mct-coverage-carichi.png"));
            vm.ShowLoads = false; vm.ShowLocalAxes = false;
        }

        // ---- Straus7 ----
        string fixture = Path.Combine(directory, "straus7"); Directory.CreateDirectory(fixture);
        bool native = Environment.GetEnvironmentVariable("GPC_STRAUS7_ELEMENTS") == "versioned" && Strand7ImportSetup.FindInstalledApi().Length > 0;
        string model, results, api; IModelViewerServices services;
        if (native)
        {
            ZipFile.ExtractToDirectory(Path.Combine(root, "tests", "fixtures", "model-viewer-straus7", "native-elements-r31.zip"), fixture, true);
            model = Path.Combine(fixture, "elements.st7"); results = Path.Combine(fixture, "elements.LSA"); api = Strand7ImportSetup.FindInstalledApi();
            string worker = new[] { Path.Combine(AppContext.BaseDirectory, Strand7WorkerProtocol.WorkerFileName),
                Path.Combine(root, "src", "ANTHEA.Strand7Worker", "bin", "Release", "net8.0", Strand7WorkerProtocol.WorkerFileName) }.First(File.Exists);
            services = new ImporterUiServices { Model = model, Results = results, Api = api, Source = new Strand7ModelSource(worker), Fit = Fit };
        }
        else
        {
            model = Path.Combine(fixture, "sostituto.st7"); results = Path.Combine(fixture, "sostituto.lsa"); api = Path.Combine(fixture, "St7api.dll");
            File.WriteAllText(model, "substitute model"); File.WriteAllText(results, "substitute results");
            File.Copy(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"), api, true);
            services = new ImporterUiServices { Model = model, Results = results, Api = api, Fit = Fit };
        }
        {
            var vm = state; field.SetValue(vm, services); SetAppearance("Light"); Wait(120);
            vm.OpenImportOptionsCommand.Execute(null); Wait(100);
            var panel = Children<ModelImportOptionsView>(control).Single();
            Children<ComboBox>(panel).Single(c => AutomationProperties.GetName(c) == "Sorgente del modello").SelectedIndex = 2; Wait(80);
            vm.ChooseStrand7ModelCommand.Execute(null); vm.ChooseStrand7ResultsCommand.Execute(null); vm.ChooseStrand7ApiCommand.Execute(null); Wait(60);
            Check(vm.IsStrand7Import && Children<TextBox>(panel).Any(t => AutomationProperties.GetName(t) == "Modello Straus ST7" && t.IsVisible) &&
                  !Children<TextBlock>(panel).Any(t => t.IsVisible && (t.Text.Contains("predisposizione", StringComparison.OrdinalIgnoreCase) || t.Text.Contains("da attivare"))),
                "Straus7 panel shows the native files without preparation notices");
            Invoke(Children<Button>(panel).Single(b => b.Command == vm.CheckStrand7SetupCommand)); Busy(vm, 180);
            Check(vm.HasStrand7Catalog && vm.ImportCases.Count == 2 && Children<ListBox>(panel).Single().Items.Count == 2 && vm.ImportCommand.CanExecute(null),
                "Straus7 check lists the linear static result cases in the production panel" + (native ? " (real worker)" : " (substitute source)"));
            ((ScrollViewer?)Children<ScrollViewer>(control).FirstOrDefault(v => Children<ModelImportOptionsView>(v).Any()))?.ScrollToTop(); Wait(60);
            Capture(control, viewport, Path.Combine(directory, "straus7-catalogo.png"));
            Invoke(Children<Button>(panel).Single(b => b.Command == vm.ImportCommand)); Busy(vm, 300);
            Check(vm.Snapshot is { } s && s.Elements.Count(e => e.Type == "PLATE") >= 2 && s.Results.Length == 2 && s.SourceCombinations?.Definitions.Length == 1 && !vm.HasImportFailure,
                "Straus7 model imported into the viewer with its cases and the LSA combination definition");
            if (vm.ClearRevisionCommand.CanExecute(null)) vm.ClearRevisionCommand.Execute(null);
            vm.ShowImportOptions = false; vm.ShowInspector = false; vm.ShowResults = false; vm.SelectedRepresentation = 1; vm.ShowRestraints = true; vm.ShowLinks = true; vm.ShowConstraints = true; vm.ShowReleases = true; Wait(400);
            Capture(control, viewport, Path.Combine(directory, "straus7-modello-volumi.png"));
            vm.SelectedPanel = 1; vm.ShowResults = true; vm.SelectedCase = vm.Cases.First(); vm.SelectedComponent = "Mxx"; vm.SelectedRepresentation = 0; Wait(400);
            Check(vm.HasResults && vm.ResultUnit.Contains("kN·m/m"), "Straus7 plate moments contoured in kN·m/m");
            Capture(control, viewport, Path.Combine(directory, "straus7-risultati-mxx.png"));
            var combination = vm.Snapshot!.SourceCombinations!.Definitions[0].Id;
            var task = new GpcModelCombinationService().CalculateAsync(vm.Snapshot, combination); Await(task, 120);
            Check(task.Result.Results.Single().Values.Length > 0, "the Straus7 LSA combination is rebuilt by GPC from the imported cases");
            // 1.16.0: support reactions of the current case in the table of the viewer, with the GPC equilibrium check of the real worker.
            vm.SelectedCase = vm.Cases.First(); vm.ShowTable = true; vm.TableOnlyVisible = false; vm.SelectedTableKind = 7; Wait(400);
            Check(vm.TableRows is { Count: > 0 } rows && Children<DataGrid>(control).Any(g => g.IsVisible && g.Items.Count == rows.Count) &&
                  (!native || vm.TableSummary.Contains("in equilibrio")), "Straus7 reactions of the current case in the table of the viewer" + (native ? " with the GPC equilibrium check" : ""));
            Capture(control, viewport, Path.Combine(directory, "straus7-reazioni.png"));
            vm.ShowTable = false; vm.SelectedTableKind = 1; Wait(100);
            vm.SelectedImportSource = 0;
        }
        field.SetValue(state, production); SetAppearance("Light");
    }

    sealed class ImporterUiServices : IModelViewerServices
    {
        public string File = "", Model = "", Results = "", Api = "";
        public IStrand7ModelSource? Source; public Action? Fit;
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.Run(() => (ModelSnapshot?)ModelSnapshotFiles.Read(File, token), token);
        public string? ChooseStrand7File(Strand7ImportFile kind) => kind switch { Strand7ImportFile.Model => Model, Strand7ImportFile.Results => Results, _ => Api };
        public Task<Strand7ModelCatalog> InspectStrand7Async(Strand7ImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) =>
            Source?.InspectAsync(request, progress, token) ?? Task.FromResult(new Strand7ModelCatalog(12, 6, [new(1, "1: Dead"), new(2, "2: Live")], "SYNTHETICGEOMETRY/SYNTHETICRESULTS")
                { Beams = 3, Plates = 3, Combinations = 1, LoadCases = ["1: Dead", "2: Live"] });
        public Task<ModelSnapshot> ImportStrand7Async(Strand7ImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) =>
            Source?.ReadAsync(request, progress, token) ?? Task.Run(() => Strand7Sample.Snapshot(), token);
        public void FitView() => Fit?.Invoke();
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }
}
