using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Checkers.Concrete.Piles;
namespace Anthea.Calculations;
public static partial class ElasticHorizontalPile
{
    public static void PrepareShared(JsonObject root)
    {
        var g=root["generali"]!.AsObject();var s=root["sezione"]!.AsObject();
        if(g["tratto_libero"] is null)g["tratto_libero"]=0;
        if(g["gamma_acqua"] is null)g["gamma_acqua"]=9.81;
        if(s["modulo_chs_mpa"] is null)s["modulo_chs_mpa"]=210000;
        if(s["vita_durabilita"] is null)s["vita_durabilita"]=50;
        if(s["gamma_ca"] is null)s["gamma_ca"]=25;
        if(s["gamma_acciaio"] is null)s["gamma_acciaio"]=78.5;
        if(s["gamma_iniezione"] is null)s["gamma_iniezione"]=0;
        if(root["elastico"] is not JsonObject e)root["elastico"]=SharedDefaults(root);
        else if(e.D("versione")!=2)
        {
            // Preserve the original model before any resolution, even if incomplete.
            if(root["elastico_legacy"] is null)root["elastico_legacy"]=e.DeepClone();
            if(e.Array("strati").Count>0||J.Number(e["EI"]) is >0)e["migrazione_richiesta"]=true;
            else root["elastico"]=SharedDefaults(root);
        }
        foreach(var survey in root.Array("stratigrafie").OfType<JsonArray>())foreach(var layer in survey.OfType<JsonObject>())EnsureSoil(layer);
        if(!NeedsMigration(root))PrepareReinforcement(root);
    }
    static JsonObject SharedDefaults(JsonObject root)=>J.Obj(("versione",2),("stratigrafia",root.Array("stratigrafie").Count==1?0:-1),("punta","Libera"),("passo",.5),("visualizzazione",new JsonObject()));
    public static bool NeedsMigration(JsonObject root)=>root["elastico"].B("migrazione_richiesta");
    public static JsonObject EnsureSoil(JsonObject layer)
    {
        if(layer["reazione_orizzontale"] is not JsonObject){layer["reazione_orizzontale"]=J.Obj(("legge",layer.S("tipologia")=="Granulare"?Laws[5]:Laws[4]),("densita","Sciolto"),("riga_146","clay-reese-1956"),("fonte","Viggiani, parametri orientativi; verificare condizioni di impiego"));InitializeMean(layer["reazione_orizzontale"]!.AsObject());}
        var soil=layer["reazione_orizzontale"]!.AsObject();
        // Old UI labels were not the actual catalog categories: normalise only known aliases.
        string category=soil.S("categoria");
        if(category=="Argilla n.c. / debolmente o.c.")soil["categoria"]=ViggianiHorizontalSoil.CohesiveTable[0].Category;
        if(category=="Argilla organica")soil["categoria"]=ViggianiHorizontalSoil.CohesiveTable.First(r=>r.Id=="organic-peck-1970").Category;
        if(soil["categoria"]==null)soil["categoria"]=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Id==soil.S("riga_146")||r.Label==soil.S("riga_146"))?.Category??ViggianiHorizontalSoil.CohesiveTable[0].Category;
        return soil;
    }
    public static string[] SoilCategories=>ViggianiHorizontalSoil.CohesiveTable.Select(r=>r.Category).Distinct().Append("Argilla sovraconsolidata").ToArray();
    /// <summary>Explicit UI choice; preserve previous inputs and manual values. Only software means follow a new row automatically.</summary>
    public static void SelectSoil(JsonObject layer,string key,string value)
    {
        var soil=EnsureSoil(layer);var before=(JsonObject)soil.DeepClone();before.Remove("storico_scelte");
        if(soil["storico_scelte"] is not JsonArray)soil["storico_scelte"]=new JsonArray();soil.Array("storico_scelte").Add(before);
        if(key=="tipologia")
        {
            layer[key]=value;
            bool incompatible=value=="Granulare"&&soil.S("legge")==Laws[4]||value=="Coesivo"&&(soil.S("legge")==Laws[3]||soil.S("legge")==Laws[5]);
            if(incompatible){soil["legge"]=value=="Granulare"?Laws[5]:soil.S("categoria")=="Argilla sovraconsolidata"?Laws[0]:Laws[4];soil["origine_scelta"]="Media iniziale del software";if(soil.S("legge")==Laws[0])soil.Remove("valore");else SetMean(soil);}
        }
        else
        {
            soil[key]=value;
            if(key=="categoria")
            {
                var rows=ViggianiHorizontalSoil.CohesiveTable.Where(r=>r.Category==value).ToArray();
                if(rows.Length==0){soil["legge"]=Laws[0];soil.Remove("valore");}
                else if(soil.S("legge")==Laws[4]&&!rows.Any(r=>r.Id==soil.S("riga_146")||r.Label==soil.S("riga_146")))soil["riga_146"]=rows[0].Id;
            }
            InitializeMean(soil);
        }
    }
    public static string SelectionKey(JsonObject soil)=>soil.S("legge")+"|"+soil.S("densita")+"|"+soil.S("riga_146");
    public static void SetMean(JsonObject soil)
    {
        if(soil.S("legge")==Laws[4])soil["nh_tabella"]=ViggianiHorizontalSoil.MeanNh(soil.S("riga_146"));
        else if(soil.S("legge")==Laws[5])soil["A"]=ViggianiHorizontalSoil.MeanA(soil.S("densita"));
        soil["origine_scelta"]="Media iniziale del software";soil["scelta_per"]=SelectionKey(soil);
        soil["media_iniziale"]=soil.S("legge")==Laws[4]?soil["nh_tabella"]?.DeepClone():soil["A"]?.DeepClone();
    }
    public static void InitializeMean(JsonObject soil)
    {
        string field=soil.S("legge")==Laws[4]?"nh_tabella":soil.S("legge")==Laws[5]?"A":"";
        if(field=="")return;
        if(soil.S("scelta_per")!=SelectionKey(soil)&&soil.S("origine_scelta")=="Media iniziale del software"&&J.Number(soil[field]) is double current&&J.Number(soil["media_iniziale"])==current){try{SetMean(soil);}catch(ArgumentException){}return;}
        if(soil["scelta_per"] is null&&J.Number(soil[field]) is not null){soil["scelta_per"]=SelectionKey(soil);soil["origine_scelta"]="Scelta utente conservata";}
        if(soil[field] is null||string.IsNullOrWhiteSpace(soil[field]!.ToString()))try{SetMean(soil);}catch(ArgumentException){ }
    }
    public static void ResolveMigration(JsonObject root,bool useLegacy)
    {
        var copy=(JsonObject)root.DeepClone();PrepareShared(copy);if(!NeedsMigration(copy))return;
        var old=copy["elastico_legacy"]!.AsObject();var g=copy["generali"]!.AsObject();var settings=SharedDefaults(copy);
        if(useLegacy)
        {
            if(old.D("C")!=0&&old.D("e")!=0)throw new ArgumentException("Legacy non valido: momento ed eccentricità entrambi non nulli. Confrontare i dati prima di migrare.");
            double length=old.Required("lunghezza",strict:true),free=old.Required("libero"),h=J.Number(old["H"])??throw new ArgumentException("H legacy non valida.");
            double eccentricity=ElasticPileSection.GroundEccentricity(h,free,old.D("C"),old.D("e"));
            if(length<=free)throw new ArgumentException("Lunghezza legacy incoerente.");
            g["diametro"]=old["diametro"]!.DeepClone();g["lunghezza"]=length-free;g["tratto_libero"]=free;g["azione_orizzontale"]=h;g["eccentricita"]=eccentricity;g["vincolo"]=old["vincolo"]!.DeepClone();
            g["presenza_falda"]=old.B("falda");g["profondita_falda"]=old["z_falda"]?.DeepClone()??JsonValue.Create(0);g["gamma_acqua"]=old["gamma_w"]?.DeepClone()??JsonValue.Create(9.81);
            var section=copy["sezione"]!.AsObject();section["ej_override"]=true;section["ej_assegnato"]=old["EI"]!.DeepClone();section["ej_motivo"]="Importazione esplicita modello legacy: "+old.S("fonte_EI");
            var survey=new JsonArray();foreach(var item in old.Array("strati").OfType<JsonObject>()){var layer=PaloOrizzontale.Layer();layer["nome"]=item.S("nome");layer["spessore"]=item["spessore"]!.DeepClone();layer["tipologia"]="Da classificare";layer["reazione_orizzontale"]=item.DeepClone();foreach(string key in new[]{"nome","spessore","gamma","gamma_sat"})layer["reazione_orizzontale"]!.AsObject().Remove(key);if(item["gamma"] is not null)layer["peso_specifico"]=item["gamma"]!.DeepClone();if(item["gamma_sat"] is not null)layer["peso_specifico_saturo"]=item["gamma_sat"]!.DeepClone();survey.Add(layer);}
            settings["stratigrafia"]=copy.Array("stratigrafie").Count;copy.Array("stratigrafie").Add(survey);settings["punta"]=old["punta"]?.DeepClone()??JsonValue.Create("Libera");settings["passo"]=old["passo"]?.DeepClone()??JsonValue.Create(.5);
        }
        settings["migrazione"]=useLegacy?"Importazione esplicita legacy; nuova stratigrafia da classificare per Broms":"Dati comuni scelti esplicitamente; legacy conservato";copy["elastico"]=settings;
        // The full pre-migration input remains recoverable and is never fed silently into the new solver.
        copy["dati_prima_migrazione"]=(JsonObject)root.DeepClone();foreach(var item in copy.ToArray()){if(item.Key is "generali" or "sezione" && root[item.Key] is JsonObject target){target.Clear();foreach(var field in item.Value!.AsObject())target[field.Key]=field.Value?.DeepClone();}else root[item.Key]=item.Value?.DeepClone();}
    }
    public static ElasticPileSection SectionStiffness(JsonObject root)
    {
        var g=root["generali"]!;var s=root["sezione"]!;ElasticPileSection section;
        if(root.S("tipo_sezione")=="CHS")
        {
            double d,t;if(s.S("modo_chs")=="Catalogo"){if(!Chs.Catalogo.TryGetValue(s.S("profilo_chs"),out var profile))throw new ArgumentException("CHS non presente in catalogo.");(d,t)=profile;}else{d=s.Required("diametro_chs_mm",strict:true);t=s.Required("spessore_chs_mm",strict:true);}
            if(d>=g.Required("diametro",strict:true)*1000)throw new ArgumentException("Il CHS deve essere contenuto nel diametro geotecnico.");
            section=ElasticPileSection.Tube(d,t,s.Required("modulo_chs_mpa",strict:true));
        }
        else section=ElasticPileSection.Concrete(g.Required("diametro",strict:true),s.Required("fck_mpa",strict:true));
        if(s.B("ej_override"))section.Override(s.Required("ej_assegnato",strict:true),s.S("ej_motivo"));return section;
    }
    public static JsonObject SharedInput(JsonObject root)
    {
        if(NeedsMigration(root))throw new ArgumentException("Risolvere il confronto con i dati elastici legacy prima del calcolo.");
        var e=root["elastico"]!;double selected=e.D("stratigrafia",-1);int index=(int)selected;var surveys=root.Array("stratigrafie");if(index!=selected||index<0||index>=surveys.Count)throw new ArgumentException("Selezionare la stratigrafia della risposta elastica.");
        var g=root["generali"]!;var section=SectionStiffness(root);double free=J.Number(g["tratto_libero"])??throw new ArgumentException("Tratto libero non valido."),h=J.Number(g["azione_orizzontale"])??throw new ArgumentException("H non valida."),ecc=J.Number(g["eccentricita"])??throw new ArgumentException("Eccentricità non valida.");
        var layers=new JsonArray();foreach(var layer in surveys[index]!.AsArray().OfType<JsonObject>())
        {
            var soil=(JsonObject)EnsureSoil(layer).DeepClone();InitializeMean(soil);string mode=soil.S("legge");
            if(layer.S("tipologia")=="Coesivo"&&(mode==Laws[3]||mode==Laws[5])||layer.S("tipologia")=="Granulare"&&mode==Laws[4])throw new ArgumentException("Tipo di terreno cambiato: scegliere una determinazione pertinente nella riga.");
            if(mode==Laws[4]&&soil.S("categoria")!=""){var catalog=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Id==soil.S("riga_146")||r.Label==soil.S("riga_146"));if(catalog?.Category!=soil.S("categoria"))throw new ArgumentException("Categoria coesiva cambiata: scegliere una riga bibliografica pertinente.");}
            if(soil.S("categoria")=="Argilla sovraconsolidata"&&mode!=Laws[0]&&mode!=Laws[2])throw new ArgumentException("Argilla sovraconsolidata: usare kh costante o k distribuito assegnato.");
            if(mode is "")throw new ArgumentException("Scegliere la legge di reazione nell'editor iniziale degli strati.");
            if((mode==Laws[4]||mode==Laws[5])&&soil.S("scelta_per")!=SelectionKey(soil))throw new ArgumentException("Teoria/addensamento modificati: confermare la nuova media o mantenere esplicitamente il valore nell'editor dello strato.");
            soil["nome"]=layer.S("nome",layer.S("tipologia"));soil["spessore"]=layer["spessore"]?.DeepClone();soil["gamma"]=layer["peso_specifico"]?.DeepClone();soil["gamma_sat"]=layer["peso_specifico_saturo"]?.DeepClone();layers.Add(soil);
        }
        return J.Obj(("diametro",g["diametro"]!.DeepClone()),("lunghezza",ElasticPileSection.TotalLength(g.Required("lunghezza",strict:true),free)),("libero",free),("H",h),("C",ElasticPileSection.HeadCouple(h,ecc,free)),("e",0),("EI",section.EI),("fonte_EI",section.Material+"; "+section.Assumption+(section.OverrideReason==""?"":"; override: "+section.OverrideReason)),("vincolo",g.S("vincolo")),("punta",e.S("punta","Libera")),("passo",e["passo"]!.DeepClone()),("falda",g.B("presenza_falda")),("z_falda",g["profondita_falda"]!.DeepClone()),("gamma_w",g["gamma_acqua"]?.DeepClone()??JsonValue.Create(9.81)),("strati",layers));
    }
    static JsonObject SharedCalculationInput(JsonObject root)
    {
        var input=SharedInput(root);var stiffness=SectionStiffness(root);var g=root["generali"]!;var section=root["sezione"]!;
        input["N"]=g["azione_assiale"]?.DeepClone()??JsonValue.Create(0);
        input["peso_lineare"]=root.S("tipo_sezione")=="CHS"?PileSegments.TubeWeight(g.Required("diametro",strict:true),stiffness.DiameterMm,stiffness.ThicknessMm,section.Required("gamma_acciaio"),section.Required("gamma_iniezione")):PileSegments.ConcreteWeight(g.Required("diametro",strict:true),section.Required("gamma_ca"));
        var segments=ReadSegments(root);PileSegments.Validate(segments,input.D("lunghezza"));var depths=segments.Select(s=>s.End).ToList();
        if(root.S("tipo_sezione")!="CHS"&&root["elastico"]!["dettagli"].B("sisma_testa"))
        {
            var zone=PileReinforcement.DescribeSeismicHead(g.Required("diametro",strict:true)*1000,input.D("lunghezza"),SeismicSettings(root).HeadLength);
            input["zona_sismica"]=System.Text.Json.JsonSerializer.SerializeToNode(zone);depths.Add(zone.MinimumEnd);depths.Add(zone.AdoptedEnd);
        }
        input["quote_verifica"]=J.Node(depths.Distinct().OrderBy(x=>x).ToArray());
        return input;
    }
    static string InputKey(JsonObject input)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToJsonString())));
    public static string ResponseKey(JsonObject parent)
    {
        var root=(JsonObject)parent.DeepClone();PrepareShared(root);return InputKey(SharedCalculationInput(root));
    }
    public static JsonObject CalculateResponse(JsonObject parent,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();var root=(JsonObject)parent.DeepClone();PrepareShared(root);var input=SharedCalculationInput(root);
        var result=CalculateModel(input,SectionStiffness(root));cancellation.ThrowIfCancellationRequested();result["chiave_risposta"]=InputKey(input);
        result["dati_condivisi"]=J.Obj(("stratigrafia",root["elastico"].D("stratigrafia")+1),("geometria","generali"),("sezione","sezione"),("origine_z","piano campagna"),("eccentricita_da_pc",root["generali"]!["eccentricita"]!.DeepClone()),("convenzione_carico","Ctesta=H·(Llibero−e_da_pc); M al p.c. da solo H è H·e_da_pc"),("migrazione",root["elastico"].S("migrazione")));return result;
    }
    public static JsonObject CompleteReinforcement(JsonObject parent,JsonObject response,ElasticPileVerificationCache? cache=null,CancellationToken cancellation=default,int maximumParallelism=0,IProgress<PileCalculationProgress>? progress=null)
    {
        cancellation.ThrowIfCancellationRequested();var root=(JsonObject)parent.DeepClone();PrepareShared(root);
        if(response.S("chiave_risposta")!=InputKey(SharedCalculationInput(root)))throw new ArgumentException("Risposta FEM obsoleta rispetto ai dati comuni.");
        var result=(JsonObject)response.DeepClone();result["armature"]=CalculateReinforcementCore(root,result,cache??new(),cancellation,maximumParallelism,progress);return result;
    }
    public static JsonObject CalculateShared(JsonObject parent)=>CompleteReinforcement(parent,CalculateResponse(parent));
}
