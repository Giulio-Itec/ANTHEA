using System.IO;
using System.Diagnostics;
using System.Text.Json;
using ANTHEA.ModelReports;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void ReportRenderingChecks(ModelViewerControl control, Viewport3DX scene, string output)
    {
        var state = (ModelViewerViewModel)control.DataContext;
        string original = state.Snapshot!.Fingerprint();
        state.ShowInspector = false; state.SelectedMode = 0; state.SelectedColorMode = 3;
        state.CameraPresetCommand.Execute("iso-ne"); Wait(400); state.FitCommand.Execute(null); Wait(700);
        state.ReportTitle = "Spalla · modello e sollecitazioni";
        state.AddReportTextCommand.Execute(null);
        state.SelectedReportChapter!.Title = "Oggetto del documento";
        state.SelectedReportChapter.Text = "Prova del report dinamico ANTHEA sulla copia importata del modello. Le immagini sono rigenerate da viste salvate; le sollecitazioni restano quelle acquisite dal software di origine.\n\nIl contouring mediato modifica esclusivamente la rappresentazione grafica. Gli estremi di inviluppo per componente non sono simultanei.";
        state.AddReportFigureCommand.Execute(null); state.SelectedReportChapter!.Title = "Geometria e proprietà";
        state.SelectedMode = 2; state.SelectedComponent = "Myy"; state.SelectedContourValues = 1; state.ShowMesh = false;
        state.ShowExtrema = true; state.SelectedContourPalette = 2; Wait(300);
        state.AddReportFigureCommand.Execute(null); state.SelectedReportChapter!.Title = "Momento Myy · valori mediati";
        state.SelectedReportChapter.Text = "Lettura del campo con media grafica ai nodi compatibili. Discontinuità conservate fra proprietà e orientamenti differenti.";
        state.SelectedComponent = "Mxx"; state.SelectedContourValues = 0;
        state.SetRepresentationCommand.Execute("1");
        state.AddReportFigureCommand.Execute(null); state.SelectedReportChapter!.Title = "Momento Mxx · valori per elemento sul modello estruso";
        state.SelectedFamily=1;state.SelectedComponent="My";state.ShowBeamDiagram=true;state.ShowLineContour=true;state.ShowExtrema=false;state.ShowContext=false;
        bool beamFigure=state.HasResults;
        if(beamFigure) {state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Aste · diagrammi e contouring sul modello estruso";}
        state.ReportSheet = state.Sheets.FirstOrDefault(); state.AddReportVerificationCommand.Execute(null);
        state.AddReportTextCommand.Execute(null);state.SelectedReportChapter!.Title="Escluso";state.SelectedReportChapter.Text="DO-NOT-EXPORT <script>";state.SelectedReportChapter.Included=false;
        state.OpenNavigationCommand.Execute("4"); Wait(250);
        Capture(control, scene, Path.Combine(output, "compositore-report.png"));
        var document = state.CaptureReport();
        string before = JsonSerializer.Serialize(state.CaptureView("Stato interattivo"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var watch = Stopwatch.StartNew();
        var task = WpfModelViewerServices.ExportReportToAsync(document, state.Snapshot, Path.Combine(output, "spalla-report.pdf"), null, timeout.Token);
        while (!task.IsCompleted) Wait(25);
        var result = task.GetAwaiter().GetResult(); watch.Stop();
        Check(File.Exists(result.PdfPath) && new FileInfo(result.PdfPath).Length > 15000, "report produces a real PDF with rendered figures");
        int figures=beamFigure?4:3;
        Check(result.Figures == figures && Directory.GetFiles(Path.GetDirectoryName(result.PdfPath)!, "figura-*.png").Length == figures, "report regenerates geometry, plate fields and available beam results");
        Check(JsonSerializer.Serialize(state.CaptureView("Stato interattivo")) == before, "batch rendering preserves active camera, selected field and appearance");
        Check(state.Snapshot.Fingerprint() == original, "report generation leaves original model and forces unchanged");
        var saved = ModelReportFiles.Read(result.RecipePath);
        Check(saved.Blocks[3].View is { Mode: 2, Representation: 1 } && File.ReadAllText(result.HtmlPath).Contains("Campo plate riportato sulle facce estruse"), "report regenerates and describes the extruded plate result figure");
        if(beamFigure) Check(saved.Blocks[4].View is { Mode:2,Representation:1,Appearance:{BeamDiagram:true,LineContour:true}} && File.ReadAllText(result.HtmlPath).Contains("diagramma lungo Z locale"),"report regenerates independent beam contour and diagram settings");
        Check(saved.SourceRevision == original && saved.Blocks[0].Text == document.Blocks[0].Text && saved.Blocks[^2].Kind == ReportBlockKind.Verification, "report bundle preserves provenance, author text and pending verification sections");
        Check(File.ReadAllText(result.HtmlPath).Contains("data:image/png;base64,") && File.ReadAllText(result.HtmlPath).Contains("RISULTATI NON COLLEGATI"), "portable HTML embeds figures and identifies unconnected verification results");
        Check(!File.ReadAllText(result.HtmlPath).Contains("DO-NOT-EXPORT") && saved.Blocks[^1].Text.Contains("<script>"), "excluded chapters remain editable but do not enter the exported report");
        state.SelectedReportChapter=state.ReportChapters[2];state.ShowReportFigureCommand.Execute(null);
        Check(state.SelectedComponent=="Myy" && state.SelectedContourValues==1 && state.SelectedPanel==4,"figure can recall its original field without duplicating editing settings");
        File.WriteAllText(Path.Combine(output,"report-export.json"),JsonSerializer.Serialize(new {Result=result,Milliseconds=watch.Elapsed.TotalMilliseconds},new JsonSerializerOptions{WriteIndented=true}));
        state.SelectedPanel = 2; Wait(150);
        Check(Children<ModelViewSettingsView>(control).Count() == 1, "a single view settings panel is available after consolidation");
        Capture(control,scene,Path.Combine(output,"scheda-vista-unificata.png"));
    }
}
