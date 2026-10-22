using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;

namespace Anthea.FoundationWorkspace;

/// <summary>Document and units adapter only. Engineering algorithms remain in GPCChecker.Geotechnics.</summary>
public static class FoundationDocument
{
    public const string Module = "geo_fondazioni_superficiali";
    public static readonly ImmutableArray<string> Methods = ["Eurocodice 7 · allegato D", "Terzaghi · Nγ tabulato", "Meyerhof", "Brinch Hansen · carico normale", "Vesić"];
    public static readonly ImmutableArray<string> Shapes = ["Rettangolare / quadrata", "Nastriforme · azioni per metro", "Circolare · carico centrato"];
    public static readonly ImmutableArray<string> Conditions = ["Drenata", "Non drenata"];
    public static readonly ImmutableArray<string> Profiles = ["NTC 2018 · A1+M1+R3", "Confronto caratteristico"];
    public static readonly ImmutableArray<string> Compressions = ["Rottura generale", "Riduzione Terzaghi · taglio locale", "Riduzione Vesić · densità relativa"];
    // Factor maps UI m/kN/kPa/degrees to the library mm/N/MPa/radians contract.
    public static readonly ImmutableArray<(string Key,string Label,string Group,double Default,double Factor)> Fields = [
        ("Width","Larghezza B [m]","Geometria",2,1000), ("Length","Lunghezza L [m]","Geometria",3,1000),
        ("Depth","Profondità D [m]","Geometria",1,1000),
        ("FrictionAngle","Angolo φ′ [°]","Strato portante",30,Math.PI/180), ("Cohesion","Coesione c′ [kPa]","Strato portante",0,.001),
        ("UndrainedStrength","Resistenza cu [kPa]","Strato portante",75,.001),
        ("UnitWeight","γ sotto la base [kN/m³]","Strato portante",18,1e-6), ("SaturatedUnitWeight","γsat sotto la base [kN/m³]","Strato portante",20,1e-6),
        ("CoverUnitWeight","γ sopra la base [kN/m³]","Ricoprimento",18,1e-6), ("CoverSaturatedUnitWeight","γsat sopra la base [kN/m³]","Ricoprimento",20,1e-6),
        ("WaterDepth","Profondità falda dal piano campagna [m]","Falda",3,1000), ("WaterUnitWeight","γ acqua [kN/m³]","Falda",9.81,1e-6),
        ("Surcharge","Sovraccarico esterno al piano campagna [kPa]","Ricoprimento",0,.001),
        ("RelativeDensity","Densità relativa Dr [0–1]","Strato portante",.5,1),
        ("Normal","N al piano di posa [kN]","Azioni",600,1000),
        ("HorizontalX","Hx parallela a B [kN]","Azioni",0,1000), ("HorizontalY","Hy parallela a L [kN]","Azioni",0,1000),
        ("MomentX","Mx · eccentricità lungo L [kNm]","Azioni",0,1e6), ("MomentY","My · eccentricità lungo B [kNm]","Azioni",0,1e6)
    ];
    public static JsonObject Defaults()
    {
        var input=new JsonObject();foreach(var f in Fields) input[f.Key]=f.Default;
        input["Method"]=0;input["Shape"]=0;input["Condition"]=0;input["Profile"]=0;input["Compression"]=0;
        input["WaterEnabled"]=false;input["RoughBase"]=true;input["IncludeDepthFactors"]=true;
        var document=new JsonObject { ["versione_fondazioni"]=2,["input"]=input,["nome_combinazione"]="SLU · A1",["azioni_confermate"]=false };
        FoundationMaterials.Complete(document);return document;
    }
    public static void ValidateShape(JsonObject data)
    {
        if (data["versione_fondazioni"] is not JsonValue version || !version.TryGetValue<int>(out int v) || v is not (1 or 2) || data["input"] is not JsonObject)
            throw new ArgumentException("Documento delle fondazioni superficiali non valido o versione non supportata.");
        if(v==2 && data["stratigrafie"] is null)throw new ArgumentException("Documento versione 2: profilo terreno comune mancante.");
        var input=data["input"]!.AsObject();
        foreach(var field in Fields)
            if(input[field.Key] is not JsonValue)throw new ArgumentException("Campo mancante o non valido: "+field.Label);
        foreach(var (key,count) in new[]{("Method",Methods.Length),("Shape",Shapes.Length),("Condition",Conditions.Length),("Profile",Profiles.Length),("Compression",Compressions.Length)})
            _=Choice(input,key,count);
        foreach(string key in new[]{"WaterEnabled","RoughBase","IncludeDepthFactors"})
            if(input[key] is not JsonValue flag || !flag.TryGetValue<bool>(out _))throw new ArgumentException("Opzione non valida: "+key);
        if(data["azioni_confermate"] is not JsonValue confirm || !confirm.TryGetValue<bool>(out _))throw new ArgumentException("Conferma delle azioni non valida.");
        if(data["stratigrafie"] is not null)
        {
            if(data["stratigrafie"] is not JsonArray {Count:>0 and <=50} surveys || surveys.Any(s=>s is not JsonArray rows || rows.Count>50 || rows.Any(r=>r is not JsonObject)))
                throw new ArgumentException("Struttura dei sondaggi non valida.");
            if(data["generali"] is not JsonObject general || data["sezione"] is not JsonObject)throw new ArgumentException("Dati comuni mancanti.");
            _=Choice(general,"sondaggio_fondazione",surveys.Count);_=FoundationMaterials.Flag(general,"presenza_falda");_=FoundationMaterials.Flag(general,"strato_portante_semiinfinito");
        }
    }
    public static double Number(JsonNode? value,string label)
    {
        string text=value?.ToString() ?? "";
        if (!double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n) || !double.IsFinite(n))
            throw new ArgumentException(label+": inserire un numero finito.");
        return n;
    }
    public static int Choice(JsonObject input,string key,int count)
    {
        double n=Number(input[key],key);
        if (n != Math.Truncate(n) || n<0 || n>=count) throw new ArgumentException("Scelta non valida: "+key);
        return (int)n;
    }
    public static JsonObject LibraryInput(JsonObject data)
    {
        ValidateShape(data);var input=data["input"]!.AsObject();var mapped=new JsonObject();
        foreach(var f in Fields)
        {
            if(data["stratigrafie"] is not null && (FoundationMaterials.LegacySoilKeys.Contains(f.Key)||f.Key=="WaterDepth"))continue;
            double value=Number(input[f.Key],f.Label)*f.Factor;
            if(!double.IsFinite(value))throw new ArgumentException(f.Label+": valore fuori dall'intervallo numerico rappresentabile.");
            mapped[f.Key]=value;
        }
        foreach(var (key,count) in new[]{("Method",Methods.Length),("Shape",Shapes.Length),("Condition",Conditions.Length),("Profile",Profiles.Length),("Compression",Compressions.Length)})
            mapped[key]=Choice(input,key,count);
        foreach(string key in new[]{"WaterEnabled","RoughBase","IncludeDepthFactors"})
        {
            if (input[key] is not JsonValue v || !v.TryGetValue<bool>(out bool flag)) throw new ArgumentException("Opzione non valida: "+key);
            mapped[key]=flag;
        }
        if (Choice(input,"Profile",Profiles.Length)==0 && data["azioni_confermate"]?.GetValue<bool>()!=true)
            throw new ArgumentException("Confermare azioni A1 già combinate al piano di posa: N efficace nel drenato, totale nel non drenato; includere tutti i pesi e le pertinenti azioni idrauliche.");
        if (string.IsNullOrWhiteSpace(data["nome_combinazione"]?.ToString())) throw new ArgumentException("Assegnare un nome alla combinazione.");
        if(data["stratigrafie"] is not null)mapped["HomogeneousHalfSpace"]=FoundationMaterials.Flag(data["generali"]!,"strato_portante_semiinfinito");
        return mapped;
    }
    public static JsonObject Calculate(JsonObject data) => new FoundationLibrary().Calculate(data);
}

/// <summary>
/// Versioned optional API boundary: the installed, manifest-checked snapshot may predate this module.
/// There is no alternative calculation engine. Once the approved GPC release is installed, the same
/// document uses its typed API. An unavailable API produces an explicit error, never a computed result.
/// </summary>
public sealed class FoundationLibrary
{
    readonly Assembly assembly;
    const string Prefix="GPC.Checkers.Geotechnics.Foundations.";
    public FoundationLibrary(Assembly? library=null) => assembly=library ?? typeof(GPC.Checkers.Geotechnics.Foundations.ShallowFoundationSeismic).Assembly;
    public bool Available => assembly.GetType(Prefix+"ShallowFoundationBearing")?.GetField("ContractVersion")?.GetRawConstantValue() as string == "2"
        && assembly.GetType(Prefix+"ShallowFoundationInput")?.GetProperty("SoilProfile") is not null;
    public JsonObject Calculate(JsonObject data)
    {
        var mapped=FoundationDocument.LibraryInput(data);
        if (!Available) throw new NotSupportedException("Il foglio richiede il nuovo rilascio approvato di GPCChecker.Geotechnics. Il calcolo non è disponibile con la libreria attualmente installata.");
        var inputType=assembly.GetType(Prefix+"ShallowFoundationInput",true)!;
        var engine=assembly.GetType(Prefix+"ShallowFoundationBearing",true)!;
        object input=JsonSerializer.Deserialize(mapped.ToJsonString(),inputType)!;
        if(FoundationMaterials.Profile(data) is {} profile)
        {
            var property=inputType.GetProperty("SoilProfile")??throw new NotSupportedException("La libreria installata non supporta ancora il profilo terreno comune: aggiornare il rilascio approvato.");
            property.SetValue(input,profile);
        }
        object result;
        try { result=engine.GetMethod("Calculate",[inputType])!.Invoke(null,[input])!; }
        catch(TargetInvocationException e) when(e.InnerException is not null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw; }
        var output=JsonSerializer.SerializeToNode(result,result.GetType())!.AsObject();
        output["dati"]=data.DeepClone();output["library_version"]=assembly.GetName().Version?.ToString();
        output["modulo_id"]=FoundationDocument.Module;output["errore"]="";
        return output;
    }
}
