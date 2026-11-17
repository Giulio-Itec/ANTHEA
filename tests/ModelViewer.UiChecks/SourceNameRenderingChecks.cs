using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;

static partial class Program
{
    /// <summary>
    /// 17/11/2026: the names that come from the model or from the source are shown as they are. The CheckBox template of the viewer recognizes
    /// access keys, so a text content loses its first «_» («Q_A» → «QA», and Alt+A toggles the box): seven of the fifteen cases of a Straus7
    /// model with two freedom cases have one. The lists of the import panel (cases, phases, stage cases) and of the report composer (cases,
    /// saved views) have a TextBlock as the content of their CheckBox.
    /// </summary>
    static void SourceNameRenderingChecks()
    {
        // The text drawn by a CheckBox: an AccessText draws «QA» for «Q_A» (its TextBlock has the text without the mark), a TextBlock «Q_A».
        static string Shown(CheckBox box) => string.Concat(Children<TextBlock>(box).Select(t => t.Text));
        static void Invoke(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();

        var services = new SourceNameServices();
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => { }, (_, _, _, _) => { });
        vm.SelectedImportSource = 1;
        var panel = new ModelImportOptionsView { DataContext = vm, Margin = new Thickness(18) };
        var host = new Window { Title = "Collaudo dei nomi della sorgente · dati simulati", Width = 540, Height = 950, Content = new ScrollViewer { Content = panel }, ShowActivated = false, ShowInTaskbar = false };
        host.SetResourceReference(Window.BackgroundProperty, "AppearancePaper"); host.SetResourceReference(Window.ForegroundProperty, "AppearanceInk");
        host.Show(); Wait(60);
        Children<PasswordBox>(panel).Single().Password = "test-only";
        Invoke(Children<Button>(panel).Single(b => b.Command == vm.InspectMidasCommand)); Wait(80);
        vm.ImportConstructionStages = true; Wait(60);
        Check(vm.ImportCases.Select(c => c.Name).SequenceEqual(SourceNameServices.CaseNames) && vm.ImportStages.Count == 1 && vm.ImportStageCases.Count == 1,
            "source names: the panel has the cases, the phase and the stage case of the source");

        // The reason of the template: a CheckBox of the viewer with a text content still takes «_» as the mark of an access key.
        var plain = new CheckBox { Content = "Q_A" };
        ((Panel)panel.Content).Children.Add(plain); Wait(40);
        Check(Shown(plain) == "QA" && Children<AccessText>(plain).Any(), "source names: a text content of the viewer CheckBox loses its «_» (access key), the reason of the TextBlock");
        ((Panel)panel.Content).Children.Remove(plain);

        var cases = Children<CheckBox>(Children<ListBox>(panel).Single()).Where(c => c.DataContext is ImportCaseOption).ToArray();
        Check(cases.Select(Shown).SequenceEqual(SourceNameServices.CaseNames) && cases.All(c => !Children<AccessText>(c).Any()),
            "source names: the case list shows «Q_A» as «Q_A» and «E_L+» as «E_L+», with no access key");
        var phase = Children<CheckBox>(panel).Single(c => c.DataContext is ImportStageOption);
        var stageCase = Children<CheckBox>(panel).Single(c => c.DataContext is ImportCaseOption option && vm.ImportStageCases.Contains(option));
        Check(Shown(phase) == vm.ImportStages[0].Label && Shown(phase).StartsWith("Fase_1 ", StringComparison.Ordinal) && Shown(stageCase) == "Stage_A" &&
              !Children<AccessText>(phase).Any() && !Children<AccessText>(stageCase).Any(),
            "source names: the phases and the stage cases keep their «_»");
        Check(cases.All(c => new CheckBoxAutomationPeer(c).GetName() == ((ImportCaseOption)c.DataContext).Name),
            "source names: the automation name of a case is its name, «_» included");
        host.Close();

        // The report composer: the cases and combinations of the copy («SLE_QP»).
        var named = Sample.Model(); named.Results = [named.Results[0] with { Name = "SLE_QP" }];
        using var report = new ModelViewerViewModel(named, [], false, services, _ => { }, (_, _, _, _) => { });
        report.SelectedPanel = ModelViewerPanels.Report;
        var composer = new ModelReportView { DataContext = report, Margin = new Thickness(18) };
        var reportHost = new Window { Title = "Collaudo dei nomi del report · dati simulati", Width = 540, Height = 950, Content = composer, ShowActivated = false, ShowInTaskbar = false };
        reportHost.SetResourceReference(Window.BackgroundProperty, "AppearancePaper"); reportHost.SetResourceReference(Window.ForegroundProperty, "AppearanceInk");
        reportHost.Show(); Wait(60);
        Children<Expander>(composer).Single(e => e.Header as string == "Serie di viste e combinazioni").IsExpanded = true; Wait(60);
        var reportCase = Children<CheckBox>(composer).Single(c => c.DataContext is ReportChoice<string> { Label: "SLE_QP" });
        Check(Shown(reportCase) == "SLE_QP" && !Children<AccessText>(reportCase).Any(),
            "source names: the report composer shows «SLE_QP» as «SLE_QP»");
        reportHost.Close();
    }

    sealed class SourceNameServices : IModelViewerServices
    {
        public static readonly string[] CaseNames = ["Q_A", "E_L+", "G1"];
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.FromResult<ModelSnapshot?>(null);
        public Task<MidasModelCatalog> InspectMidasAsync(MidasConnection connection, IProgress<ModelImportProgress> progress, CancellationToken token) =>
            Task.FromResult(new MidasModelCatalog(4, 2, CaseNames, ["BEAM", "PLATE"], "ui-fixture") { Phases = [new(1, "Fase_1", 0, true, 1)], StageCases = ["Stage_A"] });
        public Task<ModelSnapshot> ImportMidasAsync(MidasImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) => Task.FromResult(Sample.Model());
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }
}
