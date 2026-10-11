using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static void RepresentationChecks(string output)
    {
        var model = Sample.Model(); string fingerprint = model.Fingerprint();
        using var vm = new ModelViewerViewModel(model, [], false, new ViewServices(Path.Combine(output, "representation-views")), _ => { }, (_, _, _, _) => { });
        vm.SelectedCase = "SLU 01"; vm.SelectedComponent = "Myy"; vm.SelectedContourValues = 1;
        vm.ShowMesh = false; vm.SelectedColorScale = 3; vm.ColorScaleLower = "-250"; vm.ColorScaleUpper = "50"; vm.ApplyColorScaleCommand.Execute(null);
        var values = vm.ActiveValues.ToArray(); var contour = vm.ContourValues.ToArray();
        vm.SetRepresentationCommand.Execute("1");
        Check(vm.IsExtruded && vm.ShowResults && vm.SelectedPanel == 2 && !vm.ShowMesh, "extrusion preserves the results layer, mesh preference and View tab");
        Check(vm.ActiveValues.SequenceEqual(values) && vm.ContourValues.SequenceEqual(contour) && vm.ColorMinimum == -250 && vm.ColorMaximum == 50,
            "changing representation preserves raw/averaged values and manual legend bounds");
        vm.ShowResults = false;
        Check(vm.IsExtruded && !vm.IsResultsView && vm.SelectedCase == "SLU 01", "hiding results preserves solid geometry and selected case");
        vm.SelectedPanel = 1;
        Check(vm.ShowResults && vm.IsExtruded, "opening Results does not flatten an extruded model");
        vm.CompareEnabled = true;
        Check(vm.Comparison is { IsExtruded: true, ShowResults: true }, "comparison inherits both independent display choices");
        vm.CompareEnabled = false;
        vm.ShowLineContour=false;vm.ShowBeamDiagram=true;vm.ShowBeamDiagramValues=true;vm.SelectedBeamDiagramAxis=1;
        var preset = JsonSerializer.Deserialize<ModelViewPreset>(JsonSerializer.Serialize(vm.CaptureView("Estrusa")))!;
        ModelViewPresetStore.Validate([preset]); vm.SelectedRepresentation = 0; vm.ShowResults = false; vm.ApplyView(preset);
        Check(vm.IsExtruded && vm.ShowResults && preset.Representation == 1 && preset.Mode == 2, "saved view round trip retains results on volumes");
        Check(!vm.ShowLineContour&&vm.ShowBeamDiagram&&vm.ShowBeamDiagramValues&&vm.SelectedBeamDiagramAxis==1,"view and report recipe retain independent line contour, diagram, values and plane");
        vm.AddReportFigureCommand.Execute(null);
        Check(vm.CaptureReport().Blocks.Single().View is { Mode: 2, Representation: 1 }, "report figure retains extrusion independently of the result case");
        vm.ApplyView(new() { Name = "Old solid", Mode = 1, Case = "SLU 01" });
        Check(vm.IsExtruded && !vm.ShowResults, "old solid presets keep their meaning without a representation field");
        vm.ApplyView(new() { Name = "Old results", Mode = 2, Case = "SLU 01" });
        Check(!vm.IsExtruded && vm.ShowResults, "old result presets remain analytical surfaces");
        vm.SavedViews.Add(new() { Name = "Old solid series", Mode = 1, Case = "SLU 01" }); vm.SelectedPanel = 4;
        vm.ReportViews.Single().Selected = true; vm.ReportCases[0].Selected = true; vm.AddReportSeriesCommand.Execute(null);
        Check(vm.CaptureReport().Blocks.Last().View is { Mode: 2, Representation: 1 }, "result series from legacy solid views retains the original extrusion");
        Reject(() => ModelViewPresetStore.Validate([preset with { Representation = 2 }]), "invalid saved representation is rejected");
        Check(model.Fingerprint() == fingerprint, "representation and report commands do not change engineering data");
        vm.SelectedFamily=1;vm.ShowContext=false;vm.ShowResults=true;
        Check(vm.VisibilitySummary.StartsWith("1 / 3"),"view and report count only the drawn family when context is hidden");
    }
}
