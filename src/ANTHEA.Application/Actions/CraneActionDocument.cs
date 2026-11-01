using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Model.Actions.Cranes;

namespace Anthea.Application.Actions;

public sealed record CranePointDrawing(string Name,int Rail,double X,double Y,double Q,double Hx,double Hy,double Torsion);
public sealed record CraneDrawing(string Name,double Width,IReadOnlyList<CranePointDrawing> Points);
public static partial class ClimateDocument
{
    public const string Cranes="act_gru_carriponte";
    private static readonly IReadOnlyDictionary<string,string> CraneCaptions=new Dictionary<string,string>
    {
        ["Ntc2018"]="NTC 2018 · modello specialistico EN 1991-3:2006",["EurocodeFirstGeneration"]="EN 1991-3:2006 · prima generazione",
        ["FourWheelBridge"]="Ponte centrato a quattro ruote",["SupplierWheelComponents"]="Componenti nominali per ruota dal costruttore",
        ["Grab"]="Benna · rilascio lento",["Magnet"]="Magnete · rilascio rapido",
        ["BridgeAcceleration"]="Accelerazione ponte · prima di φ5",["Skewing"]="Serpeggiamento · caratteristico",["CrabAcceleration"]="Accelerazione carrello · caratteristico",
        ["InServiceWind"]="Vento di esercizio · caratteristico",["BridgeBuffer"]="Urto ponte · prima di φ7",["CrabBuffer"]="Urto carrello · valore accidentale",["Tilting"]="Ribaltamento · valore accidentale"
    };
    private static JsonObject CalculateCranes(CraneActionsInput input)
    {
        var calculation=GPC.Design.Actions.Cranes.CraneActions.Calculate(input);
        var result=JsonSerializer.SerializeToNode(calculation.Summary,Options)!.AsObject();result["CraneGroups"]=JsonSerializer.SerializeToNode(calculation.Groups,Options);result["CraneInput"]=JsonSerializer.SerializeToNode(input,Options);return result;
    }
    private static string? CraneLabel(string key)=>key switch
    {
        "SupplierSource"=>"Fonte del costruttore e dei fattori dinamici","StaticComponentsConfirmed"=>"Confermo: pesi/componenti verticali statici, dinamica non ancora inclusa","ApplicabilityAssessment"=>"Valutazione delle azioni applicabili ed escluse (anche righe vuote)",
        "Distribution"=>"Distribuzione verticale","LengthM"=>"Interasse delle due vie ℓ|m","WheelbaseM"=>"Passo delle ruote / guide a|m","BridgeWeightKn"=>"Peso nominale del ponte|kN","CrabWeightKn"=>"Peso nominale del carrello|kN","HoistLoadKn"=>"Carico sollevato nominale Qh / quota alla ruota|kN",
        "HoistingClass"=>"Classe di sollevamento","HoistingSpeedMs"=>"Velocità di sollevamento vh|m/s","SupplierPhi2"=>"φ2 dal costruttore","AssignedPhi2"=>"φ2 assegnato","TrackTolerancesConfirmed"=>"Confermo tolleranze delle vie EN 1993-6 per φ4=1",
        "Phi4"=>"Fattore di traslazione φ4","Phi5"=>"Fattore delle forze motrici φ5","AutomaticBridgeAcceleration"=>"Calcola HL e HT per quattro ruote, asse motore x=0","DriveForceKn"=>"Forza motrice totale K prima di φ5|kN",
        "RailHeadWidthMm"=>"Larghezza della testa della rotaia br|mm","WheelEccentricityFactor"=>"Fattore e/br (EN raccomandato 0,25)","ResidualHoistFraction"=>"Frazione degli accessori residui η","IncludeRelease"=>"Includi gruppo di rilascio","ReleaseDevice"=>"Dispositivo di rilascio","ReleasedMassFraction"=>"Frazione della massa rilasciata Δm/m",
        "IncludeTests"=>"Includi prove statiche e dinamiche","DynamicTestRatio"=>"QT/Qh prova dinamica (≥1,10)","StaticTestRatio"=>"QT/Qh prova statica (≥1,25)","IncludeAccidental"=>"Includi urti e ribaltamento assegnati","Phi7"=>"Fattore urto ponte φ7",
        "CraneXM"=>"Posizione origine del ponte lungo via|m","CrabYM"=>"Carrello: distanza dalla rotaia 1|m","TravelDirection"=>"Verso accelerazione automatica (+1 / −1)","Position"=>"Indice posizione (azioni: 0 = tutte)","Rail"=>"Rotaia (1 / 2)","XM"=>"Ascissa relativa all’origine del ponte x|m","YM"=>"Ordinata dalla rotaia 1 y|m","CraneWeightKn"=>"Quota statica di Qc alla ruota|kN",
        "Component"=>"Famiglia dell’azione","VerticalDownKn"=>"Componente verticale (+ giù)|kN","LongitudinalKn"=>"Componente lungo via (+x)|kN","TransverseKn"=>"Componente trasversale (+y)|kN","Source"=>"Fonte della componente e del suo stato dinamico",_=>null
    };
    private static bool CraneVisible(string key,JsonObject v)
    {
        bool Flag(string k)=>v[k]?.GetValue<bool>()==true;bool physical=v["Distribution"]?.ToString()=="FourWheelBridge";
        return key switch
        {
            "BridgeWeightKn" or "CrabWeightKn" or "HoistLoadKn" or "WheelbaseM" or "AutomaticBridgeAcceleration"=>physical,
            "SupplierWheels"=>!physical,"DriveForceKn"=>physical&&Flag("AutomaticBridgeAcceleration"),"HoistingClass" or "HoistingSpeedMs"=>!Flag("SupplierPhi2"),"AssignedPhi2"=>Flag("SupplierPhi2"),
            "ReleaseDevice" or "ReleasedMassFraction"=>Flag("IncludeRelease"),"DynamicTestRatio" or "StaticTestRatio"=>Flag("IncludeTests"),"Phi7"=>Flag("IncludeAccidental"),_=>true
        };
    }
    public static CraneDrawing? CraneDiagram(JsonObject? result,int selected)
    {
        if(result?["CraneGroups"] is not JsonArray groups||selected<0||selected>=groups.Count)return null;
        var g=groups[selected]!;double ReadRequiredDouble(JsonNode n,string key)=>n[key]!.GetValue<double>();
        return new(g["Name"]!.ToString(),result["CraneInput"]!["LengthM"]!.GetValue<double>(),g["Points"]!.AsArray().Select(p=>new CranePointDrawing(p!["Name"]!.ToString(),p["Rail"]!.GetValue<int>(),ReadRequiredDouble(p,"XM"),ReadRequiredDouble(p,"YM"),ReadRequiredDouble(p,"VerticalDownKn"),ReadRequiredDouble(p,"LongitudinalKn"),ReadRequiredDouble(p,"TransverseKn"),ReadRequiredDouble(p,"EccentricTorsionKnM"))).ToArray());
    }
}
