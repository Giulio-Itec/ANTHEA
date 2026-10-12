using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static async Task Strand7ImportSetupChecks(string output)
    {
        string directory = Path.Combine(output, "straus-preparation"); Directory.CreateDirectory(directory);
        string model = Path.Combine(directory, "fixture.st7"), results = Path.Combine(directory, "fixture.lsa"), api = Path.Combine(directory, "St7api.dll");
        // These files deliberately are not models or native code: preparation must not try to interpret or execute them.
        File.WriteAllText(model, "fixture - path preparation only"); File.WriteAllText(results, "fixture - result path only"); File.WriteAllText(api, "not a native DLL");
        var request = new Strand7ImportRequest(model, null, api, ModelUpdateScope.Geometry, [], true, true);
        var geometry = Strand7ImportSetup.Check(request);
        Check(!geometry.FilesReady && geometry.Issues.All(i => !i.StartsWith("Risultati")) && geometry.Issues.Any(i => i.Contains("libreria Windows")), "Straus geometry preparation requires no results and rejects a non-native API without loading it");
        var all = Strand7ImportSetup.Check(request with { Scope = ModelUpdateScope.Everything });
        Check(all.Issues.Any(i => i.StartsWith("Risultati")), "Straus full acquisition requires a linear-static result file");
        var wrong = Strand7ImportSetup.Check(request with { ModelPath = results, ResultsPath = model, Scope = ModelUpdateScope.Results });
        Check(wrong.Issues.Any(i => i.StartsWith("Modello")) && wrong.Issues.Any(i => i.StartsWith("Risultati")), "Straus preparation distinguishes native model and result files");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try { Strand7ImportSetup.Check(request, cancellation.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "Straus file inspection observes cancellation"); }
        }
        string installed = Strand7ImportSetup.FindInstalledApi();
        if (installed.Length > 0)
        {
            var ready = Strand7ImportSetup.Check(request with { ApiLibraryPath = installed, ResultsPath = results, Scope = ModelUpdateScope.Everything });
            Check(ready.FilesReady && ready.Summary.Contains("non sono ancora stati letti"), "installed x64 API can be prepared without claiming native import or licence validation");
        }
        var services = new Strand7SetupServices(model, results, api); int committed = 0;
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => committed++, (_, _, _, _) => { });
        vm.SelectedImportSource = 2;
        Check(vm.IsStrand7Import && !vm.IsFileImport && !vm.IsMidasImport && !vm.ImportCommand.CanExecute(null), "Straus source cannot fall through to package or MIDAS import");
        vm.ChooseStrand7ModelCommand.Execute(null); vm.ChooseStrand7ResultsCommand.Execute(null); vm.ChooseStrand7ApiCommand.Execute(null);
        Check(vm.Strand7ModelPath == model && vm.Strand7ResultsPath == results && vm.Strand7ApiPath == api, "Straus file selection uses the view service while the setup stays in MVVM");
        await vm.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(!vm.IsBusy && committed == 0 && vm.Strand7PreparationSummary.Contains("libreria Windows") && !vm.ImportCommand.CanExecute(null), "preflight reports native prerequisites without publishing a fake model");
        vm.SelectedImportSource = 0; Check(vm.ImportCommand.CanExecute(null), "switching back restores existing file import");
        using var readOnly = new ModelViewerViewModel(Sample.Model(), [], true, services, _ => committed++, (_, _, _, _) => { });
        readOnly.SelectedImportSource = 2;
        Check(!readOnly.CheckStrand7SetupCommand.CanExecute(null) && readOnly.CloseImportOptionsCommand.CanExecute(null), "read-only viewers can close import settings but cannot prepare an import");
    }
    sealed class Strand7SetupServices(string model, string results, string api) : IModelViewerServices
    {
        public string? ChooseStrand7File(Strand7ImportFile kind) => kind switch { Strand7ImportFile.Model => model, Strand7ImportFile.Results => results, _ => api };
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.FromResult<ModelSnapshot?>(null);
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }
}
