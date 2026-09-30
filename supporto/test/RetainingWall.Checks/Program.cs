using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

var directory = Path.GetFullPath(args.FirstOrDefault() ?? "supporto/artefatti/muri_sostegno/numerica");
Directory.CreateDirectory(directory);
var log = new List<string>(); int count = 0;
void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); log.Add("OK " + message); }
void Near(double a, double b, string message, double tolerance = 1e-7) => Check(Math.Abs(a - b) <= tolerance * Math.Max(1, Math.Abs(b)), message + $" ({a:G9})");
void Reject(JsonObject d, string message) { try { RetainingWall.Calculate(d); } catch (ArgumentException) { Check(true, message); return; } throw new Exception("Accettato: " + message); }
try
{
    var data = RetainingWall.LegacyDefaults(); var g = data["geometry"]!; g["stem_base"] = .3; g["stem_top"] = .3; g["slab"] = .4; g["heel"] = 1.9;
    var before = data.ToJsonString(); var result = RetainingWall.Calculate(data); var c = result.Cases[0];
    Check(before == data.ToJsonString(), "Il motore non modifica i dati");
    Near(RetainingWall.Ka(30), 1d / 3, "Rankine φ=30°");
    Near(c.Horizontal, .5 * 18 * 3.4 * 3.4 / 3 + 10 * 3.4 / 3, "Spinta di riferimento asciutta");
    Near(c.Vertical, 30 + 22.5 + 102.6 + 19, "Somma dei pesi indipendente");
    Near(c.Stabilizing, 30 * 1.5 + 22.5 * .95 + (102.6 + 19) * 2.05, "Momenti stabilizzanti indipendenti");
    Near(c.Overturning, 18 * Math.Pow(3.4, 3) / 18 + 10 * 3.4 * 3.4 / 6, "Momenti delle spinte indipendenti");
    Near(c.Sections.Single(s => s.Name == "Fusto" && s.Position == 3).M, 18 * 27 / 18 + 10 * 9 / 6, "Momento del fusto alla radice");
    double slope = (c.Contact.Heel - c.Contact.Toe) / 3;
    Near(c.Sections.Single(s => s.Name == "Valle").M, (c.Contact.Toe - 10) * .8 * .8 / 2 + slope * .8 * .8 * .8 / 6, "Momento mensola a valle da integrazione indipendente");
    double heelPressure = c.Contact.Toe + slope * 1.1 - 10 - 54 - 10;
    Near(c.Sections.Single(s => s.Name == "Monte").M, heelPressure * 1.9 * 1.9 / 2 + slope * 1.9 * 1.9 * 1.9 / 3, "Momento mensola a monte da integrazione indipendente");
    Near(result.Area, 2.1, "Area muro via GPC");
    Check(result.Cases.Count == 11, "Otto SLU e tre SLE");
    double phi = 34 * Math.PI / 180, nqf = Math.Exp(Math.PI * Math.Tan(phi)) * Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2);
    Near(c.BearingResistance!.Value, .5 * 19 * c.EffectiveWidth * c.EffectiveWidth * 2 * (nqf - 1) * Math.Tan(phi) * Math.Pow(1 - c.Horizontal / c.Vertical, 3), "Portanza drenata con riduzione per inclinazione");
    Near(result.Cases.First(r => r.State == "SLU").SlidingResistance, result.Cases.First(r => r.State == "SLU").Vertical * Math.Tan(26 * Math.PI / 180) / 1.1, "Coefficiente R3 scorrimento");
    Check(result.Structural.Any(s => s.Name.Contains("N–M GPC") && s.Resistance > 0), "Resistenza flessionale GPC effettiva");
    Check(result.Structural.Any(s => s.Name.Contains("fessurazione") && s.Resistance > 0), "Fessurazione SLE effettiva");
    foreach (var item in result.Structural.Where(s => s.Ratio is null)) log.Add("INCOMPLETA " + item.Name + " " + item.Status);
    foreach (double x in new[] { .1, .5, 1d, 1.5, 2d, 2.5, 2.9 })
    {
        var p = RetainingWall.ContactLaw(3, 100, x);
        var integral = RetainingWall.Integrate([new(p.Start, p.End, p.Toe, p.Heel)], 0, 3, 0);
        Near(integral.Force, 100, "Equilibrio verticale del contatto x=" + x);
        Near(integral.Moment, 100 * x, "Equilibrio dei momenti del contatto x=" + x);
    }
    Check(!RetainingWall.ContactLaw(3, 100, 0).Valid && !RetainingWall.ContactLaw(3, -1, 1).Valid, "Contatto impossibile rifiutato");
    var wet = (JsonObject)data.DeepClone(); wet["water"]!["enabled"] = true; wet["water"]!["depth"] = 1.4;
    var w = RetainingWall.Calculate(wet).Cases[0];
    Near(w.Uplift, .5 * 9.81 * 2 * 3, "Sottospinta triangolare");
    Near(w.Horizontal, 18 * 1.4 * 1.4 / 6 + (18 * 1.4 * 2 + .5 * (20 - 9.81) * 4) / 3 + 10 * 3.4 / 3 + .5 * 9.81 * 4, "Falda: spinte efficaci più acqua");
    Near(w.Vertical, 30 + 22.5 + 1.9 * (1.4 * 18 + 1.6 * 20) + 19 - w.Uplift, "Falda: pesi totali e sottospinta separati");
    Near(w.Vertical * w.X, w.Stabilizing - w.Overturning, "Equilibrio con sottospinta");
    wet["water"]!["front_head"] = .3;
    var wf = RetainingWall.Calculate(wet).Cases[0];
    Near(w.Horizontal - wf.Horizontal, .5 * 9.81 * .3 * .3, "Acqua a valle con punto di discontinuità");
    var split = (JsonObject)data.DeepClone(); var l = RetainingWall.Layer(); l["thickness"] = 1.3; var l2 = RetainingWall.Layer(); l2["thickness"] = 4;
    split["layers"] = new JsonArray(l, l2); var splitResult = RetainingWall.Calculate(split).Cases[0];
    Near(splitResult.Horizontal, c.Horizontal, "Invarianza alla suddivisione di uno strato"); Near(splitResult.Overturning, c.Overturning, "Invarianza del momento per strato suddiviso");
    var seismic = (JsonObject)data.DeepClone(); seismic["seismic"]!["enabled"] = true; var sr = RetainingWall.Calculate(seismic);
    Check(sr.Cases.Count(s => s.State == "SISMA") == 2, "Entrambi i segni di kv");
    Check(sr.Cases.Where(s => s.State == "SISMA").All(s => s.BearingResistance is null), "Nessun esito di portanza sismica senza inerzia del terreno");
    Near(RetainingWall.SeismicKa(30, 0, 0), RetainingWall.Ka(30), "Mononobe–Okabe al limite statico");
    Check(sr.Cases.Where(s => s.State == "SISMA").All(s => s.Horizontal > c.Horizontal), "Incremento sismico avverso");
    seismic["water"]!["enabled"] = true; Reject(seismic, "Sisma con falda non ignorato");
    var gravity = RetainingWall.Calculate(RetainingWall.Example("gravity")); Check(gravity.Structural.Any(s => s.Name.Contains("assenza trazione")), "Gravità: controllo nel corpo del muro");
    Check(gravity.SteelKg == 0, "Gravità: nessuna armatura fittizia");
    Near(gravity.Area, (2.2 + .55) * 3 / 2 + 2.7 * .45, "Area trapezio a gravità indipendente");
    var failing = RetainingWall.Example("gravity"); failing["version"] = 1; failing["loads"]!["horizontal"] = 1000; var failed = RetainingWall.Calculate(failing);
    Check(failed.Cases.Any(c => !c.Contact.Valid) && failed.Checks.Any(c => c.Name == "Contatto fondazione" && c.Status == "Perdita di equilibrio"), "Instabilità del muro segnalata");
    Check(failed.Structural.Any(c => c.Name.StartsWith("Valle") && c.Resistance is null), "Nessuna verifica delle mensole con reazioni inesistenti");
    var topLoad = RetainingWall.Example("gravity"); topLoad["version"] = 1; topLoad["geometry"]!["stem_base"] = 8; topLoad["geometry"]!["stem_top"] = .15; topLoad["geometry"]!["height"] = 1; topLoad["loads"]!["vertical"] = 2000;
    Check(RetainingWall.Calculate(topLoad).Structural.Any(c => c.Name == "Fusto z=0.00 m · compressione" && c.Ratio > 1), "Carico in testa verificato nella sezione sottile");
    foreach (var family in RetainingWall.Families.Where(f => !f.Available)) { var future = RetainingWall.LegacyDefaults(); future["family"] = family.Id; RetainingWall.ValidateShape(future); Reject(future, family.Name + " archiviabile ma non calcolabile"); }
    var invalid = RetainingWall.LegacyDefaults(); invalid["extensions"]!["anchors"] = new JsonArray(); Reject(invalid, "Componenti futuri non ignorati");
    invalid = RetainingWall.LegacyDefaults(); invalid["geometry"]!["height"] = ""; Reject(invalid, "Input vuoto non sostituito da zero");
    invalid = RetainingWall.LegacyDefaults(); invalid["layers"]![0]!["thickness"] = 1; Reject(invalid, "Stratigrafia insufficiente");
    var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = data.DeepClone(); string archive = Path.Combine(directory, "mensola.anthea"); Archivio.Scrivi(archive, doc);
    Check(J.Equivalent(Archivio.Leggi(archive)["dati"], data), "Archivio ANTHEA round trip");
    var incomplete = Archivio.Documento(RetainingWall.Module); incomplete["dati"]!["geometry"]!["height"] = "";
    Archivio.Valida(incomplete); Check(true, "Calcolo incompleto archiviabile");
    var service = CalculationService.Calculate(RetainingWall.Module, data);
    Check(J.Equivalent(service, result.Json()), "Servizio comune e motore restituiscono gli stessi risultati");
    Check(ProjectSharedData.Fields(doc).Count == 0, "Il muro non eredita dati di pali");
    byte[] report = ReportRetainingWall.Create("Benchmark muro", result);
    using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(report)))
    {
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = reader.ReadToEnd();
        Check(xml.Contains("stabilità globale") && xml.Contains("GPCChecker.Concrete") && xml.Contains("Inviluppo verifiche"), "Relazione con ambito, motore e verifiche");
    }
    var section = J.Obj(("nome", "Muri"), ("fogli", new JsonArray(doc.DeepClone())), ("strutture", new JsonArray()));
    var sheet = section.Array("fogli")[0]!.AsObject();
    ReportProject.Write(Path.Combine(directory, "progetto.docx"), new ProjectReportPlan(section), new Dictionary<JsonObject, ReportProject.SheetContent> { [sheet] = new(report) }, []);
    Check(File.Exists(Path.Combine(directory, "progetto.docx")), "Relazione del muro integrata nel report di progetto");
    var invalidType = RetainingWall.LegacyDefaults(); invalidType["water"]!["enabled"] = "true"; Reject(invalidType, "Opzioni malformate rifiutate");
    var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    try { RetainingWall.Calculate(data, cancellation.Token); throw new Exception("Cancellazione ignorata"); } catch (OperationCanceledException) { Check(true, "Cancellazione del motore"); }
    Check(RetainingWall.CheckValue("zero", "", 10, 0, "kN").Status.StartsWith("Non soddisfatta"), "Resistenza nulla non favorevole");
    Check(!result.Json().B("verifica_completa", true), "Nessun esito complessivo dell’opera");
    File.WriteAllText(Path.Combine(directory, "mensola.json"), result.Json().ToJsonString(J.Options));
    File.WriteAllText(Path.Combine(directory, "gravita.json"), gravity.Json().ToJsonString(J.Options));
    count += ActionsChecks.Run(directory);
    count += SeismicSoilChecks.Run(directory);
    count += DualSoilChecks.Run(directory);
    count += AdvancedChecks.Run(directory);
    log.Add($"PASS {count} controlli"); Console.WriteLine(log.Last());
}
finally { File.WriteAllLines(Path.Combine(directory, "test.txt"), log); }

