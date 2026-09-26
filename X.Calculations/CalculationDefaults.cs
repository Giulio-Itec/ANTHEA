using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static class CalculationDefaults
{
    public static JsonObject Vertical(bool micro)
    {
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
}
