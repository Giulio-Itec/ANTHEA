using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Plot curvaturePlot = new() { Title="Momento–curvatura",XLabel="Curvatura χ [1/m]",YLabel="Momento M [kNm]",AxisNumberFormat="G4",InvertY=false,EmptyMessage="Impostare N e direzione per calcolare la curva" };
    private readonly TextBox curvatureText = new() {IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private CancellationTokenSource? curvatureCancellation;
    private MomentCurvatureResult? curvatureResult;
    private string? curvatureSignature;
    private bool curvatureRunning;
    private InputForm curvatureForm=null!;
    private UIElement BuildCurvaturePanel()
    {
        if(settings["momento_curvatura"] is not JsonObject)settings["momento_curvatura"]=J.Obj(("N","0"),("theta","0"),("passi","60"),("frazione","1"),("campionamento","Quadratico"),("trazione_cls","No"),("angoli","64"));
        var o=settings["momento_curvatura"]!.AsObject();
        if(!o.ContainsKey("tolleranza_n"))o["tolleranza_n"]="1";
        if(!o.ContainsKey("raffina_snervamento"))o["raffina_snervamento"]="12";
        curvatureForm=new InputForm(o,[new("N","N costante (− compressione)","kN"),new("theta","Direzione del momento θ","°"),new("passi","Passi di carico"),new("frazione","Frazione di MRd finale","−"),new("campionamento","Distribuzione dei passi",Choices:["Quadratico","Uniforme"]),new("trazione_cls","CLS resistente a trazione",Choices:["No","Sì"]),new("angoli","Direzioni del dominio"),new("tolleranza_n","Residuo N massimo al limite","kN"),new("raffina_snervamento","Bisezioni primo snervamento (0–30)")],_=>{InvalidateCurvature();Modified?.Invoke();},true,true);
        var buttons=Ui.Bar(Ui.Button("Calcola curva",()=>CalculateCurvature(),inspection:true),Ui.Button("Interrompi",()=>curvatureCancellation?.Cancel(),inspection:true),Ui.Button("Esporta CSV…",ExportCurvature,inspection:true));
        return WorkspaceLayout(Panel("Percorso di carico",Scroller(Ui.Stack(curvatureForm,buttons,Ui.Text("N fisso; (Mx, My) = M · (cos θ, sin θ), negli assi locali. Analisi non lineare con i materiali del pannello di controllo. Campionamento a momento crescente fino al limite plastico; χ è il modulo del gradiente di deformazione. Il primo snervamento può essere raffinato per bisezione tra due campioni successivi.",12)))),Rows(new ViewportFrame("M [kNm] · χ [1/m]",curvaturePlot,curvaturePlot.ResetView),curvatureText));
    }
    private void InvalidateCurvature()
    {
        curvatureCancellation?.Cancel();curvatureResult=null;curvatureSignature=null;exportResult=null;curvaturePlot.Series=[];curvaturePlot.InvalidateVisual();curvatureText.Text="Dati modificati · ricalcolare la curva.";
    }
    private async void CalculateCurvature()
    {
        if(curvatureRunning||disposed)return;Commit();
        var input=(JsonObject)Input.DeepClone();var workspace=(JsonObject)settings.DeepClone();var o=workspace["momento_curvatura"]!.AsObject();
        string signature=input.ToJsonString()+workspace["trefoli"]?.ToJsonString()+workspace["coefficienti"]?.ToJsonString()+o.ToJsonString();
        if(curvatureSignature==signature&&curvatureResult is not null)return;
        curvatureRunning=true;curvatureCancellation=new();var token=curvatureCancellation.Token;
        curvatureText.Text="Calcolo del dominio e dei punti M–χ…";
        try
        {
            if(workspace.Array("trefoli").Count>0)throw new ArgumentException("M–χ con trefoli: definire prima il criterio di snervamento/predeformazione; disponibile per armatura ordinaria.");
            var request=new MomentCurvatureRequest(SectionWorkspace.Number(o.S("N"),"N"),SectionWorkspace.Number(o.S("theta"),"θ"),SectionWorkspace.Subdivisions(o.S("passi"),"Passi",10,500),SectionWorkspace.Number(o.S("frazione"),"Frazione"),o.S("campionamento")=="Quadratico",SectionWorkspace.Number(o.S("tolleranza_n"),"Tolleranza N"),SectionWorkspace.Subdivisions(o.S("raffina_snervamento"),"Bisezioni",0,30));
            o["criterio"]="N costante";o["assi"]="Locali";o["modello"]="Non lineare";o["strategia"]="Iterativo";
            var result=await Task.Run(()=>{
                var engine=new CheckerSection(input,workspace,o);var domain=engine.Domain3D(token);
                return new MomentCurvatureCalculator().Calculate(request,domain.Check,a=>engine.Stress(a,"CURVA"),engine.Geometry.Fyd/engine.Geometry.Es,token);
            },token);
            token.ThrowIfCancellationRequested();if(disposed)return;
            curvatureResult=result;curvatureSignature=signature;
            curvaturePlot.Series=[new("M–χ",result.Points.Select(p=>new[]{p.Curvature,p.Moment}).ToList(),Ui.Blue)];curvaturePlot.InvalidateVisual();
            var b=new StringBuilder(result.Status);b.AppendLine($"\n\nMRd = {result.LimitMoment:0.###} kNm\nχy = {result.YieldCurvature?.ToString("G6")??"non raggiunta"} 1/m\nχu = {result.UltimateCurvature?.ToString("G6")??"non raggiunta"} 1/m");
            if(result.YieldCurvature is >0&&result.UltimateCurvature is double u)b.AppendLine($"μχ = {u/result.YieldCurvature:0.###}");
            b.AppendLine("\nM [kNm]   χ [1/m]   ε0 [‰]   εc,comp [‰]   |εs|max [‰]");
            foreach(var p in result.Points)b.AppendLine($"{p.Moment:G7}   {p.Curvature:G7}   {p.Epsilon0:G6}   {p.ConcreteCompressionStrain:G6}   {p.SteelStrain:G6}{(p.Limit?" · limite":"")}");
            curvatureText.Text=b.ToString();exportResult=null;
        }
        catch(OperationCanceledException){curvatureText.Text="Calcolo interrotto; nessuna curva parziale pubblicata.";}
        catch(Exception ex){curvatureText.Text="Curva non disponibile: "+ex.Message;}
        finally{curvatureRunning=false;curvatureCancellation?.Dispose();curvatureCancellation=null;}
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

