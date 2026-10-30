using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthea.Application.Modules;
using GPC.Design.Actions.Climate;
using GPC.Model.Actions.Climate;

namespace Anthea.Application.Actions;

public sealed record ClimateField(string Key, string Label, string Unit, bool Boolean, bool Numeric, string[]? Choices, string Help);
public sealed record ClimateFormGroup(string Name, string Path, JsonObject Values, IReadOnlyList<ClimateField> Fields);

/// <summary>Versioned GPC input document, UI projections and application dispatch. Engineering stays in GPC.Design.</summary>
public static class ClimateDocument
{
    public const string Snow = "act_neve", BuildingWind = "act_vento_edifici", BuildingThermal = "act_temperatura_edifici", BridgeWind = "act_vento_ponti", BridgeThermal = "act_temperatura_ponti";
    public static IReadOnlyList<ModuleInfo> Modules { get; } = new[]
    {
        Info(Snow,"Neve sugli edifici","NTC / EN · neve al suolo, coperture e accumuli"),
        Info(BuildingWind,"Vento sugli edifici","NTC / EN · clima, pareti, coperture e pressioni interne"),
        Info(BuildingThermal,"Temperatura negli edifici","NTC / EN · componente uniforme e profilo termico"),
        Info(BridgeWind,"Vento sui ponti","CNR-DT 207 R1/2018 · impalcati isolati e affiancati"),
        Info(BridgeThermal,"Temperatura nei ponti","NTC / EN · impalcati, gradienti, appoggi e giunti")
    };
    private static ModuleInfo Info(string id,string name,string description)=>new(id,"Azioni",name,description,"Azione",name,"Azioni climatiche");
    public static bool Supports(string module)=>Modules.Any(m=>m.Id==module);
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() }, NumberHandling=JsonNumberHandling.AllowReadingFromString, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    private static object DefaultInput(string module)=>module switch
    {
        Snow=>new SnowInput(), BuildingWind=>new BuildingWindInput(), BuildingThermal=>new BuildingThermalInput(), BridgeWind=>new BridgeWindInput(), BridgeThermal=>new BridgeThermalInput(), _=>throw new ArgumentException("Modulo climatico sconosciuto.")
    };
    public static JsonObject Create(string module)=>new() { ["climate_schema"]=1, ["input_type"]=DefaultInput(module).GetType().Name, ["input"]=JsonSerializer.SerializeToNode(DefaultInput(module),Options) };
    private static object Read(string module,JsonObject document)
    {
        if(document["climate_schema"]?.GetValue<int>()!=1)throw new ArgumentException("Versione del documento Azioni non supportata.");
        Type type=DefaultInput(module).GetType();
        if(document["input_type"]?.ToString()!=type.Name || document["input"] is not JsonObject input)throw new ArgumentException("Contratto GPC del foglio Azioni non coerente con il modulo.");
        try {return input.Deserialize(type,Options)??throw new ArgumentException("Input climatico mancante.");}
        catch(JsonException e){throw new ArgumentException("Campo non valido nel documento Azioni: "+e.Path,e);}
    }
    public static void Validate(string module,JsonObject document)=>Read(module,document);
    public static JsonObject Calculate(string module,JsonObject document,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested(); object input=Read(module,document);
        ClimateResult result=input switch
        {
            SnowInput x=>SnowActions.Calculate(x), BuildingWindInput x=>BuildingWindActions.Calculate(x), BuildingThermalInput x=>ThermalActions.Building(x), BridgeWindInput x=>BridgeWindActions.Calculate(x), BridgeThermalInput x=>ThermalActions.Bridge(x), _=>throw new ArgumentException("Input non riconosciuto.")
        };
        cancellation.ThrowIfCancellationRequested(); return JsonSerializer.SerializeToNode(result,Options)!.AsObject();
    }
    // Enum captions are a UI projection only. Documents retain the GPC contract names.
    private static readonly Dictionary<string,string> Captions = new()
    {
        ["Ntc2018"]="Italia · NTC 2018",["EurocodeFirstGeneration"]="Eurocodice 1 · prima generazione",
        ["AlpineI"]="I · Alpina",["MediterraneanI"]="I · Mediterranea",["II"]="II",["III"]="III",
        ["Normal"]="Normale",["Windswept"]="Battuta dai venti",["Sheltered"]="Riparata",
        ["Flat"]="Piana",["Monopitch"]="Falda singola",["Duopitch"]="Due falde",["MultiSpan"]="Falde multiple",["Cylindrical"]="Cilindrica",["LowerRoof"]="Copertura inferiore presso salto",
        ["DistributedOpenings"]="Aperture distribuite",["DominantFace"]="Faccia dominante",["Assigned"]="Coefficienti assegnati",
        ["SimplifiedNtc"]="Semplificato NTC §3.5.5",["AssignedSurfaceProfile"]="Temperature di superficie assegnate",["HomogeneousLayer"]="Trasmissione in strato omogeneo",
        ["ReinforcedConcrete"]="Cemento armato",["Steel"]="Acciaio",["SteelBox"]="Cassone in acciaio",["SteelTrussOrPlate"]="Travi in acciaio / reticolare",["Composite"]="Composto acciaio-calcestruzzo",["ConcreteBox"]="Cassone in calcestruzzo",["ConcreteBeam"]="Travi in calcestruzzo",["ConcreteSlab"]="Soletta in calcestruzzo",
        ["Unsurfaced"]="Senza pavimentazione",["Waterproofed"]="Impermeabilizzato",["Asphalt"]="Pavimentazione 50 / 100 / 150 mm",["Ballast750"]="Ballast 750 mm"
    };
    private static readonly Dictionary<string,string> Labels = new()
    {
        ["Standard"]="Normativa",["NationalParameters"]="Paese, appendice nazionale e fonte dei parametri",["NtcZone"]="Zona NTC (vedere guida)",["Zone"]="Zona neve NTC",["AltitudeM"]="Quota sul mare|m",["AssignedGroundLoad"]="Assegna il carico al suolo",["GroundLoadKnM2"]="Neve al suolo sk|kN/m²",["Exposure"]="Esposizione della copertura",["ThermalCoefficient"]="Coefficiente termico Ct",["ThermalJustification"]="Motivazione dello studio per Ct",
        ["Roof"]="Geometria della copertura",["PitchLeftDeg"]="Inclinazione falda sinistra/superiore|°",["PitchRightDeg"]="Inclinazione falda destra|°",["SnowGuards"]="Fermaneve / scivolamento impedito",["WidthM"]="Larghezza b / d impalcato|m",["RiseM"]="Freccia copertura cilindrica|m",["UpperWidthM"]="Larghezza copertura superiore b1|m",["StepHeightM"]="Dislivello h|m",["SlidingCoefficient"]="Coefficiente di scivolamento μs",["SnowDensityKnM3"]="Peso specifico neve γ|kN/m³",
        ["AssignedBaseSpeed"]="Assegna velocità base",["BaseSpeedMs"]="Velocità base vb (EN: vb,0)|m/s",["ReturnYears"]="Periodo di ritorno TR|anni",["ExposureCategory"]="Categoria: NTC 1…5; EN 0…4",["TopographyFactor"]="Topografia ct / orografia co",["DirectionFactor"]="Direzionalità cdir",["SeasonFactor"]="Stagionalità cseason",["ProbabilityK"]="Parametro probabilità K",["ProbabilityExponent"]="Esponente probabilità n",["TurbulenceFactor"]="Turbolenza kI",["AirDensityKgM3"]="Densità dell’aria ρ|kg/m³",
        ["DepthM"]="Profondità edificio / altezza impalcato|m",["HeightM"]="Altezza massima edificio h|m",["PitchDeg"]="Inclinazione falde|°",["WindDirectionDeg"]="Vento rispetto al colmo: 0 / 90 / 180|°",["ParapetM"]="Altezza parapetto hp|m",["LoadedAreaM2"]="Area caricata dell’elemento A|m²",["InternalPressure"]="Pressione interna",["DominantOpeningRatio"]="Rapporto aperture dominante / altre",["DominantExternalCoefficient"]="cpe medio alle aperture dominanti",["InternalCoefficientMin"]="Coefficiente interno minimo",["InternalCoefficientMax"]="Coefficiente interno massimo",["InternalReferenceHeightM"]="Quota di riferimento interna zi|m",["StructuralFactor"]="Coefficiente dinamico cd / cs·cd",["StructuralFactorAssessed"]="Coefficiente strutturale valutato per il caso",["FrictionCoefficient"]="Coefficiente d’attrito cf",
        ["TotalHeightM"]="Ingombro totale htot (arredi/traffico)|m",["DeckCentreHeightM"]="Quota massima centro impalcato dal suolo|m",["BridgeLengthM"]="Lunghezza totale ponte|m",["ApplicabilityConfirmed"]="Confermo applicabilità CNR G.11 e valutazione dinamica",["AssignedDragCoefficient"]="Assegna cfX (necessario per d/htot < 2)",["DragCoefficient"]="Coefficiente cfX",["AdjacentDeck"]="Due impalcati affiancati",["SecondWidthM"]="Larghezza secondo impalcato|m",["GapM"]="Interspazio netto|m",["CoupledDecks"]="Impalcati collegati strutturalmente",
        ["AssignedTemperatures"]="Assegna temperature dell’aria",["MinimumAirC"]="Temperatura minima all’ombra|°C",["MaximumAirC"]="Temperatura massima all’ombra|°C",["Method"]="Metodo termico",["Material"]="Materiale",["Exposed"]="Struttura esposta",["SimplifiedApplicabilityConfirmed"]="Azione termica non fondamentale: applicabile §3.5.5",["InitialTemperatureC"]="Temperatura iniziale T0|°C",["SummerOuterSurfaceC"]="Estate · superficie esterna|°C",["SummerInnerSurfaceC"]="Estate · superficie interna|°C",["WinterOuterSurfaceC"]="Inverno · superficie esterna|°C",["WinterInnerSurfaceC"]="Inverno · superficie interna|°C",["ProfileSource"]="Fonte del profilo e dei parametri termici",["AssignedExpansionCoefficient"]="Assegna coefficiente di dilatazione α",["ExpansionPerKelvin"]="Coefficiente di dilatazione α|1/K",["LengthM"]="Lunghezza efficace dal punto fisso|m",["ThicknessM"]="Spessore strato omogeneo|m",["SummerInnerAirC"]="Estate · aria interna|°C",["WinterInnerAirC"]="Inverno · aria interna|°C",["SolarIncrementC"]="Incremento estivo da radiazione solare|°C",["ConductivityWmK"]="Conduttività λ|W/(m K)",["InnerResistanceM2KW"]="Resistenza superficiale interna Rsi|m²K/W",["OuterResistanceM2KW"]="Resistenza superficiale esterna Rse|m²K/W",
        ["Deck"]="Tipo di impalcato",["Surfacing"]="Pavimentazione",["SurfacingMm"]="Spessore tabellato (50 / 100 / 150)|mm",["OmegaN"]="Simultaneità uniforme ωN",["OmegaM"]="Simultaneità gradiente ωM",["BearingSettingTemperatureKnown"]="Temperatura posa appoggi/giunti specificata",["HorizontalDifferenceC"]="Differenza trasversale orizzontale|°C",["AssignedBridgeTemperatures"]="Assegna temperature uniformi impalcato",["MinimumBridgeC"]="Temperatura minima impalcato|°C",["MaximumBridgeC"]="Temperatura massima impalcato|°C",["AssignedGradients"]="Assegna gradienti già corretti per pavimentazione",["HeatingDifferenceC"]="Sopra più caldo · ΔTM,heat|°C",["CoolingDifferenceC"]="Sopra più freddo · modulo ΔTM,cool|°C"
    };
    public static IReadOnlyList<ClimateFormGroup> Forms(string module,JsonObject document)
    {
        var input=document["input"]!.AsObject();var groups=new List<ClimateFormGroup>();
        void Group(string name,string path,JsonObject values,Type type)
        {
            var display=(JsonObject)values.DeepClone();var fields=new List<ClimateField>();
            foreach(var p in type.GetProperties().Where(p=>p.PropertyType.IsEnum||p.PropertyType==typeof(double)||p.PropertyType==typeof(int)||p.PropertyType==typeof(bool)||p.PropertyType==typeof(string)))
            {
                if(!Visible(p.Name,values))continue;
                var caption=Labels.TryGetValue(p.Name,out var label)?label:p.Name;
                if(p.Name=="WidthM")caption=module==BuildingWind?"Larghezza ortogonale al vento b|m":module==Snow?"Larghezza copertura b / inferiore b2|m":"Larghezza impalcato d|m";
                if(p.Name=="DepthM")caption=module==BuildingWind?"Profondità parallela al vento d|m":"Altezza strutturale impalcato h|m";
                if(p.Name=="NtcZone")caption=module is BuildingWind or BridgeWind?"Zona vento NTC (1…9)":"Zona termica NTC (1…4)";
                var parts=caption.Split('|');
                string[]? choices=p.PropertyType.IsEnum?Enum.GetNames(p.PropertyType).Select(n=>Captions[n]).ToArray():null;
                if(choices is not null && display[p.Name] is { } raw && Captions.TryGetValue(raw.ToString(),out var translated))display[p.Name]=translated;
                fields.Add(new(p.Name,parts[0],parts.Length>1?parts[1]:"",p.PropertyType==typeof(bool),p.PropertyType==typeof(double)||p.PropertyType==typeof(int),choices,caption.Replace('|',' ')));
            }
            groups.Add(new(name,path,display,fields));
        }
        Type type=DefaultInput(module).GetType();
        if(input["Site"] is JsonObject site)Group("Clima del sito","Site",site,type.GetProperty("Site")!.PropertyType);
        Group("Geometria, parametri e campo di applicazione","",input,type);return groups;
    }
    private static bool Visible(string key,JsonObject v)
    {
        string S(string k)=>v[k]?.ToString()??"";bool B(string k)=>v[k]?.ToString().Equals("true",StringComparison.OrdinalIgnoreCase)==true;
        return key switch
        {
            "ExpansionPerKelvin"=>B("AssignedExpansionCoefficient"),
            "NationalParameters" or "DirectionFactor" or "SeasonFactor" or "ProbabilityK" or "ProbabilityExponent" or "TurbulenceFactor"=>S("Standard")=="EurocodeFirstGeneration",
            "NtcZone" or "Zone"=>S("Standard")=="Ntc2018",
            "GroundLoadKnM2"=>B("AssignedGroundLoad"),"BaseSpeedMs"=>B("AssignedBaseSpeed"),"MinimumAirC" or "MaximumAirC"=>B("AssignedTemperatures"),
            "PitchRightDeg"=>S("Roof") is "Duopitch" or "MultiSpan", "RiseM"=>S("Roof")=="Cylindrical", "UpperWidthM" or "StepHeightM" or "SlidingCoefficient" or "SnowDensityKnM3"=>S("Roof")=="LowerRoof",
            "DominantOpeningRatio" or "DominantExternalCoefficient"=>S("InternalPressure")=="DominantFace", "InternalCoefficientMin" or "InternalCoefficientMax"=>S("InternalPressure")=="Assigned", "InternalReferenceHeightM"=>S("InternalPressure")!="DistributedOpenings",
            "DragCoefficient"=>B("AssignedDragCoefficient"), "SecondWidthM" or "GapM" or "CoupledDecks"=>B("AdjacentDeck"),
            "Exposed" or "SimplifiedApplicabilityConfirmed"=>S("Method")=="SimplifiedNtc",
            "SummerOuterSurfaceC" or "SummerInnerSurfaceC" or "WinterOuterSurfaceC" or "WinterInnerSurfaceC"=>S("Method")=="AssignedSurfaceProfile",
            "ProfileSource"=>S("Method")!="SimplifiedNtc",
            "SummerInnerAirC" or "WinterInnerAirC" or "SolarIncrementC" or "ConductivityWmK" or "InnerResistanceM2KW" or "OuterResistanceM2KW"=>S("Method")=="HomogeneousLayer",
            "MinimumBridgeC" or "MaximumBridgeC"=>B("AssignedBridgeTemperatures"), "HeatingDifferenceC" or "CoolingDifferenceC"=>B("AssignedGradients"), "SurfacingMm"=>S("Surfacing")=="Asphalt"&&!B("AssignedGradients"), _=>true
        };
    }
    public static void ApplyField(JsonObject document,ClimateFormGroup group,string key)
    {
        JsonObject target=group.Path.Length==0?document["input"]!.AsObject():document["input"]![group.Path]!.AsObject();
        JsonNode? value=group.Values[key]?.DeepClone();
        if(group.Fields.First(f=>f.Key==key).Choices is not null && value is not null)
            value=JsonValue.Create(Captions.FirstOrDefault(p=>p.Value==value.ToString()).Key??value.ToString());
        target[key]=value;
    }
    public static string Csv(JsonObject result)
    {
        var b=new StringBuilder("Caso;Simbolo;Descrizione;Valore;Unita;Formula;Riferimento\r\n");
        string Q(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        void Add(string name,JsonArray? values){foreach(var v in values??[])b.AppendLine(string.Join(";",new[]{name,v!["Symbol"]?.ToString()??"",v["Description"]?.ToString()??"",v["Value"]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),v["Unit"]?.ToString()??"",v["Formula"]?.ToString()??"",v["Reference"]?.ToString()??""}.Select(Q)));}
        Add("Parametri",result["Parameters"]?.AsArray());foreach(var c in result["Cases"]?.AsArray()??[])Add(c!["Name"]!.ToString(),c["Values"]?.AsArray());return b.ToString();
    }
}

public sealed class ClimateModuleService(string module) : IModuleService
{
    public string ModuleId=>module;
    public JsonObject Prepare(JsonObject source){ClimateDocument.Validate(module,source);return (JsonObject)source.DeepClone();}
    public ModuleCalculation Calculate(JsonObject source,CancellationToken cancellation=default){var data=Prepare(source);return new(module,data,ClimateDocument.Calculate(module,data,cancellation));}
}
