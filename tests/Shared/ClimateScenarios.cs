using System.Text.Json.Nodes;
using Anthea.Application.Actions;

namespace Anthea.Testing.Climate;

internal static class ClimateScenarios
{
    internal sealed record Scenario(string Module,string File,string Title,JsonObject Data);
    internal static IEnumerable<Scenario> All()
    {
        Scenario S(string module,string file,string title,Action<JsonObject> configure)
        {var data=ClimateDocument.Create(module);configure(data["input"]!.AsObject());return new(module,file,title,data);}
        void En(JsonObject p){p["Standard"]="EurocodeFirstGeneration";p["NationalParameters"]="Esempio didattico: valori raccomandati EN di prima generazione, clima assegnato; nessuna appendice nazionale selezionata.";}
        yield return S(ClimateDocument.Snow,"neve-ntc-due-falde","Neve NTC · due falde 30° / 45°",p=>{p["PitchLeftDeg"]=30;p["PitchRightDeg"]=45;p["AltitudeM"]=481;p["Zone"]="II";});
        yield return S(ClimateDocument.Snow,"neve-en-salto","Neve EN · accumulo presso salto",p=>{En(p);p["AssignedGroundLoad"]=true;p["GroundLoadKnM2"]=1;p["Roof"]="LowerRoof";p["PitchLeftDeg"]=0;p["StepHeightM"]=3;p["WidthM"]=3;p["UpperWidthM"]=9;});
        yield return S(ClimateDocument.BuildingWind,"vento-ntc-due-falde","Vento NTC · edificio a due falde",p=>{p["PitchDeg"]=15;p["LoadedAreaM2"]=10;});
        yield return S(ClimateDocument.BuildingWind,"vento-en-piana","Vento EN · copertura piana con parapetto",p=>{En(p["Site"]!.AsObject());p["Site"]!["AssignedBaseSpeed"]=true;p["Roof"]="Flat";p["ParapetM"]=.5;p["InternalPressure"]="DominantFace";p["DominantOpeningRatio"]=2.5;p["InternalReferenceHeightM"]=10.5;});
        yield return S(ClimateDocument.BuildingThermal,"temperatura-ntc-ca","Temperatura NTC · elemento in c.a. esposto",p=>p["SimplifiedApplicabilityConfirmed"]=true);
        yield return S(ClimateDocument.BuildingThermal,"temperatura-en-strato","Temperatura EN · parete omogenea",p=>{En(p["Site"]!.AsObject());p["Site"]!["AssignedTemperatures"]=true;p["Method"]="HomogeneousLayer";p["ProfileSource"]="Esempio monostrato: Rsi=0,16; Rse=0,04 m²K/W; h=0,30 m; λ=1 W/(m K); incremento solare assegnato 30 °C.";p["ConductivityWmK"]=1;p["InnerResistanceM2KW"]=.16;p["WinterInnerAirC"]=20;});
        yield return S(ClimateDocument.BridgeWind,"vento-cnr-isolato","Vento CNR · impalcato isolato",p=>{p["ApplicabilityConfirmed"]=true;p["WidthM"]=10;p["TotalHeightM"]=2;p["DeckCentreHeightM"]=19;});
        yield return S(ClimateDocument.BridgeWind,"vento-cnr-affiancati","Vento CNR · impalcati indipendenti affiancati",p=>{p["ApplicabilityConfirmed"]=true;p["WidthM"]=10;p["SecondWidthM"]=10;p["TotalHeightM"]=2;p["DeckCentreHeightM"]=19;p["AdjacentDeck"]=true;p["GapM"]=1;});
        yield return S(ClimateDocument.BridgeThermal,"temperatura-ponte-cassone","Temperatura ponte · cassone in calcestruzzo",p=>{p["Site"]!["AssignedTemperatures"]=true;p["Site"]!["MinimumAirC"]=-10;p["Site"]!["MaximumAirC"]=40;});
        yield return S(ClimateDocument.BridgeThermal,"temperatura-ponte-acciaio","Temperatura EN · impalcato in acciaio",p=>{En(p["Site"]!.AsObject());p["Site"]!["AssignedTemperatures"]=true;p["Deck"]="SteelTrussOrPlate";p["SurfacingMm"]=100;p["BearingSettingTemperatureKnown"]=true;});
        yield return S(ClimateDocument.PermanentLoads,"carichi-ntc-abitazione","Carichi NTC · solaio residenziale e tramezzi",p=>{p["PartitionLineKnM"]=2.1;p["PartitionDistributionConfirmed"]=true;});
        yield return S(ClimateDocument.PermanentLoads,"carichi-en-ufficio","Carichi EN · ufficio con partizioni mobili",p=>{En(p);p["AssignedImposedLoads"]=true;p["Occupancy"]="OfficeB1";p["DistributedKnM2"]=3;p["ConcentratedKn"]=4.5;p["HorizontalKnM"]=1;p["PartitionLineKnM"]=1;p["PartitionDistributionConfirmed"]=true;});
        yield return S(ClimateDocument.Combinations,"combinazioni-ntc-slu","Combinazioni NTC · SLU con vento di segno opposto",p=>{});
        yield return S(ClimateDocument.Combinations,"combinazioni-en-610ab","Combinazioni EN · confronto 6.10a e 6.10b",p=>{En(p);p["RecommendedEnFactorsConfirmed"]=true;p["EnExpression"]="Equations610a610b";});
        yield return S(ClimateDocument.RoadTraffic,"traffico-ntc-curva","Traffico NTC · carreggiata 10 m e curva R=1000 m",p=>p["StraightBridge"]=false);
        yield return S(ClimateDocument.RoadTraffic,"traffico-en-giunto","Traffico EN · asse LM2 presso giunto",p=>{En(p);p["AddJointAmplification"]=true;p["JointDistanceM"]=0;});
        yield return S(ClimateDocument.Distribution,"ripartizione-rigida","Ripartizione rigida · tre travi e corsia eccentrica",p=>{p["ApplicabilityConfirmed"]=true;p["Method"]="RigidCourbon";});
        yield return S(ClimateDocument.Distribution,"ripartizione-deformabile","Ripartizione deformabile · tre travi flesso-torsionali",p=>p["ApplicabilityConfirmed"]=true);
        yield return S(ClimateDocument.Impact,"urto-ntc-pila","Urto NTC · pila prossima alla strada",p=>{});
        yield return S(ClimateDocument.Impact,"urto-en-impalcato","Urto EN · impalcato con franco netto 5,5 m",p=>{En(p);p["Target"]="Superstructure";});
    }
}
