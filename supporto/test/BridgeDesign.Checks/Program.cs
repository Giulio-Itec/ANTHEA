using Anthea.Calculations;
using X.Core;
using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Xml.Linq;

string output = Path.GetFullPath(args.FirstOrDefault() ?? "supporto/artefatti/bridge_design/calcoli");
Directory.CreateDirectory(output); var log = new List<string>(); int checks = 0;
void Assert(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
void Near(double actual, double expected, string message, double tol = 1e-9) => Assert(Math.Abs(actual - expected) <= tol * Math.Max(1, Math.Abs(expected)), message + $": {actual} vs {expected}");
void Invalid(Action action, string message) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception(message); }
try
{
    const double q = 20, l = 12, ei = 2e5;
    var simple = BridgeConcept.Analyze([l], q, ei, false);
    Near(simple.Reactions[0], q*l/2, "Appoggio semplice R");
    Near(simple.Stations.Max(s=>s.Moment), q*l*l/8, "Appoggio semplice M");
    Near(simple.Stations.Max(s=>s.DeflectionMm), 5*q*Math.Pow(l,4)/(384*ei)*1000, "Appoggio semplice freccia");
    var two = BridgeConcept.Analyze([l,l], q, ei, true);
    Near(two.Stations.Min(s=>s.Moment), -q*l*l/8, "Due campate M centrale");
    Near(two.Reactions[0], 3*q*l/8, "Due campate R esterna");
    Near(two.Reactions[1], 5*q*l/4, "Due campate R centrale");
    Near(two.Stations.Max(s=>s.Moment), 9*q*l*l/128, "Due campate M positivo esatto");
    log.Add("PASS: trave appoggiata e due campate, momenti / reazioni / freccia da formule chiuse.");
    var rng = new Random(260926);
    for (int t=0; t<200; t++)
    {
        double[] spans = Enumerable.Range(0,rng.Next(1,25)).Select(_ => 5+rng.NextDouble()*70).ToArray();
        double load = rng.NextDouble()*500; var beam = BridgeConcept.Analyze(spans,load,ei,true);
        Near(beam.Reactions.Sum(),load*spans.Sum(),"Equilibrio verticale");
        double x=0, moment=0;
        for(int j=0;j<beam.Reactions.Length;j++) { moment += beam.Reactions[j]*x; if(j<spans.Length)x+=spans[j]; }
        Near(moment,load*spans.Sum()*spans.Sum()/2,"Equilibrio globale momenti");
        foreach(var span in beam.Stations.GroupBy(s=>s.Span)) { Near(span.First().DeflectionMm,0,"Freccia iniziale"); Near(span.Last().DeflectionMm,0,"Freccia finale",1e-5); }
    }
    log.Add("PASS: 200 travi con campate diseguali, equilibrio globale e compatibilità agli appoggi.");
    var data=BridgeConcept.Defaults(); string before=data.ToJsonString(); var r=BridgeConcept.Calculate(data);
    Assert(before==data.ToJsonString(),"Il motore modifica gli input"); Near(r.Width,20.2,"Larghezza riferimento"); Near(r.Spans.Sum(),105,"Somma campate");
    Near(r.Spans[0]/r.Spans[1],.8,"Rapporto campate terminali");
    Near(r.TotalCost,r.Quantities.Sum(v=>v.Amount*v.Rate)*1.12*1.15,"Computo e maggiorazioni");
    Assert(r.Supports.Length==6,"Numero appoggi");
    Assert(r.Quantities.Where(v=>v.Group=="Fondazioni"&&v.Unit=="m").Sum(v=>v.Amount)==r.Supports.Sum(s=>s.Piles)*r.PileLength,"Lunghezza pali");
    Assert(r.Supports.All(s=>s.FootingWidth>=s.FootingSize),"Dimensioni fondazione");
    Near(r.Carbon,r.Quantities.Sum(v=>v.Carbon)*1.15,"CO2 totale");
    data["scene"]=87; Near(BridgeConcept.Calculate(data).TotalCost,r.TotalCost,"Paesaggio non influenza calcolo");
    foreach(var p in BridgeConcept.Rates) data["rates"]![p.Key]=2*data["rates"].D(p.Key);
    var doubled=BridgeConcept.Calculate(data); Near(doubled.TotalCost,2*r.TotalCost,"Prezzi raddoppiati"); Near(doubled.Carbon,r.Carbon,"Prezzi non alterano CO2");
    data=BridgeConcept.Defaults(); data["input"]!["low_carbon"]=true; var low=BridgeConcept.Calculate(data);
    Assert(low.Carbon<r.Carbon,"Cls ridotto non riduce CO2"); Near(low.TotalCost,r.TotalCost,"Fattori ambientali non alterano prezzi");
    data=BridgeConcept.Defaults(); data["input"]!["spans"]=0; var automatic=BridgeConcept.Calculate(data);
    Assert(automatic.Spans.Length>=1&&data["input"].D("spans")==0,"Zero auto non preservato");
    foreach(var family in BridgeConcept.Families)
        foreach(var soil in BridgeConcept.Soils)
            foreach(var pier in BridgeConcept.Piers)
            {
                var d=BridgeConcept.Defaults(); d["input"]!["family"]=family.Id; d["input"]!["length"]=(family.MinSpan+family.MaxSpan)/2*3;
                d["input"]!["spans"]=3;d["input"]!["height"]=40;d["input"]!["obstacle"]="Nessuno";d["input"]!["soil"]=soil;d["input"]!["pier"]=pier;
                var result=BridgeConcept.Calculate(d);
                Assert(double.IsFinite(result.TotalCost)&&result.TotalCost>0&&result.Carbon>0&&result.Inertia>0,"Risultato non finito / negativo");
                Assert(result.Quantities.All(v=>v.Amount>=0&&double.IsFinite(v.Amount)),"Quantità negativa");
                var exported=CalculationService.Calculate(BridgeConcept.Module,d); Assert(exported["bridge_design"] is JsonObject,"Dispatch del modulo");
                if(soil==BridgeConcept.Soils[0]&&pier==BridgeConcept.Piers[0]) File.WriteAllText(Path.Combine(output,family.Id+".json"),result.Json().ToJsonString(J.Options));
            }
    log.Add("PASS: 128 combinazioni di famiglia, terreno e pila, dispatch della libreria.");
    data=BridgeConcept.Defaults(); data["input"]!["continuous"]=false; data["input"]!["spans"]=1; data["input"]!["length"]=40; data["input"]!["end_pier"]=true;
    var ends=BridgeConcept.Calculate(data); Assert(ends.Supports.Count(s=>s.Type=="Spalla")==1,"Estremo su pila");
    data=BridgeConcept.Defaults(); data["input"]!["foundation"]="Pali Ø 1,5 m"; data["input"]!["pile_count"]=1;
    Assert(BridgeConcept.Calculate(data).Supports.All(s=>s.Piles==1),"Numero pali manuale");
    foreach(var invalid in new[] { ("input","length","NaN"),("input","lanes","2.5"),("rates","steel","-1"),("assumptions","co2_site",""),("input","obstacle_width","500") })
    { var d=BridgeConcept.Defaults();d[invalid.Item1]![invalid.Item2]=invalid.Item3;Invalid(()=>BridgeConcept.Calculate(d),"Dato invalido accettato"); }
    data=BridgeConcept.Defaults();data["input"]!["depth"]=.1;Invalid(()=>BridgeConcept.Calculate(data),"Sezione impossibile accettata");
    data=BridgeConcept.Defaults(); data["alternative_a"]=BridgeConcept.Defaults(); data["rates"]!["concrete_deck"]=333;
    var doc=Archivio.Documento(BridgeConcept.Module);doc["dati"]=data; var file=Path.Combine(output,"esempio.anthea");Archivio.Scrivi(file,doc);
    var reopened=Archivio.Leggi(file); Assert(J.Equivalent(reopened["dati"],data),"Round-trip prezzi / baseline");
    r=BridgeConcept.Calculate(data); File.WriteAllText(Path.Combine(output,"quantita.csv"),BridgeConceptExport.Csv(r));
    var bytes=BridgeConceptExport.Report("Prova Bridge Design",data,r); File.WriteAllBytes(Path.Combine(output,"report.docx"),bytes);
    using(var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read))
    {
        foreach(var entry in zip.Entries.Where(e=>e.FullName.EndsWith(".xml")||e.FullName.EndsWith(".rels"))) { using var stream=entry.Open(); _=XDocument.Load(stream); checks++; }
        using var documentStream=zip.GetEntry("word/document.xml")!.Open(); string text=XDocument.Load(documentStream).ToString();
        Assert(text.Contains("Confronto con alternativa A")&&text.Contains("333")&&text.Contains("Predimensionamento"),"Report incompleto");
    }
    log.Add("PASS: modifica listino, CO₂, invalidazione, archivi, confronto A, CSV e struttura DOCX.");
    int optimizationChecks = OptimizationChecks.Run(output); checks += optimizationChecks;
    log.Add($"PASS: {optimizationChecks} controlli nuovi su ottimizzazione, vincoli e dimensioni tecniche.");
    int explorationChecks = ExplorationChecks.Run(output); checks += explorationChecks;
    log.Add($"PASS: {explorationChecks} controlli su esplorazione, intervalli, progress e frontiera Pareto.");
    log.Add($"TOTALE: {checks} controlli superati.");
    File.WriteAllLines(Path.Combine(output,"checks.txt"),log); Console.WriteLine(string.Join(Environment.NewLine,log));
}
catch(Exception ex) { File.WriteAllText(Path.Combine(output,"errore.txt"),ex.ToString()); Console.Error.WriteLine(ex); Environment.ExitCode=1; }
