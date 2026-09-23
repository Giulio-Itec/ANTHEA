using System.Text;
using System.Text.Json.Nodes;

namespace X.Core;

public static class Archivio
{
    public static readonly string[] Moduli=["geo_palo_verticale","geo_palo_orizzontale","geo_micropalo_verticale","geo_micropalo_orizzontale","str_palo"];
    public static JsonObject Leggi(string path)
    {
        var doc=JsonNode.Parse(File.ReadAllText(path,Encoding.UTF8)) as JsonObject??throw new ArgumentException("Contenuto non riconosciuto.");
        if(doc.S("formato")==Encoding.ASCII.GetString(Convert.FromHexString("414E54484541")))doc["formato"]="X";
        Valida(doc);return doc;
    }
    public static void Valida(JsonObject doc)
    {
        if(doc.S("formato")!="X"||doc.D("versione")!=1)throw new ArgumentException("Formato o versione del file non supportati.");
        void Sheet(JsonNode? sheet,bool allowEmpty)
        {
            if(sheet is not JsonObject||!Moduli.Contains(sheet.S("modulo_id")))throw new ArgumentException("Modulo del foglio non disponibile.");
            var data=sheet["dati"];if(data is null&&allowEmpty)return;
            if(data is not JsonObject)throw new ArgumentException("Dati del foglio mancanti.");
            if(sheet.S("modulo_id")=="str_palo") {if(data["input"] is not JsonObject||data.D("versione_sezione",1) is not (1 or 2))throw new ArgumentException("Dati della sezione non validi.");}
            else if(sheet.S("modulo_id") is PaloOrizzontale.Module or MicropaloOrizzontale.Module) {
                PaloOrizzontale.ValidateShape(data.AsObject());
                if ((sheet.S("modulo_id") == MicropaloOrizzontale.Module) != (data.S("tipo_sezione") == "CHS")) throw new ArgumentException("Tipo di sezione incoerente con il modulo orizzontale.");
            }
            else Calcolo.ValidaForma(data);
        }
        if(doc.S("tipo")=="calcolo"){Sheet(doc,false);return;}
        if(doc.S("tipo")!="progetti"||doc["progetti"] is not JsonArray projects)throw new ArgumentException("Dati dei progetti mancanti.");
        foreach(var p in projects)
        {
            if(p is not JsonObject||p["strutture"] is not JsonArray structures)throw new ArgumentException("Strutture del progetto non valide.");
            void Sections(JsonArray items)
            {
                foreach (var s in items)
                {
                    if (s is not JsonObject || s["fogli"] is not JsonArray sheets) throw new ArgumentException("Fogli della struttura non validi.");
                    foreach (var f in sheets) Sheet(f, true);
                    if (s.AsObject().ContainsKey("strutture"))
                    {
                        if (s["strutture"] is not JsonArray nested) throw new ArgumentException("Sottosezioni non valide.");
                        Sections(nested);
                    }
                }
            }
            Sections(structures);
        }
    }
    public static void Scrivi(string path,JsonObject document)
    {
        Valida(document);ScriviAtomico(path,Encoding.UTF8.GetBytes(document.ToJsonString(J.Options)));
    }
    public static void ScriviAtomico(string path,byte[] bytes)
    {
        path=Path.GetFullPath(path);string temp=Path.Combine(Path.GetDirectoryName(path)!,".x_"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temp,path,true);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static JsonObject NuovoFoglio(string module)
    {
        if(module=="str_palo")return SezioneCA.DefaultData();
        if(module==PaloOrizzontale.Module)return PaloOrizzontale.Defaults();
        if(module==MicropaloOrizzontale.Module)return MicropaloOrizzontale.Defaults();
        bool micro=module=="geo_micropalo_verticale";
        var g=J.Obj(("tipo_palo","Trivellato"),("sottotipo_palo_battuto","Profilato d'acciaio"),("diametro",micro?"0.25":"1"),("lunghezza",""),("peso_specifico_palo","25"),("azione_compressione",""),("azione_trazione",""),("presenza_falda",false),("considera_sottospinta",false),("profondita_falda",""),("verticali_indagate","1"),("sicurezza_laterale_compressione","1.15"),("sicurezza_laterale_trazione","1.25"),("sicurezza_base","1.35"),("peso_palo_sfavorevole","1.30"),("peso_palo_favorevole","1.00"),("metodo_nq","Parametrizzata"));
        if(micro)
        {
            g["diametro"]="0.24";
            foreach(var (k,v) in J.Obj(("metodo_micropalo",BustamanteDoix.Versione),("tipo_iniezione","IGU"),("profilo_chs",""),("pressione_iniezione",""),("inclinazione","0"),("inizio_aderenza","0"),("considera_punta",false),("percentuale_punta","0")))g[k]=v?.DeepClone();
        }
        return J.Obj(("generali",g),("efficienza",J.Obj(("metodo","Nessuna riduzione"),("numero_pali_x","1"),("numero_pali_y","1"),("interasse_x",""),("interasse_y",""),("eta_compressione","1"),("eta_trazione","1"))),("stratigrafie",new JsonArray(new JsonArray(micro ? NuovoStratoMicropalo() : NuovoStratoPalo()))),("visibilita_grafici",new JsonObject()));
    }
    public static JsonObject NuovoStratoMicropalo() => J.Obj(("spessore", "0"), ("terreno", ""), ("alpha", "0"), ("laterale_attiva", true));
    public static JsonObject NuovoStratoPalo() => J.Obj(("spessore", "0"), ("tipologia", ""), ("addensamento", ""),
        ("peso_specifico", "0"), ("peso_specifico_saturo", ""), ("angolo_attrito", "0"),
        ("coesione_efficace", "0"), ("coesione_non_drenata", "0"), ("nc", "9"), ("laterale_attiva", true));
    public static JsonObject Documento(string module)=>J.Obj(("formato","X"),("versione",1),("tipo","calcolo"),("modulo_id",module),("dati",NuovoFoglio(module)));
}
