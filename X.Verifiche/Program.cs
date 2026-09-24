using System.Text.Json.Nodes;
using X.Core;
if (args.Length == 2 && args[0] == "--software") { SoftwareChecks.Run(JsonNode.Parse(File.ReadAllText(args[1]))!.AsArray()); return 0; }
if (args.Length == 2 && args[0] == "--micropalo") { MicropileChecks.Run(JsonNode.Parse(File.ReadAllText(args[1]))!.AsArray()); return 0; }
if (args.Length == 1 && args[0] == "--coesione") { CohesionChecks.Run(); return 0; }
if (args.Length == 1 && args[0] == "--gamma-sat") { SaturatedWeightChecks.Run(); return 0; }
if (args.Length == 1 && args[0] == "--horizontal") { Console.WriteLine($"Palo orizzontale: {HorizontalChecks.Run()} controlli superati."); HorizontalChsChecks.Run(); return 0; }
if (args.Length == 1 && args[0] == "--checker") { SectionWorkspaceChecks.Run(); SectionExchangeChecks.Run(); ConcreteEnhancementChecks.Run(); return 0; }
if (args.Length == 1 && args[0] == "--ca-module") { ConcreteModuleChecks.Run(); return 0; }
if (args.Length == 2 && args[0] == "--ca-benchmark") { ConcreteBenchmark.Run(args[1]); return 0; }

if(args.Length==3&&args[0]=="--calcola")
{
    var doc=JsonNode.Parse(File.ReadAllText(args[1]))!;var data=doc["dati"]??doc;var module=doc.S("modulo_id");
    if(module=="str_palo" && data["workspace_ca"].D("versione")>=2)
    {
        Console.Error.WriteLine("Il comando diagnostico --calcola usa il motore storico, con altra convenzione dei segni. Per un workspace Checker usare il calcolo e l’esportazione JSON dell’interfaccia WPF.");return 2;
    }
    var result=module is PaloOrizzontale.Module or MicropaloOrizzontale.Module?PaloOrizzontale.Calculate(data.AsObject()):module=="str_palo"?CalcoloSezione.Calcola(data.AsObject()):Calcolo.Calcola(data,module=="geo_micropalo_verticale");
    File.WriteAllText(args[2],result.ToJsonString(J.Options));return result.S("errore")==""?0:1;
}
Console.WriteLine("ANTHEA — verifiche C#");
if(args.Length==0){Console.WriteLine("Specificare il percorso del file casi_confronto.json.");return 2;}
var cases=JsonNode.Parse(File.ReadAllText(args[0]))!.AsArray();int count=0,numbers=0,failed=0;double maxAbs=0,maxRel=0;string maxPath="";
void Compare(JsonNode? a,JsonNode? b,string path)
{
    if(a is null||b is null){if(a is not null||b is not null)throw new Exception(path+": null diverso");return;}
    if(a is JsonObject ao)
    {
        if(b is not JsonObject bo)throw new Exception(path+": oggetto atteso");
        foreach(var (k,v) in ao){if(k=="checks")continue;if(!bo.ContainsKey(k))throw new Exception(path+"."+k+": campo assente");Compare(v,bo[k],path+"."+k);}return;
    }
    if(a is JsonArray ar)
    {
        if(b is not JsonArray br||ar.Count!=br.Count)throw new Exception(path+": lunghezza diversa");
        for(int i=0;i<ar.Count;i++)Compare(ar[i],br[i],path+"["+i+"]");return;
    }
    if(a is JsonValue av&&av.TryGetValue<double>(out double x)&&b is JsonValue bv&&bv.TryGetValue<double>(out double y))
    {
        numbers++;double abs=Math.Abs(x-y),rel=abs/Math.Max(1,Math.Abs(x));if(abs>maxAbs){maxAbs=abs;maxPath=path;}maxRel=Math.Max(maxRel,rel);
        // Errori double tra runtime; nessuna tolleranza percentuale ingegneristica.
        if(!double.IsFinite(y)||abs>1e-8+1e-10*Math.Abs(x))throw new Exception($"{path}: Python={x:R}; C#={y:R}; delta={abs:R}");return;
    }
    if(a.ToJsonString()!=b.ToJsonString())throw new Exception(path+": valore diverso: "+a+" / "+b);
}
foreach(var item in cases)
{
    try
    {
        var c=item!;JsonNode actual;string kind=c.S("tipo");var input=c["input"]!;
        switch(kind)
        {
            case "palo":case "micropalo":actual=Calcolo.Calcola(input,kind=="micropalo");break;
            case "efficienza":actual=Calcolo.Efficienza(input);break;
            case "nq":actual=Nq.Dettaglio(input.D("phi"),input.D("rapporto"),input.B("grande"));break;
            case "chs":actual=Chs.Peso(input.S("profilo"),input.D("diametro"),input.D("gamma"));break;
            case "bd":actual=BustamanteDoix.Parametro(input.S("terreno"),input.S("iniezione"),input.D("pressione"),input.D("alpha"));break;
            case "sezione":
                var engine=new SezioneCA(input["parametri"]!.AsObject(),(int)input.D("nr",28),(int)input.D("na",96));
                actual=engine.Analyze(input.D("n"),input.D("mx"),input.D("my"));break;
            case "elastica":
                var e=new SezioneCA(input["parametri"]!.AsObject(),(int)input.D("nr",28),(int)input.D("na",96));
                actual=J.Node(SezioneElastica.Tensioni(e,input.D("coeff_n"),input.D("n"),input.D("mx"),input.D("my")))!;break;
            case "resistenza_elastica":
                var re=new SezioneCA(input["parametri"]!.AsObject(),(int)input.D("nr",28),(int)input.D("na",96));
                var (m,st)=SezioneElastica.Resistenza(re,input.D("coeff_n"),input.D("n"),input.D("direction"));actual=J.Obj(("moment",m),("state",st));break;
            case "dominio":
                var de=new SezioneCA(input["parametri"]!.AsObject(),12,36);actual=J.Obj(("nm",Domini.NM(de,input.S("mode"),input.D("coeff_n"))),("mm",Domini.MM(de,input.S("mode"),input.D("coeff_n"),input.D("n"),12)));break;
            default:throw new Exception("Tipo sconosciuto: "+kind);
        }
        if(c.B("atteso_errore")){if(actual.S("errore")=="")throw new Exception("Errore atteso, calcolo accettato.");}
        else Compare(c["atteso"],actual,c.S("nome"));count++;
    }
    catch(Exception ex){Console.WriteLine("FAIL "+item.S("nome")+": "+ex.Message);failed++;}
}
int softwareChecks=0;try{softwareChecks=SoftwareChecks.Run(cases);}catch(Exception ex){failed++;Console.WriteLine("FAIL software: "+ex);}
try{softwareChecks+=SectionWorkspaceChecks.Run();}catch(Exception ex){failed++;Console.WriteLine("FAIL workspace CA: "+ex);}
try{softwareChecks+=SectionExchangeChecks.Run();}catch(Exception ex){failed++;Console.WriteLine("FAIL Excel azioni: "+ex);}
try{softwareChecks+=ConcreteEnhancementChecks.Run();}catch(Exception ex){failed++;Console.WriteLine("FAIL estensioni CA: "+ex);}
try{softwareChecks+=HorizontalChecks.Run();}catch(Exception ex){failed++;Console.WriteLine("FAIL palo orizzontale: "+ex);}
var report=J.Obj(("casi_superati",count),("controlli_software_superati",softwareChecks),("casi_falliti",failed),("valori_numerici_confrontati",numbers),("massimo_delta_assoluto",maxAbs),("massimo_delta_relativo_scalato",maxRel),("percorso_massimo_delta",maxPath),("tolleranza_assoluta",1e-8),("tolleranza_relativa",1e-10));
Console.WriteLine(report.ToJsonString(J.Options));if(args.Length>1)File.WriteAllText(args[1],report.ToJsonString(J.Options));return failed==0?0:1;
