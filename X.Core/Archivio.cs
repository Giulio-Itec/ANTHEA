using System.Text;
using System.Text.Json.Nodes;

namespace X.Core;

public static class Archivio
{
    public static readonly string[] Moduli = ModuleCatalog.All.Select(m => m.Id).ToArray();
    public static JsonObject Leggi(string path)
    {
        var doc=JsonNode.Parse(File.ReadAllText(path,Encoding.UTF8)) as JsonObject??throw new ArgumentException("Contenuto non riconosciuto.");
        if(doc.S("formato")==Encoding.ASCII.GetString(Convert.FromHexString("414E54484541")))doc["formato"]="X";
        ProjectRevisions.Unpack(doc); Valida(doc); ProjectRevisions.Validate(doc); return doc;
    }
    public static void Valida(JsonObject doc)
    {
        if(doc.S("formato")!="X"||doc.D("versione")!=1)throw new ArgumentException("Formato o versione del file non supportati.");
        void Sheet(JsonNode? sheet,bool allowEmpty)
        {
            if(sheet is not JsonObject||!Moduli.Contains(sheet.S("modulo_id")))throw new ArgumentException("Modulo del foglio non disponibile.");
            if(sheet["dati_ref"] is not null)throw new ArgumentException("Riferimento ai dati del foglio non risolto.");
            var data=sheet["dati"];if(data is null&&allowEmpty)return;
            if(data is not JsonObject)throw new ArgumentException("Dati del foglio mancanti.");
            ModuleCatalog.ValidateData(sheet.S("modulo_id"), data.AsObject());
        }
        if(doc.S("tipo")=="calcolo"){Sheet(doc,false);return;}
        if(doc.S("tipo")!="progetti"||doc["progetti"] is not JsonArray projects)throw new ArgumentException("Dati dei progetti mancanti.");
        foreach(var p in projects)
        {
            if(p is not JsonObject||p["strutture"] is not JsonArray structures)throw new ArgumentException("Strutture del progetto non valide.");
            if (p.AsObject().ContainsKey("fogli"))
            {
                if (p["fogli"] is not JsonArray projectSheets) throw new ArgumentException("Fogli del progetto non validi.");
                foreach (var sheet in projectSheets) Sheet(sheet, true);
            }
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
        Valida(document); ProjectRevisions.Validate(document); ScriviAtomico(path,Encoding.UTF8.GetBytes(ProjectRevisions.Pack(document).ToJsonString(J.Options)));
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
    public static JsonObject NuovoFoglio(string module) => ModuleCatalog.CreateData(module);
    public static JsonObject NuovoStratoMicropalo() => CalculationDefaults.NuovoStratoMicropalo();
    public static JsonObject NuovoStratoPalo() => CalculationDefaults.NuovoStratoPalo();
    public static JsonObject Documento(string module)=>J.Obj(("formato","X"),("versione",1),("tipo","calcolo"),("modulo_id",module),("dati",NuovoFoglio(module)));
}
