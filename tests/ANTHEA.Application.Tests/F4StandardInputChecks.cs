using System.IO.Compression;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class F4StandardInputChecks
{
    internal static int Run(string root)
    {
        int checks=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        void Reject(Action action){try{action();}catch(ArgumentException){checks++;return;}throw new Exception("Invalid standard accepted");}
        var fresh=SezioneCA.DefaultData();var ws=SectionWorkspace.Prepare(fresh);
        Check(ws.S("normativa")=="NTC 2018"&&!ws.ContainsKey("migrazione_normativa"),"New document default is not explicit");
        var old=(JsonObject)fresh.DeepClone();old["workspace_ca"]!.AsObject().Remove("normativa");old["input"]!["gamma_c"]="1.71";
        var migrated=SectionWorkspace.Prepare(old);
        Check(migrated.S("normativa")=="NTC 2018"&&migrated.S("migrazione_normativa").Length>0,"Old profile not recorded");
        Check(ConcreteStandards.Effective(old["input"]!.AsObject(),migrated).GammaC==1.71,"Migration overwrote assigned coefficient");
        string once=old.ToJsonString();SectionWorkspace.Prepare(old);Check(once==old.ToJsonString(),"Migration is not idempotent");
        foreach(JsonNode? invalid in new JsonNode?[]{null,JsonValue.Create(""),JsonValue.Create("unknown"),JsonValue.Create(123)})
        {
            var bad=(JsonObject)fresh.DeepClone();bad["workspace_ca"]!["normativa"]=invalid?.DeepClone();
            Reject(()=>SectionWorkspace.Prepare(bad));
            Reject(()=>ConcreteStandards.Effective(bad["input"]!.AsObject(),bad["workspace_ca"]!.AsObject()));
        }
        Reject(()=>ConcreteStandards.Effective(fresh["input"]!.AsObject(),new JsonObject()));
        var incomplete=(JsonObject)fresh.DeepClone(); incomplete["input"]!.AsObject().Remove("gamma_c");
        var restored=SectionWorkspace.Prepare(incomplete);
        Check(restored["migrazione_input"]?["gamma_c"]?.ToJsonString()==incomplete["input"]!["gamma_c"]?.ToJsonString(),"Implicit input default was not recorded");
        string restoredOnce=incomplete.ToJsonString();SectionWorkspace.Prepare(incomplete);Check(restoredOnce==incomplete.ToJsonString(),"Input migration repeated");
        incomplete["input"]!["gamma_c"]=null;SectionWorkspace.Prepare(incomplete);
        Reject(()=>ConcreteStandards.Effective(incomplete["input"]!.AsObject(),restored));
        foreach(string badName in new[]{"", "NTC typo", "Unknown"})
        { var bridge=BridgeSection.Defaults();bridge["normativa"]=badName;Reject(()=>BridgeSection.ApplyStandardDefaults(bridge)); }
        var missing=SezioneCA.DefaultData();missing["workspace_ca"]!.AsObject().Remove("normativa");
        Check(CalculationValidation.Coefficients("str_palo",missing).Any(i=>i.Path=="normativa"),"Project masks missing standard");
        var result=new JsonObject{["motore"]="GPCChecker.Concrete.dll",["normativa_riferimento"]="NTC 2018",["dati"]=fresh.DeepClone()};
        var changed=(JsonObject)fresh.DeepClone();changed["workspace_ca"]!["normativa"]="DIN EN 1992-1-1";
        using(var zip=new ZipArchive(new MemoryStream(ReportConcrete.Create("Source",changed,result,["materiali"],includeInputs:false))))
        using(var reader=new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {
            string text=reader.ReadToEnd();Check(text.Contains("Normativa selezionata: NTC 2018")&&!text.Contains("Normativa selezionata: DIN"),"Report read modified editor standard");
        }
        result["normativa_riferimento"]="DIN EN 1992-1-1";
        Reject(()=>ReportConcrete.Create("Invalid",fresh,result,["materiali"]));
        result.Remove("dati");Reject(()=>ReportConcreteShort.Create("Old",fresh,result));
        foreach (bool active in new[] { false, true })
        {
            var micro=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/f3/inputs/geo_micropalo_verticale--regressione-micropalo_igu_0_nessuna-riduzione.json")))!["data"]!.AsObject();
            micro["stratigrafie"]![0]![0]!["terreno"]="Sconosciuto";micro["stratigrafie"]![0]![0]!["laterale_attiva"]=active;
            Check(Calcolo.Calcola(micro,true).S("errore").Length>0,"Unknown micropile soil replaced by gravel");
        }
        return checks;
    }
}
