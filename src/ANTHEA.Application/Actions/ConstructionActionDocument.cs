using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Model.Actions.Construction;

namespace Anthea.Application.Actions;

public sealed record ConstructionPhaseDrawing(string Name,double Start,double End);
public sealed record ConstructionPatchDrawing(string Name,double X,double Y,double Length,double Width,double Pressure);
public sealed record ConstructionLoadDrawing(string Name,string Nature,double PointX,double PointY,double PointLoad,double Vertical,double Hx,double Hy,IReadOnlyList<ConstructionPatchDrawing> Patches);
public sealed record ConstructionDrawing(string Name,int Selected,IReadOnlyList<ConstructionPhaseDrawing> Phases,IReadOnlyList<ConstructionLoadDrawing> Loads);

public static partial class ClimateDocument
{
    public const string Construction="act_fasi_costruttive";
    private static readonly IReadOnlyDictionary<string,string> ConstructionCaptions=new Dictionary<string,string>
    {
        ["Assigned"]="Azioni assegnate",["SelfWeight"]="Peso proprio da volume",
        ["EnPersonnel"]="EN · personale e piccoli utensili",["EnBridgeStorage"]="EN · deposito su ponti",["EnConcreteCasting"]="EN · getto del calcestruzzo"
    };
    private static string UiCaption(string module,string name)=>module==Fire?FireCaptions.GetValueOrDefault(name)??Caption(name):module==Cranes?CraneCaptions.GetValueOrDefault(name)??Caption(name):module==Prestressing?PrestressCaptions.GetValueOrDefault(name)??TimeCaptions.GetValueOrDefault(name)??Caption(name):module==TimeEffects?TimeCaptions.GetValueOrDefault(name)??Caption(name):module==Construction?ConstructionCaptions.GetValueOrDefault(name)??Caption(name):Caption(name);
    private static IEnumerable<string> FieldEnumNames(string module,string key,Type type)=>Enum.GetNames(type).Where(name=>module!=Construction||key!="Nature"||name!="Prestress");
    private static string ExtendedModule(JsonObject document)=>document["input_type"]?.ToString() switch
    {"FireActionsInput"=>Fire,"CraneActionsInput"=>Cranes,"PrestressActionsInput"=>Prestressing,"TimeEffectsInput"=>TimeEffects,"HydraulicActionsInput"=>Hydraulics,"ConstructionActionsInput"=>Construction,_=>""};
    private static JsonObject CalculateConstruction(ConstructionActionsInput input)
    {
        var calculation=GPC.Design.Actions.Construction.ConstructionActions.Calculate(input);
        var result=JsonSerializer.SerializeToNode(calculation.Summary,Options)!.AsObject();
        result["ConstructionStages"]=JsonSerializer.SerializeToNode(calculation.Stages,Options);
        return result;
    }
    private static string? ConstructionLabel(string key)=>key switch
    {
        "RecommendedEnLoadsConfirmed"=>"Confermo i carichi raccomandati EN 1991-1-6:2005 applicabili",
        "ExecutionPlan"=>"Piano di esecuzione, ipotesi e sorgenti dei dati","DurationDays"=>"Durata della fase|giorni",
        "Model"=>"Modello del carico","Nature"=>"Natura dell’azione","StartStage"=>"Prima fase attiva (indice da 1)","StopStage"=>"Prima fase esclusa (0 = nessuna)",
        "LeftXM"=>"Origine dell’area x|m","BottomYM"=>"Origine dell’area y|m","LengthM"=>"Lunghezza area lungo x|m","WidthM"=>"Larghezza area lungo y|m",
        "ThicknessM"=>"Spessore del materiale|m","UnitWeightKnM3"=>"Peso specifico del materiale|kN/m³","FormworkKpa"=>"Peso di casseri e sostegni sull’area|kPa",
        "AssignedPressureKpa"=>"Pressione assegnata / minimo per il preset EN|kPa","PointLoadKn"=>"Forza concentrata (+ verso il basso)|kN",
        "PointXM"=>"Punto di applicazione forze · x|m","PointYM"=>"Punto di applicazione forze · y|m",
        "HorizontalXKn"=>"Forza orizzontale Hx|kN","HorizontalYKn"=>"Forza orizzontale Hy|kN","HorizontalHeightM"=>"Quota di applicazione Hx e Hy|m",
        "WorkXOffsetM"=>"Area di lavoro · distanza dal bordo x|m","WorkYOffsetM"=>"Area di lavoro · distanza dal bordo y|m",
        "DynamicFactor"=>"Amplificazione dinamica assegnata (una sola volta)","Source"=>"Fonte dei carichi e dell’amplificazione",_=>null
    };
    private static bool ConstructionRowVisible(string table,string key,JsonObject row)
    {
        if(table!="ConstructionLoads")return true;
        string model=row["Model"]?.ToString()??"";
        return key switch
        {
            "ThicknessM" or "UnitWeightKnM3"=>model is "SelfWeight" or "EnConcreteCasting",
            "FormworkKpa" or "WorkXOffsetM" or "WorkYOffsetM"=>model=="EnConcreteCasting",
            "AssignedPressureKpa"=>model is "Assigned" or "EnPersonnel" or "EnBridgeStorage",
            "PointLoadKn"=>model!="EnConcreteCasting",_=>true
        };
    }
    public static ConstructionDrawing? ConstructionDiagram(JsonObject? result,int selected)
    {
        if(result?["ConstructionStages"] is not JsonArray stages||selected<0||selected>=stages.Count)return null;
        double ReadRequiredDouble(JsonNode n,string name)=>n[name]!.GetValue<double>();
        var phase=stages[selected]!;
        return new(phase["Name"]!.ToString(),selected,
            stages.Select(s=>new ConstructionPhaseDrawing(s!["Name"]!.ToString(),ReadRequiredDouble(s,"StartDay"),ReadRequiredDouble(s,"EndDay"))).ToArray(),
            phase["Loads"]!.AsArray().Select(l=>new ConstructionLoadDrawing(l!["Name"]!.ToString(),Caption(l["Nature"]!.ToString()),ReadRequiredDouble(l,"PointXM"),ReadRequiredDouble(l,"PointYM"),ReadRequiredDouble(l,"PointLoadKn"),ReadRequiredDouble(l,"VerticalDownKn"),ReadRequiredDouble(l,"HorizontalXKn"),ReadRequiredDouble(l,"HorizontalYKn"),l["Patches"]!.AsArray().Select(p=>new ConstructionPatchDrawing(p!["Name"]!.ToString(),ReadRequiredDouble(p,"LeftXM"),ReadRequiredDouble(p,"BottomYM"),ReadRequiredDouble(p,"LengthM"),ReadRequiredDouble(p,"WidthM"),ReadRequiredDouble(p,"PressureKpa"))).ToArray())).ToArray());
    }
}
