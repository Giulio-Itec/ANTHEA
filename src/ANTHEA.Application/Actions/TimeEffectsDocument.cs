using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Model.Actions.TimeEffects;

namespace Anthea.Application.Actions;

public sealed record ActionPlotPoint(double X,double Y);
public sealed record ActionPlotSeries(string Name,IReadOnlyList<ActionPlotPoint> Points);
public sealed record ActionPlotDrawing(string Name,string XLabel,string YLabel,string Note,IReadOnlyList<ActionPlotSeries> Series);

public static partial class ClimateDocument
{
    public const string TimeEffects="act_effetti_differiti";
    private static readonly IReadOnlyDictionary<string,string> TimeCaptions=new Dictionary<string,string>
    {
        ["Ntc2018"]="NTC 2018 · ritiro; leggi temporali EC2",["EurocodeFirstGeneration"]="EN 1992-1-1:2004 · prima generazione",
        ["ClassS"]="S · cemento lento",["ClassN"]="N · cemento normale",["ClassR"]="R · cemento rapido",
        ["Free"]="Libero · solo carichi e deformazioni proprie",["FixedDistance"]="Distanza imposta fra le estremità",["EndSpring"]="Molla assiale all’estremità destra"
    };
    private static JsonObject CalculateTimeEffects(TimeEffectsInput input)
    {
        var calculated=GPC.Design.Actions.TimeEffects.ConcreteTimeActions.Calculate(input);
        var result=JsonSerializer.SerializeToNode(calculated.Summary,Options)!.AsObject();
        result["TimeEffects"]=JsonSerializer.SerializeToNode(calculated,Options);return result;
    }
    private static string? TimeLabel(string key)=>key switch
    {
        "FckMpa"=>"Resistenza caratteristica cilindrica a 28 giorni|MPa","Cement"=>"Classe del cemento",
        "RelativeHumidityPercent"=>"Umidità relativa costante|%","AreaMm2"=>"Area di calcestruzzo Ac|mm²","DryingPerimeterMm"=>"Perimetro esposto all’essiccamento u|mm",
        "LengthM"=>"Lunghezza della barra L|m","ReferenceAgeDays"=>"Età della configurazione iniziale priva di tensioni|giorni",
        "DryingStartDays"=>"Inizio dell’essiccamento (fine maturazione umida)|giorni","IncludeShrinkage"=>"Applica il ritiro dalla configurazione iniziale","IncludeCreep"=>"Applica la viscosità",
        "Restraint"=>"Vincolo assiale del modello locale","EndSpringKnMm"=>"Rigidezza della molla K|kN/mm","TimeSteps"=>"Intervalli temporali (verifica con griglia doppia)",
        "AgeDays"=>"Età del calcestruzzo|giorni","IncrementMpa"=>"Incremento di P/A (+ trazione, − compressione)|MPa",
        "DisplacementMm"=>"Spostamento relativo appoggi (+ allontanamento)|mm",_=>null
    };
    private static bool TimeVisible(string key,JsonObject values)=>key switch
    {
        "NationalParameters"=>values["Standard"]?.ToString()=="EurocodeFirstGeneration",
        "EndSpringKnMm"=>values["Restraint"]?.ToString()=="EndSpring",
        "Displacements"=>values["Restraint"]?.ToString()!="Free",_=>true
    };
    public static IReadOnlyList<ActionPlotDrawing> TimePlots(JsonObject? result,int selected)
    {
        if(result?["TimeEffects"] is not JsonObject time||result["Cases"] is not JsonArray cases||selected<0||selected>=cases.Count)return [];
        double end=cases[selected]!["Values"]!.AsArray().First(v=>v!["Symbol"]!.ToString()=="t")!["Value"]!.GetValue<double>();
        var rows=time["History"]!.AsArray().Where(p=>p!["AgeDays"]!.GetValue<double>()<=end).ToArray();
        string note=time["WithinLinearRange"]!.GetValue<bool>()?"Barra integra · deformazioni positive in allungamento":"ATTENZIONE: campo lineare integro superato nella storia";
        if(!time["Converged"]!.GetValue<bool>())note+=" · aumentare gli intervalli temporali";
        ActionPlotSeries Series(string label,string key)=>new(label,rows.Select(p=>new ActionPlotPoint(p!["AgeDays"]!.GetValue<double>(),p[key]!.GetValue<double>())).ToArray());
        ActionPlotDrawing Plot(string name,string unit,params (string Label,string Key)[] fields)=>new(name,"Età [giorni]",unit,note,fields.Select(f=>Series(f.Label,f.Key)).ToArray());
        return [Plot("Spostamenti","u [mm]",("Libero","FreeDisplacementMm"),("Effettivo","ActualDisplacementMm"),("Appoggi","PrescribedDisplacementMm")),
            Plot("Tensioni","σ [MPa]",("Barra","StressMpa"),("Carico esterno P/A","ExternalStressMpa"),("fctm(t)","MeanTensileStrengthMpa")),
            Plot("Deformazioni","ε [−]",("Libera","FreeStrain"),("Effettiva","ActualStrain"),("Ritiro da tref","RelativeShrinkage"),("Viscosa libera","FreeCreepStrain")),
            Plot("Maturazione","Ecm [MPa]",("Modulo secante","EcmMpa"))];
    }
}
