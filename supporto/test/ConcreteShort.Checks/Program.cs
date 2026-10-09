using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Xml.Linq;
using Anthea.Calculations;
using X.Core;
using X.Desktop;

string folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
// SLE engine (refactoring F2.7, commit A5): '--motore-sle legacy|libreria', otherwise the default engine of ConcreteServiceabilityAdapter.
ServiceabilityEngine? sle=args.SkipWhile(a=>a!="--motore-sle").Skip(1).FirstOrDefault() switch
{
    null=>null,"legacy"=>ServiceabilityEngine.Legacy,"libreria"=>ServiceabilityEngine.Library,
    var other=>throw new ArgumentException("--motore-sle: legacy o libreria, non "+other)
};
int assertions=0;void Check(bool pass,string message){if(!pass)throw new Exception(message);assertions++;}
var data=SezioneCA.DefaultData();var ws=SectionWorkspace.Prepare(data);var input=data["input"]!.AsObject();
ws["sle_comuni"]!["esposizione"]="XC1";ws["dettagli_costruttivi"]!["elemento"]="Pilastro";
SectionWorkspace.Prepare(data);
var result=J.Obj(("motore","GPCChecker.Concrete.dll"),("domini",new JsonObject()),("tensioni",new JsonObject()),("taglio",new JsonObject()));
foreach(string family in SectionWorkspace.Sets)
{
    var actions=new JsonArray();data["combinazioni"]![family]=actions;
    foreach(int i in new[]{1,2})actions.Add(J.Obj(("id",family+i),("nome",family+" combinazione "+i),("azioni",new[]{(-500*i).ToString(),(80*i).ToString(),(30*i).ToString()}),("visible",i==1)));
    if(family is "SLU" or "SLV")
    {
        var domain=new CheckerSection(input,ws,ws["dominio3d"]!.AsObject(),family).Domain3D();var checks=new JsonObject();
        foreach(var action in actions)checks[action.S("id")]=J.Node(domain.Check(new(action!.Array("azioni")[0]!.GetValue<string>() is string n?double.Parse(n):0,double.Parse(action.Array("azioni")[1]!.GetValue<string>()),double.Parse(action.Array("azioni")[2]!.GetValue<string>()))));
        result["domini"]!["3D:"+family]=checks;
    }
    else
    {
        var options=ws["sle"]![family]!.AsObject();options["esposizione"]="XC1";
        var engine=new CheckerSection(input,ws,options,engine:sle);var checks=new Dictionary<string,StressOutcome>();
        foreach(var row in actions)
        {
            var a=row!.Array("azioni");var force=new ActionPoint(double.Parse(a[0]!.GetValue<string>()),double.Parse(a[1]!.GetValue<string>()),double.Parse(a[2]!.GetValue<string>()));
            var s=engine.Stress(force,family);var crack=ConcreteServiceabilityAdapter.Cracking(engine,s,force,input,ws,options,family,sle);
            checks[row.S("id")]=new(s,s.Ratio,s.Status,crack.Status,crack);
        }
        result["tensioni"]![family]=ConcreteAnalysisSession.ExportStress(checks);
    }
}
var so=ws["taglio"]!.AsObject();ConcreteCalculationSettings.UpdateAutomaticShear(input,so);
so["azioni"]=new JsonArray(J.Obj(("id","v1"),("nome","Taglio 1"),("N","0"),("Mx","0"),("My","0"),("Vx","20"),("Vy","40"),("T","0")),J.Obj(("id","v2"),("nome","Taglio 2"),("N","0"),("Mx","0"),("My","0"),("Vx","80"),("Vy","20"),("T","0")));
foreach(var row in so.Array("azioni"))result["taglio"]![row.S("id")]=J.Node(ConcreteShearAnalysis.Calculate(input,ws,so,row!.AsObject()).Shear);
var selected=ReportConcreteShort.Select(data,result);
Check(selected.Length==7,"Seven checks required");Check(selected.All(r=>r.Complete==2 && r.Total==2),"Incomplete fixture");
Check(selected[0].Combination.EndsWith("2"),"Hidden worst SLU omitted");Check(selected[2].Combination=="Taglio 2","Wrong shear combination");
// Stress and cracking must select their own simultaneous load cases, not the same stress governor.
var changed=(JsonObject)result.DeepClone();changed["tensioni"]!["SLE_QP"]!["SLE_QP1"]!["Ratio"]=.99;
changed["tensioni"]!["SLE_QP"]!["SLE_QP2"]!["CrackResult"]!["Ratio"]=1.1;
var independent=ReportConcreteShort.Select(data,changed);
Check(independent[5].Combination.EndsWith("1")&&independent[6].Combination.EndsWith("2"),"Independent SLE governors mixed");
Check(independent[6].Outcome=="NON VERIFICATA","Failed check hidden");
changed["domini"]!["3D:SLU"]!.AsObject().Remove("SLU1");
Check(ReportConcreteShort.Select(data,changed)[0].Outcome.StartsWith("INCOMPLETA"),"Missing result silently discarded");
var empty=(JsonObject)data.DeepClone();empty["combinazioni"]!["SLU"]=new JsonArray();
Check(ReportConcreteShort.Select(empty,result)[0].Outcome.StartsWith("Non richiesta"),"Empty family confused with missing results");
var close=(JsonObject)result.DeepClone();close["domini"]!["3D:SLU"]!["SLU1"]!["Utilization"]=.9902;close["domini"]!["3D:SLU"]!["SLU2"]!["Utilization"]=.9901;
Check(ReportConcreteShort.Select(data,close)[0].Combination.EndsWith("1"),"Rounded ratio used for selection");
var bytes=ReportConcreteShort.Create("Sezione di prova con tutte le verifiche",data,result);
using(var zip=new ZipArchive(new MemoryStream(bytes)))
{
    using var reader=new StreamReader(zip.GetEntry("word/document.xml")!.Open());var xml=XDocument.Parse(reader.ReadToEnd());XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    Check(xml.Descendants(w+"tbl").Count()==2,"Short report should have two tables");
    Check(xml.Descendants(w+"br").Count(b=>b.Attribute(w+"type")?.Value=="page")==1,"Expected one deliberate page break");
}
await ShortReportExport.WriteAsync(Path.Combine(folder,"Relazione_short.docx"),bytes);
Check(File.Exists(Path.Combine(folder,"Relazione_short.pdf")),"Companion PDF missing");
var oversize=ReportConcreteShort.Create(string.Join("\n",Enumerable.Repeat("Titolo di prova volutamente lungo",150)),data,result);
bool rejected=false;
try{await ShortReportExport.WriteAsync(Path.Combine(folder,"oversize.docx"),oversize);}
catch(InvalidOperationException ex) when(ex.Message.Contains("pagine")){rejected=true;}
Check(rejected && !File.Exists(Path.Combine(folder,"oversize.docx")) && !File.Exists(Path.Combine(folder,"oversize.pdf")),"More than two pages exported");
File.WriteAllText(Path.Combine(folder,"source.json"),data.ToJsonString());File.WriteAllText(Path.Combine(folder,"results.json"),result.ToJsonString());
File.WriteAllText(Path.Combine(folder,"test-results.txt"),$"PASS {assertions} assertions (SLE engine {sle??ConcreteServiceabilityAdapter.Default}); Word pagination accepted (at most two pages), PDF exported.");
Console.WriteLine($"PASS {assertions} assertions");
