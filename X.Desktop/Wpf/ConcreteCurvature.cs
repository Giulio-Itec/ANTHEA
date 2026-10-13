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
        curvatureForm=new InputForm(o,[new("N","N costante (− compressione)","kN"),new("theta","Direzione del momento θ","°"),new("passi","Passi di carico"),new("frazione","Frazione di MRd finale","−"),new("campionamento","Distribuzione dei passi",Choices:["Quadratico","Uniforme"]),new("trazione_cls","CLS resistente a trazione",Choices:["No","Sì"]),new("angoli","Direzioni del dominio"),new("tolleranza_n","Residuo N massimo al limite","kN"),new("raffina_snervamento","Bisezioni primo snervamento (0–30)")],_=>{InvalidateCurvature();Modified?.Invoke();},true,true);
        var buttons=Ui.Bar(Ui.Button("Calcola curva",()=>_ = CalculateCurvature(),inspection:true),Ui.Button("Interrompi",()=>curvatureScheduler.Invalidate(),inspection:true),Ui.Button("Esporta CSV…",ExportCurvature,inspection:true));
        return WorkspaceLayout(Panel("Percorso di carico",Scroller(Ui.Stack(curvatureForm,buttons,Ui.Text("N fisso; (Mx, My) = M · (cos θ, sin θ), negli assi locali. Analisi non lineare con i materiali del pannello di controllo. Campionamento a momento crescente fino al limite plastico; χ è il modulo del gradiente di deformazione. Il primo snervamento può essere raffinato per bisezione tra due campioni successivi.",12)))),Rows(new ViewportFrame("M [kNm] · χ [1/m]",curvaturePlot,curvaturePlot.ResetView),curvatureText));
    }
    private void InvalidateCurvature()
    {
        curvatureScheduler.Invalidate();curvatureResult=null;curvatureSignature=null;exportResult=null;curvaturePlot.Series=[];curvaturePlot.InvalidateVisual();curvatureText.Text="Dati modificati · ricalcolare la curva.";
    }
    private async Task CalculateCurvature()
    {
        if(curvatureRunning||disposed)return;Commit();
        var input=(JsonObject)Input.DeepClone();var workspace=(JsonObject)settings.DeepClone();var o=workspace["momento_curvatura"]!.AsObject();
        string signature=input.ToJsonString()+workspace["trefoli"]?.ToJsonString()+workspace["coefficienti"]?.ToJsonString()+o.ToJsonString();
        if(curvatureSignature==signature&&curvatureResult is not null)return;
        curvatureRunning=true;
        curvatureText.Text="Calcolo del dominio e dei punti M–χ…";
        try
        {
            var completion = await curvatureScheduler.ScheduleAsync(token =>
                Task.Run(() => Anthea.Application.Modules.StructuralQueries.ConcreteCurvature(input, workspace, o, token), token));
            if (disposed) return;
            if (completion.Cancelled) { curvatureText.Text="Calcolo interrotto; nessuna curva parziale pubblicata."; return; }
            completion.TryPublish(current =>
            {
                if (current.Error is not null) { curvatureText.Text="Curva non disponibile: "+current.Error.Message; return; }
                var result = current.Value!;
                curvatureResult=result;curvatureSignature=signature;
                PlotPresentation.ApplyCurvature(curvaturePlot, result);
                var b=new StringBuilder(result.Status);b.AppendLine($"\n\nMRd = {result.LimitMoment:0.###} kNm\nχy = {result.YieldCurvature?.ToString("G6")??"non raggiunta"} 1/m\nχu = {result.UltimateCurvature?.ToString("G6")??"non raggiunta"} 1/m");
                if(result.YieldCurvature is >0&&result.UltimateCurvature is double u)b.AppendLine($"μχ = {u/result.YieldCurvature:0.###}");
                b.AppendLine("\nM [kNm]   χ [1/m]   ε0 [‰]   εc,comp [‰]   |εs|max [‰]");
                foreach(var p in result.Points)b.AppendLine($"{p.Moment:G7}   {p.Curvature:G7}   {p.Epsilon0:G6}   {p.ConcreteCompressionStrain:G6}   {p.SteelStrain:G6}{(p.Limit?" · limite":"")}");
                curvatureText.Text=b.ToString();exportResult=null;
            });
        }
        finally{curvatureRunning=false;}
    }
    private void ExportCurvature()
    {
        if(curvatureResult is null)return;
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="CSV|*.csv",FileName="ANTHEA_Momento_Curvatura.csv"};
        if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
        string F(double v)=>v.ToString("G17",CultureInfo.InvariantCulture);
        var b=new StringBuilder("M_kNm;Mx_kNm;My_kNm;chi_1_m;gx_1_m;gy_1_m;epsilon0_permille;epsilon_c_permille;epsilon_s_permille;limite\n");
        foreach(var p in curvatureResult.Points)b.AppendLine(string.Join(";",new[]{p.Moment,p.Mx,p.My,p.Curvature,p.GradientX,p.GradientY,p.Epsilon0,p.ConcreteCompressionStrain,p.SteelStrain}.Select(F))+";"+p.Limit);
        try{Archivio.ScriviAtomico(dialog.FileName,Encoding.UTF8.GetBytes(b.ToString()));}catch(Exception ex){MessageBox.Show(ex.Message,"Esportazione curva");}
    }
}

