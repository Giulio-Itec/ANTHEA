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
            foreach (var test in cases.Where(c => c.S("tipo") == "palo"))
            {
                var capacity = Calcolo.Calcola(test!["input"]!.AsObject());
                if (capacity.S("errore") != "") continue;
                string original = capacity.ToJsonString();
                var capacityTables = Tabelle.CapacitaPalo(capacity);
                Assert(capacityTables.Count == 2 && capacityTables.All(t => t.Colonne.Length == 4 && t.Righe.Count == 3 && t.Righe.All(row => row.Length == 4)), "Tabelle capacità palo: struttura compatta");
                double Number(string value) => double.Parse(value, System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
                foreach (var table in capacityTables)
                {
                    string condition = table.Titolo == "NON DRENATE" ? "non_drenante" : "drenante";
                    var curve = capacity["curve"]![condition + "_compressione"]!;
                    double expected = curve.Array("progetto")[^1]![1]!.GetValue<double>();
                    Assert(Math.Abs(Number(table.Righe[0][3]) + Number(table.Righe[1][3]) - expected) <= .00011, "Tabelle capacità palo: progetto coerente con curva");
                    string branch = curve.Array("media")[^1]![1]!.GetValue<double>() <= curve.Array("minima")[^1]![1]!.GetValue<double>() ? "Media" : "Minimo";
                    var components = capacity.Array("dettagli")[^1]!["componenti"]![condition]![branch]!;
                    for (int row = 0; row < 2; row++)
                    for (int column = 0; column < 3; column++)
                        Assert(table.Righe[row][column + 1] == Tabelle.F(components.Array(new[] { "calc", "k", "d" }[column])[row]), "Tabelle capacità palo: corrispondenza componente/livello");
                    Assert(table.Righe[0][0] == "Laterale compressione" && table.Righe[1][0] == "Punta compressione" && table.Righe[2][0] == "Laterale trazione", "Tabelle capacità palo: intestazioni righe");
                    double tension = capacity["curve"]![condition + "_trazione"]!.Array("progetto")[^1]![1]!.GetValue<double>();
                    Assert(Math.Abs(Number(table.Righe[2][3]) - tension) <= .00011, "Tabella trazione diversa dal risultato di progetto");
                }
                Assert(original == capacity.ToJsonString(), "Le tabelle capacità hanno modificato i risultati");
            }
            foreach(var invalid in new[]{"null","[]","{\"generali\":null}","{\"stratigrafie\":[null]}","{\"generali\":{\"presenza_falda\":\"false\"}}"})
            {
                bool rejected=false;try{Calcolo.ValidaForma(JsonNode.Parse(invalid));}catch(ArgumentException){rejected=true;}Assert(rejected,"Formato non valido accettato: "+invalid);
            }
            var sheets=new JsonArray();
            foreach(var module in Archivio.Moduli)
            {
                var d=module==MicropaloOrizzontale.Module?MicropaloOrizzontale.Defaults():module==PaloOrizzontale.Module?PaloOrizzontale.Defaults():module=="str_palo"?SezioneCA.DefaultData():module=="geo_micropalo_verticale"?cases.First(c=>c.S("tipo")=="micropalo")!["input"]!.AsObject():data;
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
            foreach(var (complete,action,expected) in new[]{(true,99.0,"Verifica soddisfatta"),(true,100.0,"Verifica soddisfatta"),(true,101.0,"Verifica non soddisfatta"),(false,0.0,"Verifica incompleta: stratigrafia insufficiente"),(true,-1.0,"Azione non inserita")})
            {
                var outcomeResult=J.Obj(("copertura_completa",complete),("curve",J.Obj(("drenante_compressione",J.Obj(("progetto",new JsonArray(new JsonArray(1.0,100.0))))))),
                    ("azioni",J.Obj(("compressione",action<0?new JsonArray():new JsonArray(new JsonArray(1,action))))));
                string path=Path.Combine(temp,"esito.docx");
                ReportWord.Esporta(path,"Test esito","geo_palo_verticale",data,outcomeResult,new HashSet<string>{"risultati"});
                using var archive=ZipFile.OpenRead(path);using var stream=archive.GetEntry("word/document.xml")!.Open();var xml=XDocument.Load(stream);
                XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                Assert(xml.Descendants(w+"t").Any(t=>t.Value==expected),"Esito relazione: "+expected);
                Assert(!xml.ToString().Contains("Verificato alla quota disponibile"),"Dicitura ambigua nella relazione");
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
