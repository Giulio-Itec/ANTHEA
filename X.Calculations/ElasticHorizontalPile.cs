using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
namespace Anthea.Calculations;
/// <summary>JSON/unit/presentation adapter. Engineering calculations are exclusively in Checker.</summary>
public static partial class ElasticHorizontalPile
{
    public static readonly string[] Laws=["kh costante assegnato", "Reese–Matlock · nh assegnato", "k distribuito costante assegnato", "nh da tabella 14.5 · sabbie", "nh da tabella 14.6 · coesivi", "nh = A·γ/1,35 · incoerenti"];
    public static string MethodLabel(string method)=>method switch {"ManualKh"=>Laws[0],"ManualNh"=>Laws[1],"ManualDistributed"=>Laws[2],"SandTable145"=>Laws[3],"CohesiveTable146"=>Laws[4],"SandCorrelation"=>Laws[5],_=>method};
    public static string ParameterLabel(string law)=>law switch {"ConstantKh"=>"kh","LinearKhReference"=>"nh","ConstantDistributed"=>"k",_=>law};
    public const string Signs="x dalla testa verso la punta; z=x−Llibero dal piano campagna. H e y positivi verso destra; θ=dy/dx; C positivo con θ; M=EI y″; V=dM/dx; q=−k y; dV/dx=q. Mtesta=−C−Rθ. N positivo a compressione; N=Ntesta+w x. Le reazioni laterali sono azioni sul palo.";
    public const string Limits="Analisi elastica lineare del palo singolo, Euler–Bernoulli/Winkler, piccoli spostamenti, EI costante. Esclusi plasticità, distacco, curve p-y, interazione di gruppo, trasferimento assiale al terreno e secondo ordine. Nessuna verifica della capacità portante. Viggiani §14.4.1: kh costante per argille o.c.; kh=nh·z/D per incoerenti e argille n.c./debolmente o.c. Parametri orientativi: media iniziale della singola riga, modificabile; condizioni drenate/non drenate e applicabilità da documentare.";
    public static void Upgrade(JsonObject d)
    {
        foreach(var row in d.Array("strati").OfType<JsonObject>())if(row.S("legge")=="kh = kh,rif · z/D assegnato")row["legge"]=Laws[1];
        if(!d.ContainsKey("falda"))d["falda"]=false;
        if(!d.ContainsKey("z_falda"))d["z_falda"]="0";
        if(!d.ContainsKey("gamma_w"))d["gamma_w"]="9.81";
    }
    public static JsonObject Defaults(JsonObject parent)
    {
        var g=parent["generali"]!;
        return J.Obj(("diametro",g["diametro"]?.DeepClone()),("lunghezza",g["lunghezza"]?.DeepClone()),("libero","0"),("EI",""),("fonte_EI",""),
            ("H",g["azione_orizzontale"]?.DeepClone()),("C","0"),("e","0"),("vincolo",g.S("vincolo","Libera")),("punta","Libera"),("passo","0.5"),("strati",new JsonArray()));
    }
    public static JsonObject Layer()=>J.Obj(("nome",""),("spessore",""),("legge",Laws[0]),("valore",""),("fonte","Assegnazione manuale"));
    public static HorizontalSoilProfile Soil(JsonObject d)
    {
        double? Optional(JsonNode n,string key){if(n[key] is null||string.IsNullOrWhiteSpace(n[key]!.ToString()))return null;return J.Number(n[key])??throw new ArgumentException(key+": numero finito richiesto.");}
        var assignments=d.Array("strati").Select(s=>
        {
            int index=Array.IndexOf(Laws,s.S("legge"));if(s.S("legge")=="kh = kh,rif · z/D assegnato")index=1;
            if(index<0)throw new ArgumentException("Legge del terreno non valida.");
            var mode=new[]{HorizontalSoilMode.ManualKh,HorizontalSoilMode.ManualNh,HorizontalSoilMode.ManualDistributed,HorizontalSoilMode.SandTable145,HorizontalSoilMode.CohesiveTable146,HorizontalSoilMode.SandCorrelation}[index];
            var row=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Label==s.S("riga_146")||r.Id==s.S("riga_146"));
            return new HorizontalSoilAssignment{Name=s.S("nome"),Thickness=Optional(s!,"spessore")??throw new ArgumentException("Spessore richiesto."),Mode=mode,ManualValue=index<=2?Optional(s!,"valore"):null,Density=s.S("densita"),CohesiveRowId=row?.Id??"",SelectedNhNPerCm3=index==4?Optional(s!,"nh_tabella"):null,A=index==5?Optional(s!,"A"):null,UnitWeight=index==5?Optional(s!,"gamma"):null,SaturatedUnitWeight=index==5?Optional(s!,"gamma_sat"):null,Conditions=s.S("fonte"),SelectionOrigin=s.S("origine_scelta"),OverrideNh=index>=3&&s.B("override")?Optional(s!,"nh_override")??throw new ArgumentException("Inserire nh di override."):(double?)null,OverrideReason=s.S("motivo_override")};
        }).ToArray();
        return ViggianiHorizontalSoil.Resolve(assignments,d.B("falda")?J.Number(d["z_falda"])??throw new ArgumentException("Profondità falda richiesta."):(double?)null,d.B("falda")?J.Number(d["gamma_w"])??throw new ArgumentException("γw richiesto."):9.81);
    }
    public static ElasticPileInput Read(JsonObject d) => Read(d,Soil(d));
    static ElasticPileInput Read(JsonObject d,HorizontalSoilProfile profile)
    {
        double N(JsonNode n,string key)=>J.Number(n[key])??throw new ArgumentException(key+": inserire un numero finito.");
        int Choice(string value,string[] choices,string label){int i=Array.IndexOf(choices,value);if(i<0)throw new ArgumentException(label+": opzione non valida.");return i;}
        return new ElasticPileInput { Diameter=N(d,"diametro"),Length=N(d,"lunghezza"),FreeLength=N(d,"libero"),EI=N(d,"EI"),EISource=d.S("fonte_EI"),Force=N(d,"H"),HeadMoment=N(d,"C"),Eccentricity=N(d,"e"),FixedHeadRotation=Choice(d.S("vincolo"),["Libera","Impedita"],"Testa")==1,Tip=(ElasticPileTip)Choice(d.S("punta"),["Libera","Cerniera","Incastro"],"Punta"),Step=N(d,"passo"),Layers=profile.Layers };
    }
    public static JsonObject Calculate(JsonObject data)=>CalculateModel(data,null);
    static JsonObject CalculateModel(JsonObject data,ElasticPileSection? section)
    {
        var input=(JsonObject)data.DeepClone();Upgrade(input);var soil=Soil(input);var model=Read(input,soil);model.Section=section;model.AxialHeadForce=input.ContainsKey("N")?J.Number(input["N"])??throw new ArgumentException("N non valido."):0;model.WeightPerLength=input.D("peso_lineare");model.AdditionalNodes=input.Array("quote_verifica").Select(n=>J.Number(n)??throw new ArgumentException("Quota non valida.")).ToArray();var result=ElasticPile.CalculateWithConvergence(model);
        return J.Obj(("tipo_risultato","palo_elastico"),("errore",""),("input",input),("segni",Signs),("limiti",Limits),("fonte",ViggianiHorizontalSoil.Source),("raccordo",ViggianiHorizontalSoil.StratificationConvention),("parametri_terreno",JsonSerializer.SerializeToNode(soil.Determinations)),("risposta",JsonSerializer.SerializeToNode(result)),("unita","m, kN, kNm, rad; kh e nh [kN/m³]; k [kN/m²]. Matrice di fondazione consistente, nessuna molla nodale indipendente."));
    }
    public static string Csv(JsonObject result)
    {
        var text=new System.Text.StringBuilder();
        text.AppendLine("# "+Signs).AppendLine("# "+Limits).AppendLine("# "+result.S("unita"));
        text.AppendLine("# "+result.S("fonte")).AppendLine("# "+result.S("raccordo"));
        text.AppendLine("# DATI_CONDIVISI "+result["dati_condivisi"]?.ToJsonString());
        text.AppendLine("# SEZIONE_EJ "+result["risposta"]?["Section"]?.ToJsonString());
        text.AppendLine("# ARMATURE_E_VERIFICHE "+result["armature"]?.ToJsonString());
        text.AppendLine("# N positivo a compressione; N=Ntesta+w*x, nessun trasferimento assiale al terreno o secondo ordine.");
        foreach(var node in result["risposta"]!.Array("Nodes"))text.AppendLine("# NODO_EQUIVALENTE_SOLO_VISUALIZZAZIONE "+node!.ToJsonString());
        foreach(var row in result.Array("parametri_terreno"))text.AppendLine("# PARAMETRI "+row!.ToJsonString());
        text.AppendLine("x [m];z [m];strato;lato;kh [kN/m3];nh [kN/m3];k [kN/m2];y [m];theta [rad];q [kN/m];N [kN];V [kN];M [kNm]");
        foreach(var p in result["risposta"]!.Array("Points"))text.AppendLine(string.Join(";",new[]{"Depth","GroundDepth","Layer","Side","Kh","Nh","DistributedStiffness","Displacement","Rotation","SoilReaction","AxialForce","Shear","Moment"}.Select(k=>J.Number(p![k]) is double v?v.ToString("G17",CultureInfo.InvariantCulture):p[k]?.ToString()??"")));
        return text.ToString();
    }
}
