using System.Text.Json.Nodes;
using Anthea.Application.Actions;

namespace Anthea.Testing.Climate;

internal static class ClimateScenarios
{
    internal sealed record Scenario(string Module,string File,string Title,JsonObject Data);
    private static IEnumerable<Scenario> HydraulicScenarios()
    {
        Scenario S(string file,string title,Action<JsonObject> configure)
        {var data=ClimateDocument.Create(ClimateDocument.Hydraulics);configure(data["input"]!.AsObject());return new(ClimateDocument.Hydraulics,file,title,data);}
        yield return S("idraulica-parete","Parete · livelli ordinario e piena",p=>
        {p["UnitWeightKnM3"]=10;p["WaterCases"]!.AsArray().Add(new JsonObject{["Name"]="Piena",["LeftLevelM"]=5,["RightLevelM"]=1,["CurrentVelocityMs"]=0,["ProfileMultiplier"]=1});});
        yield return S("idraulica-platea","Platea sommersa · spinta di Archimede",p=>
        {p["Element"]="HorizontalSlab";p["UnitWeightKnM3"]=10;p["WaterCases"]![0]!["LeftLevelM"]=5;p["WaterCases"]![0]!["RightLevelM"]=5;});
        yield return S("idraulica-sottospinta","Platea · sottospinta da profilo assegnato",p=>
        {p["Element"]="HorizontalSlab";p["Uplift"]="AssignedPressureProfile";p["PressureSource"]="Esempio didattico di distribuzione lineare assegnata; non è un calcolo di filtrazione.";});
        yield return S("idraulica-corrente","Pila · corrente variabile lungo l’altezza",p=>
        {p["Element"]="RiverPier";p["UseVelocityProfile"]=true;p["DragCoefficient"]=1.2;p["DragSource"]="Coefficiente assegnato per il solo esempio numerico, non tabella normativa.";});
    }
    private static IEnumerable<Scenario> ConstructionScenarios()
    {
        Scenario S(string file,string title,Action<JsonObject> configure)
        {var data=ClimateDocument.Create(ClimateDocument.Construction);configure(data["input"]!.AsObject());return new(ClimateDocument.Construction,file,title,data);}
        yield return S("fasi-peso-proprio","Fasi NTC · getto e disarmo",p=>p["ExecutionPlan"]="Esempio didattico: peso fresco temporaneo nella prima fase, peso G1 dalla fase di disarmo. Il foglio non calcola la capacità dei puntelli.");
        void En(JsonObject p){p["Standard"]="EurocodeFirstGeneration";p["NationalParameters"]="Esempio didattico con valori raccomandati EN 1991-1-6:2005; nessuna appendice nazionale implicita.";p["RecommendedEnLoadsConfirmed"]=true;}
        yield return S("fasi-getto-en","Fasi EN · getto e area di lavoro",p=>
        {En(p);p["ConstructionLoads"]![0]!["Model"]="EnConcreteCasting";p["ConstructionLoads"]![0]!["ThicknessM"]=.4;p["ConstructionLoads"]![1]!["ThicknessM"]=.4;p["ConstructionLoads"]![0]!["WorkXOffsetM"]=4;p["ConstructionLoads"]![0]!["WorkYOffsetM"]=2;});
        yield return S("fasi-deposito-ponte","Fasi EN · deposito temporaneo su ponte",p=>
        {En(p);p["ConstructionStages"]![0]!["Name"]="Deposito";p["ConstructionStages"]![1]!["Name"]="Sgombero";var loads=p["ConstructionLoads"]!.AsArray();loads.RemoveAt(1);loads[0]!["Name"]="Materiali depositati";loads[0]!["Model"]="EnBridgeStorage";loads[0]!["PointXM"]=4;loads[0]!["PointYM"]=2;});
    }
    private static IEnumerable<Scenario> CraneScenarios()
    {
        Scenario S(string file,string title,Action<JsonObject> configure)
        {var data=ClimateDocument.Create(ClimateDocument.Cranes);var p=data["input"]!.AsObject();p["NationalParameters"]="Esempio EN 1991-3:2006, valori raccomandati.";p["SupplierSource"]="Dati nominali dimostrativi, da sostituire con documentazione del costruttore.";p["StaticComponentsConfirmed"]=true;p["TrackTolerancesConfirmed"]=true;p["ApplicabilityAssessment"]="Esempio: ponte a quattro ruote, azioni alle guide assegnate; vento, urti e fatica da valutare per il progetto reale.";configure(p);return new(ClimateDocument.Cranes,file,title,data);}
        yield return S("carriponte-quattro-ruote","Carroponte EN · sollevamento e gruppi compatibili",p=>
        {p["AuxiliaryActions"]!.AsArray().Add(new JsonObject{["Name"]="Guida",["Position"]=0,["Component"]="Skewing",["XM"]=0,["YM"]=0,["VerticalDownKn"]=0,["LongitudinalKn"]=0,["TransverseKn"]=12,["Source"]="Forza dimostrativa del costruttore, già caratteristica."});});
        yield return S("carriponte-rilascio","Carroponte NTC con modello EN · rilascio e urto assegnato",p=>
        {p["Standard"]="Ntc2018";p["IncludeRelease"]=true;p["ReleaseDevice"]="Magnet";p["IncludeAccidental"]=true;p["IncludeTests"]=false;p["AuxiliaryActions"]!.AsArray().Add(new JsonObject{["Name"]="Respingente R1",["Position"]=0,["Component"]="BridgeBuffer",["XM"]=0,["YM"]=0,["VerticalDownKn"]=0,["LongitudinalKn"]=50,["TransverseKn"]=0,["Source"]="Forza dimostrativa prima di φ7; sostituire con sistema/massa/velocità del costruttore."});});
        yield return S("carriponte-costruttore","Carroponte EN · componenti alle ruote assegnate",p=>
        {p["Distribution"]="SupplierWheelComponents";p["AutomaticBridgeAcceleration"]=false;p["IncludeTests"]=false;p["CranePositions"]!.AsArray().RemoveAt(1);for(int n=0;n<4;n++)p["SupplierWheels"]!.AsArray().Add(new JsonObject{["Name"]="Ruota "+(n+1),["Position"]=1,["Rail"]=n<2?1:2,["XM"]=n%2*4,["CraneWeightKn"]=n<2?35:25,["HoistLoadKn"]=n<2?90:10});});
    }
    internal static JsonObject PrestressTargetSection()
    {
        var data=Anthea.Application.Modules.ModuleDirectory.CreateData("str_palo");
        if(data["workspace_ca"] is not JsonObject)data["workspace_ca"]=new JsonObject();
        data["workspace_ca"]!["trefoli"]=new JsonArray(new JsonObject{["id"]="P1",["x"]="0",["y"]="0",["area"]="1800",["Ep"]="195000",["fpyk"]="1640",["fpk"]="1860",["eps_u"]="35",["sigma0"]="1000",["diagramma"]="Incrudente",["materiale"]="Trefolo di prova 1860/1640"});
        return data;
    }
    internal static IEnumerable<Scenario> All()
    {
        Scenario S(string module,string file,string title,Action<JsonObject> configure)
        {var data=ClimateDocument.Create(module);configure(data["input"]!.AsObject());return new(module,file,title,data);}
        void En(JsonObject p){p["Standard"]="EurocodeFirstGeneration";p["NationalParameters"]="Esempio didattico: valori raccomandati EN di prima generazione, clima assegnato; nessuna appendice nazionale selezionata.";}
        foreach(var scenario in HydraulicScenarios())yield return scenario;
        foreach(var scenario in ConstructionScenarios())yield return scenario;
        foreach(var scenario in CraneScenarios())yield return scenario;
        yield return S(ClimateDocument.TimeEffects,"tempo-ntc-libero","Effetti differiti NTC · carico sostenuto e ritiro",p=>{});
        yield return S(ClimateDocument.TimeEffects,"tempo-en-molla","Effetti differiti EN · barra con molla e spostamento imposto",p=>
        {En(p);p["NationalParameters"]="Esempio didattico EN 1992-1-1:2004 a 20 °C, cemento N e RH costante; nessuna appendice nazionale implicita.";p["Restraint"]="EndSpring";p["StressEvents"]!.AsArray().Clear();p["Displacements"]!.AsArray().Add(new JsonObject{["Name"]="Spostamento graduale",["AgeDays"]=365,["DisplacementMm"]=.2});});
        yield return S(ClimateDocument.TimeEffects,"tempo-ntc-vincolato","Ritiro impedito · previsione integra e controllo di fessurazione",p=>
        {p["Restraint"]="FixedDistance";p["StressEvents"]!.AsArray().Clear();});
        void PrestressSource(JsonObject p){p["SystemSource"]="Dati esemplificativi: trefolo 1860/1640, Ep 195000 MPa, μ 0,19, k 0,006 rad/m, rientro 6 mm. Sostituire con ETA e certificati del sistema.";p["ConcreteStressSource"]="Esempio numerico con tensioni uniformi assegnate: Δσc=8 MPa, σc,QP=6 MPa. Da ricavare dall’analisi strutturale del progetto.";}
        yield return S(ClimateDocument.Prestressing,"precompressione-posteso","Posteso NTC · cavo parabolico e perdite",p=>PrestressSource(p));
        yield return S(ClimateDocument.Prestressing,"precompressione-bilaterale","Posteso EN · tiro bilaterale simultaneo",p=>{PrestressSource(p);En(p);p["NationalParameters"]="Valori raccomandati EN 1992-1-1:2004, esempio didattico.";p["Jacking"]="BothSymmetric";});
        yield return S(ClimateDocument.Prestressing,"precompressione-preteso","Preteso NTC · banco, rilassamento e trasferimento",p=>
        {PrestressSource(p);p["Method"]="PreTensioned";p["Profile"]="Straight";p["StartEccentricityM"]=-.3;p["EndEccentricityM"]=-.3;p["JackingAgeDays"]=0;p["TransferAgeDays"]=7;p["Observations"]![0]!["AgeDays"]=7;});
        yield return S(ClimateDocument.Prestressing,"precompressione-non-aderente","Posteso EN interno non aderente · perdite medie",p=>
        {PrestressSource(p);En(p);p["NationalParameters"]="Valori raccomandati EN 1992-1-1:2004, esempio didattico.";p["Method"]="PostTensionedUnbonded";});
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
