using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Anthea.Calculations;

// Freezes the legacy outputs of the calculation cores before they are moved to Checker (migration step M2).
// Usage: dotnet CheckerMigration.Capture.dll <output directory> <ANTHEA commit>
// Output: shear-legacy.csv (ConcreteCodeChecks.Shear). Units as in the legacy API: kN, kNm, mm, MPa.
string output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("supporto", "artefatti", "migrazione-checker"));
string commit = args.Length > 1 ? args[1] : "unknown";
Directory.CreateDirectory(output);
static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
string assembly = typeof(ConcreteCodeChecks).Assembly.Location;
string sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly)));

var cases = new List<ConcreteCodeChecks.ShearInput>();
string[] standards = ConcreteStandards.OrdinaryNames;
foreach (var s in standards)
{
    // A. Without shear reinforcement: axial force, shear, depth (DIN vmin interpolation) and strength.
    foreach (double n in new[] { -500.0, 0, 100 })
        foreach (double v in new[] { 80.0, 250 })
            foreach (double d in new[] { 460.0, 700, 900 })
                foreach (double fck in new[] { 30.0, 75 })
                    cases.Add(new(s, n, v, 120, 300 * (d + 40), 300, d, 1256, fck, .85 * fck / 1.5, 391.3, 1.5, 200000, 0, 1));
    cases.Add(new(s, 0, 0, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1));
    cases.Add(new(s, -200, 120, 80, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1, Aggregate: 10));
    // B. With shear reinforcement: spacing, inclination, assigned or optimised cot θ, axial force, demand, moment and strength.
    foreach (double spacing in new[] { 150.0, 300 })
        foreach (double alpha in new[] { 90.0, 45 })
            foreach (double? cot in new double?[] { null, 1, 2.5 })
                foreach (double n in new[] { -500.0, 0, 200 })
                    foreach (double v in new[] { 150.0, 600 })
                        foreach (double fck in new[] { 30.0, 75 })
                            cases.Add(new(s, n, v, 150, 150000, 300, 460, 1256, fck, .85 * fck / 1.5, 391.3, 1.5, 200000, 100.53, spacing, alpha, cot));
    // C. Out-of-range data and limits.
    cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 95, 53.8, 391.3, 1.5, 200000, 100.53, 150));
    cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150, 30));
    cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150, CotTheta: 3));
    cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 0, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
    cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150, LeverFactor: 1));
    cases.Add(new(s, 1500, 400, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
}
// D. Seeded random cases across the whole input range.
var random = new Random(20261001);
double U(double a, double b) => a + (b - a) * random.NextDouble();
for (int i = 0; i < 700; i++)
{
    double fck = new[] { 20.0, 25, 30, 35, 40, 45, 50, 55, 60, 70, 80, 90 }[random.Next(12)];
    double bw = U(150, 1000), d = U(150, 1500), area = bw * (d + U(30, 80)) * U(1, 1.6);
    double asw = random.NextDouble() < .3 ? 0 : U(50, 600);
    double? cot = random.NextDouble() < .6 ? null : U(.8, 3.2);
    cases.Add(new(standards[i % standards.Length], U(-2000, 800), U(0, 1500), U(0, 600), area, bw, d, U(200, 8000), fck, .85 * fck / 1.5,
        new[] { 391.3, 450 / 1.15 }[random.Next(2)], 1.5, 200000, asw, U(50, 400), U(45, 90), cot,
        new[] { .9, .75, .6 }[random.Next(3)], new[] { 8.0, 16, 20, 32 }[random.Next(4)], U(-200, 200)));
}

var csv = new StringBuilder();
csv.AppendLine("# ConcreteCodeChecks.Shear legacy outputs; ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
csv.AppendLine("# Units: N_kN, V_kN (kN), M_kNm (kNm), lengths mm, areas mm2, stresses MPa; resistances kN. Compression negative.");
csv.AppendLine("id;standard;N_kN;V_kN;M_kNm;area;bw;d;asl;fck;fcd;fyd;gammaC;es;asw;spacing;alpha;cot;lever;aggregate;ecc;outcome;VRsd_kN;VRcd_kN;VRd_kN;ratio;cotTheta;status;details");
for (int i = 0; i < cases.Count; i++)
{
    var c = cases[i];
    string head = string.Join(";", i, c.Standard, F(c.N), F(c.V), F(c.M), F(c.Area), F(c.Bw), F(c.D), F(c.Asl), F(c.Fck), F(c.Fcd), F(c.Fyd), F(c.GammaC), F(c.Es),
        F(c.Asw), F(c.Spacing), F(c.Alpha), F(c.CotTheta), F(c.LeverFactor), F(c.Aggregate), F(c.AxialEccentricity));
    try
    {
        var r = ConcreteCodeChecks.Shear(c);
        string details = string.Join("|", r.Details.Select(x => x.Symbol + "=" + F(x.Value) + "=" + x.Unit));
        csv.AppendLine(string.Join(";", head, "ok", F(r.VRsd), F(r.VRcd), F(r.VRd), F(r.Ratio), F(r.CotTheta), r.Status.Replace(";", ","), details));
    }
    catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", "", ex.Message.Replace(";", ",").Replace("\n", " "), "")); }
}
File.WriteAllText(Path.Combine(output, "shear-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
Console.WriteLine($"{cases.Count} casi di taglio -> {Path.Combine(output, "shear-legacy.csv")}");
StressCapture.Run(output, commit, sha);

// Serviceability stresses (CheckerSection.Stress): sections saved with the Model archive, effective standard coefficients,
// actions already transformed to the local axes passed to the native checker, legacy results.
internal static class StressCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    internal static void Run(string output, string commit, string sha)
    {
        var sections = new List<(string Name, Action<System.Text.Json.Nodes.JsonObject> Edit)>
        {
            ("R300x500", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3";
                i["side_bar_count_per_side"] = "0"; i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["fck_mpa"] = "30"; }),
            ("T1200x800", i => { i["shape"] = "A T"; i["fck_mpa"] = "35"; }),
            ("C1000", i => { i["shape"] = "Circolare"; i["fck_mpa"] = "40"; }),
            ("R600x800H", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "600"; i["height_mm"] = "800"; i["foro_presente"] = true; i["inner_width_mm"] = "300";
                i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; i["fck_mpa"] = "28"; })
        };
        var model = new GPC.Model.Models.Model("Sezioni SLE congelate");
        var csv = new StringBuilder();
        csv.AppendLine("# CheckerSection.Stress legacy outputs; ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
        csv.AppendLine("# Sections in stress-sections.xml (Model archive, properties by name). Forces N, Nmm in the local axes passed to the checker; stresses MPa.");
        csv.AppendLine("id;section;standard;coefficients;linear;psi;tension;divisions;reduction;origin;v1;v2;N;V1;V2;T;M1;M2;set;outcome;sigmaC;sigmaS;ratio;limitC;limitS;status");
        var actions = new[] { new ActionPoint(-500, 50, 0), new ActionPoint(-200, 150, 40), new ActionPoint(0, 120, 0), new ActionPoint(100, 30, 0), new ActionPoint(-1500, 0, 0), new ActionPoint(-800, -60, 90) };
        int id = 0;
        foreach (var (name, edit) in sections)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject(); edit(input);
            var prepared = CheckerSection.PrepareModel(input, settings);
            prepared.Section.Name = name; model.AddProperty(prepared.Section);
            foreach (var standard in ConcreteStandards.Names)
                foreach (var (linear, psi, tension, thin) in new[] { (false, "0", "No", false), (true, "2", "No", false), (true, "0", "Sì", false), (true, "2", "No", true) })
                {
                    if (thin && standard != "NTC 2018") continue;
                    settings["normativa"] = standard; input["gettato_sottile"] = thin ? "Sì" : "No";
                    var options = (System.Text.Json.Nodes.JsonObject)settings["sle"]!["SLE"]!.DeepClone();
                    options["modello"] = linear ? "Lineare" : "Non lineare"; options["phi"] = psi; options["trazione_cls"] = tension; options["angoli"] = "32";
                    CheckerSection engine;
                    try { engine = new CheckerSection(prepared, input, settings, options); }
                    catch (Exception ex) { csv.AppendLine(string.Join(";", id++, name, standard, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "error:" + ex.GetType().Name, "", "", "", "", "", ex.Message.Replace(";", ","))); continue; }
                    var effective = ConcreteStandards.Effective(input, settings); double reduction = standard == "NTC 2018" && thin ? .8 : 1; effective.AlphaCC *= reduction;
                    string coefficients = string.Join(",", ConcreteStandards.Coefficients.Select(c => c.Key + "=" + F((double)typeof(GPC.Model.Standards.StandardModelCode2010).GetProperty(c.Key)!.GetValue(effective)!)));
                    foreach (var action in actions)
                    {
                        var force = engine.Force(action); var cs = force.CoordinateSystem;
                        string head = string.Join(";", id++, name, standard, coefficients, linear, psi, tension == "Sì", 32, F(reduction),
                            F(cs.Origin.X) + "," + F(cs.Origin.Y) + "," + F(cs.Origin.Z), F(cs.V1.X) + "," + F(cs.V1.Y) + "," + F(cs.V1.Z), F(cs.V2.X) + "," + F(cs.V2.Y) + "," + F(cs.V2.Z),
                            F(force.N), F(force.V1), F(force.V2), F(force.T), F(force.M1), F(force.M2));
                        foreach (var set in new[] { "SLE", "SLE_QP", "SLE_FREQ" })
                        {
                            try
                            {
                                var s = engine.Stress(action, set);
                                csv.AppendLine(string.Join(";", head, set, "ok", F(s.sigma_cls), F(s.sigma_acciaio), F(s.Ratio), F(s.ConcreteStressLimit), F(s.SteelStressLimit), s.Status));
                            }
                            catch (Exception ex) { csv.AppendLine(string.Join(";", head, set, "error:" + ex.GetType().Name, "", "", "", "", "", ex.Message.Replace(";", ",").Replace("\n", " "))); }
                        }
                    }
                }
        }
        using (var stream = File.Create(Path.Combine(output, "stress-sections.xml"))) GPC.Model.Persistence.ModelArchive.Save(model, stream);
        File.WriteAllText(Path.Combine(output, "stress-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{id} stati tensionali -> {Path.Combine(output, "stress-legacy.csv")}");
    }
}
