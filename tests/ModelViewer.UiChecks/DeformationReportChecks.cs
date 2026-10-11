using System.IO;
using System.Text.Json;
using ANTHEA.ModelReports;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void DeformationReportChecks(ModelViewerControl control, Viewport3DX scene, string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;
        string original=state.Snapshot!.Fingerprint();
        state.ShowInspector=false;state.SelectedResultType=1;state.SelectedCase="SismaX";
        state.SelectedComponent="|u|";state.ShowDeformation=true;state.SelectedRepresentation=1;
        state.ShowMesh=false;state.CameraPresetCommand.Execute("iso-ne");Wait(400);state.FitCommand.Execute(null);Wait(800);
        state.ReportTitle="Spalla · spostamenti e deformata";
        state.AddReportTextCommand.Execute(null);state.SelectedReportChapter!.Title="Dati e rappresentazione";
        state.SelectedReportChapter.Text="I risultati sono le traslazioni nodali globali importate da MIDAS. La scala modifica soltanto la forma rappresentata. Il modello estruso conserva spessori e offset; non si ricostruiscono curvature interne o rotazioni delle sezioni. Il modulo vettoriale è disponibile per i casi con componenti simultanee, non per gli inviluppi.";
        state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Sisma X · modulo degli spostamenti";
        state.SelectedComponent="uz";state.DisplacementUnit="cm";state.DeformationPhase=.5;
        state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Sisma X · componente verticale";
        state.ShowResults=false;state.DeformationPhase=1;Wait(150);
        state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Sagoma deformata e riferimento originale";
        var document=state.CaptureReport();
        string before=JsonSerializer.Serialize(state.CaptureView("Interattiva"));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var task=WpfModelViewerServices.ExportReportToAsync(document,state.Snapshot,Path.Combine(output,"spalla-deformata.pdf"),null,timeout.Token);
        while(!task.IsCompleted)Wait(25);
        var result=task.GetAwaiter().GetResult();
        var saved=ModelReportFiles.Read(result.RecipePath);
        string html=File.ReadAllText(result.HtmlPath);
        Check(result.Figures==3 && new FileInfo(result.PdfPath).Length>15000,"deformation report produces three real rendered figures and a PDF");
        Check(saved.Blocks[1].View is {ResultType:1,Component:"|u|",DisplacementUnit:"mm",Appearance:{Deformation:true,Undeformed:true}},"recipe preserves nodal field, shape and reference together");
        Check(saved.Blocks[2].View is {Component:"uz",DisplacementUnit:"cm",Appearance:{DeformationPhase:.5}},"recipe preserves a signed component, units and static graphical phase");
        Check(saved.Blocks[3].View is {Mode:1,Appearance:{Deformation:true}},"geometry-only report figure retains its displacement case");
        Check(html.Contains("Deformata nodale") && html.Contains("senza curvature interne") && html.Contains("cm") && html.Contains("fase grafica"),"report describes magnification, phase, units and limits of the imported deformation");
        string after=JsonSerializer.Serialize(state.CaptureView("Interattiva"));
        File.WriteAllText(Path.Combine(output,"before-view.json"),before);
        File.WriteAllText(Path.Combine(output,"after-view.json"),after);
        Check(after==before,"deformation batch export preserves the interactive state");
        Check(state.Snapshot.Fingerprint()==original,"deformation report does not modify geometry or any imported result");
        state.ShowResults=true;state.SelectedComponent="|u|";state.DisplacementUnit="mm";state.SelectedPanel=4;
        Capture(control,scene,Path.Combine(output,"report-deformata.png"));
    }
}
