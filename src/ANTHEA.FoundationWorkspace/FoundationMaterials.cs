using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Model.Geotechnics;
using System.Collections.Immutable;

namespace Anthea.FoundationWorkspace;

/// <summary>Persistence and mapping of the same soil and concrete keys used by the other ANTHEA modules.</summary>
public static class FoundationMaterials
{
    public static readonly ImmutableArray<(string Key,string Label,double Scale)> SoilFields = [
        ("spessore","Spessore [m]",1000),("peso_specifico","Peso specifico γ [kN/m³]",1e-6),
        ("peso_specifico_saturo","Peso specifico saturo γsat [kN/m³]",1e-6),("angolo_attrito","Angolo di attrito φ′ [°]",Math.PI/180),
        ("coesione_efficace","Coesione efficace c′ [kPa]",.001),("coesione_non_drenata","Coesione non drenata cu [kPa]",.001)];
    public static readonly ImmutableArray<string> LegacySoilKeys=["FrictionAngle","Cohesion","UndrainedStrength","UnitWeight","SaturatedUnitWeight","CoverUnitWeight","CoverSaturatedUnitWeight"];
    public static JsonObject NewLayer()=>new(){["name"]="Strato",["spessore"]=10,["tipologia"]="Granulare",["peso_specifico"]=18,["peso_specifico_saturo"]=20,["angolo_attrito"]=30,["coesione_efficace"]=0,["coesione_non_drenata"]=75};
    public static void Complete(JsonObject data)
    {
        var input=data["input"]!.AsObject();
        if(data["stratigrafie"] is null)
        {
            JsonObject Layer(bool cover)
            {
                var row=NewLayer();row["name"]=cover?"Ricoprimento":"Strato portante";
                row["peso_specifico"]=input[cover?"CoverUnitWeight":"UnitWeight"]!.DeepClone();
                row["peso_specifico_saturo"]=input[cover?"CoverSaturatedUnitWeight":"SaturatedUnitWeight"]!.DeepClone();
                row["angolo_attrito"]=input["FrictionAngle"]!.DeepClone();row["coesione_efficace"]=input["Cohesion"]!.DeepClone();row["coesione_non_drenata"]=input["UndrainedStrength"]!.DeepClone();
                if(cover)row["spessore"]=input["Depth"]!.DeepClone();return row;
            }
            // Numeric zero may be stored as text (including the Italian decimal comma).
            // Keep incomplete input verbatim so editing an old document remains possible.
            bool zeroDepth=double.TryParse(input["Depth"]?.ToString().Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double depth)&&depth==0;
            var rows=new JsonArray();if(!zeroDepth)rows.Add(Layer(true));rows.Add(Layer(false));
            data["stratigrafie"]=new JsonArray(rows);
        }
        data["generali"]??=new JsonObject();data["sezione"]??=new JsonObject();
        foreach(var (key,value) in new JsonObject { ["presenza_falda"]=input["WaterEnabled"]!.DeepClone(),["profondita_falda"]=input["WaterDepth"]!.DeepClone(),["sondaggio_fondazione"]=0,["strato_portante_semiinfinito"]=true })
            if(!data["generali"]!.AsObject().ContainsKey(key))data["generali"]![key]=value?.DeepClone();
        foreach(var (key,value) in new JsonObject { ["classe_cls"]="C30/37",["materiale_cls_nome"]="C30/37",["fck_mpa"]=30,["cls_diagramma"]="Parabola-rettangolo" })
            if(!data["sezione"]!.AsObject().ContainsKey(key))data["sezione"]![key]=value?.DeepClone();
        // Older applications must reject the new archive rather than calculate the dormant two-zone inputs.
        data["versione_fondazioni"]=2;
    }
    public static JsonArray Layers(JsonObject data)
    {
        var surveys=data["stratigrafie"] as JsonArray??throw new ArgumentException("Profilo terreno mancante.");
        int index=FoundationDocument.Choice(data["generali"]!.AsObject(),"sondaggio_fondazione",surveys.Count);
        return surveys[index] as JsonArray??throw new ArgumentException("Sondaggio non valido.");
    }
    public static SoilProfile? Profile(JsonObject data)
    {
        if(data["stratigrafie"] is null)return null; // Old archives retain their original two-zone interpretation.
        var rows=Layers(data);if(rows.Count is <1 or >50)throw new ArgumentException("Il sondaggio deve avere da 1 a 50 strati.");
        var layers=new List<SoilLayer>();double top=0;
        foreach(var node in rows)
        {
            var row=node as JsonObject??throw new ArgumentException("Strato non valido.");
            if(row["tipologia"]?.ToString() is not ("Granulare" or "Coesivo"))throw new ArgumentException("Assegnare la tipologia di ogni strato.");
            double Read(string key)=>FoundationDocument.Number(row[key],key);
            string name=string.IsNullOrWhiteSpace(row["name"]?.ToString())?"Strato "+(layers.Count+1):row["name"]!.ToString();
            double? cu=string.IsNullOrWhiteSpace(row["coesione_non_drenata"]?.ToString())?null:Read("coesione_non_drenata")*SoilUnits.KiloPascal;
            if(cu==0)cu=null;
            // Same optional γsat convention as the vertical-pile editor: blank follows γ, never a hidden stored replacement.
            double gamma=Read("peso_specifico"),sat=string.IsNullOrWhiteSpace(row["peso_specifico_saturo"]?.ToString())?gamma:Read("peso_specifico_saturo");
            var soil=new Soil(name,gamma*SoilUnits.KiloNewtonPerCubicMetre,sat*SoilUnits.KiloNewtonPerCubicMetre,Read("angolo_attrito")*SoilUnits.Degree,
                Read("coesione_efficace")*SoilUnits.KiloPascal,"Parametri ANTHEA assegnati dal progettista",cu);
            double bottom=top-Read("spessore")*SoilUnits.Metre;
            layers.Add(new SoilLayer(soil,top,bottom));top=bottom;
        }
        var general=data["generali"]!;
        bool water=Flag(general,"presenza_falda");
        return new SoilProfile("Sondaggio "+(FoundationDocument.Choice(general.AsObject(),"sondaggio_fondazione",data["stratigrafie"]!.AsArray().Count)+1),layers,"Profilo comune ANTHEA",
            water?-FoundationDocument.Number(general["profondita_falda"],"Profondità falda")*SoilUnits.Metre:null,
            FoundationDocument.Number(data["input"]!["WaterUnitWeight"],"Peso specifico acqua")*SoilUnits.KiloNewtonPerCubicMetre);
    }
    public static bool Flag(JsonNode node,string key)=>node[key] is JsonValue value && value.TryGetValue<bool>(out bool flag)?flag:throw new ArgumentException("Opzione non valida: "+key);
    public static IEnumerable<(string Key,string Path,JsonNode? Value)> SharedConcrete(JsonObject data)
    {
        var copy=data.DeepClone().AsObject();Complete(copy);
        foreach(var key in new[]{"fck_mpa","cls_diagramma"})yield return(key=="fck_mpa"?"CLS · fck [MPa]":key,"sezione/"+key,copy["sezione"]![key]?.DeepClone());
    }
}
