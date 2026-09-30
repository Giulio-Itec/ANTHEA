using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

if (args.Contains("--diagnose")) { DesignDiagnosis.Run(args); return; }
if (args.Contains("--performance")) { DesignPerformanceChecks.Run(args[0], args[2], args.Length > 3 ? int.Parse(args[3]) : 0); return; }
if (args.Contains("--coverage"))
{
    try { Console.WriteLine($"PASS {SearchCoverageChecks.Run(args.First(a => a != "--coverage"))} search-coverage assertions."); }
    catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    return;
}

int checks = 0;
void Check(bool b, string text) { if (!b) throw new Exception(text); checks++; }
void Reject(Action a, string text) { try { a(); } catch (ArgumentException) { checks++; return; } throw new Exception(text); }
JsonObject Fixture(string kind = "Trave", string shape = "Rettangolare")
{
    var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data); var input = data["input"]!;
    input["shape"] = shape; input["cover_mm"] = "40"; input["width_mm"] = "400"; input["height_mm"] = "600";
    input["diameter_mm"] = "600";
    ws["dettagli_costruttivi"]!["elemento"] = kind; ws["sle_comuni"]!["esposizione"] = "XC1";
    ws["taglio"]!["modello_circolare"] = "Parametri assegnati";
    ws["ancoraggi"]!["lunghezza"] = "2000";
    foreach (string set in SectionWorkspace.Sets) data["combinazioni"]![set] = new JsonArray();
    data["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("id", "u1"), ("nome", "Flessione"), ("azioni", new[] { "0", "100", "20" })));
    return data;
}
var small = new ConcreteDesignOptions { Diameters = [16, 20], TopCounts = [3, 4], BottomCounts = [3, 4], SideCounts = [2],
    CircularCounts = [8, 12], StirrupDiameters = [8], StirrupSpacings = [100, 150, 200], MaxEvaluations = 1000 };
var watch = Stopwatch.StartNew();
try
{
    var data = Fixture(); string original = data.ToJsonString();
    var r = ConcreteReinforcementDesign.Optimize(data, small);
    Check(data.ToJsonString() == original, "The optimizer mutated its source");
    Check(r.Completed && r.Evaluated == 48, "Finite search/count wrong");
    Check(r.Candidates.Count > 1, "No alternatives: " + string.Join("; ", r.Exclusions.Keys));
    Check(r.Candidates.All(c => c.Checks.All(x => x.Passed != false)), "Invalid candidate accepted");
    Check(r.Candidates.All(c => c.Checks.Any(x => x.Key == "slu" && x.Ratio.HasValue)), "Resistance missing");
    Check(r.Candidates.All(c => c.NeedsReview), "Missing execution checks marked completed");
    Check(r.Candidates.First().Metrics.KgPerM == r.Candidates.Min(c => c.Metrics.KgPerM), "Weight ordering");
    var best = r.Candidates[0]; var bars = new SezioneCA(best.Input).Bars;
    double longitudinalMass = bars.Sum(b => Math.PI * b.Diametro * b.Diametro / 4) * 7850 / 1e6;
    Check(Math.Abs(best.Metrics.LongitudinalKgPerM - longitudinalMass) < 1e-10, "Independent mass check");
    var referenceOptions = J.Obj(("criterio", "Eccentricità costante"), ("strategia", "Iterativo"));
    var reference = new CheckerSection(best.Input, data["workspace_ca"]!.AsObject(), referenceOptions).Domain3D().Check(new(0, 100, 20));
    Check(Math.Abs(reference.Utilization!.Value - best.Checks.Single(c => c.Key == "slu").Ratio!.Value) < 1e-8, "Direct resistance differs from existing verifier");
    foreach (var c in r.Candidates)
        Check(c.Pareto == !r.Candidates.Any(b => b.Metrics.KgPerM <= c.Metrics.KgPerM && b.Metrics.PiecesPerM <= c.Metrics.PiecesPerM && b.Metrics.Diameters <= c.Metrics.Diameters &&
            (b.Metrics.KgPerM < c.Metrics.KgPerM || b.Metrics.PiecesPerM < c.Metrics.PiecesPerM || b.Metrics.Diameters < c.Metrics.Diameters)), "Pareto frontier differs from brute force");
    var strict = small with { Targets = new(small.Targets) { ["slu"] = .35 } };
    var tight = ConcreteReinforcementDesign.Optimize(data, strict);
    Check(tight.Candidates.All(c => c.Checks.Where(x => x.Key == "slu").All(x => x.Ratio <= .35 + 1e-10)), "Target not enforced");
    Check(tight.Candidates.Count < r.Candidates.Count, "Tighter target has no effect");
    var limited = ConcreteReinforcementDesign.Optimize(data, small with { MaxEvaluations = 1 });
    Check(!limited.Completed && limited.Evaluated == 1, "Budget truncation not explicit");
    using (var stop = new CancellationTokenSource())
    {
        var partial = ConcreteReinforcementDesign.Optimize(data, small, stop.Token, new Callback<ConcreteDesignProgress>(p => stop.Cancel()));
        Check(!partial.Completed && partial.Evaluated < partial.Planned, "Cancellation failed");
    }
    Reject(() => ConcreteReinforcementDesign.Optimize(data, small with { Targets = new() { ["slu"] = 1.1 } }), "Unsafe target accepted");
    Reject(() => ConcreteReinforcementDesign.Optimize(data, small with { Diameters = [double.NaN] }), "NaN accepted");
    var missing = Fixture(); missing["workspace_ca"]!["sle_comuni"]!["esposizione"] = "Da scegliere";
    Reject(() => ConcreteReinforcementDesign.Optimize(missing, small), "Missing durability accepted");
    var impossible = Fixture(); impossible["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "100000", "0");
    var none = ConcreteReinforcementDesign.Optimize(impossible, small with { MaxEvaluations = 5 });
    Check(none.Candidates.Count == 0 && none.Exclusions.Count > 0, "Impossible demand accepted");
    var shearOnly = Fixture(); shearOnly["combinazioni"]!["SLU"] = new JsonArray();
    shearOnly["workspace_ca"]!["taglio"]!["azioni"] = new JsonArray(J.Obj(("nome", "N-M simultanei"), ("N", "0"), ("Mx", "100000"), ("My", "0"), ("Vx", "0"), ("Vy", "0"), ("T", "0")));
    Check(ConcreteReinforcementDesign.Optimize(shearOnly, small with { MaxEvaluations = 5 }).Candidates.Count == 0, "N-M from shear rows ignored");
    var axial = Fixture("Pilastro", "Circolare"); axial["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("-500", "0", "0");
    var circle = ConcreteReinforcementDesign.Optimize(axial, small);
    Check(circle.Candidates.Count > 0, "Pure axial/circular search: " + string.Join("; ", circle.Exclusions.Keys));
    Check(circle.Candidates.All(c => new SezioneCA(c.Input).CircularSides == 32), "Circle subdivision changed");
    var hollow = Fixture("Pilastro"); hollow["input"]!["foro_presente"] = true; hollow["input"]!["inner_width_mm"] = "120"; hollow["input"]!["inner_height_mm"] = "200";
    var hr = ConcreteReinforcementDesign.Optimize(hollow, small with { MaxEvaluations = 10 });
    Check(hr.Candidates.Count > 0 && hr.Candidates.All(c => c.Input.B("foro_presente")), "Hollow section lost");
    var sle = Fixture(); sle["workspace_ca"]!["sle_comuni"]!["spaziatura_fessure"] = "150";
    foreach (string set in SectionWorkspace.Sets.Skip(2)) sle["combinazioni"]![set] = new JsonArray(J.Obj(("id", set), ("nome", set), ("azioni", new[] { "0", "50", "5" })));
    sle["combinazioni"]!["SLV"] = new JsonArray(J.Obj(("id", "e"), ("nome", "Elastico"), ("azioni", new[] { "0", "50", "5" })));
    sle["workspace_ca"]!["taglio"]!["azioni"] = new JsonArray(J.Obj(("nome", "Taglio"), ("N", "0"), ("Mx", "100"), ("My", "20"), ("Vx", "20"), ("Vy", "40"), ("T", "0")));
    var sr = ConcreteReinforcementDesign.Optimize(sle, small with { MaxEvaluations = 8 });
    Check(sr.Candidates.Count > 0, "SLE/shear: " + string.Join("; ", sr.Exclusions.Keys));
    foreach (var k in new[] { "slv", "sigma_c_rara", "sigma_s_rara", "sigma_c_qp", "wk_freq", "wk_qp", "vx", "vy" })
        Check(sr.Candidates.All(c => c.Checks.Any(x => x.Key == k && x.Passed == true)), "Missing check " + k);
    var torsion = Fixture(); torsion["workspace_ca"]!["taglio"]!["azioni"] = new JsonArray(J.Obj(("nome", "VT"), ("N", "0"), ("Vx", "10"), ("Vy", "20"), ("T", "5")));
    Reject(() => ConcreteReinforcementDesign.Optimize(torsion, small), "Unconfirmed torsion layout accepted");
    var tr = ConcreteReinforcementDesign.Optimize(torsion, small with { MaxEvaluations = 8, TorsionLayoutConfirmed = true });
    Check(tr.Candidates.Count > 0, "Torsion: " + string.Join("; ", tr.Exclusions.Keys));
    Check(tr.Candidates.All(c => c.Checks.Any(x => x.Key == "vt_staffe") && c.Checks.Any(x => x.Combination.Contains("distribuzione"))), "Torsion distribution missing");
    var match = r.Candidates.First(c => c.Input.ToJsonString() == tr.Candidates[0].Input.ToJsonString());
    Check(tr.Candidates[0].Checks.Single(c => c.Key == "slu" && c.Combination == "Flessione").Ratio > match.Checks.Single(c => c.Key == "slu").Ratio, "Torsion reserve not removed from flexural capacity");
    foreach (string kind in new[] { "Soletta piena", "Parete" })
    {
        var plate = Fixture(kind); plate["input"]!["width_mm"] = "1000"; plate["input"]!["height_mm"] = "250";
        plate["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "15", "0");
        var po = small with { TopCounts = [6], BottomCounts = [6], SideCounts = [0], Stirrups = "Senza staffe", SecondaryDiameters = [8, 12], SecondarySpacings = [100, 200] };
        var pr = ConcreteReinforcementDesign.Optimize(plate, po);
        Check(pr.Candidates.Count > 0, kind + ": " + string.Join("; ", pr.Exclusions.Keys));
        Check(pr.Candidates.All(c => c.Detailing.D("as_secondaria") > 0 && c.Detailing.D("diametro_secondaria") > 0 && c.Metrics.TransverseKgPerM > 0), "Orthogonal reinforcement missing");
    }
    var parsed = Fixture(); var ps = SectionWorkspace.Prepare(parsed); var persisted = ConcreteReinforcementDesign.Prepare(ps);
    persisted["diametri"] = "12; 16;20"; persisted["riserva_torsione"] = "25,5";
    Check(ConcreteDesignOptions.Read(persisted).TorsionReservePercent == 25.5, "Italian decimal parsing");
    var malformed = Fixture(); malformed["workspace_ca"]!["calcola_armature"] = new JsonArray();
    Reject(() => SectionWorkspace.Prepare(malformed), "Malformed persisted options silently overwritten");
    foreach (string objective in new[] { "Numero barre", "Numero diametri", "Compromesso" })
    {
        var opt = small with { Objective = objective }; var ranked = ConcreteReinforcementDesign.Rank(r.Candidates, opt);
        Check(ranked[0].Score == ranked.Min(c => ConcreteReinforcementDesign.Score(c.Metrics, opt)), "Ranking " + objective);
    }
    checks += ParallelDesignChecks.Run(sle, small);
    string folder = args.FirstOrDefault() ?? Path.Combine(Environment.CurrentDirectory, "supporto", "artefatti", "calcola-armature-20260930"); Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "engine-results.json"), JsonSerializer.Serialize(new { checks, seconds = watch.Elapsed.TotalSeconds, result = r, strict = tight.Candidates.Count, circular = circle.Candidates.Count, sle = sr.Candidates.Count, torsion = tr.Candidates.Count }, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(folder, "ui-fixture.json"), sle.ToJsonString());
    Console.WriteLine($"PASS {checks} assertions; {watch.Elapsed.TotalSeconds:0.00}s; {r.Candidates.Count}/{r.Evaluated} alternatives.");
}
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
sealed class Callback<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }
