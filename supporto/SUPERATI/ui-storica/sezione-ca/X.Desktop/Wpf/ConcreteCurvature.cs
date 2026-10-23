using Anthea.Application.Concrete;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Plot curvaturePlot = PlotPresentation.CurvaturePlot();
    private readonly TextBox curvatureText = new() {IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly Anthea.Application.Scheduling.RecalculationScheduler curvatureScheduler = new();
    private ConcreteResults.CurvatureResult? curvatureResult;
    private string? curvatureSignature;
    private bool curvatureRunning;
    private InputForm curvatureForm=null!;
    private UIElement BuildCurvaturePanel()
    {
        var o=settings["momento_curvatura"]!.AsObject();
        curvatureForm=new InputForm(o,[new("N","N costante (− compressione)","kN"),new("theta","Direzione del momento θ","°"),new("passi","Passi di carico"),new("frazione","Frazione di MRd finale","−"),new("campionamento","Distribuzione dei passi",Choices:["Quadratico","Uniforme"]),new("trazione_cls","CLS resistente a trazione",Choices:["No","Sì"]),new("cls_teso_risposte","CLS teso nelle risposte",Choices:ConcreteEditor.ResponseConcreteTensionModes,Default:ConcreteEditor.ResponseConcreteTensionModes[0]),new("angoli","Direzioni del dominio"),new("tolleranza_n","Residuo N massimo al limite","kN"),new("raffina_snervamento","Bisezioni primo snervamento (0–30)")],_=>{InvalidateCurvature();Modified?.Invoke();},true,true);
        var buttons=Ui.Bar(Ui.Button("Calcola curva",()=>_ = CalculateCurvature(),inspection:true),Ui.Button("Interrompi",()=>curvatureScheduler.Invalidate(),inspection:true),Ui.Button("Esporta CSV…",ExportCurvature,inspection:true));
        return WorkspaceLayout(Panel("Percorso di carico",Scroller(Ui.Stack(curvatureForm,buttons,Ui.Text(ConcreteCurvaturePresentation.Notice,12)))),Rows(new ViewportFrame("M [kNm] · χ [1/m]",curvaturePlot,curvaturePlot.ResetView),curvatureText));
    }
    private void InvalidateCurvature()
    {
        curvatureScheduler.Invalidate();curvatureResult=null;curvatureSignature=null;exportResult=null;curvaturePlot.Series=[];curvaturePlot.InvalidateVisual();curvatureText.Text=ConcreteCurvaturePresentation.Stale;
    }
    private async Task CalculateCurvature()
    {
        if(curvatureRunning||disposed)return;Commit();
        var input=(JsonObject)Input.DeepClone();var workspace=(JsonObject)settings.DeepClone();var o=workspace["momento_curvatura"]!.AsObject();
        string signature=ConcreteCurvaturePresentation.Signature(input,workspace);
        if(curvatureSignature==signature&&curvatureResult is not null)return;
        curvatureRunning=true;
        curvatureText.Text=ConcreteCurvaturePresentation.Running;
        try
        {
            var completion = await curvatureScheduler.ScheduleAsync(token =>
                Task.Run(() => Anthea.Application.Modules.StructuralQueries.ConcreteCurvature(input, workspace, o, token), token));
            if (disposed) return;
            if (completion.Cancelled) { curvatureText.Text=ConcreteCurvaturePresentation.Interrupted; return; }
            completion.TryPublish(current =>
            {
                if (current.Error is not null) { curvatureText.Text=ConcreteCurvaturePresentation.Unavailable(current.Error.Message); return; }
                var result = current.Value!;
                curvatureResult=result;curvatureSignature=signature;
                PlotPresentation.ApplyCurvature(curvaturePlot, result);
                curvatureText.Text=ConcreteCurvaturePresentation.Text(result);exportResult=null;
            });
        }
        finally{curvatureRunning=false;}
    }
    private void ExportCurvature()
    {
        if(curvatureResult is null)return;
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="CSV|*.csv",FileName=ConcreteCurvaturePresentation.CsvFileName};
        if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
        try{Archivio.ScriviAtomico(dialog.FileName,Encoding.UTF8.GetBytes(ConcreteCurvaturePresentation.Csv(curvatureResult)));}catch(Exception ex){MessageBox.Show(ex.Message,"Esportazione curva");}
    }
}

