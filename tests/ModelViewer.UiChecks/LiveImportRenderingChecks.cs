using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void LiveImportRenderingChecks(ModelViewerControl control, Viewport3DX viewport, string output)
    {
        var state = (ModelViewerViewModel)control.DataContext;
        state.OpenImportOptionsCommand.Execute(null); Wait(80);
        var inline = Children<ModelImportOptionsView>(control).Single();
        var sourceChoice = Children<ComboBox>(inline).Single(c => AutomationProperties.GetName(c) == "Sorgente del modello");
        sourceChoice.SelectedIndex = 1; Wait(80);
        var key = Children<PasswordBox>(inline).Single(); key.Password = "chiave-di-collaudo-non-valida";
        Check(state.IsMidasImport && state.MidasApiKey == key.Password && state.ImportActionLabel.Contains("modello aperto"), "production import panel binds source and masked password to MVVM");
        Check(!Children<TextBox>(inline).Any(t => t.Text.Contains("chiave-di-collaudo")) && !state.ImportCommand.CanExecute(null), "the key is masked and import stays disabled until inspection");
        Check(inline.ActualWidth > 430, "import settings have a dedicated readable drawer rather than the narrow model tree");
        var panelPosition=inline.TransformToAncestor(control).Transform(new Point());
        var viewportPosition=viewport.TransformToAncestor(control).Transform(new Point());
        Check(viewportPosition.X+viewport.ActualWidth<=panelPosition.X, "the import drawer never overlaps the native viewport or exported render");
        state.ShowInspector = false; SetAppearance("VeryDark"); Wait(100);
        Capture(control, viewport, Path.Combine(output, "modelli-importazione-midas.png"));
        state.MidasApiKey = ""; key.Clear();

        var services = new ImportUiServices();
        int commits = 0;
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => commits++, (_, _, _, _) => { });
        vm.SelectedImportSource = 1;
        var panel = new ModelImportOptionsView { DataContext = vm, Margin = new Thickness(18) };
        var host = new Window { Title = "Collaudo del pannello di importazione · dati simulati", Width = 540, Height = 950, Content = new ScrollViewer { Content = panel }, ShowActivated = false, ShowInTaskbar = false };
        host.SetResourceReference(Window.BackgroundProperty, "AppearancePaper"); host.SetResourceReference(Window.ForegroundProperty, "AppearanceInk");
        WatchAppearance(host); host.Show(); Wait(60);
        var password = Children<PasswordBox>(panel).Single(); password.Password = "test-only";
        static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        Invoke(Children<Button>(panel).Single(b => b.Command == vm.InspectMidasCommand)); Wait(80);
        Check(vm.ImportCases.Count == 3 && vm.ImportCommand.CanExecute(null) && commits == 0, "connection control populates selectable cases without importing");
        var cases = Children<ListBox>(panel).Single();
        Check(cases.Items.Count == 3, "case list displays every returned case");
        Invoke(Children<Button>(panel).Single(b => b.Command == vm.ClearImportCasesCommand)); Wait(40);
        Check(vm.ImportCases.All(c => !c.IsSelected) && !vm.ImportCommand.CanExecute(null), "clear selection disables result import through the actual button");
        Invoke(Children<Button>(panel).Single(b => b.Command == vm.SelectAllImportCasesCommand)); Wait(40);
        vm.ClearImportCasesCommand.Execute(null); vm.ImportConstructionStages = true; Wait(30);
        var stageToggle = Children<CheckBox>(panel).Single(c => AutomationProperties.GetName(c) == "Importa risultati stage");
        Check(stageToggle.IsChecked == true && vm.HasImportStages && !vm.ImportCommand.CanExecute(null), "stage selection is visible and requires an exact step");
        vm.ImportStages[0].IsSelected = true; vm.ImportStages[0].StepsText = "001(last)"; Wait(30);
        var reference = Children<ComboBox>(panel).Single(c => AutomationProperties.GetName(c) == "Riferimento spostamenti stage");
        reference.SelectedItem = "Current"; Wait(30);
        Check(vm.ImportCommand.CanExecute(null) && vm.ImportDisplacementReference == "Current", "stage controls bind phase, step and displacement reference to MVVM");
        ((ScrollViewer)host.Content).ScrollToEnd(); Wait(40);
        CapturePage(host, Path.Combine(output, "importazione-stage-passi.png"));
        vm.ImportConstructionStages = false; vm.SelectAllImportCasesCommand.Execute(null); ((ScrollViewer)host.Content).ScrollToTop();
        foreach (string mode in new[] { "Light", "Dark", "VeryDark" })
        {
            SetAppearance(mode); Wait(70); CapturePage(host, Path.Combine(output, "importazione-casi-" + mode + ".png"));
            Check(password.Password == "test-only" && panel.ActualWidth > 400, "import controls remain available in " + mode);
        }
        Invoke(Children<Button>(panel).Single(b => b.Command == vm.ImportCommand)); Wait(80);
        Check(vm.IsBusy && !password.IsEnabled && !vm.InspectMidasCommand.CanExecute(null), "active acquisition disables credentials and competing operations");
        Check(Children<ProgressBar>(panel).Single().IsVisible && vm.ImportProgressMessage.Contains("simulato"), "live import progress is visible and bound to source progress");
        Invoke(Children<Button>(panel).Single(b => b.Command == vm.ImportCancelCommand)); Wait(100);
        Check(!vm.IsBusy && vm.Status.Contains("annullata") && commits == 0, "cancel stops acquisition without publishing a partial copy");
        password.Password = "changed-test-key"; Wait(50);
        Check(vm.ImportCases.Count == 0 && !vm.ImportCommand.CanExecute(null), "editing the masked credential invalidates old case selection");
        vm.SelectedImportSource=2;Wait(60);
        Check(Children<TextBox>(panel).Any(t=>AutomationProperties.GetName(t)=="Modello Straus ST7" && t.IsVisible) && !vm.ImportCommand.CanExecute(null), "Straus preparation displays native file settings with import explicitly unavailable");
        CapturePage(host,Path.Combine(output,"importazione-straus-predisposizione.png"));
        host.Close(); state.SelectedImportSource = 0; SetAppearance("Light");
        var staged = Sample.Model(); staged.Version = 2;
        var sourceResult = staged.Results[0];
        staged.Results = new[] { "Getto", "Completamento" }.Select((phase, index) => {
            var context = new ModelStageResult("Summation", phase, "001(last)", null, new string('A', 64));
            return sourceResult with { Name = context.Label, IsEnvelope = false, Combination = null, Stage = context,
                Values = sourceResult.Values.Select(v => v with { Values = v.Values.Select(x => x * (index + 1)).ToArray() }).ToArray(), Displacements = null };
        }).ToArray();
        using (var stagedView = new ModelViewerViewModel(staged, [], false, services, _ => { }, (_, _, _, _) => { }))
        {
            control.DataContext = stagedView; stagedView.SelectedPanel = 1; stagedView.SelectedResultScope = 2;
            SetAppearance("VeryDark"); Wait(130);
            var phaseChoice = Children<ComboBox>(control).Single(c => AutomationProperties.GetName(c) == "Fase costruttiva");
            phaseChoice.SelectedItem = "Completamento"; Wait(80);
            Check(stagedView.FilteredCases.Count == 1 && stagedView.SelectedCase!.Contains("Completamento") && phaseChoice.IsVisible,
                "production results panel filters the render by the selected construction phase");
            Capture(control, viewport, Path.Combine(output, "modelli-stage-risultati.png"));
            control.DataContext = state; Wait(50);
        }
        SetAppearance("Light");
    }

    sealed class ImportUiServices : IModelViewerServices
    {
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.FromResult<ModelSnapshot?>(null);
        public Task<MidasModelCatalog> InspectMidasAsync(MidasConnection connection, IProgress<ModelImportProgress> progress, CancellationToken token) =>
            Task.FromResult(new MidasModelCatalog(4, 2, ["Peso proprio", "Permanenti", "Variabile"], ["BEAM", "PLATE"], "ui-fixture") { Phases = [new(1, "Getto", 0, true, 1), new(2, "Completamento", 28, true, 2)], StageCases = ["Summation"] });
        public async Task<ModelSnapshot> ImportMidasAsync(MidasImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token)
        {
            progress.Report(new("Scaricamento simulato · Peso proprio", 2, 10));
            await Task.Delay(Timeout.Infinite, token); return Sample.Model();
        }
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }
}
