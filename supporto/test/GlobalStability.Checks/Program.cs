using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Calculations.Geotechnics;
using X.Core;

string directory = Path.GetFullPath(args.FirstOrDefault() ?? "supporto/artefatti/stabilita-globale"); Directory.CreateDirectory(directory);
bool writeReports = !args.Contains("--checks-only");
File.WriteAllText(Path.Combine(directory, "stato-esecuzione.txt"), "In corso " + DateTimeOffset.Now.ToString("O"));
var log = new List<string>(); int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); log.Add("OK " + message); checks++; }
void Near(double value, double reference, string message, double tolerance = 1e-6) => Check(Math.Abs(value - reference) <= tolerance * Math.Max(1, Math.Abs(reference)), message + $" ({value:G10}; atteso {reference:G10})");
void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { Check(true, message); return; } throw new Exception("Accettato: " + message); }
JsonObject Model(string family = "cantilever")
{
    var d = RetainingWall.Example(family); d["layers"]![0]!["thickness"] = 25;
    RetainingWall.PrepareGlobalProfile(d); d["global_stability"]!["soil_mode"] = "Profilo unico"; // Historical benchmarks use one deep geological column.
    var g = d["global_stability"]!; g["enabled"] = true; g["profile_confirmed"] = true;
    g["grid"] = 9; g["slices"] = 60; g["refinements"] = 4; g["depth_min"] = .1; g["depth_max"] = 10; g["search_mode"] = "Assegnata";
    return d;
}
try
{
    var proposal = RetainingWall.Example("cantilever"); RetainingWall.PrepareGlobalProfile(proposal); var pg = proposal["global_stability"]!;
    pg.Array("layers")[0]!["bottom"] = -8; pg.Array("valley_layers")[0]!["bottom"] = -2;
    pg.Array("valley")[0]!["x"] = -30; pg.Array("uphill")[1]!["x"] = 22;
    string soilBefore = pg.Array("layers").ToJsonString() + pg.Array("valley_layers").ToJsonString();
    RetainingWall.ProposeGlobalSearch(proposal);
    Near(pg.D("depth_max"), 2, "Proposta automatica limitata dalla colonna indagata meno profonda");
    Near(pg.D("exit_min"), -30, "Proposta delle uscite entro il rilievo"); Near(pg.D("entry_max"), 22, "Proposta degli ingressi entro il rilievo");
    Check(soilBefore == pg.Array("layers").ToJsonString() + pg.Array("valley_layers").ToJsonString(), "Proposta di ricerca non modifica né estende le indagini");
    pg.Array("valley_layers")[0]!["bottom"] = ""; RetainingWall.ProposeGlobalSearch(proposal);
    Check(pg.S("depth_max") == "", "Indagine incompleta non genera una profondità fittizia");
    pg["soil_mode"] = "Profilo unico"; RetainingWall.ProposeGlobalSearch(proposal);
    Near(pg.D("depth_max"), 6.9, "Profilo unico: proposta fino a 2(H+t) entro gli strati noti");
    var circle = new SlipCircle(0, 10, 10, -3, 6);
    SlopeSlice Slice(double phi, double cohesion, double u = 0) => new(1, 0, 1, 0, 1, 30, "Analitico", 100, 0, 5, 1, 0, 0, u, phi, cohesion, 100, 50, 0, 0, 0, 0);
    var friction = BishopSolver.Solve(circle, [Slice(30, 0)], 1.1)!;
    Near(friction.Factor, 1, "Bishop caso analitico senza coesione F=tanφ/tanα"); Near(friction.Ratio, 1.1, "γR applicato una sola volta");
    var cohesive = BishopSolver.Solve(circle, [Slice(0, 10)], 1)!;
    Near(cohesive.Factor, 10 / Math.Cos(Math.PI / 6) / 50, "Bishop φ=0: F=Σcl/ΣWsinα");
    Check(BishopSolver.Solve(circle, [Slice(30, 0, 10)], 1)!.Factor < friction.Factor, "Pressione interstiziale riduce F");
    Check(BishopSolver.Solve(circle, [Slice(30, 0) with { Driving = 65 }], 1)!.Factor < friction.Factor, "Momento esterno destabilizzante riduce F");
    Near(friction.Slices[0].NormalEffective * Math.Cos(Math.PI / 6) + friction.Slices[0].Mobilized * Math.Sin(Math.PI / 6), 100, "Equilibrio verticale indipendente del concio");
    Near(friction.Resistance / friction.Factor, friction.Driving, "Equilibrio dei momenti indipendente");
    var constructed = SlopeGeometry.Through(new(-4, 0), new(12, 3.45), -3)!;
    Near(constructed.Base(-4), 0, "Cerchio passa per uscita"); Near(constructed.Base(12), 3.45, "Cerchio passa per ingresso"); Near(constructed.Y - constructed.Radius, -3, "Cerchio con profondità assegnata");
    var tangent = SlopeGeometry.TangentAtEntry(new(-2, 0), new(4, 3.5))!;
    Near(tangent.Base(-2), 0, "Cerchio tangente passa per l’uscita");
    Near(tangent.Base(4), 3.5, "Cerchio tangente passa per l’ingresso");
    Check(SlopeGeometry.Through(new(-2, 0), new(4, 3.5), tangent.Y - tangent.Radius) is not null, "Ingresso verticale ammesso: regressione emersa dal confronto MAX");
    var triangle = SlopeGeometry.Properties([new(0, 0), new(3, 0), new(0, 6)]);
    Near(triangle.Area, 9, "Area triangolo via GPC.Geometry"); Near(triangle.Centroid.X, 1, "Baricentro GPC x"); Near(triangle.Centroid.Y, 2, "Baricentro GPC y");
    var baseline = Model(); string before = baseline.ToJsonString(); var baselineResult = RetainingWall.CalculateGlobal(baseline);
    Check(before == baseline.ToJsonString(), "Calcolo globale puro: input invariato");
    Check(baselineResult.Cases.All(c => c.Critical is not null), "Caso base: ogni combinazione trova una superficie");
    Console.WriteLine(string.Join("\n", baselineResult.Cases.Select(c => $"BASE {c.Factors.Name}: F={c.Critical?.Factor} {c.Status}; {c.Solved}/{c.Tried}, errori={c.NumericalFailures}")));
    var solved = baselineResult.Cases[0].Critical!; var factors = baselineResult.Cases[0].Factors;
    var slices = SlopeStability.Slices(baselineResult.Section, solved.Circle, factors, 80);
    Near(slices.Sum(s => s.BodyWeight), RetainingWall.Outline(baseline).Length > 0 ? SectionGeometry.Area(RetainingWall.Outline(baseline)) * baseline["materials"].D("gamma") : 0, "Massa del muro integrata una volta", 1e-7);
    Near(slices.Sum(s => s.VerticalLoad), 0, "Combinazione senza variabile: nessun sovraccarico");
    Near(solved.Slices.Sum(s => s.Resistance) / solved.Factor, solved.Driving, "Momenti equilibrati sulla superficie reale", 1e-7);
    Check(factors.MPhi == 1.25 && factors.MC == 1.25 && factors.MCu == 1.4 && factors.R == 1.1, "Preset globale A2 M2 R2");
    var dense = (JsonObject)baseline.DeepClone(); dense["global_stability"]!["grid"] = 13; dense["global_stability"]!["slices"] = 120;
    var denseResult = RetainingWall.CalculateGlobal(dense);
    Console.WriteLine($"SEARCH coarse={solved.Circle} F={solved.Factor}; dense={denseResult.Cases[0].Critical?.Circle} F={denseResult.Cases[0].Critical?.Factor}");
    Check(Math.Abs(denseResult.Cases[0].Critical!.Factor / solved.Factor - 1) < .03, "Ricerca più densa: variazione F inferiore al 3%");
    var incomplete = (JsonObject)baseline.DeepClone(); incomplete["global_stability"]!["profile_confirmed"] = false; Reject(() => RetainingWall.CalculateGlobal(incomplete), "Profilo non confermato rifiutato");
    incomplete = (JsonObject)baseline.DeepClone(); incomplete["global_stability"]!["depth_max"] = 100; Reject(() => RetainingWall.CalculateGlobal(incomplete), "Nessuna estrapolazione sotto gli strati indagati");
    incomplete = (JsonObject)baseline.DeepClone(); incomplete["geometry"]!["height"] = 4; Reject(() => RetainingWall.CalculateGlobal(incomplete), "Geometria modificata richiede allineamento profilo");
    incomplete = (JsonObject)baseline.DeepClone(); incomplete["global_stability"]!["condition"] = "Non drenata"; Reject(() => RetainingWall.CalculateGlobal(incomplete), "cu mancante rifiutata in non drenato");
    incomplete = (JsonObject)baseline.DeepClone(); incomplete["global_stability"]!["water_enabled"] = true; foreach (var p in incomplete["global_stability"]!.Array("water")) p!["y"] = 10;
    Reject(() => RetainingWall.CalculateGlobal(incomplete), "Acqua esterna non supportata non ignorata");
    var custom = (JsonObject)baseline.DeepClone(); var cg = custom["global_stability"]!; cg["combinations"] = RetainingWall.GenerateGlobalCombinations(custom); cg["combination_mode"] = "Personalizzate"; cg["combination_signature"] = RetainingWall.GlobalSignature(custom);
    cg["combinations"]![0]!["r"] = 1.5; var cr = RetainingWall.CalculateGlobal(custom); Near(cr.Cases[0].Critical!.Factor, solved.Factor, "Cambio γR non modifica F"); Near(cr.Cases[0].Critical!.Ratio, 1.5 / solved.Factor, "Matrice globale personalizzata applicata");
    custom["actions"]![0]!["psi0"] = .6; Reject(() => RetainingWall.CalculateGlobal(custom), "Matrice globale obsoleta rifiutata");
    var cts = new CancellationTokenSource(); cts.Cancel(); try { RetainingWall.CalculateGlobal(baseline, cts.Token); throw new Exception("Token ignorato"); } catch (OperationCanceledException) { Check(true, "Ricerca cancellabile"); }

    var examples = new List<(string Id, string Description, JsonObject Data)>();
    examples.Add(("01-mensola-base", "Mensola H=3 m, terreno granulare asciutto φ=30°, q=10 kPa.", baseline));
    var high = Model(); high["geometry"]!["height"] = 5; RetainingWall.PrepareGlobalProfile(high); high["global_stability"]!["enabled"] = true; high["global_stability"]!["profile_confirmed"] = true; high["global_stability"]!["depth_min"] = .1;
    examples.Add(("02-mensola-alta", "Mensola H=5 m; effetto dell’altezza con profilo riallineato.", high));
    examples.Add(("03-gravita", "Muro a gravità, sezione trapezia; peso reale del corpo rigido.", Model("gravity")));
    var weak = Model(); weak["global_stability"]!["layers"]![0]!["bottom"] = 0; weak["global_stability"]!.Array("layers").Add(J.Obj(("name", "Fondazione debole"), ("bottom", -22), ("gamma", 18), ("gamma_sat", 20), ("phi", 22), ("c", 0), ("cu", "")));
    examples.Add(("04-fondazione-debole", "φ=30° nel riempimento; φ=22° sotto il piano di posa.", weak));
    var coh = (JsonObject)weak.DeepClone(); coh["global_stability"]!["layers"]![1]!["c"] = 10;
    examples.Add(("05-coesione-profonda", "Come 04, c′=10 kPa nello strato di fondazione; drenato.", coh));
    var lens = (JsonObject)weak.DeepClone(); lens["global_stability"]!["layers"]![1]!["bottom"] = -2; lens["global_stability"]!.Array("layers").Add(J.Obj(("name", "Substrato resistente"), ("bottom", -22), ("gamma", 20), ("gamma_sat", 22), ("phi", 38), ("c", 0), ("cu", "")));
    examples.Add(("06-tre-strati", "Riempimento, strato debole fino a −2 m e substrato φ=38°.", lens));
    var wet = Model(); wet["global_stability"]!["water_enabled"] = true;
    var waterLine = wet["global_stability"]!.Array("water"); waterLine[0]!["y"] = -.1; waterLine[1]!["y"] = -.1; waterLine[2]!["x"] = 3; waterLine[2]!["y"] = 1.5; waterLine[3]!["y"] = 1.5;
    examples.Add(("07-falda", "Falda da y=−0,10 m a valle a y=1,50 m al tallone e a monte; pesi saturi e pressioni interstiziali.", wet));
    var seismic = Model(); var sd = seismic["seismic"]!; sd["enabled"] = true; sd["ag_g"] = .2; sd["f0"] = 2.5; sd["soil_class"] = "C"; seismic["global_stability"]!["seismic"] = true;
    var seismicRows = RetainingWall.GenerateGlobalCombinations(seismic); var seismicRow = seismicRows.First(c => c.S("state") == "SISMA")!;
    Near(seismicRow.D("kh"), .38 * 1.4 * .2, "SLV βs indipendente dalle spinte"); Near(seismicRow.D("r"), 1.2, "γR=1,2 globale SLV");
    sd["method"] = "Wood semplificato"; Near(RetainingWall.GenerateGlobalCombinations(seismic).First(c => c.S("state") == "SISMA").D("kh"), seismicRow.D("kh"), "Wood non sostituisce βs con βm"); sd["method"] = "Mononobe–Okabe";
    examples.Add(("08-sisma", "SLV: ag/g=0,20, F0=2,5, categoria C, St=1; kh=0,1064 e kv=±0,0532.", seismic));
    var impact = Model(); var action = RetainingWall.NewAction(impact, "Urto"); action["value"] = 50; impact.Array("actions").Add(action);
    examples.Add(("09-urto", "Urto equivalente H=50 kN/m in testa; combinazione eccezionale separata.", impact));
    var undrained = Model(); undrained["global_stability"]!["condition"] = "Non drenata"; undrained["global_stability"]!["layers"]![0]!["cu"] = 50;
    examples.Add(("10-non-drenato", "cu=50 kPa, φu=0; analisi globale a tensioni totali. Conci in trazione segnalati, nessun taglio automatico.", undrained));
    var summary = new List<object>();
    foreach (var example in examples)
    {
        string folder = Path.Combine(directory, "esempi", example.Id); Directory.CreateDirectory(folder);
        var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = example.Data.DeepClone(); Archivio.Scrivi(Path.Combine(folder, "modello.anthea"), doc);
        var loaded = Archivio.Leggi(Path.Combine(folder, "modello.anthea")); Check(loaded["dati"]!.ToJsonString() == example.Data.ToJsonString(), "Archivio ripercorribile " + example.Id);
        var result = RetainingWall.Calculate(example.Data); Check(result.GlobalStability is not null, "Integrazione calcolo locale/globale " + example.Id);
        File.WriteAllText(Path.Combine(folder, "risultati-anthea.json"), result.Json().ToJsonString(J.Options));
        File.WriteAllText(Path.Combine(folder, "conci-anthea.csv"), ReportRetainingWall.GlobalCsv(result.GlobalStability!));
        if (writeReports) Archivio.ScriviAtomico(Path.Combine(folder, "relazione-anthea.docx"), ReportRetainingWall.Create(example.Id, result));
        var guide = $"# {example.Id}\n\n{example.Description}\n\nAprire modello.anthea, Terreno → Stabilità globale. I parametri, le combinazioni e il dominio di ricerca sono salvati nel file. Vista Verifiche → Stabilità globale.\n\n## Confronto MAX 16\n\nQuesta procedura genera soltanto i risultati ANTHEA. I risultati indipendenti MAX e il loro stato sono documentati separatamente nel rapporto di confronto.\n\nPer confrontare: impostare Bishop, terreno, falda, carichi e fattori identici a risultati-anthea.json. Confrontare prima il medesimo cerchio (xc, yc, R), quindi la ricerca con il medesimo dominio; documentare conci, esclusioni, fattori e versione MAX. Non confrontare F caratteristico con F su parametri M2 ridotti.\n\n";
        guide += string.Join("\n", result.GlobalStability!.Cases.Select(c => $"- {c.Factors.Name}: F={c.Critical?.Factor:G10}; η={c.Critical?.Ratio:G10}; {c.Status}; risolte {c.Solved}/{c.Tried}."));
        if (writeReports) File.WriteAllText(Path.Combine(folder, "riproduzione.md"), guide);
        summary.Add(new { example.Id, example.Description, MAX = "Confronto indipendente documentato separatamente; questa procedura genera solo ANTHEA", Cases = result.GlobalStability.Cases.Select(c => new { c.Factors.Name, F = c.Critical?.Factor, Eta = c.Critical?.Ratio, c.Status, c.NumericalFailures, c.Boundary }) });
        Console.WriteLine(example.Id + " " + string.Join(" | ", result.GlobalStability.Cases.Select(c => $"F={c.Critical?.Factor:0.0000} {c.Status}")));
    }
    File.WriteAllText(Path.Combine(directory, "indice-confronti.json"), JsonSerializer.Serialize(summary, J.Options));
    log.Add($"PASS {checks} controlli; 10 esempi ANTHEA; confronti MAX non eseguiti."); Console.WriteLine(log[^1]); File.WriteAllText(Path.Combine(directory, "stato-esecuzione.txt"), log[^1]);
}
catch (Exception ex) { log.Add("FAIL " + ex); Console.WriteLine(log[^1]); Environment.ExitCode = 1; }
finally { File.WriteAllLines(Path.Combine(directory, "controlli-numerici.txt"), log); }
