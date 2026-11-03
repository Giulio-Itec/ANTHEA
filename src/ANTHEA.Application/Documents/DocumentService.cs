using System.Text;
using System.Text.Json.Nodes;

using X.Core;

namespace Anthea.Application.Documents;

public static class DocumentService
{
    public static readonly string[] ModuleIds = Modules.ModuleDirectory.All.Select(m => m.Id).ToArray();
    public static JsonObject Open(string path) => Open(path, null);

    /// <summary>
    /// The document at <paramref name="path"/>, read and checked. With <paramref name="problems"/> (the shell, step 2b of
    /// docs/sviluppo/progetto-sistema.md, 3/11/2026) a sheet or a revision that cannot be read does not refuse the document: it is marked
    /// (<see cref="DocumentQuarantine"/>), kept as it was in the file, and its problem added with its place. The file itself (format, version,
    /// structure of the projects) must still be readable.
    /// </summary>
    public static JsonObject Open(string path, List<DocumentProblem>? problems)
    {
        var doc=JsonNode.Parse(File.ReadAllText(path,Encoding.UTF8)) as JsonObject??throw new ArgumentException("Contenuto non riconosciuto.");
        if(doc.S("formato")==Encoding.ASCII.GetString(Convert.FromHexString("414E54484541")))doc["formato"]="X";
        ResultArchiveDocumentPersistence.Unpack(doc); ConcreteDocumentPersistence.Unpack(doc, problems); ProjectRevisions.Unpack(doc, problems); Validate(doc, problems); ProjectRevisions.Validate(doc, problems); return doc;
    }
    public static void Validate(JsonObject doc) => Validate(doc, null);

    /// <summary>Checks <paramref name="doc"/>; the sheets marked as not readable are skipped (written back as they were). With
    /// <paramref name="problems"/> a sheet that fails is marked and added instead of refusing the document.</summary>
    public static void Validate(JsonObject doc, List<DocumentProblem>? problems)
    {
        if(doc.S("formato")!="X"||doc.D("versione")!=1)throw new ArgumentException("Formato o versione del file non supportati.");
        void Sheet(JsonNode? sheet,bool allowEmpty)
        {
            if(DocumentQuarantine.IsQuarantined(sheet))return;
            if(problems is not null&&sheet is JsonObject marked)
            {
                try{Check(marked,allowEmpty);}
                catch(ArgumentException error){DocumentQuarantine.Mark(marked,error.Message,problems);}
                return;
            }
            Check(sheet,allowEmpty);
        }
        void Check(JsonNode? sheet,bool allowEmpty)
        {
            if(sheet is not JsonObject||!ModuleIds.Contains(sheet.S("modulo_id")))throw new ArgumentException("Modulo del foglio non disponibile.");
            if(sheet["dati_ref"] is not null)throw new ArgumentException("Riferimento ai dati del foglio non risolto.");
            var data=sheet["dati"];if(data is null&&allowEmpty)return;
            if(data is not JsonObject)throw new ArgumentException("Dati del foglio mancanti.");
            Modules.ModuleDirectory.ValidateData(sheet.S("modulo_id"), data.AsObject());
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
    public static void Save(string path,JsonObject document)
    {
        Validate(document); ProjectRevisions.Validate(document);
        var packed=ResultArchiveDocumentPersistence.Pack(ConcreteDocumentPersistence.Pack(document));
        // Step 2b: the marks of the sheets not readable stay in memory; the file keeps them as they were, and the next opening finds them again.
        DocumentQuarantine.Strip(packed);
        Anthea.Reports.DocumentFile.Write(path,Encoding.UTF8.GetBytes(packed.ToJsonString(J.Options)));
    }
    public static JsonObject CreateSheet(string module) => Anthea.Application.Modules.ModuleDirectory.CreateData(module);
    public static JsonObject CreateMicropileLayer() => CalculationDefaults.NuovoStratoMicropalo();
    public static JsonObject CreatePileLayer() => CalculationDefaults.NuovoStratoPalo();
    public static JsonObject CreateDocument(string module)=>J.Obj(("formato","X"),("versione",1),("tipo","calcolo"),("modulo_id",module),("dati",CreateSheet(module)));
}
