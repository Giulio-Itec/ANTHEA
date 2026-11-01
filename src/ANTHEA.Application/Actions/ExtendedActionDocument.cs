using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using GPC.Model.Actions.Hydraulics;

namespace Anthea.Application.Actions;

public sealed record HydraulicPointDrawing(double Position,double Positive,double Opposing,double Net,double LineLoad);
public sealed record HydraulicDrawing(string Element,string Name,bool AssignedUplift,double Bottom,double Top,double Width,double Length,double Thickness,double LeftLevel,double RightLevel,double Force,double Moment,double? Application,IReadOnlyList<HydraulicPointDrawing> Points);

/// <summary>Application projections only. Hydraulic pressures and resultants are calculated by GPC.Design.</summary>
public static partial class ClimateDocument
{
    public const string Hydraulics="act_idrauliche";
    private static IEnumerable<ModuleInfo> ExtendedModules=>new[]
    {
        new ModuleInfo(Hydraulics,"Azioni","Azioni idrauliche","Pareti, platee e pile in corrente · scenari e pressioni nette","Azione","Azioni idrauliche","Acqua e corrente"),
        new ModuleInfo(Construction,"Azioni","Fasi costruttive","Sequenza, carichi temporanei, getto e azioni assegnate","Azione","Fasi costruttive","Esecuzione"),
        new ModuleInfo(TimeEffects,"Azioni","Ritiro e viscosità","Storia dei carichi, maturazione e spostamenti assiali imposti","Azione","Ritiro e viscosità","Effetti differiti"),
        new ModuleInfo(Prestressing,"Azioni","Precompressione e perdite","Profilo, tiro, attrito, ancoraggi e stato efficace nel tempo","Azione","Precompressione e perdite","Precompressione"),
        new ModuleInfo(Cranes,"Azioni","Gru e carriponte","Azioni alle ruote e gruppi compatibili EN 1991-3:2006","Azione","Gru e carriponte","Gru industriali"),
        new ModuleInfo(Fire,"Azioni","Incendio","Curve dei gas, facce esposte e flussi su temperature superficiali prescritte","Azione","Azioni termiche da incendio","Incendio")
    };
    private static bool IsExtended(string module)=>ExtendedModules.Any(m=>m.Id==module);
    private static object ExtendedInput(string module)=>module switch
    {Fire=>new GPC.Model.Actions.Fire.FireActionsInput(),Cranes=>new GPC.Model.Actions.Cranes.CraneActionsInput(),Prestressing=>new GPC.Model.Actions.Prestressing.PrestressActionsInput(),TimeEffects=>new GPC.Model.Actions.TimeEffects.TimeEffectsInput(),Hydraulics=>new HydraulicActionsInput(),Construction=>new GPC.Model.Actions.Construction.ConstructionActionsInput(),_=>throw new ArgumentException("Modulo Azioni sconosciuto.")};
    private static JsonObject CalculateExtended(object input)
    {
        if(input is GPC.Model.Actions.Fire.FireActionsInput fire)return CalculateFire(fire);
        if(input is GPC.Model.Actions.Cranes.CraneActionsInput crane)return CalculateCranes(crane);
        if(input is GPC.Model.Actions.Prestressing.PrestressActionsInput prestress)return CalculatePrestress(prestress);
        if(input is GPC.Model.Actions.TimeEffects.TimeEffectsInput time)return CalculateTimeEffects(time);
        if(input is HydraulicActionsInput hydraulic)
        {
            var calculated=GPC.Design.Actions.Hydraulics.HydraulicActions.Calculate(hydraulic);
            var result=JsonSerializer.SerializeToNode(calculated.Summary,Options)!.AsObject();
            result["Hydraulics"]=JsonSerializer.SerializeToNode(calculated,Options);
            // The diagram uses exactly the geometry of this calculation, never later uncalculated edits.
            result["HydraulicInput"]=JsonSerializer.SerializeToNode(hydraulic,Options);
            return result;
        }
        if(input is GPC.Model.Actions.Construction.ConstructionActionsInput construction)return CalculateConstruction(construction);
        throw new ArgumentException("Input Azioni non riconosciuto.");
    }
    private static readonly IReadOnlyDictionary<string,string> ExtendedCaptions=new Dictionary<string,string>
    {
        ["VerticalWall"]="Parete verticale",["HorizontalSlab"]="Platea orizzontale",["RiverPier"]="Pila in corrente",
        ["HydrostaticHead"]="Battente idrostatico",["AssignedPressureProfile"]="Profilo di sottospinta assegnato"
    };
    private static string? ExtendedLabel(string module,string key,JsonObject? context=null)
    {
        if(module==Fire)return FireLabel(key);
        if(module==Cranes)return CraneLabel(key);
        if(module==Prestressing)return PrestressLabel(key);
        if(module==TimeEffects)return TimeLabel(key);
        if(module==Construction)return ConstructionLabel(key);
        if(module!=Hydraulics)return null;
        string element=context?["Element"]?.ToString()??"";
        return key switch
        {
            "Element"=>"Elemento / modello fisico","UnitWeightKnM3"=>"Peso specifico dell’acqua γw|kN/m³",
            "BottomElevationM"=>"Quota della base z0|m","TopElevationM"=>"Quota della sommità z1|m",
            "WidthM"=>element=="RiverPier"?"Larghezza proiettata normale alla corrente B|m":"Larghezza fuori piano B|m",
            "SlabLengthM"=>"Lunghezza della platea L|m","SlabThicknessM"=>"Spessore della platea t|m",
            "Uplift"=>"Modello della sottospinta","PressureSource"=>"Fonte del profilo di sottospinta",
            "DragCoefficient"=>"Coefficiente di resistenza Cd","DragSource"=>"Fonte di Cd e campo di applicazione",
            "UseVelocityProfile"=>"Profilo di velocità variabile con la quota","ProjectSource"=>"Studio idraulico / fonte degli scenari",
            "LeftLevelM"=>element switch{"HorizontalSlab"=>"Quota piezometrica sotto la platea|m","RiverPier"=>"Quota del pelo libero|m",_=>"Quota acqua a sinistra|m"},
            "RightLevelM"=>element=="HorizontalSlab"?"Quota acqua sopra la platea|m":"Quota acqua a destra|m",
            "CurrentVelocityMs"=>"Velocità uniforme con segno v|m/s","ProfileMultiplier"=>"Moltiplicatore del profilo nello scenario",
            "PositionM"=>"Ascissa lungo la platea x|m","PressureKpa"=>"Sottospinta positiva p|kPa",
            "ElevationM"=>"Quota z|m","VelocityMs"=>"Velocità con segno v|m/s",_=>null
        };
    }
    private static bool ExtendedVisible(string module,string key,JsonObject values)
    {
        if(module==Fire)return FireVisible(key,values);
        if(module==Cranes)return CraneVisible(key,values);
        if(module==Prestressing)return PrestressVisible(key,values);
        if(module==TimeEffects)return TimeVisible(key,values);
        if(module==Construction)return key is "NationalParameters" or "RecommendedEnLoadsConfirmed"?values["Standard"]?.ToString()=="EurocodeFirstGeneration":true;
        if(module!=Hydraulics)return true;
        string element=values["Element"]?.ToString()??"";
        bool slab=element=="HorizontalSlab",pier=element=="RiverPier";
        return key switch
        {
            "TopElevationM"=>!slab,"SlabLengthM" or "SlabThicknessM" or "Uplift"=>slab,
            "PressureSource" or "PressureProfile"=>slab&&values["Uplift"]?.ToString()=="AssignedPressureProfile",
            "DragCoefficient" or "DragSource" or "UseVelocityProfile"=>pier,
            "VelocityProfile"=>pier&&values["UseVelocityProfile"]?.GetValue<bool>()==true,_=>true
        };
    }
    private static bool ExtendedRowVisible(string module,string table,string key,JsonObject? context,JsonObject row)
    {
        if(module==Fire)return FireRowVisible(table,key,context,row);
        if(module==Construction)return ConstructionRowVisible(table,key,row);
        if(module!=Hydraulics||table!="WaterCases"||context is null)return true;
        string element=context["Element"]?.ToString()??"";
        bool profile=context["UseVelocityProfile"]?.GetValue<bool>()==true;
        return key switch
        {
            "RightLevelM"=>element!="RiverPier",
            "LeftLevelM"=>element!="HorizontalSlab"||context["Uplift"]?.ToString()=="HydrostaticHead",
            "CurrentVelocityMs"=>element=="RiverPier"&&!profile,
            "ProfileMultiplier"=>element=="RiverPier"&&profile||element=="HorizontalSlab"&&context["Uplift"]?.ToString()=="AssignedPressureProfile",_=>true
        };
    }
    private static Type ExtendedRowType(string key)=>key switch
    {"FireFaces"=>typeof(GPC.Model.Actions.Fire.FireFaceInput),"GasTemperatures"=>typeof(GPC.Model.Actions.Fire.FireTemperaturePoint),"SurfaceTemperatures"=>typeof(GPC.Model.Actions.Fire.FireSurfacePoint),"CranePositions"=>typeof(GPC.Model.Actions.Cranes.CranePosition),"SupplierWheels"=>typeof(GPC.Model.Actions.Cranes.CraneWheelInput),"AuxiliaryActions"=>typeof(GPC.Model.Actions.Cranes.CraneAuxiliaryAction),"StressEvents"=>typeof(GPC.Model.Actions.TimeEffects.ConcreteStressEvent),"Observations"=>typeof(GPC.Model.Actions.TimeEffects.ConcreteObservation),"Displacements"=>typeof(GPC.Model.Actions.TimeEffects.AxialDisplacementPoint),"ConstructionStages"=>typeof(GPC.Model.Actions.Construction.ConstructionStageInput),"ConstructionLoads"=>typeof(GPC.Model.Actions.Construction.ConstructionLoadInput),"WaterCases"=>typeof(HydraulicWaterCase),"PressureProfile"=>typeof(HydraulicPressurePoint),"VelocityProfile"=>typeof(HydraulicVelocityPoint),_=>throw new ArgumentException("Tabella Azioni sconosciuta.")};
    private static string ExtendedTableTitle(string key)=>key switch
    {"FireFaces"=>"Facce e condizioni al contorno","GasTemperatures"=>"Storia dei gas · tempi crescenti","SurfaceTemperatures"=>"Storie superficiali per faccia · tempi crescenti","CranePositions"=>"Posizioni e direzioni da analizzare","SupplierWheels"=>"Componenti nominali per ruota e posizione","AuxiliaryActions"=>"Azioni del costruttore · componenti firmate","StressEvents"=>"Incrementi di carico esterno P/A","Observations"=>"Età di osservazione","Displacements"=>"Spostamenti relativi degli appoggi · età crescente","ConstructionStages"=>"Sequenza delle fasi (ordine della lista)","ConstructionLoads"=>"Carichi e intervalli di attivazione","WaterCases"=>"Scenari idraulici alternativi","PressureProfile"=>"Profilo di sottospinta · x crescente","VelocityProfile"=>"Profilo di velocità · z crescente",_=>key};
    public static HydraulicDrawing? HydraulicDiagram(JsonObject? result,int selectedCase)
    {
        if(result?["Hydraulics"] is not JsonObject calculated||result["HydraulicInput"] is not JsonObject input)return null;
        var i=input.Deserialize<HydraulicActionsInput>(Options)!;
        var r=calculated.Deserialize<HydraulicActionsResult>(Options)!;
        if(selectedCase<0||selectedCase>=r.Cases.Count)return null;
        var c=r.Cases[selectedCase];var scenario=i.WaterCases[selectedCase];
        return new(i.Element.ToString(),c.Name,i.Uplift==UpliftDistribution.AssignedPressureProfile,i.BottomElevationM,i.TopElevationM,i.WidthM,i.SlabLengthM,i.SlabThicknessM,scenario.LeftLevelM,scenario.RightLevelM,c.NetForceKn,c.MomentKnM,c.ApplicationCoordinateM,c.Diagram.Select(p=>new HydraulicPointDrawing(p.PositionM,p.PositivePressureKpa,p.OpposingPressureKpa,p.NetPressureKpa,p.NetLineLoadKnM)).ToArray());
    }
}
