using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthea.Application.Modules;
using GPC.Design.Actions.Climate;
using GPC.Model.Actions.Climate;

namespace Anthea.Application.Actions;

public sealed record ClimateField(string Key, string Label, string Unit, bool Boolean, bool Numeric, string[]? Choices, string Help, bool Integer=false);
public sealed record ClimateFormGroup(string Name, string Path, JsonObject Values, IReadOnlyList<ClimateField> Fields);

/// <summary>Versioned GPC input document, UI projections and application dispatch. Engineering stays in GPC.Design.</summary>
public static partial class ClimateDocument
{
    public const string Snow = "act_neve", BuildingWind = "act_vento_edifici", BuildingThermal = "act_temperatura_edifici", BridgeWind = "act_vento_ponti", BridgeThermal = "act_temperatura_ponti";
    public static IReadOnlyList<ModuleInfo> Modules { get; } = new[]
    {
        Info(Snow,"Neve sugli edifici","NTC / EN · neve al suolo, coperture e accumuli"),
        Info(BuildingWind,"Vento sugli edifici","NTC / EN · clima, pareti, coperture e pressioni interne"),
        Info(BuildingThermal,"Temperatura negli edifici","NTC / EN · componente uniforme e profilo termico"),
        Info(BridgeWind,"Vento sui ponti","CNR-DT 207 R1/2018 · impalcati isolati e affiancati"),
        Info(BridgeThermal,"Temperatura nei ponti","NTC / EN · impalcati, gradienti, appoggi e giunti")
    }.Concat(AdditionalModules).Concat(ExtendedModules).ToArray();
    private static ModuleInfo Info(string id,string name,string description)=>new(id,"Azioni",name,description,"Azione",name,"Azioni climatiche");
    public static bool Supports(string module)=>Modules.Any(m=>m.Id==module);
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() }, NumberHandling=JsonNumberHandling.AllowReadingFromString, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    private static object DefaultInput(string module)=>module switch
    {
        Snow=>new SnowInput(), BuildingWind=>new BuildingWindInput(), BuildingThermal=>new BuildingThermalInput(), BridgeWind=>new BridgeWindInput(), BridgeThermal=>new BridgeThermalInput(), _=>AdditionalInput(module)
    };
    public static JsonObject Create(string module)=>new() { ["climate_schema"]=1, ["input_type"]=DefaultInput(module).GetType().Name, ["input"]=JsonSerializer.SerializeToNode(DefaultInput(module),Options) };
    private static object Read(string module,JsonObject document)
    {
        if(document["climate_schema"]?.GetValue<int>()!=1)throw new ArgumentException("Versione del documento Azioni non supportata.");
        Type type=DefaultInput(module).GetType();
        if(document["input_type"]?.ToString()!=type.Name || document["input"] is not JsonObject input)throw new ArgumentException("Contratto GPC del foglio Azioni non coerente con il modulo.");
        try {return input.Deserialize(type,Options)??throw new ArgumentException("Input climatico mancante.");}
        catch(JsonException e){throw new ArgumentException(InvalidValueMessage(module,document,e.Path),e);}
    }
    // Structure of the document as Read. A number field holding text (typed by the user, kept in the document as in the other modules) is
    // accepted here and reported by the field and by the calculation, so that the sheet can still be confirmed, saved and reopened.
    public static void Validate(string module,JsonObject document)
    {
        var copy=(JsonObject)document.DeepClone();
        for(int attempt=0;;attempt++)
        {
            try{Read(module,copy);return;}
            catch(ArgumentException e) when(e.InnerException is JsonException json&&attempt<MaximumTextNumbers&&TextNumber(module,copy,json.Path) is { } text)
            {text.ReplaceWith(0);}
        }
    }
    private const int MaximumTextNumbers=1000;
    private static readonly System.Text.RegularExpressions.Regex PathSegment=new(@"\.([^.\[]+)|\[(\d+)\]",System.Text.RegularExpressions.RegexOptions.Compiled);
    // The JSON value at the serializer path of the input when a number property of the GPC contract holds what the user typed and is not
    // its number: a text, or a decimal in an integer property. Null otherwise (a structural error of the document).
    private static JsonValue? TextNumber(string module,JsonObject document,string? path)
    {
        if(path is null||document["input"] is not JsonObject input)return null;
        Type? type=DefaultInput(module).GetType();JsonNode? node=input;
        foreach(System.Text.RegularExpressions.Match m in PathSegment.Matches(path))
        {
            if(m.Groups[1].Success){type=type?.GetProperty(m.Groups[1].Value)?.PropertyType;node=(node as JsonObject)?[m.Groups[1].Value];}
            else{type=ElementType(type);node=(node as JsonArray)?.ElementAtOrDefault(int.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture));}
        }
        type=type is null?null:Nullable.GetUnderlyingType(type)??type;
        bool integer=type==typeof(int)||type==typeof(long),number=integer||type==typeof(double)||type==typeof(float)||type==typeof(decimal);
        if(!number||node is not JsonValue value)return null;
        if(value.GetValueKind()==JsonValueKind.String)
            return (integer?int.TryParse(value.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out _):double.TryParse(value.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out _))?null:value;
        return integer&&value.GetValueKind()==JsonValueKind.Number&&!value.TryGetValue<int>(out _)&&!value.TryGetValue<long>(out _)?value:null;
    }
    private static Type? ElementType(Type? type)=>type is null?null:type.IsArray?type.GetElementType():type.GetInterfaces().Concat([type]).FirstOrDefault(i=>i.IsGenericType&&i.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GetGenericArguments()[0];
    // The message of an invalid value with the label of its field (and table row), as the user sees them, instead of the serializer path.
    private static string InvalidValueMessage(string module,JsonObject document,string? path)
    {
        var segments=PathSegment.Matches(path??"").Select(m=>m.Groups[1].Success?m.Groups[1].Value:"#"+m.Groups[2].Value).ToArray();
        string? label=null,place="";
        try
        {
            if(segments.Length>=3&&segments[1].StartsWith('#')&&Tables(document).FirstOrDefault(t=>t.Key==segments[0]) is { } table&&table.Rows.ElementAtOrDefault(int.Parse(segments[1][1..],CultureInfo.InvariantCulture)) is JsonObject row)
            {
                label=RowForm(module,table.Key,row,table.Context).Fields.FirstOrDefault(f=>f.Key==segments[2])?.Label;
                place=table.Title+" · "+(row["Name"]?.ToString()??"riga "+(int.Parse(segments[1][1..],CultureInfo.InvariantCulture)+1))+" · ";
            }
            else if(segments.Length>0)
                label=Forms(module,document).Where(g=>g.Path==(segments.Length>1?segments[0]:"")).SelectMany(g=>g.Fields).FirstOrDefault(f=>f.Key==segments[^1])?.Label;
        }
        catch(Exception){label=null;}// the message of the error never hides the error itself
        label??=segments.Length==0?"Dato del foglio":Labels.TryGetValue(segments[^1],out var caption)?caption.Split('|')[0]:segments[^1];
        if(TextNumber(module,document,path) is not { } text)return place+label+": valore non valido.";
        string shown=text.GetValueKind()==JsonValueKind.Number?text.GetValue<double>().ToString(CultureInfo.CurrentCulture):text.ToString();
        return place+label+": «"+shown+"» "+(text.GetValueKind()==JsonValueKind.Number?"non è un numero intero; inserire un intero.":"non è un numero; inserire un valore numerico.");
    }
    public static JsonObject Calculate(string module,JsonObject document,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested(); object input=Read(module,document);
        if(module==Distribution)return CalculateDistribution((GPC.Model.Actions.Traffic.TransverseDistributionInput)input,cancellation);
        if(IsExtended(module))return CalculateExtended(input);
        ClimateResult result=input switch
        {
            SnowInput x=>SnowActions.Calculate(x), BuildingWindInput x=>BuildingWindActions.Calculate(x), BuildingThermalInput x=>ThermalActions.Building(x), BridgeWindInput x=>BridgeWindActions.Calculate(x), BridgeThermalInput x=>ThermalActions.Bridge(x), _=>CalculateAdditional(input,cancellation)
        };
        cancellation.ThrowIfCancellationRequested(); return JsonSerializer.SerializeToNode(result,Options)!.AsObject();
    }
    // Enum captions are a UI projection only. Documents retain the GPC contract names.
    private static readonly FrozenDictionary<string,string> Captions = new Dictionary<string,string>()
    {
        ["Ntc2018"]="Italia · NTC 2018",["EurocodeFirstGeneration"]="Eurocodice 1 · prima generazione",
        ["AlpineI"]="I · Alpina",["MediterraneanI"]="I · Mediterranea",["II"]="II",["III"]="III",
        ["Normal"]="Normale",["Windswept"]="Battuta dai venti",["Sheltered"]="Riparata",
        ["Flat"]="Piana",["Monopitch"]="Falda singola",["Duopitch"]="Due falde",["MultiSpan"]="Falde multiple",["Cylindrical"]="Cilindrica",["LowerRoof"]="Copertura inferiore presso salto",
        ["DistributedOpenings"]="Aperture distribuite",["DominantFace"]="Faccia dominante",["Assigned"]="Coefficienti assegnati",
        ["SimplifiedNtc"]="Semplificato NTC §3.5.5",["AssignedSurfaceProfile"]="Temperature di superficie assegnate",["HomogeneousLayer"]="Trasmissione in strato omogeneo",
        ["ReinforcedConcrete"]="Cemento armato",["Steel"]="Acciaio",["SteelBox"]="Cassone in acciaio",["SteelTrussOrPlate"]="Travi in acciaio / reticolare",["Composite"]="Composto acciaio-calcestruzzo",["ConcreteBox"]="Cassone in calcestruzzo",["ConcreteBeam"]="Travi in calcestruzzo",["ConcreteSlab"]="Soletta in calcestruzzo",
        ["Unsurfaced"]="Senza pavimentazione",["Waterproofed"]="Impermeabilizzato",["Asphalt"]="Pavimentazione 50 / 100 / 150 mm",["Ballast750"]="Ballast 750 mm"
    }.ToFrozenDictionary();
    private static readonly FrozenDictionary<string,string> Labels = new Dictionary<string,string>()
    {
        ["Standard"]="Normativa",["NationalParameters"]="Paese, appendice nazionale e fonte dei parametri",["NtcZone"]="Zona NTC (vedere guida)",["Zone"]="Zona neve NTC",["AltitudeM"]="Quota sul mare|m",["AssignedGroundLoad"]="Assegna il carico al suolo",["GroundLoadKnM2"]="Neve al suolo sk|kN/m²",["Exposure"]="Esposizione della copertura",["ThermalCoefficient"]="Coefficiente termico Ct",["ThermalJustification"]="Motivazione dello studio per Ct",
        ["Roof"]="Geometria della copertura",["PitchLeftDeg"]="Inclinazione falda sinistra/superiore|°",["PitchRightDeg"]="Inclinazione falda destra|°",["SnowGuards"]="Fermaneve / scivolamento impedito",["WidthM"]="Larghezza b / d impalcato|m",["RiseM"]="Freccia copertura cilindrica|m",["UpperWidthM"]="Larghezza copertura superiore b1|m",["StepHeightM"]="Dislivello h|m",["SlidingCoefficient"]="Coefficiente di scivolamento μs",["SnowDensityKnM3"]="Peso specifico neve γ|kN/m³",
        ["AssignedBaseSpeed"]="Assegna velocità base",["BaseSpeedMs"]="Velocità base vb (EN: vb,0)|m/s",["ReturnYears"]="Periodo di ritorno TR|anni",["ExposureCategory"]="Categoria: NTC 1…5; EN 0…4",["TopographyFactor"]="Topografia ct / orografia co",["DirectionFactor"]="Direzionalità cdir",["SeasonFactor"]="Stagionalità cseason",["ProbabilityK"]="Parametro probabilità K",["ProbabilityExponent"]="Esponente probabilità n",["TurbulenceFactor"]="Turbolenza kI",["AirDensityKgM3"]="Densità dell’aria ρ|kg/m³",
        ["DepthM"]="Profondità edificio / altezza impalcato|m",["HeightM"]="Altezza massima edificio h|m",["PitchDeg"]="Inclinazione falde|°",["WindDirectionDeg"]="Vento rispetto al colmo: 0 / 90 / 180|°",["ParapetM"]="Altezza parapetto hp|m",["LoadedAreaM2"]="Area caricata dell’elemento A|m²",["InternalPressure"]="Pressione interna",["DominantOpeningRatio"]="Rapporto aperture dominante / altre",["DominantExternalCoefficient"]="cpe medio alle aperture dominanti",["InternalCoefficientMin"]="Coefficiente interno minimo",["InternalCoefficientMax"]="Coefficiente interno massimo",["InternalReferenceHeightM"]="Quota di riferimento interna zi|m",["StructuralFactor"]="Coefficiente dinamico cd / cs·cd",["StructuralFactorAssessed"]="Coefficiente strutturale valutato per il caso",["FrictionCoefficient"]="Coefficiente d’attrito cf",
        ["TotalHeightM"]="Ingombro totale htot (arredi/traffico)|m",["DeckCentreHeightM"]="Quota massima centro impalcato dal suolo|m",["BridgeLengthM"]="Lunghezza totale ponte|m",["ApplicabilityConfirmed"]="Confermo applicabilità CNR G.11 e valutazione dinamica",["AssignedDragCoefficient"]="Assegna cfX (necessario per d/htot < 2)",["DragCoefficient"]="Coefficiente cfX",["AdjacentDeck"]="Due impalcati affiancati",["SecondWidthM"]="Larghezza secondo impalcato|m",["GapM"]="Interspazio netto|m",["CoupledDecks"]="Impalcati collegati strutturalmente",
        ["AssignedTemperatures"]="Assegna temperature dell’aria",["MinimumAirC"]="Temperatura minima all’ombra|°C",["MaximumAirC"]="Temperatura massima all’ombra|°C",["Method"]="Metodo termico",["Material"]="Materiale",["Exposed"]="Struttura esposta",["SimplifiedApplicabilityConfirmed"]="Azione termica non fondamentale: applicabile §3.5.5",["InitialTemperatureC"]="Temperatura iniziale T0|°C",["SummerOuterSurfaceC"]="Estate · superficie esterna|°C",["SummerInnerSurfaceC"]="Estate · superficie interna|°C",["WinterOuterSurfaceC"]="Inverno · superficie esterna|°C",["WinterInnerSurfaceC"]="Inverno · superficie interna|°C",["ProfileSource"]="Fonte del profilo e dei parametri termici",["AssignedExpansionCoefficient"]="Assegna coefficiente di dilatazione α",["ExpansionPerKelvin"]="Coefficiente di dilatazione α|1/K",["LengthM"]="Lunghezza efficace dal punto fisso|m",["ThicknessM"]="Spessore strato omogeneo|m",["SummerInnerAirC"]="Estate · aria interna|°C",["WinterInnerAirC"]="Inverno · aria interna|°C",["SolarIncrementC"]="Incremento estivo da radiazione solare|°C",["ConductivityWmK"]="Conduttività λ|W/(m K)",["InnerResistanceM2KW"]="Resistenza superficiale interna Rsi|m²K/W",["OuterResistanceM2KW"]="Resistenza superficiale esterna Rse|m²K/W",
        ["Deck"]="Tipo di impalcato",["Surfacing"]="Pavimentazione",["SurfacingMm"]="Spessore tabellato (50 / 100 / 150)|mm",["OmegaN"]="Simultaneità uniforme ωN",["OmegaM"]="Simultaneità gradiente ωM",["BearingSettingTemperatureKnown"]="Temperatura posa appoggi/giunti specificata",["HorizontalDifferenceC"]="Differenza trasversale orizzontale|°C",["AssignedBridgeTemperatures"]="Assegna temperature uniformi impalcato",["MinimumBridgeC"]="Temperatura minima impalcato|°C",["MaximumBridgeC"]="Temperatura massima impalcato|°C",["AssignedGradients"]="Assegna gradienti già corretti per pavimentazione",["HeatingDifferenceC"]="Sopra più caldo · ΔTM,heat|°C",["CoolingDifferenceC"]="Sopra più freddo · modulo ΔTM,cool|°C"
    }.ToFrozenDictionary();
    public static IReadOnlyList<ClimateFormGroup> Forms(string module,JsonObject document)
    {
        var input=document["input"]!.AsObject();var groups=new List<ClimateFormGroup>();
        void Group(string name,string path,JsonObject values,Type type)
        {
            var display=(JsonObject)values.DeepClone();var fields=new List<ClimateField>();
            foreach(var p in type.GetProperties().Where(p=>p.PropertyType.IsEnum||p.PropertyType==typeof(double)||p.PropertyType==typeof(int)||p.PropertyType==typeof(bool)||p.PropertyType==typeof(string)))
            {
                if(IsExtended(module)?!ExtendedVisible(module,p.Name,values):!Visible(p.Name,values)||!AdditionalVisible(module,p.Name,values))continue;
                var caption=ExtendedLabel(module,p.Name,values)??ExtraLabel(module,p.Name)??(Labels.TryGetValue(p.Name,out var label)?label:p.Name);
                if(p.Name=="WidthM"&&!IsExtended(module))caption=module==BuildingWind?"Larghezza ortogonale al vento b|m":module==Snow?"Larghezza copertura b / inferiore b2|m":"Larghezza impalcato d|m";
                if(p.Name=="DepthM"&&!IsExtended(module))caption=module==BuildingWind?"Profondità parallela al vento d|m":"Altezza strutturale impalcato h|m";
                if(p.Name=="NtcZone")caption=module is BuildingWind or BridgeWind?"Zona vento NTC (1…9)":"Zona termica NTC (1…4)";
                var parts=caption.Split('|');
                string[]? choices=p.PropertyType.IsEnum?FieldEnumNames(module,p.Name,p.PropertyType).Select(name=>UiCaption(module,name)).ToArray():null;
                if(choices is not null && display[p.Name] is { } raw)display[p.Name]=UiCaption(module,raw.ToString());
                fields.Add(new(p.Name,parts[0],parts.Length>1?parts[1]:"",p.PropertyType==typeof(bool),p.PropertyType==typeof(double)||p.PropertyType==typeof(int),choices,caption.Replace('|',' '),p.PropertyType==typeof(int)));
            }
            groups.Add(new(name,path,display,fields));
        }
        Type type=DefaultInput(module).GetType();
        if(input["Site"] is JsonObject site)Group("Clima del sito","Site",site,type.GetProperty("Site")!.PropertyType);
        Group("Geometria, parametri e campo di applicazione","",input,type);return groups;
    }
    public static IReadOnlyList<string[]> ReportInputs(string module,JsonObject document)
    {
        var rows=new List<string[]>();
        void Add(ClimateFormGroup group,string prefix)
        {
            foreach(var field in group.Fields)
            {
                var value=group.Values[field.Key];string text=value?.ToString()??"";
                if(field.Boolean)text=text.Equals("true",StringComparison.OrdinalIgnoreCase)?"Sì":"No";
                else if(field.Numeric&&double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double number))
                    text=number.ToString("G10",CultureInfo.GetCultureInfo("it-IT"));
                rows.Add([prefix+field.Label,text+(field.Unit.Length==0?"":" "+field.Unit)]);
            }
        }
        foreach(var group in Forms(module,document))Add(group,group.Path.Length==0?"":"Sito · ");
        foreach(var table in Tables(document))
            foreach(var row in table.Rows.OfType<JsonObject>())
                Add(RowForm(module,table.Key,row,table.Context),table.Title+" · "+row["Name"]+" · ");
        return rows;
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
        if(value is not null&&group.Fields.First(f=>f.Key==key).Choices is not null)value=JsonValue.Create(Uncaption(value.ToString()));
        target[key]=value;
    }
    public static string Csv(JsonObject result)
    {
        var b=new StringBuilder("Caso;Simbolo;Descrizione;Valore;Unita;Formula;Riferimento\r\n");
        string Q(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        void Add(string name,JsonArray? values){foreach(var v in values??[])b.AppendLine(string.Join(";",new[]{name,v!["Symbol"]?.ToString()??"",v["Description"]?.ToString()??"",v["Value"]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),v["Unit"]?.ToString()??"",v["Formula"]?.ToString()??"",v["Reference"]?.ToString()??""}.Select(Q)));}
        Add("Parametri",result["Parameters"]?.AsArray());foreach(var c in result["Cases"]?.AsArray()??[])Add(c!["Name"]!.ToString(),c["Values"]?.AsArray());
        if(result["Distribution"] is JsonObject distribution)
        {
            var names=distribution["Cases"]!.AsArray().Last()!["Girders"]!.AsArray().Select(g=>g!["Name"]!.ToString()).ToArray();
            foreach(var point in distribution["Influence"]!.AsArray())
            {
                string y=point!["PositionM"]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture);
                var ordinates=point["MomentOrdinatesM"]!.AsArray();
                for(int i=0;i<ordinates.Count;i++)b.AppendLine(string.Join(";",new[]{"Influenza y="+y+" m",names[i],"Momento per forza unitaria alla x dichiarata",ordinates[i]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),"m","M / Punit","Modello meccanico assegnato"}.Select(Q)));
            }
        }
        if(result["Prestress"]?["Stages"] is JsonArray prestressStages)
            foreach(var stage in prestressStages)
                foreach(var point in stage!["Points"]!.AsArray())
                    foreach(string field in new[]{"JackingStressMpa","AfterFrictionMpa","AfterAnchoringMpa","ShortRelaxationLossMpa","ElasticLossMpa","InitialStressMpa","DeferredLossMpa","EffectiveStressMpa","EffectiveForceKn","EffectivePrestrain"})
                        b.AppendLine(string.Join(";",new[]{stage["Name"]!.ToString(),field,"x = "+point!["XM"]+" m",point[field]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),field.EndsWith("Mpa")?"MPa":field.EndsWith("Kn")?"kN":"−","Profilo calcolato","GPC.Design · precompressione"}.Select(Q)));
        if(result["TimeEffects"]?["History"] is JsonArray history)
            foreach(var point in history)
                foreach(string field in new[]{"FreeStrain","ActualStrain","StressMpa","FreeDisplacementMm","ActualDisplacementMm","EcmMpa"})
                    b.AppendLine(string.Join(";",new[]{"Storia temporale",field,"Età = "+point!["AgeDays"]+" giorni",point[field]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),field.EndsWith("Mpa")?"MPa":field.EndsWith("Mm")?"mm":"−","Ordinata calcolata","GPC.Design · effetti differiti"}.Select(Q)));
        if(result["Fire"] is JsonObject fire)
        {
            foreach(var gas in fire["GasHistory"]!.AsArray())b.AppendLine(string.Join(";",new[]{"Incendio · gas",gas!["TimeMinutes"]!.ToString(),"TemperatureC",gas["TemperatureC"]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),"°C","Tempo in minuti","GPC.Design · incendio"}.Select(Q)));
            foreach(var face in fire["Faces"]!.AsArray())foreach(var point in face!["History"]!.AsArray())foreach(string field in new[]{"GasC","SurfaceC","ConvectionWm2","RadiationWm2","NetWm2"})
                if(point![field] is JsonValue value)b.AppendLine(string.Join(";",new[]{face["Name"]!.ToString(),point["TimeMinutes"]!.ToString(),field,value.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),field is "GasC" or "SurfaceC"?"°C":"W/m²","Temperatura prescritta / flusso al contorno","GPC.Design · incendio"}.Select(Q)));
        }
        if(result["CraneGroups"] is JsonArray craneGroups)
            foreach(var group in craneGroups)
                foreach(var point in group!["Points"]!.AsArray())
                    foreach(string field in new[]{"XM","YM","VerticalDownKn","LongitudinalKn","TransverseKn","EccentricTorsionKnM"})
                        b.AppendLine(string.Join(";",new[]{group["Name"]!.ToString(),point!["Name"]!.ToString(),field,point[field]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),field is "XM" or "YM"?"m":field=="EccentricTorsionKnM"?"kN·m":"kN","Azioni puntuali del gruppo compatibile","GPC.Design · carriponte"}.Select(Q)));
        if(result["ConstructionStages"] is JsonArray phases)
            foreach(var phase in phases)
                foreach(var load in phase!["Loads"]!.AsArray())
                    foreach(var patch in load!["Patches"]!.AsArray())
                        foreach(string field in new[]{"LeftXM","BottomYM","LengthM","WidthM","PressureKpa"})
                            b.AppendLine(string.Join(";",new[]{phase["Name"]!.ToString(),load["Name"]+" · "+patch!["Name"],field,patch[field]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),field=="PressureKpa"?"kPa":"m","Geometria e pressione della zona","GPC.Design · carichi di esecuzione"}.Select(Q)));
        if(result["Hydraulics"] is JsonObject hydraulics)
            foreach(var c in hydraulics["Cases"]!.AsArray())
                foreach(var p in c!["Diagram"]!.AsArray())
                    foreach(string field in new[]{"PositivePressureKpa","OpposingPressureKpa","NetPressureKpa","NetLineLoadKnM"})
                        b.AppendLine(string.Join(";",new[]{c["Name"]!.ToString(),field,"Coordinata = "+p!["PositionM"]!.ToString()+" m",p[field]!.GetValue<double>().ToString("G17",CultureInfo.InvariantCulture),field=="NetLineLoadKnM"?"kN/m":"kPa","Ordinata del diagramma","GPC.Design · modello idraulico"}.Select(Q)));
        return b.ToString();
    }
}

public sealed class ClimateModuleService(string module) : IModuleService
{
    public string ModuleId=>module;
    public JsonObject Prepare(JsonObject source){ClimateDocument.Validate(module,source);return (JsonObject)source.DeepClone();}
    public ModuleCalculation Calculate(JsonObject source,CancellationToken cancellation=default){var data=Prepare(source);return new(module,data,ClimateDocument.Calculate(module,data,cancellation));}
}
