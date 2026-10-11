using System.IO;
using System.Windows;
using System.Text.Json;
using ANTHEA.ModelReports;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void MemberDiagramRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;
        string before=state.Snapshot!.Fingerprint();
        state.SelectedCase=state.Cases.Contains("G1")?"G1":state.Cases.First();
        var forces=ResultFields.Read(state.Snapshot.Results.First(r=>r.Name==state.SelectedCase),"BEAM","LOCAL","My");
        int id=forces.GroupBy(v=>v.Element).MaxBy(g=>g.Max(v=>v.Value)-g.Min(v=>v.Value))!.Key;
        state.SelectElement(id);state.OpenMemberDiagramCommand.Execute(null);state.SelectedComponent="My";
        state.ShowInspector=false;state.SelectedRepresentation=1;state.ShowBeamDiagram=true;state.ShowLineContour=true;
        state.ShowContext=true;Wait(350);
        var plots=Children<MemberDiagramPlot>(control).ToArray();
        Check(plots.Length==1 && plots[0].Series!.Component=="My" && plots[0].Series!.Samples.Length==5,"real member detail plot displays all five imported My stations");
        plots[0].SetCurrentValue(MemberDiagramPlot.StationProperty,.25);
        Check(state.MemberDiagramStation==.25 && state.MemberDiagramReadings.Single().Origin=="Importato","plot station binds back to the MVVM cursor and recognizes an imported quarter point");
        SetAppearance("Dark");Wait(120);Capture(control,viewport,Path.Combine(output,"spalla-diagramma-asta.png"));
        state.MemberDiagramAllComponents=true;state.MemberDiagramStation=.375;Wait(150);
        plots=Children<MemberDiagramPlot>(control).ToArray();
        Check(plots.Length==6 && plots.All(p=>p.Station==.375),"six WPF plots share the same cursor without separate result settings");
        Check(state.MemberDiagramReadings.All(r=>r.Origin=="Interpolazione grafica"),"real intermediate readings are identified as graphical interpolation");
        state.MemberDiagramInvert=true;state.MemberDiagramCursorLocked=true;state.MemberDiagramFill=false;Wait(50);
        Check(plots.All(p=>p.Invert && p.CursorLocked && !p.FillDiagram),"invert, outline-only and cursor lock propagate to every plot");
        state.MemberDiagramInvert=false;state.MemberDiagramFill=true;SetAppearance("VeryDark");Wait(100);
        Capture(control,viewport,Path.Combine(output,"spalla-sei-diagrammi-molto-scuro.png"));
        SetAppearance("Light");Wait(100);Capture(control,viewport,Path.Combine(output,"spalla-sei-diagrammi-chiaro.png"));
        state.MemberDiagramElementId=state.Snapshot.Elements.First(e=>e.Type=="BEAM"&&e.Id!=id).Id;Wait(100);
        Check(state.SelectedElementIds.SetEquals([state.MemberDiagramElementId!.Value]) && state.MemberDiagramSeries.Count==6,"changing the plotted member selects the same element in the model");
        state.SelectedCase=state.Cases.Contains("G2")?"G2":state.Cases.First();Wait(100);
        Check(state.MemberDiagramTitle.Contains(state.SelectedCase!),"case changes refresh the detail plot from the shared selector");
        state.SelectedCase=state.Cases.Contains("G1")?"G1":state.Cases.First();state.MemberDiagramElementId=id;
        state.ReportTitle="Spalla · diagrammi delle aste";
        state.MemberDiagramAllComponents=true;state.MemberDiagramStation=.375;
        state.AddReportMemberDiagramCommand.Execute(null);state.SelectedReportChapter!.Title="Sei componenti locali dell’asta";
        state.MemberDiagramAllComponents=false;state.SelectedComponent="My";state.MemberDiagramInvert=true;state.MemberDiagramStation=.25;
        state.AddReportMemberDiagramCommand.Execute(null);state.SelectedReportChapter!.Title="Momento My · lettura alla stazione importata";
        var document=state.CaptureReport();Wait(150);string viewBefore=JsonSerializer.Serialize(state.CaptureView("Interattiva"));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var task=WpfModelViewerServices.ExportReportToAsync(document,state.Snapshot,Path.Combine(output,"diagrammi-asta.pdf"),null,timeout.Token);
        while(!task.IsCompleted)Wait(25);
        var report=task.GetAwaiter().GetResult();var saved=ModelReportFiles.Read(report.RecipePath);
        Check(report.Figures==2 && new FileInfo(report.PdfPath).Length>10000,"member diagram report regenerates both figures and a PDF");
        Check(saved.Blocks[0].MemberDiagram is {AllComponents:true,Station:.375} && saved.Blocks[1].MemberDiagram is {AllComponents:false,Invert:true,Station:.25},"report retains separate component, orientation and cursor settings for each plot");
        Check(File.ReadAllText(report.HtmlPath).Contains("interpolato graficamente") && File.ReadAllText(report.HtmlPath).Contains("importato"),"member report distinguishes imported and interpolated readings");
        Check(JsonSerializer.Serialize(state.CaptureView("Interattiva"))==viewBefore,"member report uses an independent plot host and preserves the live scene");
        state.SelectedReportChapter=state.ReportChapters[0];state.ShowReportFigureCommand.Execute(null);Wait(100);
        Check(state.ShowMemberDiagram && state.MemberDiagramAllComponents && state.MemberDiagramStation==.375,"member report figure recalls the same diagram for editing");
        Check(state.Snapshot.Fingerprint()==before,"plot rendering and theme changes preserve the complete imported model");
    }
}
