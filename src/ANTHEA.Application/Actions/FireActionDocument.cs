using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Model.Actions.Fire;

namespace Anthea.Application.Actions;

public sealed record FireFaceDrawing(string Name,string Exposure,double X0,double Y0,double X1,double Y1,double Alpha,double? Flux);
public sealed record FireDrawing(string Name,IReadOnlyList<FireFaceDrawing> Faces);
public static partial class ClimateDocument
{
    public const string Fire="act_incendio";
    private static readonly IReadOnlyDictionary<string,string> FireCaptions=new Dictionary<string,string>
    {
        ["Ntc2018"]="NTC 2018 · curve nominali e scambio EN",["EurocodeFirstGeneration"]="EN 1991-1-2:2002 · prima generazione",
        ["Standard"]="Nominale standard",["External"]="Nominale esterna",["Hydrocarbon"]="Nominale idrocarburi",["AssignedHistory"]="Storia assegnata",
        ["Exposed"]="Esposta al fuoco",["Unexposed"]="Non esposta · ambiente",["Adiabatic"]="Adiabatica",["ConstantReference"]="Temperatura costante di riferimento"
    };
    private static JsonObject CalculateFire(FireActionsInput input)
    {
        var calculation=GPC.Design.Actions.Fire.FireActions.Calculate(input);
        var result=JsonSerializer.SerializeToNode(calculation.Summary,Options)!.AsObject();result["Fire"]=JsonSerializer.SerializeToNode(calculation,Options);result["FireInput"]=JsonSerializer.SerializeToNode(input,Options);return result;
    }
    private static string? FireLabel(string key)=>key switch
    {
        "Curve"=>"Curva dei gas","DurationMinutes"=>"Durata dello scenario|min","TimeStepMinutes"=>"Passo massimo di campionamento|min","LengthM"=>"Lunghezza dell’elemento estruso|m","AmbientC"=>"Temperatura ambiente non esposto|°C","FireEmissivity"=>"Emissività del fuoco εf",
        "FireScenarioSource"=>"Scenario, durata e provenienza della curva","BoundaryParametersSource"=>"Fonte di emissività, scambio e configurazione (norma del materiale)","CalculateBoundaryFlux"=>"Calcola flussi su temperature superficiali prescritte","PrescribedSurfaceStateConfirmed"=>"Confermo: θm è prescritta, il foglio non calcola la temperatura della sezione","RadiationEqualsGasConfirmed"=>"Confermo applicabile l’ipotesi θr = θg per le facce esposte",
        "TimeMinutes"=>"Tempo dall’inizio|min","TemperatureC"=>"Temperatura assegnata|°C","Face"=>"Indice della faccia (da 1)","Exposure"=>"Esposizione della faccia","StartXM"=>"Inizio segmento x|m","StartYM"=>"Inizio segmento y|m","EndXM"=>"Fine segmento x|m","EndYM"=>"Fine segmento y|m",
        "MemberEmissivity"=>"Emissività della superficie εm","ConfigurationFactor"=>"Fattore di configurazione Φ","AssignedConvection"=>"Assegna coefficiente convettivo","ConvectionWm2K"=>"Coefficiente convettivo assegnato αc|W/(m² K)","CombinedUnexposedTransfer"=>"Non esposto: α=9 include anche l’irraggiamento","SurfaceState"=>"Temperatura superficiale prescritta","ReferenceSurfaceC"=>"Temperatura superficiale costante di riferimento|°C","SurfaceSource"=>"Fonte dell’analisi termica / significato del riferimento θm",_=>null
    };
    private static bool FireVisible(string key,JsonObject v)=>key switch
    {
        "GasTemperatures"=>v["Curve"]?.ToString()=="AssignedHistory","SurfaceTemperatures" or "PrescribedSurfaceStateConfirmed" or "RadiationEqualsGasConfirmed"=>v["CalculateBoundaryFlux"]?.GetValue<bool>()==true,_=>true
    };
    private static bool FireRowVisible(string table,string key,JsonObject? context,JsonObject row)
    {
        if(table!="FireFaces")return true;bool flux=context?["CalculateBoundaryFlux"]?.GetValue<bool>()==true;bool active=row["Exposure"]?.ToString()!="Adiabatic";
        return key switch
        {
            "MemberEmissivity" or "ConfigurationFactor" or "AssignedConvection"=>active,"ConvectionWm2K"=>active&&row["AssignedConvection"]?.GetValue<bool>()==true,"CombinedUnexposedTransfer"=>row["Exposure"]?.ToString()=="Unexposed",
            "SurfaceState" or "SurfaceSource"=>active&&flux,"ReferenceSurfaceC"=>active&&flux&&row["SurfaceState"]?.ToString()=="ConstantReference",_=>true
        };
    }
    public static IReadOnlyList<ActionPlotDrawing> FirePlots(JsonObject? result)
    {
        if(result?["Fire"] is not JsonObject fire)return [];var plots=new List<ActionPlotDrawing>();
        ActionPlotSeries Series(string name,JsonArray points,string key)=>new(name,points.Where(p=>p![key] is not null).Select(p=>new ActionPlotPoint(p!["TimeMinutes"]!.GetValue<double>(),p[key]!.GetValue<double>())).ToArray());
        plots.Add(new("Temperatura dei gas","t [min]","θg [°C]","La curva dei gas non è la temperatura della sezione",[Series("Gas",fire["GasHistory"]!.AsArray(),"TemperatureC")]));
        if(result["FireInput"]?["CalculateBoundaryFlux"]?.GetValue<bool>()==true)
            foreach(var field in new[]{("SurfaceC","Temperature superficiali prescritte","θm [°C]"),("ConvectionWm2","Convezione alle facce","hc [W/m²]"),("RadiationWm2","Irraggiamento alle facce","hr [W/m²]"),("NetWm2","Flusso netto alle facce","hnet [W/m²]")})
                plots.Add(new(field.Item2,"t [min]",field.Item3,"Flusso positivo entrante; θm assegnata o da analisi termica esterna",fire["Faces"]!.AsArray().Select(f=>Series(f!["Name"]!.ToString(),f["History"]!.AsArray(),field.Item1)).Where(s=>s.Points.Count>0).ToArray()));
        return plots;
    }
    public static FireDrawing? FireDiagram(JsonObject? result,int selected)
    {
        if(result?["Fire"] is not JsonObject fire||result["Cases"] is not JsonArray cases||selected<0||selected>=cases.Count)return null;
        double time=cases[selected]!["Values"]![0]!["Value"]!.GetValue<double>();var source=result["FireInput"]!["FireFaces"]!.AsArray();
        double ReadRequiredDouble(JsonNode n,string key)=>n[key]!.GetValue<double>();
        return new(cases[selected]!["Name"]!.ToString(),fire["Faces"]!.AsArray().Select((f,n)=>new FireFaceDrawing(f!["Name"]!.ToString(),FireCaptions[f["Exposure"]!.ToString()],ReadRequiredDouble(source[n]!,"StartXM"),ReadRequiredDouble(source[n]!,"StartYM"),ReadRequiredDouble(source[n]!,"EndXM"),ReadRequiredDouble(source[n]!,"EndYM"),ReadRequiredDouble(f,"ConvectionWm2K"),f["History"]!.AsArray().Single(p=>ReadRequiredDouble(p!,"TimeMinutes")==time)!["NetWm2"]?.GetValue<double>())).ToArray());
    }
}
