using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

public static class SoftwareChecks
{
    public static int Run(JsonArray cases)
    {
        int passed=0;
        void Assert(bool condition,string message){if(!condition)throw new Exception(message);passed++;}
        var temp=Path.Combine(Path.GetTempPath(),"X_CSharp_Test_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var data=cases.First(c=>c.S("tipo")=="palo")!["input"]!.AsObject();string snapshot=data.ToJsonString();var result=Calcolo.Calcola(data);
            Assert(snapshot==data.ToJsonString(),"Il calcolo ha modificato gli input.");
            foreach(var invalid in new[]{"null","[]","{\"generali\":null}","{\"stratigrafie\":[null]}","{\"generali\":{\"presenza_falda\":\"false\"}}"})
            {
                bool rejected=false;try{Calcolo.ValidaForma(JsonNode.Parse(invalid));}catch(ArgumentException){rejected=true;}Assert(rejected,"Formato non valido accettato: "+invalid);
            }
            var sheets=new JsonArray();
            foreach(var module in Archivio.Moduli)
            {
                var d=module=="str_palo"?SezioneCA.DefaultData():module=="geo_micropalo_verticale"?cases.First(c=>c.S("tipo")=="micropalo")!["input"]!.AsObject():data;
                var doc=J.Obj(("formato","X"),("versione",1),("tipo","calcolo"),("modulo_id",module),("dati",d));string file=Path.Combine(temp,module+".programma");Archivio.Scrivi(file,doc);Assert(JsonNode.DeepEquals(doc,Archivio.Leggi(file)),"Round trip "+module);
                sheets.Add(J.Obj(("id",module),("nome",module),("modulo_id",module),("dati",d)));
            }
            var structure=J.Obj(("id","S1"),("nome","Struttura"),("fogli",sheets));
            var projectData=J.Obj(("id","P1"),("nome","Prova"),("strutture",new JsonArray(structure)));
            var project=J.Obj(("formato","X"),("versione",1),("tipo","progetti"),("progetti",new JsonArray(projectData)));
            string projectPath=Path.Combine(temp,"progetti.programma");Archivio.Scrivi(projectPath,project);Assert(JsonNode.DeepEquals(project,Archivio.Leggi(projectPath)),"Round trip progetti");
            var legacy=(JsonObject)project.DeepClone();legacy["formato"]=System.Text.Encoding.ASCII.GetString(Convert.FromHexString("414E54484541"));File.WriteAllText(projectPath,legacy.ToJsonString());Assert(Archivio.Leggi(projectPath).S("formato")=="X","Migrazione identificatore storico");
            foreach(bool micro in new[]{false,true})
            {
                var source=micro?cases.First(c=>c.S("tipo")=="micropalo")!["input"]!.AsObject():data;var output=Calcolo.Calcola(source,micro);var tables=Tabelle.Crea(output,micro);
                Assert(tables.Count>=5&&tables.All(t=>t.Righe.All(r=>r.Length==t.Colonne.Length)),"Tabelle incoerenti");
                Assert(tables.Any(t=>t.Titolo.Contains(micro?"aderenza":"parametri alla punta")),"Dettagli mancanti");
                string report=Path.Combine(temp,micro?"micro.docx":"palo.docx");ReportWord.Esporta(report,"Test",micro?"geo_micropalo_verticale":"geo_palo_verticale",source,output);
                using var archive=ZipFile.OpenRead(report);var entry=archive.GetEntry("word/document.xml");Assert(entry is not null,"Documento Word mancante");using var stream=entry!.Open();var xml=XDocument.Load(stream);Assert(xml.Descendants().Any(n=>n.Value.Contains("Dettagli ogni 0,50")),"Dettaglio Word mancante");
                foreach(var e in archive.Entries.Where(e=>e.FullName.EndsWith(".xml")||e.FullName.EndsWith(".rels"))){using var s=e.Open();_=XDocument.Load(s);}
                Assert(true,"XML Word");
                string partial=Path.Combine(temp,"parziale.docx");ReportWord.Esporta(partial,"Test",micro?"geo_micropalo_verticale":"geo_palo_verticale",source,output,new HashSet<string>{"risultati"});using var zip=ZipFile.OpenRead(partial);using var xmlStream=zip.GetEntry("word/document.xml")!.Open();Assert(!XDocument.Load(xmlStream).ToString().Contains("Dettagli ogni 0,50"),"Selezione sezioni report");
            }
            var section=CalcoloSezione.Calcola(SezioneCA.DefaultData());Assert(section.S("errore")==""&&section["risultati"]!.AsObject().Count==3,"Combinazioni SLU SLV SLE");
            Assert(section["risultati"]!.AsObject().All(p=>p.Value.S("errore")==""),"Errore combinazione sezione");
            Console.WriteLine($"Controlli software, archivi e report: {passed} superati.");return passed;
        }
        finally
        {
            // Directory temporanea univoca creata esclusivamente da questa prova.
            Directory.Delete(temp,true);
        }
    }
}
