using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using GPC.Model.Actions.Climate;
using GPC.Model.Actions.General;
using GPC.Model.Actions.Traffic;
using GPC.Design.Actions.General;

namespace Anthea.Application.Actions;

public sealed record ActionTableDescriptor(string Key,string Title,JsonArray Rows);
public sealed record TrafficGirderDrawing(string Name,double Position);
public sealed record TrafficPatchDrawing(string Name,double Left,double Right,double Start,double End);
public sealed record TrafficTandemDrawing(string Name,double CentreY,double CentreX,double Axles,double Wheels,double Length,double Width);
public sealed record TrafficInfluenceDrawing(double Position,IReadOnlyList<double> Ordinates);
public sealed record TrafficDrawing(double Left,double Right,double Span,double Section,double InfluenceX,IReadOnlyList<TrafficGirderDrawing> Girders,IReadOnlyList<TrafficPatchDrawing> Patches,IReadOnlyList<TrafficTandemDrawing> Tandems,IReadOnlyList<TrafficInfluenceDrawing> Influence);

public static partial class ClimateDocument
{
    public const string PermanentLoads="act_carichi_edifici",RoadTraffic="act_traffico_ponti",Impact="act_urti_veicolari",Combinations="act_combinazioni",Distribution="act_ripartizione_traffico";
    private static IEnumerable<ModuleInfo> AdditionalModules=>new[]
    {
        new ModuleInfo(PermanentLoads,"Azioni","Permanenti e sovraccarichi","NTC / EN · strati, tramezzi e destinazione d’uso","Azione","Carichi edifici","Permanenti e variabili"),
        new ModuleInfo(Combinations,"Azioni","Combinazioni delle azioni","NTC / EN · coefficienti, correlazioni ed esclusioni","Azione","Combinazioni","Combinazioni"),
        new ModuleInfo(RoadTraffic,"Azioni","Traffico sui ponti","NTC / EN · corsie, tandem, frenatura e centrifuga","Azione","Traffico ponti","Traffico"),
        new ModuleInfo(Distribution,"Azioni","Ripartizione trasversale del traffico","Traverso rigido o deformabile · quote di momento per trave","Azione","Ripartizione traffico","Traffico"),
        new ModuleInfo(Impact,"Azioni","Urti veicolari","NTC / EN · piedritti, impalcati e carrelli elevatori","Azione","Urti veicolari","Azioni eccezionali")
    };
    private static object AdditionalInput(string module)=>module switch
    {PermanentLoads=>new BuildingLoadsInput(),RoadTraffic=>new RoadTrafficInput(),Impact=>new VehicleImpactInput(),Combinations=>new ActionCombinationsInput(),Distribution=>new TransverseDistributionInput(),_=>throw new ArgumentException("Modulo Azioni sconosciuto.")};
    private static ClimateResult CalculateAdditional(object input,CancellationToken cancellation)=>input switch
    {BuildingLoadsInput x=>BuildingLoads.Calculate(x),RoadTrafficInput x=>RoadTrafficActions.Calculate(x),VehicleImpactInput x=>VehicleImpact.Calculate(x),ActionCombinationsInput x=>ActionCombinations.Calculate(x,cancellation),_=>throw new ArgumentException("Input Azioni non riconosciuto.")};
    private static JsonObject CalculateDistribution(TransverseDistributionInput input,CancellationToken cancellation)
    {
        var calculation=GPC.Design.Actions.Traffic.TransverseDistribution.Calculate(input,cancellation);
        var result=JsonSerializer.SerializeToNode(GPC.Design.Actions.Traffic.TransverseDistribution.Summary(calculation),Options)!.AsObject();
        result["Distribution"]=JsonSerializer.SerializeToNode(calculation,Options);return result;
    }
    private static readonly FrozenDictionary<string,string> ExtraCaptions=new Dictionary<string,string>()
    {
        ["RigidCourbon"]="Traverso rigido · Courbon",["DeformableFourier"]="Traverso deformabile · Fourier",
        ["ResidentialA"]="A · Residenziale",["StairsAB"]="Scale / balconi / ballatoi A e B",["OfficeB1"]="B1 · Uffici non aperti al pubblico",["OfficeB2"]="B2 · Uffici aperti al pubblico",
        ["GatheringC1"]="C1 · Aree con tavoli",["GatheringC2"]="C2 · Posti a sedere fissi",["GatheringC3"]="C3 · Aree libere",["GatheringC4"]="C4 · Attività fisiche",["GatheringC5"]="C5 · Grandi affollamenti",
        ["ShopD1"]="D1 · Negozi",["RetailD2"]="D2 · Centri commerciali",["StorageE1"]="E1 · Depositi",["VehiclesF"]="F · Veicoli fino a 30 kN",["VehiclesG"]="G · Veicoli 30–160 kN",["MaintenanceH"]="H · Coperture per manutenzione",
        ["None"]="Nessuna",["LoadedArea"]="Per area d’influenza",["Storeys"]="Per numero di piani",
        ["SupportingMember"]="Piedritto / membratura verticale",["Superstructure"]="Impalcato sopra la strada",["Forklift"]="Carrello elevatore",
        ["Motorway"]="Autostrada / strada principale",["Rural"]="Strada locale / rurale",["Urban"]="Strada urbana",["CarParking"]="Parcheggio automobili",["LorryParking"]="Parcheggio autocarri",
        ["Ultimate"]="SLU fondamentale",["Characteristic"]="SLE caratteristica",["Frequent"]="SLE frequente",["QuasiPermanent"]="SLE quasi permanente",["Accidental"]="Eccezionale",
        ["Structural"]="A1 / Set B · STR",["Equilibrium"]="EQU",["Geotechnical"]="A2 / Set C · GEO",["Equation610"]="EN 6.10",["Equations610a610b"]="EN 6.10a e 6.10b",
        ["PermanentG1"]="G1 · Permanente strutturale",["PermanentG2"]="G2 · Permanente non strutturale",["Prestress"]="P · Precompressione (γ=1)",["Variable"]="Q · Variabile",
        ["Residential"]="A · Residenziale (ψ)",["Office"]="B · Uffici (ψ)",["Gathering"]="C · Affollamento (ψ)",["Retail"]="D · Commercio (ψ)",["Storage"]="E · Depositi (ψ)",["RoofH"]="H · Copertura manutenzione (ψ)",
        ["Wind"]="Vento",["SnowLow"]="Neve fino a 1000 m",["SnowHigh"]="Neve oltre 1000 m",["Temperature"]="Temperatura"
    }.ToFrozenDictionary();
    private static string Caption(string name)=>ExtraCaptions.GetValueOrDefault(name)??Captions.GetValueOrDefault(name)??name;
    private static string Uncaption(string value)=>ExtraCaptions.FirstOrDefault(p=>p.Value==value).Key??Captions.FirstOrDefault(p=>p.Value==value).Key??value;
    private static readonly FrozenDictionary<string,string> ExtraLabels=new Dictionary<string,string>()
    {
        ["Name"]="Nome",["Occupancy"]="Destinazione d’uso",["AssignedImposedLoads"]="Assegna sovraccarichi (obbligatorio EN / E1 / G)",["DistributedKnM2"]="Distribuito qk|kN/m²",["ConcentratedKn"]="Concentrato per impronta Qk|kN",["HorizontalKnM"]="Orizzontale locale Hk|kN/m",["ContactSideM"]="Lato impronta EN|m",
        ["PartitionLineKnM"]="Peso lineare partizioni equivalenti|kN/m",["PartitionDistributionConfirmed"]="Confermo ripartizione e applicabilità delle partizioni",["Reduction"]="Riduzione sovraccarichi",["LoadedAreaM2"]="Area d’influenza|m²",["Storeys"]="Numero di piani caricati",["TributaryWidthM"]="Larghezza tributaria|m",["UnitWeightKnM3"]="Peso specifico|kN/m³",["Structural"]="Strato strutturale (G1)",
        ["Target"]="Elemento soggetto a urto",["Road"]="Categoria di traffico",["MemberWidthM"]="Larghezza membratura|m",["ClearanceM"]="Franco netto futuro|m",["EnTruckImpactHeightM"]="EN · quota urto autocarro|m",["EnFullImpactClearanceM"]="EN · franco h0, forza piena|m",["EnNoImpactClearanceM"]="EN · franco h1, forza nulla|m",["ForkliftWeightKn"]="Peso carrello + massimo carico|kN",
        ["AssignedImpactForce"]="Forze equivalenti da studio specifico",["ImpactForceKn"]="Forza parallela assegnata|kN",["TransverseForceKn"]="Forza trasversale assegnata|kN",["ForceSource"]="Fonte dello studio d’urto",
        ["CarriagewayWidthM"]="Larghezza carrabile fra cordoli|m",["LoadedLengthM"]="Lunghezza caricata|m",["RadiusM"]="Raggio planimetrico|m",["StraightBridge"]="Ponte rettilineo",["AlphaQ1"]="EN · αQ1 tandem corsia 1",["AlphaQ2"]="EN · αQ2 tandem corsia 2",["AlphaQ3"]="EN · αQ3 tandem corsia 3",["Alphaq1"]="EN · αq1 distribuito corsia 1",["AlphaqOther"]="EN · αq altre corsie",["AlphaqRemaining"]="EN · αq zona rimanente",["BetaQ"]="EN · βQ asse LM2",
        ["PavementThicknessM"]="Spessore pavimentazione|m",["SlabThicknessM"]="Spessore soletta|m",["JointDistanceM"]="EN · distanza dal giunto|m",["AddJointAmplification"]="Amplificazione addizionale LM2 al giunto",["NtcJointFactor"]="NTC · fattore al giunto",["JointSource"]="NTC · fonte dello studio al giunto",["FootwayWidthM"]="Larghezza marciapiede|m",
        ["Family"]="Famiglia di combinazioni",["Set"]="Set coefficienti delle azioni",["EnExpression"]="Espressione fondamentale EN",["EnXi"]="EN · fattore ξ",["EnAccidentalLeadingPsi1"]="EN · ψ1 della dominante eccezionale",["EffectUnit"]="Unità comune degli effetti",["RecommendedEnFactorsConfirmed"]="Confermo fattori EN raccomandati applicabili",["Nature"]="Natura dell’azione",["Effect"]="Effetto con segno (unità comune)",["Correlation"]="Correlazione: stessa sorgente / gruppo",["ExclusiveGroup"]="Esclusione: alternative dello stesso gruppo",["PsiCategory"]="Categoria per ψ",["Psi0"]="ψ0 assegnato",["Psi1"]="ψ1 assegnato",["Psi2"]="ψ2 assegnato",["DefinedPermanentG2"]="G2 noto: stessi γ di G1",["OmitFavourableG2"]="Consenti omissione G2 favorevole",
        ["SpanM"]="Luce comune delle travi|m",["SectionM"]="Ascissa della sezione esaminata x|m",["DeckLeftM"]="Bordo sinistro y|m",["DeckRightM"]="Bordo destro y|m",["TransverseRigidityKnM"]="EI soletta per metro longitudinale|kN·m",["RelativeTolerance"]="Tolleranza relativa",["MaximumModes"]="Numero massimo di modi",["InfluenceLongitudinalM"]="Ascissa carico unitario per influenza|m",["InfluenceStations"]="Stazioni trasversali di influenza",
        ["PositionM"]="Posizione trasversale y|m",["FlexuralRigidityKnM2"]="Rigidezza flessionale EI|kN·m²",["TorsionalRigidityKnM2"]="Rigidezza torsionale GJ|kN·m²",["LeftM"]="Bordo sinistro corsia y|m",["StartM"]="Inizio zona caricata x|m",["EndM"]="Fine zona caricata x|m",["LoadKnM2"]="Carico uniforme|kN/m²",["CentreTransverseM"]="Centro tandem y|m",["CentreLongitudinalM"]="Centro tandem x|m",["AxleLoadKn"]="Carico per asse|kN",["AxleSpacingM"]="Interasse assi|m",["WheelSpacingM"]="Interasse ruote|m",["FootprintLengthM"]="Impronta longitudinale|m",["FootprintWidthM"]="Impronta trasversale|m"
    }.ToFrozenDictionary();
    private static string? ExtraLabel(string module,string key)
    {
        if(module==Distribution&&key=="Method")return "Metodo di ripartizione";
        if(module==Distribution&&key=="ApplicabilityConfirmed")return "Confermo travi prismatiche in semplice appoggio e ipotesi del modello";
        if(key=="ThicknessM"&&module==PermanentLoads)return "Spessore strato|m";
        return ExtraLabels.GetValueOrDefault(key);
    }
    private static bool AdditionalVisible(string module,string key,JsonObject values)
    {
        string S(string k)=>values[k]?.ToString()??"";bool B(string k)=>S(k).Equals("true",StringComparison.OrdinalIgnoreCase);bool en=S("Standard")=="EurocodeFirstGeneration";
        if(module==Combinations)return key switch{"EnExpression" or "EnXi" or "EnAccidentalLeadingPsi1" or "RecommendedEnFactorsConfirmed"=>en,_=>true};
        if(module==PermanentLoads)return key switch{"DistributedKnM2" or "ConcentratedKn" or "HorizontalKnM"=>B("AssignedImposedLoads")||S("Occupancy")=="Assigned"||en,"ContactSideM"=>en,"LoadedAreaM2"=>S("Reduction")=="LoadedArea","Storeys"=>S("Reduction")=="Storeys",_=>true};
        if(module==RoadTraffic)return key switch{"AlphaQ1" or "AlphaQ2" or "AlphaQ3" or "Alphaq1" or "AlphaqOther" or "AlphaqRemaining" or "BetaQ"=>en,"JointDistanceM"=>en&&B("AddJointAmplification"),"NtcJointFactor" or "JointSource"=>!en&&B("AddJointAmplification"),"RadiusM"=>!B("StraightBridge"),_=>true};
        if(module==Impact)return key switch{"EnTruckImpactHeightM"=>en&&S("Target")=="SupportingMember"&&S("Road")!="CarParking","EnFullImpactClearanceM" or "EnNoImpactClearanceM"=>en&&S("Target")=="Superstructure","ClearanceM"=>S("Target")=="Superstructure","ForkliftWeightKn"=>S("Target")=="Forklift","MemberWidthM"=>S("Target")=="SupportingMember","ImpactForceKn" or "TransverseForceKn" or "ForceSource"=>B("AssignedImpactForce"),"Road" or "AssignedImpactForce"=>S("Target")!="Forklift",_=>true};
        if(module==Distribution&&key is "TransverseRigidityKnM" or "MaximumModes" or "RelativeTolerance")return S("Method")=="DeformableFourier";
        return true;
    }
    private static Type RowType(string key)=>key switch{"Girders"=>typeof(DistributionGirder),"Lanes"=>typeof(DistributionLane),"Tandems"=>typeof(DistributionTandem),"Layers"=>typeof(LoadLayer),"Actions"=>typeof(CombinationAction),_=>throw new ArgumentException("Tabella Azioni sconosciuta.")};
    public static IReadOnlyList<ActionTableDescriptor> Tables(JsonObject document)=>document["input"]!.AsObject().Where(p=>p.Value is JsonArray).Select(p=>new ActionTableDescriptor(p.Key,p.Key switch{"Girders"=>"Travi longitudinali","Lanes"=>"Corsie / aree distribuite","Tandems"=>"Tandem","Layers"=>"Strati permanenti","Actions"=>"Azioni elementari",_=>p.Key},p.Value!.AsArray())).ToArray();
    public static JsonObject NewTableRow(string key,JsonArray existing)
    {
        var row=JsonSerializer.SerializeToNode(Activator.CreateInstance(RowType(key)),Options)!.AsObject();
        string name=row["Name"]!.ToString();int n=1;while(existing.Any(r=>r?["Name"]?.ToString()==name+" "+n))n++;row["Name"]=name+" "+n;return row;
    }
    public static ClimateFormGroup RowForm(string module,string key,JsonObject row)
    {
        var values=(JsonObject)row.DeepClone();var fields=new List<ClimateField>();
        foreach(var p in RowType(key).GetProperties())
        {
            string caption=key=="Lanes"&&p.Name=="WidthM"?"Larghezza corsia|m":ExtraLabel(module,p.Name)??Labels.GetValueOrDefault(p.Name)??p.Name;var parts=caption.Split('|');
            var choices=p.PropertyType.IsEnum?Enum.GetNames(p.PropertyType).Select(Caption).ToArray():null;
            if(choices is not null&&values[p.Name] is { } v)values[p.Name]=Caption(v.ToString());
            fields.Add(new(p.Name,parts[0],parts.Length>1?parts[1]:"",p.PropertyType==typeof(bool),p.PropertyType==typeof(double)||p.PropertyType==typeof(int),choices,caption.Replace('|',' ')));
        }
        return new(key,"",values,fields);
    }
    public static void ApplyRowField(JsonObject row,ClimateFormGroup form,string key)
    {var value=form.Values[key]?.DeepClone();if(value is not null&&form.Fields.First(f=>f.Key==key).Choices is not null)value=JsonValue.Create(Uncaption(value.ToString()));row[key]=value;}
    public static TrafficDrawing? Drawing(JsonObject document,JsonObject? result)
    {
        try
        {
            var input=(TransverseDistributionInput)Read(Distribution,document);
            if(input.Girders is null||input.Lanes is null||input.Tandems is null||input.Girders.Any(g=>g is null)||input.Lanes.Any(l=>l is null)||input.Tandems.Any(t=>t is null))return null;
            var distribution=result?["Distribution"]?.Deserialize<TransverseDistributionResult>(Options);
            return new(input.DeckLeftM,input.DeckRightM,input.SpanM,input.SectionM,input.InfluenceLongitudinalM,
                input.Girders.OrderBy(g=>g.PositionM).Select(g=>new TrafficGirderDrawing(g.Name,g.PositionM)).ToArray(),
                input.Lanes.Select(l=>new TrafficPatchDrawing(l.Name,l.LeftM,l.LeftM+l.WidthM,l.StartM,l.EndM)).ToArray(),
                input.Tandems.Select(t=>new TrafficTandemDrawing(t.Name,t.CentreTransverseM,t.CentreLongitudinalM,t.AxleSpacingM,t.WheelSpacingM,t.FootprintLengthM,t.FootprintWidthM)).ToArray(),
                distribution?.Influence.Select(p=>new TrafficInfluenceDrawing(p.PositionM,p.MomentOrdinatesM)).ToArray()??[]);
        }
        catch(ArgumentException){return null;}catch(JsonException){return null;}catch(InvalidOperationException){return null;}
    }
}
