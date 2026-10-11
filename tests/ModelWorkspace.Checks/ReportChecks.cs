using ANTHEA.ModelReports;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static async Task ReportChecks(string output)
    {
        var model = Sample.Model(); string fingerprint = model.Fingerprint();
        using var vm = new ModelViewerViewModel(model, [new("sheet-1", "Sezione A", "Predisposta")], false,
            new ViewServices(Path.Combine(output, "report-views")), _ => { }, (_, _, _, _) => { });
        vm.ShowIsolines = true; vm.SelectedContourPalette = 2; vm.SelectedContourValues = 1;
        vm.ToggleViewSettingsCommand.Execute(null);
        Check(vm.SelectedPanel == 2 && !vm.ShowViewSettings, "view shortcut navigates to the sole settings tab");
        vm.ResetViewSettingsCommand.Execute(null);
        Check(vm.ShowIsolines && vm.SelectedContourPalette == 2 && vm.SelectedContourValues == 1, "camera reset preserves settings owned by Results");
        vm.ShowNodeIds = true; vm.ShowRestraints = true; vm.ShowNodeAxes = true; vm.SelectedSurfaceMode = 2;
        vm.LabelScale = 1.2; vm.IsolineWidth = .8; vm.LegendPrecision = 5; vm.ShowExtrema = true;
        var captured = vm.CaptureView("Prova");
        vm.ShowNodeIds = false; vm.SelectedSurfaceMode = 0; vm.ShowRestraints = false; vm.LegendPrecision = 1;
        vm.ApplyView(captured);
        Check(JsonSerializer.Serialize(vm.CaptureView("Prova").Appearance) == JsonSerializer.Serialize(captured.Appearance), "view restore includes labels, overlays, contour and rendering settings");
        vm.AddReportFigureCommand.Execute(null); vm.AddReportTextCommand.Execute(null);
        vm.SelectedReportChapter!.Title = "Note <script>"; vm.SelectedReportChapter.Text = "Testo dell’autore\nSeconda riga & <valore>.";
        vm.ReportSheet = vm.Sheets[0]; vm.AddReportVerificationCommand.Execute(null);
        vm.MoveReportChapterCommand.Execute("up");
        vm.ReportCases[0].Selected = true; vm.AddReportSeriesCommand.Execute(null);
        Check(vm.ReportChapters.Count == 4 && vm.ReportChapters[1].Source.SheetId == "sheet-1" && vm.ReportChapters[2].Text.Contains("Seconda riga"), "report commands preserve chapter order, author text, and verification reference");
        var document = vm.CaptureReport(); string file = Path.Combine(output, "report.antheareport");
        ModelReportFiles.Write(file, document); var loaded = ModelReportFiles.Read(file);
        Check(JsonSerializer.Serialize(loaded) == JsonSerializer.Serialize(document), "portable report recipe round trips complete graphical state and text");
        ModelReportFiles.ValidateReferences(loaded, model);
        var bad = loaded with { Version = 99 }; Reject(() => ModelReportFiles.Validate(bad), "unsupported report schema is rejected");
        bad = loaded with { Blocks = [loaded.Blocks[0], loaded.Blocks[0]] }; Reject(() => ModelReportFiles.Validate(bad), "duplicate chapter identity is rejected");
        bad = loaded with { Blocks = [loaded.Blocks[0] with { View = captured with { Case = "not available", Mode = 2 } }] };
        Reject(() => ModelReportFiles.ValidateReferences(bad, model), "missing case is not silently replaced");
        bad = loaded with { Blocks = [loaded.Blocks[0] with { View = captured with { Case = "not available", Mode = 0, Appearance = captured.Appearance! with { Deformation = true } } }] };
        Reject(() => ModelReportFiles.ValidateReferences(bad, model), "deformed geometry without contours still requires its exact result case");
        bad = loaded with { Blocks = [loaded.Blocks[0] with { View = captured with { Visible = [9999] } }] };
        Reject(() => ModelReportFiles.ValidateReferences(bad, model), "deleted visible element requires an explicit figure edit");
        var invalidAppearance = captured with { Appearance = captured.Appearance! with { LineWidth = double.NaN } };
        Reject(() => ModelViewPresetStore.Validate([invalidAppearance]), "non-finite report appearance is rejected before rendering");
        vm.ApplyReport(loaded with { SourceRevision = new string('A', 64) });
        Check(vm.ReportRevisionStatus.Contains("cambiata") && vm.ReportChapters[2].Text == loaded.Blocks[2].Text, "new model revision is visible without discarding authored text");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        bool canceled = false;
        try { await ModelReportExporter.ExportAsync(loaded, fingerprint, Path.Combine(output, "cancel.pdf"), (_, _, _) => throw new Exception("Capture should not run"), null, cts.Token); }
        catch (OperationCanceledException) { canceled = true; }
        Check(canceled && !Directory.EnumerateDirectories(output, ".cancel-*.pending").Any(), "cancellation publishes no partial report and removes temporary files");
        bool failed = false;
        try { await ModelReportExporter.ExportAsync(loaded, fingerprint, Path.Combine(output, "failure.pdf"), (_, _, _) => throw new IOException("Capture failed"), null, CancellationToken.None); }
        catch (IOException) { failed = true; }
        Check(failed && !Directory.EnumerateDirectories(output, ".failure-*.pending").Any() && !Directory.EnumerateDirectories(output, "failure-*").Any(), "failed figure leaves no misleading completed export");
        Check(model.Fingerprint() == fingerprint, "report composition and serialization never mutate engineering data");
    }
}
