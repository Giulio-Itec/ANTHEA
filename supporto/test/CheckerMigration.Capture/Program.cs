using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Anthea.Calculations;
using Anthea.Calculations.Geotechnics;

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
TorsionCapture.Run(output, commit, sha);
CrackCapture.Run(output, commit, sha);
DetailingCapture.Run(output, commit, sha);
DurabilityCapture.Run(output, commit, sha);
GeotechnicsCapture.Run(output, commit, sha);

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

// NTC 2018 torsion (ConcreteTorsionCalculator): resisting geometry from the outline and resistances with the shear interaction.
// Shear results are synthetic values with the same cot θ (and some mismatches) as ConcreteShearAnalysis passes them.
internal static class TorsionCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    static string Clean(string s) => s.Replace(";", ",").Replace("\n", " ");
    internal static void Run(string output, string commit, string sha)
    {
        var geometry = new StringBuilder();
        geometry.AppendLine("# ConcreteTorsionCalculator.Geometry legacy outputs; ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
        geometry.AppendLine("# Lengths mm, areas mm2. axis = cover + stirrup diameter + largest bar diameter / 2.");
        geometry.AppendLine("id;name;shape;width;height;areaCls;axis;hollow;innerWidth;innerHeight;innerDiameter;outcome;A;P;t;message");
        var outlines = new List<(string Name, Action<System.Text.Json.Nodes.JsonObject> Edit)>
        {
            ("R300x500", i => { i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3"; i["side_bar_count_per_side"] = "0";
                i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["transverse_bar_diameter_mm"] = "8"; }),
            ("R600x800", i => { }),
            ("R200x200", i => { i["width_mm"] = "200"; i["height_mm"] = "200"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "2"; i["side_bar_count_per_side"] = "0";
                i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "16"; i["cover_mm"] = "30"; i["transverse_bar_diameter_mm"] = "8"; }),
            ("R160x160", i => { i["width_mm"] = "160"; i["height_mm"] = "160"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "2"; i["side_bar_count_per_side"] = "0";
                i["top_bar_diameter_mm"] = "12"; i["bottom_bar_diameter_mm"] = "12"; i["cover_mm"] = "40"; i["transverse_bar_diameter_mm"] = "8"; }),
            ("C1000", i => { i["shape"] = "Circolare"; }),
            ("C400", i => { i["shape"] = "Circolare"; i["diameter_mm"] = "400"; i["longitudinal_bar_count"] = "8"; i["longitudinal_bar_diameter_mm"] = "16";
                i["cover_mm"] = "40"; i["transverse_bar_diameter_mm"] = "8"; }),
            ("R600x800H", i => { i["foro_presente"] = true; i["inner_width_mm"] = "300"; i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; }),
            ("R1200x1000H", i => { i["width_mm"] = "1200"; i["height_mm"] = "1000"; i["foro_presente"] = true; i["inner_width_mm"] = "700"; i["inner_height_mm"] = "500";
                i["side_bar_count_per_side"] = "0"; i["cover_mm"] = "40"; i["top_bar_diameter_mm"] = "20"; i["bottom_bar_diameter_mm"] = "20"; }),
            ("C1000H", i => { i["shape"] = "Circolare"; i["foro_presente"] = true; i["inner_diameter_mm"] = "500"; }),
            ("T1200x800", i => { i["shape"] = "A T"; })
        };
        int gid = 0;
        foreach (var (name, edit) in outlines)
        {
            var input = SezioneCA.DefaultInput(); edit(input);
            SezioneCA s;
            try { s = new SezioneCA(input); }
            catch (Exception ex) { geometry.AppendLine(string.Join(";", gid++, name, "", "", "", "", "", "", "", "", "", "section-error:" + ex.GetType().Name, "", "", "", Clean(ex.Message))); continue; }
            double axis = s.Input.D("cover_mm") + s.Input.D("transverse_bar_diameter_mm") + s.Bars.Max(b => b.Diametro) / 2;
            bool hollow = s.Input.B("foro_presente");
            string head = string.Join(";", gid++, name, s.Shape, F(s.Width), F(s.Height), F(s.AreaCls), F(axis), hollow,
                hollow && s.Shape == "Rettangolare" ? F(s.Input.D("inner_width_mm")) : "", hollow && s.Shape == "Rettangolare" ? F(s.Input.D("inner_height_mm")) : "",
                hollow && s.Shape == "Circolare" ? F(s.Input.D("inner_diameter_mm")) : "");
            try
            {
                var g = ConcreteTorsionCalculator.Geometry(s);
                geometry.AppendLine(string.Join(";", head, "ok", F(g.Area), F(g.Perimeter), F(g.Thickness), ""));
            }
            catch (Exception ex) { geometry.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", Clean(ex.Message))); }
        }
        File.WriteAllText(Path.Combine(output, "torsion-geometry-legacy.csv"), geometry.ToString(), new UTF8Encoding(false));

        var cases = new List<TorsionInput>();
        static Ntc2018Checks.ShearResult Shear(double rsd, double rcd, double cot) => new(rsd, rcd, Math.Min(rsd, rcd), null, cot, "synthetic");
        var shapes = new[] { new TorsionGeometry(82416, 1216, 96), new TorsionGeometry(256256, 2064, 184), new TorsionGeometry(Math.PI * 375 * 375, Math.PI * 750, 250) };
        int k = 0;
        // A. Grid: geometry, torque (sign), stirrups, spacing, longitudinal bars, cot θ, concomitant shear.
        foreach (var g in shapes)
            foreach (double t in new[] { 0.0, 25, -60, 180 })
                foreach (double leg in new[] { 50.27, 113.1 })
                    foreach (double s in new[] { 100.0, 250 })
                        foreach (double al in new[] { 0.0, 1200 })
                            foreach (double cot in new[] { 1, 1.8, 2.5 })
                                foreach (var (vx, vy) in new[] { (0.0, 0.0), (120.0, -80.0) })
                                {
                                    double fcd = k++ % 2 == 0 ? .85 * 25 / 1.5 : .85 * 45 / 1.5;
                                    cases.Add(new(t, g, fcd, 391.3, leg, s, al, cot, vx, vy, Shear(400, 900, cot), Shear(250, 700, cot)));
                                }
        // B. Limits and rejected data.
        var g0 = shapes[0];
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, .9, 0, 0, Shear(400, 900, .9), Shear(250, 700, .9)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 2.6, 0, 0, Shear(400, 900, 2.6), Shear(250, 700, 2.6)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 100, 0, Shear(400, 900, 2), Shear(250, 700, 1.5)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 0, 100, Shear(400, 900, 1.5), Shear(250, 700, 2)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 0, 0, Shear(400, 900, 2), Shear(250, 700, 2)));
        cases.Add(new(40, g0, 14.17, 391.3, 0, 150, 800, 1.5, 0, 0, Shear(400, 900, 1.5), Shear(250, 700, 1.5)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 0, 800, 1.5, 0, 0, Shear(400, 900, 1.5), Shear(250, 700, 1.5)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, -1, 1.5, 0, 0, Shear(400, 900, 1.5), Shear(250, 700, 1.5)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 100, 0, Shear(400, 0, 1.5), Shear(250, 700, 1.5)));
        cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 0, 100, Shear(0, 700, 1.5), Shear(0, 700, 1.5)));
        // C. Seeded random cases.
        var random = new Random(20261002);
        double U(double a, double b) => a + (b - a) * random.NextDouble();
        for (int i = 0; i < 400; i++)
        {
            double area = U(2e4, 2e6), t = U(40, 400), cot = U(1, 2.5);
            double cx = random.NextDouble() < .1 ? U(1, 2.5) : cot, cy = random.NextDouble() < .1 ? U(1, 2.5) : cot;
            double vx = random.NextDouble() < .3 ? 0 : U(-800, 800), vy = random.NextDouble() < .3 ? 0 : U(-800, 800);
            cases.Add(new(random.NextDouble() < .1 ? 0 : U(-400, 400), new TorsionGeometry(area, 4 * Math.Sqrt(area) * U(1, 1.5), t), U(8, 40), new[] { 391.3, 450 / 1.15, 500 / 1.15 }[random.Next(3)],
                U(28, 314), U(50, 300), random.NextDouble() < .1 ? 0 : U(0, 6000), cot, vx, vy,
                Shear(U(0, 1500), random.NextDouble() < .05 ? 0 : U(100, 2500), cx), Shear(U(0, 1500), U(100, 2500), cy)));
        }

        var csv = new StringBuilder();
        csv.AppendLine("# ConcreteTorsionCalculator.Calculate legacy outputs (NTC 2018); ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
        csv.AppendLine("# Units: T_kNm (kNm), Vx/Vy and shear resistances kN, lengths mm, areas mm2, stresses MPa; torsional resistances kNm. Shear results synthetic.");
        csv.AppendLine("id;T_kNm;A;P;t;fcd;fyd;leg;s;Al;cot;Vx_kN;Vy_kN;xVRsd;xVRcd;xCot;yVRsd;yVRcd;yCot;outcome;TRcd;TRsd;TRld;TRd;torsionRatio;concrete;steel;requiredAl;passed;status");
        var calculator = new ConcreteTorsionCalculator();
        for (int i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            string head = string.Join(";", i, F(c.TorqueKnM), F(c.Geometry.Area), F(c.Geometry.Perimeter), F(c.Geometry.Thickness), F(c.Fcd), F(c.Fyd), F(c.StirrupLegArea),
                F(c.Spacing), F(c.AvailableLongitudinalArea), F(c.CotTheta), F(c.VxKn), F(c.VyKn), F(c.ShearX.VRsd), F(c.ShearX.VRcd), F(c.ShearX.CotTheta),
                F(c.ShearY.VRsd), F(c.ShearY.VRcd), F(c.ShearY.CotTheta));
            try
            {
                var r = calculator.Calculate(c);
                csv.AppendLine(string.Join(";", head, "ok", F(r.TRcd), F(r.TRsd), F(r.TRld), F(r.TRd), F(r.TorsionRatio), F(r.ConcreteCombinedRatio), F(r.SteelCombinedRatio),
                    F(r.RequiredLongitudinalArea), r.Passed, Clean(r.Status)));
            }
            catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", "", "", "", "", "", Clean(ex.Message))); }
        }
        File.WriteAllText(Path.Combine(output, "torsion-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{gid} geometrie e {cases.Count} casi di torsione -> {Path.Combine(output, "torsion-legacy.csv")}");
    }
}

// Cracking (Ntc2018Checks.Cracking with ConcreteCodeChecks, ConcreteTensionCracking, ConcreteInnerCracking): sections saved with the Model
// archive, actions in the local axes passed to the native checker, crack options rotated over the states, legacy result and regions.
internal static class CrackCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    static string Clean(string s) => s.Replace(";", ",").Replace("\n", " ").Replace("|", "/");
    static readonly string[] Symbols = ["Criterio k₂", "hc,eff", "Ac,eff", "As,eff", "Øeq", "σs", "c", "s", "sr,max", "wk", "εsm − εcm", "Δsm adottata", "σct,max", "σct,lim", "h − x", "Qtaglio"];
    internal static void Run(string output, string commit, string sha)
    {
        var sections = new List<(string Name, Action<System.Text.Json.Nodes.JsonObject> Edit)>
        {
            ("R300x500", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3";
                i["side_bar_count_per_side"] = "0"; i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["fck_mpa"] = "30"; }),
            ("T1200x800", i => { i["shape"] = "A T"; i["fck_mpa"] = "35"; }),
            ("C1000", i => { i["shape"] = "Circolare"; i["fck_mpa"] = "40"; }),
            ("R600x800H", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "600"; i["height_mm"] = "800"; i["foro_presente"] = true; i["inner_width_mm"] = "300";
                i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; i["fck_mpa"] = "28"; }),
            ("C1000H", i => { i["shape"] = "Circolare"; i["foro_presente"] = true; i["inner_diameter_mm"] = "500"; i["second_inner_enabled"] = true;
                i["second_inner_diameter"] = "16"; i["second_inner_count"] = "12"; i["second_inner_gap"] = "88"; i["fck_mpa"] = "32"; }),
            ("R400x400", i => { i["shape"] = "Rettangolare"; i["width_mm"] = "400"; i["height_mm"] = "400"; i["top_bar_count"] = "4"; i["bottom_bar_count"] = "4";
                i["side_bar_count_per_side"] = "1"; i["top_bar_diameter_mm"] = "20"; i["bottom_bar_diameter_mm"] = "20"; i["side_bar_diameter_mm"] = "20"; i["cover_mm"] = "35"; i["fck_mpa"] = "25"; })
        };
        var variants = new (string Exposure, string Sensitivity, string Duration, string Bond, string Cover, string Spacing, string Limit)[]
        {
            ("XC3", "Poco sensibile", "Lunga", "Migliorata", "", "", ""), ("XD1", "Sensibile", "Lunga", "Migliorata", "", "", ""),
            ("XS3", "Poco sensibile", "Breve", "Migliorata", "", "", ""), ("XC1", "Sensibile", "Breve", "Liscia", "", "", ""),
            ("XD3", "Poco sensibile", "Lunga", "Migliorata", "45", "180", ""), ("XC2", "Poco sensibile", "Lunga", "Migliorata", "", "", "0.25"),
            ("Da scegliere", "Poco sensibile", "Lunga", "Migliorata", "", "", ""), ("XA1", "Poco sensibile", "Breve", "Migliorata", "", "", "")
        };
        var actions = new[] { new ActionPoint(-500, 50, 0), new ActionPoint(-200, 150, 40), new ActionPoint(0, 120, 0), new ActionPoint(100, 30, 0), new ActionPoint(-1500, 0, 0),
            new ActionPoint(-800, -60, 90), new ActionPoint(300, 0, 0), new ActionPoint(400, 20, 15), new ActionPoint(50, -90, 60),
            new ActionPoint(-100, 80, 0), new ActionPoint(0, 60, 40), new ActionPoint(-50, 260, 0), new ActionPoint(20, -150, 10) };
        int skipped = 0;
        var model = new GPC.Model.Models.Model("Sezioni fessurazione congelate");
        var csv = new StringBuilder();
        csv.AppendLine("# Ntc2018Checks.Cracking legacy outputs; ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
        csv.AppendLine("# Sections in crack-sections.xml (Model archive, properties by name). Forces N, Nmm in the local axes passed to the checker; widths mm, areas mm2, stresses MPa.");
        csv.AppendLine("# regions: name:area:steel:width:bar indices (0-based, '/' separated); details: symbol=value of the selected trace entries.");
        csv.AppendLine("id;section;standard;coefficients;linear;psi;tension;divisions;origin;v1;v2;N;V1;V2;T;M1;M2;set;exposure;sensitivity;duration;bond;cover;spacing;limit;outcome;width;wlim;ratio;passed;status;aceff;aseff;barSpacing;spacingSource;regions;details");
        int id = 0, variant = 0;
        foreach (var (name, edit) in sections)
        {
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject(); edit(input);
            var prepared = CheckerSection.PrepareModel(input, settings);
            prepared.Section.Name = name; model.AddProperty(prepared.Section);
            foreach (var standard in ConcreteStandards.OrdinaryNames)
                foreach (var action in actions)
                    foreach (var set in new[] { "SLE", "SLE_FREQ", "SLE_QP" })
                    {
                        var v = variants[variant++ % variants.Length];
                        bool linear = id % 11 != 5, tension = id % 13 == 7; string psi = id % 2 == 0 ? "0" : "15";
                        settings["normativa"] = standard; input["gettato_sottile"] = "No";
                        var options = (System.Text.Json.Nodes.JsonObject)settings["sle"]![set]!.DeepClone();
                        options["modello"] = linear ? "Lineare" : "Non lineare"; options["phi"] = psi; options["trazione_cls"] = tension ? "Sì" : "No"; options["angoli"] = "32";
                        options["esposizione"] = v.Exposure; options["sensibilita"] = v.Sensitivity; options["durata"] = v.Duration; options["aderenza"] = v.Bond;
                        options["copriferro_fessure"] = v.Cover; options["spaziatura_fessure"] = v.Spacing; options["limite_fessure"] = v.Limit;
                        // Model Code 2010 has no default limit: a design wlim in three states out of four.
                        if (standard == "Model Code 2010" && v.Limit == "" && variant % 4 != 0) options["limite_fessure"] = "0.3";
                        // Keep one state out of four where the standard does not require the check for this combination.
                        if (ConcreteCodeChecks.CrackRequirement(standard, set, options).Kind.StartsWith("Non richiesta") && skipped++ % 4 != 0) continue;
                        var effective = ConcreteStandards.Effective(input, settings);
                        string coefficients = string.Join(",", ConcreteStandards.Coefficients.Select(c => c.Key + "=" + F((double)typeof(GPC.Model.Standards.StandardModelCode2010).GetProperty(c.Key)!.GetValue(effective)!)));
                        CheckerSection engine;
                        try { engine = new CheckerSection(prepared, input, settings, options); }
                        catch (Exception ex) { csv.AppendLine(string.Join(";", id++, name, standard, "", "", "", "", "", "", "", "", "", "", "", "", "", "", set, "", "", "", "", "", "", "", "error:" + ex.GetType().Name, "", "", "", "", Clean(ex.Message), "", "", "", "", "", "")); continue; }
                        var force = engine.Force(action); var cs = force.CoordinateSystem;
                        string head = string.Join(";", id++, name, standard, coefficients, linear, psi, tension, 32,
                            F(cs.Origin.X) + "," + F(cs.Origin.Y) + "," + F(cs.Origin.Z), F(cs.V1.X) + "," + F(cs.V1.Y) + "," + F(cs.V1.Z), F(cs.V2.X) + "," + F(cs.V2.Y) + "," + F(cs.V2.Z),
                            F(force.N), F(force.V1), F(force.V2), F(force.T), F(force.M1), F(force.M2), set, v.Exposure, v.Sensitivity, v.Duration, v.Bond, v.Cover, v.Spacing,
                            options.S("limite_fessure"));
                        try
                        {
                            var state = engine.Stress(action, set);
                            var r = Ntc2018Checks.Cracking(engine, state, action, input, settings, options, set);
                            string regions = string.Join("|", r.Regions.Select(g => string.Join(":", Clean(g.Name), F(g.Area), F(g.SteelArea), F(g.Width), string.Join("/", g.BarIndices))));
                            string details = string.Join("|", r.Details.Where(d => d.Value is double && Symbols.Any(s => d.Symbol == s || d.Symbol.EndsWith(" · " + s)))
                                .Select(d => Clean(d.Symbol) + "=" + F(d.Value)));
                            csv.AppendLine(string.Join(";", head, "ok", F(r.Width), F(r.Limit), F(r.Ratio), r.Passed?.ToString() ?? "", Clean(r.Status), F(r.EffectiveArea), F(r.EffectiveSteel),
                                F(r.BarSpacing), r.SpacingSource ?? "", regions, details));
                        }
                        catch (Exception ex) { csv.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", Clean(ex.Message), "", "", "", "", "", "")); }
                    }
        }
        using (var stream = File.Create(Path.Combine(output, "crack-sections.xml"))) GPC.Model.Persistence.ModelArchive.Save(model, stream);
        File.WriteAllText(Path.Combine(output, "crack-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{id} stati di fessurazione -> {Path.Combine(output, "crack-legacy.csv")}");

        // Scalar cores on seeded random inputs: crack width of every ordinary standard and the requirement table.
        var scalar = new StringBuilder();
        scalar.AppendLine("# ConcreteCodeChecks.CrackWidth / CrackRequirement legacy outputs; ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
        scalar.AppendLine("# width: standard;sigma;es;ecm;fct;rho;phi;cover;spacing;tensileDepth;short;ribbed;k2;outcome;wk|sr|strain  requirement: standard;set;exposure;sensitive;limit;kind;wlim");
        var random = new Random(20261003);
        double U(double a, double b) => a + (b - a) * random.NextDouble();
        for (int i = 0; i < 1400; i++)
        {
            string standard = ConcreteStandards.OrdinaryNames[i % ConcreteStandards.OrdinaryNames.Length];
            double sigma = i % 97 == 0 ? 0 : U(0, 450), es = 200000, ecm = U(26000, 42000), fct = U(1.8, 4.8), rho = U(.002, .08), phi = U(8, 32);
            double cover = i % 89 == 0 ? 0 : U(15, 80), spacing = U(50, 450), depth = U(40, 900); bool shortTerm = random.NextDouble() < .4, ribbed = random.NextDouble() < .85;
            double k2 = random.NextDouble() < .5 ? .5 : random.NextDouble() < .5 ? 1 : U(.5, 1);
            string head = string.Join(";", "W" + i, standard, F(sigma), F(es), F(ecm), F(fct), F(rho), F(phi), F(cover), F(spacing), F(depth), shortTerm, ribbed, F(k2));
            try
            {
                var trace = new List<CrackCalculationDetail>();
                double w = ConcreteCodeChecks.CrackWidth(standard, sigma, es, ecm, fct, rho, phi, cover, spacing, depth, shortTerm, ribbed, k2, trace);
                scalar.AppendLine(string.Join(";", head, "ok", F(w)));
            }
            catch (Exception ex) { scalar.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "")); }
        }
        foreach (var standard in ConcreteStandards.OrdinaryNames)
            foreach (var set in new[] { "SLE", "SLE_FREQ", "SLE_QP" })
                foreach (var exposure in Ntc2018Checks.Exposures)
                    foreach (var sensitive in new[] { false, true })
                        foreach (var limit in new[] { "", "0.25" })
                        {
                            var o = new System.Text.Json.Nodes.JsonObject { ["esposizione"] = exposure, ["sensibilita"] = sensitive ? "Sensibile" : "Poco sensibile", ["limite_fessure"] = limit };
                            var req = ConcreteCodeChecks.CrackRequirement(standard, set, o);
                            scalar.AppendLine(string.Join(";", "R", standard, set, exposure, sensitive, limit, Clean(req.Kind), F(req.Limit)));
                        }
        File.WriteAllText(Path.Combine(output, "crack-scalar-legacy.csv"), scalar.ToString(), new UTF8Encoding(false));
        Console.WriteLine("nuclei scalari della fessurazione -> " + Path.Combine(output, "crack-scalar-legacy.csv"));
    }
}

// Detailing (ConcreteAnchorageCalculator, ConcreteBond, ConcreteDetailingCalculator for beams and columns) and the moment-curvature
// response (ConcreteCurvatureAnalysis). Sections saved with the Model archive; curves with the forces passed to the native checker.
internal static class DetailingCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    static string Clean(string s) => s.Replace(";", ",").Replace("\n", " ").Replace("|", "/");
    internal static void Run(string output, string commit, string sha)
    {
        string header = "ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha;
        // A. Anchorage and laps: grid and seeded random cases.
        var anchorage = new StringBuilder();
        anchorage.AppendLine("# ConcreteAnchorageCalculator / ConcreteBond.Strength legacy outputs; " + header);
        anchorage.AppendLine("# Units mm, MPa. Columns: id;diameter;stress;fctk05;gammaC;goodBond;available;lap;percent;clear;outcome;fbd;basic;required;passed;eta1;eta2;alpha6;maxClear;lengthPassed;clearPassed");
        var calculator = new ConcreteAnchorageCalculator(); var random = new Random(20261004); int aid = 0;
        double U(double a, double b) => a + (b - a) * random.NextDouble();
        void Anchor(AnchorageInput input)
        {
            string head = string.Join(";", aid++, F(input.Diameter), F(input.Stress), F(input.Fctk05), F(input.GammaC), input.GoodBond, F(input.AvailableLength), input.Lap,
                F(input.LapPercent), F(input.LapClearDistance));
            try
            {
                var r = calculator.Calculate(input);
                anchorage.AppendLine(string.Join(";", head, "ok", F(r.Fbd), F(r.BasicLength), F(r.RequiredLength), r.Passed, F(r.Eta1), F(r.Eta2), F(r.Alpha6), F(r.MaximumLapClearDistance),
                    r.LengthPassed, r.LapClearDistancePassed));
            }
            catch (Exception ex) { anchorage.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, "", "", "", "", "", "", "", "", "", "")); }
        }
        foreach (double d in new[] { 8.0, 16, 26, 32, 36, 40 })
            foreach (double stress in new[] { 0.0, 250, 391.3 })
                foreach (bool good in new[] { true, false })
                    foreach (bool lap in new[] { false, true })
                        foreach (double percent in lap ? new[] { 25.0, 50, 100 } : new[] { 100.0 })
                            Anchor(new AnchorageInput(d, stress, 1.8, 1.5, good, 800, lap, percent, lap ? 2 * d : 0));
        Anchor(new AnchorageInput(0, 391.3, 1.8, 1.5, true, 800, false, 100, 0)); Anchor(new AnchorageInput(16, -1, 1.8, 1.5, true, 800, false, 100, 0));
        Anchor(new AnchorageInput(16, 391.3, 1.8, 1.5, true, 800, true, 0, 0)); Anchor(new AnchorageInput(16, 391.3, 1.8, 1.5, true, 800, true, 50, -1));
        Anchor(new AnchorageInput(140, 391.3, 1.8, 1.5, true, 800, false, 100, 0)); Anchor(new AnchorageInput(16, 391.3, 1.8, 1.5, true, 800, true, 50, 80));
        for (int i = 0; i < 300; i++)
            Anchor(new AnchorageInput(new[] { 8.0, 10, 12, 14, 16, 20, 24, 26, 28, 32, 36, 40 }[random.Next(12)], U(0, 450), U(1.1, 3.1), new[] { 1.5, 1.45, 1.4 }[random.Next(3)],
                random.NextDouble() < .7, U(0, 2000), random.NextDouble() < .4, U(5, 100), U(0, 200)));
        foreach (double fct in new[] { 1.2, 1.8, 2.7 })
            foreach (double d in new[] { 12.0, 32, 40 })
                foreach (double eta1 in new[] { 1.0, .7 })
                    anchorage.AppendLine(string.Join(";", "B" + aid++, F(d), "", F(fct), "1.5", eta1 == 1, "", "", "", "", "bond", F(ConcreteBond.Strength(fct, d, eta1, 1, 1.5))));
        File.WriteAllText(Path.Combine(output, "anchorage-legacy.csv"), anchorage.ToString(), new UTF8Encoding(false));

        // B. Detailing of beams and columns on sections saved with the Model archive.
        var sections = new List<(string Name, Action<System.Text.Json.Nodes.JsonObject> Edit)>
        {
            ("R300x500", i => { i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3"; i["side_bar_count_per_side"] = "0";
                i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["transverse_bar_diameter_mm"] = "8"; i["fck_mpa"] = "30"; }),
            ("T1200x800", i => { i["shape"] = "A T"; i["fck_mpa"] = "35"; }),
            ("R400x400", i => { i["width_mm"] = "400"; i["height_mm"] = "400"; i["top_bar_count"] = "3"; i["bottom_bar_count"] = "3"; i["side_bar_count_per_side"] = "1";
                i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "16"; i["side_bar_diameter_mm"] = "16"; i["cover_mm"] = "35"; i["fck_mpa"] = "25"; }),
            ("C400", i => { i["shape"] = "Circolare"; i["diameter_mm"] = "400"; i["longitudinal_bar_count"] = "8"; i["longitudinal_bar_diameter_mm"] = "16"; i["cover_mm"] = "40";
                i["transverse_bar_diameter_mm"] = "8"; i["fck_mpa"] = "32"; }),
            ("R250x250", i => { i["width_mm"] = "250"; i["height_mm"] = "250"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "2"; i["side_bar_count_per_side"] = "0";
                i["top_bar_diameter_mm"] = "10"; i["bottom_bar_diameter_mm"] = "10"; i["cover_mm"] = "25"; i["transverse_bar_diameter_mm"] = "6"; i["fck_mpa"] = "20"; }),
            ("R600x800H", i => { i["foro_presente"] = true; i["inner_width_mm"] = "300"; i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; i["fck_mpa"] = "28"; })
        };
        var model = new GPC.Model.Models.Model("Sezioni dettagli congelate");
        var detailing = new StringBuilder();
        detailing.AppendLine("# ConcreteDetailingCalculator legacy outputs (beams and columns); " + header);
        detailing.AppendLine("# Sections in detailing-sections.xml. Lengths mm, areas mm2, NEd N (compression positive), checks: name:actual:limit:unit:passed separated by '|'.");
        detailing.AppendLine("id;section;kind;fck;fyk;fyd;areaCls;width;height;topWidth;bottomWidth;webWidth;nominalCover;compression;stirrups;stirrupDiameter;stirrupSpacing;legs;aggregate;durabilityCover;deviation;lap;restrained;endAnchorage;outcome;checks");
        int did = 0, variant = 0;
        foreach (var (name, edit) in sections)
        {
            var input = SezioneCA.DefaultInput(); edit(input);
            var s = new SezioneCA(input);
            var settings = SectionWorkspace.Prepare(SezioneCA.DefaultData());
            var prepared = CheckerSection.PrepareModel(input, settings); prepared.Section.Name = name; model.AddProperty(prepared.Section);
            double top = s.Shape == "A T" ? s.Input.D("flange_width_mm") : s.Width, bottom = s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width;
            foreach (var kind in new[] { ConcreteMemberKind.Beam, ConcreteMemberKind.Column })
                foreach (double nEd in kind == ConcreteMemberKind.Column ? new[] { 0.0, 1500, 4000 } : new[] { 0.0 })
                    for (int k = 0; k < 6; k++)
                    {
                        int v = variant++;
                        bool stirrups = v % 7 != 3; double sd = new[] { 6.0, 8, 10 }[v % 3], sp = new[] { 100.0, 200, 300 }[(v / 3) % 3]; int legs = v % 2 == 0 ? 2 : 4;
                        double aggregate = new[] { 16.0, 20, 40 }[(v / 2) % 3], durability = new[] { double.NaN, 25, 45 }[v % 3], deviation = v % 2 == 0 ? 10 : 5;
                        bool lap = v % 5 == 1, restrained = v % 4 != 2, end = v % 3 != 1;
                        var request = new ConcreteDetailingInput(kind, s, nEd, stirrups, sd, sp, legs, aggregate, durability, deviation, lap, 0, 0, false, restrained, end);
                        string head = string.Join(";", did++, name, kind, F(s.Input.D("fck_mpa")), F(s.Input.D("fyk_mpa")), F(s.Fyd), F(s.AreaCls), F(s.Width), F(s.Height), F(top), F(bottom),
                            F(s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width), F(s.Input.D("cover_mm")), F(nEd * 1000), stirrups, F(sd), F(sp), legs, F(aggregate), F(durability), F(deviation),
                            lap, restrained, end);
                        try
                        {
                            var checks = new ConcreteDetailingCalculator().Calculate(request);
                            detailing.AppendLine(string.Join(";", head, "ok", string.Join("|", checks.Select(c => string.Join(":", Clean(c.Name), F(c.Actual), F(c.Limit), c.Unit, c.Passed?.ToString() ?? "")))));
                        }
                        catch (Exception ex) { detailing.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, Clean(ex.Message))); }
                    }
        }
        using (var stream = File.Create(Path.Combine(output, "detailing-sections.xml"))) GPC.Model.Persistence.ModelArchive.Save(model, stream);
        File.WriteAllText(Path.Combine(output, "detailing-legacy.csv"), detailing.ToString(), new UTF8Encoding(false));

        // C. Moment-curvature curves: limit point at constant N on the 3D domain, nonlinear stress responses.
        var curves = new StringBuilder();
        curves.AppendLine("# ConcreteCurvatureAnalysis legacy outputs; " + header);
        curves.AppendLine("# Sections in detailing-sections.xml. Moments kNm, curvatures 1/m, strains per mille; force columns: N, M1, M2 (N, Nmm) passed to the native checker in its local axes.");
        curves.AppendLine("id;section;standard;coefficients;origin;v1;v2;yieldStrain;N_kN;theta;steps;fraction;quadratic;tolerance;refine;outcome;limitMoment;yield;ultimate;limitN;residual;points");
        int cid = 0;
        foreach (var (name, n, theta, standard) in new[] { ("R300x500", -200.0, 0.0, "NTC 2018"), ("R300x500", 0.0, 180.0, "EN 1992-1-1"), ("R400x400", -1200.0, 30.0, "NTC 2018"),
            ("C400", -500.0, 45.0, "Model Code 2010"), ("T1200x800", 0.0, 0.0, "NTC 2018") })
        {
            var input = SezioneCA.DefaultInput(); sections.Single(x => x.Name == name).Edit(input);
            var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); settings["normativa"] = standard;
            var options = new System.Text.Json.Nodes.JsonObject { ["N"] = F(n), ["theta"] = F(theta), ["passi"] = "40", ["frazione"] = "1", ["campionamento"] = "Quadratico",
                ["tolleranza_n"] = "1", ["raffina_snervamento"] = "12", ["angoli"] = "32", ["phi"] = "0" };
            var probe = new CheckerSection(input, settings, options); var axes = probe.Force(new ActionPoint(n, 1, 0)).CoordinateSystem;
            var effective = ConcreteStandards.Effective(input, settings);
            string coefficients = string.Join(",", ConcreteStandards.Coefficients.Select(c => c.Key + "=" + F((double)typeof(GPC.Model.Standards.StandardModelCode2010).GetProperty(c.Key)!.GetValue(effective)!)));
            string head = string.Join(";", cid++, name, standard, coefficients, F(axes.Origin.X) + "," + F(axes.Origin.Y) + "," + F(axes.Origin.Z),
                F(axes.V1.X) + "," + F(axes.V1.Y) + "," + F(axes.V1.Z), F(axes.V2.X) + "," + F(axes.V2.Y) + "," + F(axes.V2.Z), F(probe.Geometry.Fyd / probe.Geometry.Es),
                F(n), F(theta), 40, 1, true, 1, 12);
            try
            {
                var r = ConcreteCurvatureAnalysis.Calculate(input, settings, options);
                var engine = probe;
                string points = string.Join("|", r.Points.Select(p =>
                {
                    var force = engine.Force(new ActionPoint(n, p.Mx, p.My));
                    return string.Join(":", F(p.Moment), F(p.Curvature), F(p.GradientX), F(p.GradientY), F(p.Epsilon0), F(p.ConcreteCompressionStrain), F(p.SteelStrain), p.Yielded, p.Limit,
                        F(force.N), F(force.M1), F(force.M2));
                }));
                curves.AppendLine(string.Join(";", head, "ok", F(r.LimitMoment), F(r.YieldCurvature), F(r.UltimateCurvature), F(r.LimitAxialKn), F(r.AxialResidualKn), points));
            }
            catch (Exception ex) { curves.AppendLine(string.Join(";", head, "error:" + ex.GetType().Name, Clean(ex.Message))); }
        }
        File.WriteAllText(Path.Combine(output, "curvature-legacy.csv"), curves.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{aid} ancoraggi, {did} dettagli, {cid} curve M-χ -> {output}");
    }
}

// Durability (Materiali: Durability.Cover EC2 4.4N with structural classes, NtcCover.Calculate, MinimumConcrete, AtecapMix) on a grid of exposures,
// strengths and options, single and combined exposures.
internal static class DurabilityCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    static string Clean(string s) => s.Replace(";", ",").Replace("\n", " ").Replace("|", "/");
    internal static void Run(string output, string commit, string sha)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# Materiali.Durability / NtcCover / MinimumConcrete / AtecapMix legacy outputs; ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha);
        csv.AppendLine("# Lengths mm, strengths MPa. method;exposures;fck;life;strengthReduction;slab;quality;diameter;aggregate;deviation;rough;abrasion;ground;plate;ntcQuality;outcome;bond;durability;minimum;nominal;lines|...");
        var exposures = Materiali.Durability.Exposures;
        var sets = exposures.Select(e => new[] { e }).Concat(new[] { new[] { "XC4", "XF2" }, new[] { "XC4", "XS3", "XF4" }, new[] { "XD1", "XA2" }, new[] { "XF1", "XF3" }, new[] { "X0", "XC1" }, new[] { "XF4" } }
            .Select(c => c.Select(code => exposures.Single(e => e.Code == code)).ToArray())).ToArray();
        int n = 0;
        foreach (var set in sets)
            foreach (double fck in new[] { 20.0, 25, 30, 35, 40, 45, 50 })
                for (int k = 0; k < 6; k++)
                {
                    int v = n++;
                    var p = new Materiali.CoverInput(v % 4 == 3 ? 100 : 50, v % 3 == 1, v % 5 == 2, v % 7 == 4, new[] { 12.0, 16, 20, 32 }[v % 4], new[] { 16.0, 20, 40 }[v % 3],
                        new[] { 10.0, 5, 0 }[v % 3], v % 6 == 5, new[] { 0, 5, 10, 15 }[v % 4], new[] { 0, 40, 75 }[(v / 3) % 3]);
                    string codes = string.Join("+", set.Select(e => e.Code));
                    string head = string.Join(";", codes, F(fck), p.Life, p.StrengthReduction, p.Slab, p.Quality, F(p.Diameter), F(p.Aggregate), F(p.Deviation), p.Rough, p.Abrasion, p.Ground);
                    try
                    {
                        var r = Materiali.Durability.Cover(set, fck, p);
                        csv.AppendLine(string.Join(";", "EC2", head, "", "", "ok", F(r.Bond), F(r.Durability), F(r.Minimum), F(r.Nominal), string.Join("|", r.Lines.Select(l => l.Exposure + ":" + l.StructuralClass + ":" + F(l.Durability)))));
                    }
                    catch (Exception ex) { csv.AppendLine(string.Join(";", "EC2", head, "", "", "error:" + ex.GetType().Name, "", "", "", "", Clean(ex.Message))); }
                    foreach (bool plate in new[] { false, true })
                    {
                        bool quality = v % 2 == 1; int? pertinent = null;
                        try
                        {
                            if (v % 5 == 3) pertinent = Materiali.MinimumConcrete.Required(set);
                            var r = Materiali.NtcCover.Calculate(set, fck, p, plate, quality, pertinent);
                            csv.AppendLine(string.Join(";", "NTC", head, plate, quality + (pertinent is int c ? ":" + c : ""), "ok", F(r.Cover.Bond), F(r.Cover.Durability), F(r.Cover.Minimum), F(r.Cover.Nominal),
                                string.Join("|", r.Environment, r.Severity, F(r.Cmin), F(r.C0), F(r.TableCover), F(r.LifeExtra), F(r.LowStrengthExtra), F(r.QualityReduction))));
                        }
                        catch (Exception ex) { csv.AppendLine(string.Join(";", "NTC", head, plate, quality + (pertinent is int c ? ":" + c : ""), "error:" + ex.GetType().Name, "", "", "", "", Clean(ex.Message))); }
                    }
                }
        foreach (var set in sets)
        {
            try
            {
                var mix = Materiali.AtecapMix.Required(set);
                csv.AppendLine(string.Join(";", "MIX", string.Join("+", set.Select(e => e.Code)), F(Materiali.MinimumConcrete.Required(set)), F(mix.Ratio), mix.Cement?.ToString() ?? "",
                    F(Materiali.AtecapMix.Air(set, 16)), F(Materiali.AtecapMix.Air(set, 32)), F(Materiali.AtecapMix.Air(set, 8))));
            }
            catch (Exception ex) { csv.AppendLine(string.Join(";", "MIX", string.Join("+", set.Select(e => e.Code)), "error:" + ex.GetType().Name, "", "", "", "", Clean(ex.Message))); }
        }
        File.WriteAllText(Path.Combine(output, "durability-legacy.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"{n} combinazioni di durabilità -> {Path.Combine(output, "durability-legacy.csv")}");
    }
}

// General geotechnics (Anthea.Calculations.Geotechnics): Bishop on assigned slices, slope geometry, slices and searches on sections, oedometric
// settlement under a strip, Newmark rigid block, EN 1998-5 Annex F bearing capacity. Legacy units: m, kN, kN/m, kPa, kN/m³, degrees, g, s.
// Every input is written in the files so that the Checker tests rebuild it after the unit conversion.
internal static class GeotechnicsCapture
{
    static string F(double? v) => v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : "";
    static string Clean(string s) => s.Replace(";", ",").Replace("\n", " ").Replace("|", "/");
    static string Points(IEnumerable<SlopePoint> p) => string.Join("/", p.Select(q => F(q.X) + ":" + F(q.Y)));
    static string Circle(SlipCircle? c) => c is null ? "null" : string.Join(":", F(c.X), F(c.Y), F(c.Radius), F(c.Left), F(c.Right));
    static string Slice(SlopeSlice s) => string.Join(":", s.Index, F(s.Left), F(s.Right), F(s.BaseY), F(s.TopY), F(s.Alpha), s.Soil.Replace(":", " "), F(s.SoilWeight),
        F(s.BodyWeight), F(s.WeightX), F(s.WeightY), F(s.VerticalLoad), F(s.HorizontalLoad), F(s.U), F(s.Phi), F(s.Cohesion), F(s.Vertical), F(s.Driving), F(s.NormalEffective),
        F(s.Resistance), F(s.Mobilized), F(s.MAlpha));
    static string Soils(IEnumerable<SlopeSoil> soils) => string.Join("|", soils.Select(s => string.Join(":", s.Name, F(s.Bottom), F(s.Gamma), F(s.GammaSat), F(s.Phi), F(s.Cohesion), F(s.Cu))));
    static string Factors(SlopeFactors f) => string.Join(":", f.Name, F(f.Soil), F(f.Body), F(f.MPhi), F(f.MC), F(f.MCu), F(f.R), F(f.Kh), F(f.Kv), f.Undrained,
        string.Join("/", f.Loads.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + F(p.Value))));

    internal static void Run(string output, string commit, string sha)
    {
        string header = "ANTHEA commit " + commit + "; ANTHEA.Calculations.dll SHA-256 " + sha;
        Bishop(output, header); Slopes(output, header); Settlement(output, header); Newmark(output, header); Seismic(output, header);
    }

    static void Bishop(string output, string header)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# BishopSolver.Solve legacy outputs; " + header);
        csv.AppendLine("# Units m, kN/m, kPa, degrees. id;gammaR;slices alpha:phi:c:u:b:V:D|...;outcome;F;ratio;iterations;residual;driving;resistance;slices N':R:Rmob:malpha|...");
        var random = new Random(20261002);
        double U(double a, double b) => a + (b - a) * random.NextDouble();
        SlopeSlice Make(int i, double left, double b, double alpha, double phi, double c, double u, double v, double d)
            => new(i, left, left + b, 0, 1, alpha, "S", v, 0, left + b / 2, .5, 0, 0, u, phi, c, v, d, 0, 0, 0, 0);
        var cases = new List<(double GammaR, SlopeSlice[] Slices)>
        {
            (1.1, new[] { Make(1, 0, 1, 30, 30, 0, 0, 100, 50) }), (1, new[] { Make(1, 0, 1, 30, 0, 10, 0, 100, 50) }),
            (1, new[] { Make(1, 0, 1, 30, 30, 0, 10, 100, 50) }), (1, new[] { Make(1, 0, 1, 30, 30, 0, 0, 100, 0) }),
            (1, new[] { Make(1, 0, 1, 30, 30, 0, 0, 100, -20) }), (1, new[] { Make(1, 0, 1, -40, 35, 50, 0, 10, 1), Make(2, 1, 1, 60, 35, 0, 0, 100, 80) }),
            (0, new[] { Make(1, 0, 1, 30, 30, 0, 0, 100, 50) }), (double.NaN, new[] { Make(1, 0, 1, 30, 30, 0, 0, 100, 50) }), (1, Array.Empty<SlopeSlice>()),
            (1, new[] { Make(1, 0, 1, 30, 30, 0, 500, 100, 50) }), (1, new[] { Make(1, 0, 1, 0, 30, 0, 0, 100, 30) }), (1, new[] { Make(1, 0, 1, 80, 45, 0, 0, 100, 98) }),
            (1, new[] { Make(1, 0, 1, -60, 45, 0, 0, 100, 1), Make(2, 1, 1, 30, 0, 1, 0, 100, 50) }), (1, new[] { Make(1, 0, 1, 30, 30, 0, 0, 100, 1e-10) })
        };
        for (int k = 0; k < 400; k++)
        {
            int n = 1 + random.Next(40); var slices = new SlopeSlice[n]; double x = 0;
            for (int i = 0; i < n; i++)
            {
                double b = U(.2, 2), alpha = U(-25, 60), v = U(5, 400);
                slices[i] = Make(i + 1, x, b, alpha, random.NextDouble() < .15 ? 0 : U(0, 45), random.NextDouble() < .3 ? 0 : U(0, 40), random.NextDouble() < .6 ? 0 : U(0, .4 * v / b),
                    v, v * Math.Sin(alpha * Math.PI / 180) * U(.7, 1.3));
                x += b;
            }
            cases.Add((new[] { 1, 1.1, 1.2, 1.3 }[random.Next(4)], slices));
        }
        var circle = new SlipCircle(0, 10, 10, -3, 6); int ok = 0, none = 0, errors = 0;
        for (int id = 0; id < cases.Count; id++)
        {
            var (gammaR, slices) = cases[id];
            string head = string.Join(";", id, F(gammaR), string.Join("|", slices.Select(s => string.Join(":", F(s.Alpha), F(s.Phi), F(s.Cohesion), F(s.U), F(s.Right - s.Left), F(s.Vertical), F(s.Driving)))));
            try
            {
                var r = BishopSolver.Solve(circle, slices, gammaR);
                if (r is null) { csv.AppendLine(head + ";null"); none++; continue; }
                csv.AppendLine(string.Join(";", head, "ok", F(r.Factor), F(r.Ratio), r.Iterations, F(r.Residual), F(r.Driving), F(r.Resistance),
                    string.Join("|", r.Slices.Select(s => string.Join(":", F(s.NormalEffective), F(s.Resistance), F(s.Mobilized), F(s.MAlpha)))))); ok++;
            }
            catch (Exception ex) { csv.AppendLine(head + ";error:" + ex.GetType().Name + ";" + Clean(ex.Message)); errors++; }
        }
        File.WriteAllText(Path.Combine(output, "geotechnics-bishop.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Bishop: {ok} risolti, {none} senza soluzione, {errors} rifiuti");
    }

    static void Slopes(string output, string header)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# SlopeGeometry and SlopeStability legacy outputs; " + header);
        csv.AppendLine("# Units m, kN/m, kPa, kN/m³, degrees. Rows: SECTION, SEARCH, CASE (search result and slices of the critical circle), SLICES (assigned circle), GEO, ERROR.");
        csv.AppendLine("# SECTION;id;surface x:y/...;soils name:bottom:gamma:gammaSat:phi:c:cu|...;valleySoils;split;water;bodies name~gamma~points|...;loads id:left:right:y:V:H:M:distributed|...;requiredLeft;requiredRight");
        csv.AppendLine("# CASE;section;search;factors name:soil:body:mphi:mc:mcu:r:kh:kv:undrained:loads;outcome;circle x:y:R:left:right;F;ratio;iterations;residual;driving;resistance;tried;valid;solved;failures;boundary;status;slices");
        var sections = new Dictionary<string, SlopeSection>(StringComparer.Ordinal);
        var s1 = new SlopeSection(new SlopePoint[] { new(-30, 0), new(0, 0), new(12, 6), new(40, 6) }, new[] { new SlopeSoil("Limo", -20, 19, 20, 30, 5, 0) },
            Array.Empty<SlopePoint>(), Array.Empty<SlopeBody>(), Array.Empty<SlopeLoad>(), 0, 12);
        var wall = new SlopeBody("Muro", new SlopePoint[] { new(0, -1), new(3, -1), new(3, -.4), new(.6, -.4), new(.6, 5), new(0, 5) }, 25);
        var s2 = new SlopeSection(new SlopePoint[] { new(-20, 0), new(0, 0), new(0, 5), new(25, 5) },
            new[] { new SlopeSoil("Riporto", -3, 18, 20, 32, 0, 0), new SlopeSoil("Argilla", -25, 19, 20.5, 26, 10, 60) },
            new SlopePoint[] { new(-20, -2), new(25, 1) }, new[] { wall },
            new[] { new SlopeLoad("Q1", 3, 25, 5, 15, 0, 0, true), new SlopeLoad("P1", 1.5, 1.5, 5, 50, 0, 0, false), new SlopeLoad("M1", 3, 3, 2, 0, 0, 20, false),
                new SlopeLoad("H1", 3, 3, 3, 0, 30, 0, false) }, 0, 3);
        var s3 = new SlopeSection(new SlopePoint[] { new(-20, 0), new(0, 0), new(8, 4), new(30, 4) }, new[] { new SlopeSoil("Argilla molle", -15, 17, 18, 22, 5, 25) },
            new SlopePoint[] { new(-20, -2), new(30, 1.5) }, Array.Empty<SlopeBody>(), new[] { new SlopeLoad("Q", 10, 30, 4, 20, 0, 0, true) }, 0, 8);
        var s4 = new SlopeSection(new SlopePoint[] { new(-25, 0), new(0, 0), new(10, 5), new(35, 5) },
            new[] { new SlopeSoil("Sabbia", -6, 18, 19, 34, 0, 0), new SlopeSoil("Marna", -30, 21, 22, 28, 25, 0) }, Array.Empty<SlopePoint>(), Array.Empty<SlopeBody>(),
            Array.Empty<SlopeLoad>(), 0, 10)
        { ValleySoils = new[] { new SlopeSoil("Alluvione", -4, 17.5, 19, 30, 0, 0), new SlopeSoil("Marna", -30, 21, 22, 28, 25, 0) }, SoilSplitX = 2 };
        sections["S1"] = s1; sections["S2"] = s2; sections["S3"] = s3; sections["S4"] = s4;
        foreach (var (id, s) in sections)
            csv.AppendLine(string.Join(";", "SECTION", id, Points(s.Surface), Soils(s.Soils), Soils(s.ValleySoils), F(s.SoilSplitX), Points(s.Water),
                string.Join("|", s.Bodies.Select(b => b.Name + "~" + F(b.Gamma) + "~" + Points(b.Polygon))),
                string.Join("|", s.Loads.Select(l => string.Join(":", l.Id, F(l.Left), F(l.Right), F(l.Y), F(l.Vertical), F(l.Horizontal), F(l.Moment), l.Distributed))),
                F(s.RequiredLeft), F(s.RequiredRight)));
        var searches = new Dictionary<string, SlopeSearch>(StringComparer.Ordinal)
        {
            ["Q1"] = new(-15, -.5, 12.5, 30, .5, 12, 5, 30, 2), ["Q2"] = new(-12, -.3, 3.5, 20, 1.5, 15, 5, 40, 2), ["Q2N"] = new(-12, -.3, 3.5, 20, .2, .9, 3, 20, 0),
            ["Q3"] = new(-14, -.5, 8.5, 25, .5, 10, 5, 30, 1), ["Q4"] = new(-15, -.5, 10.5, 30, 1, 14, 4, 30, 2), ["Q1F"] = new(-15, -.5, 12.5, 30, .5, 12, 3, 20, 0)
        };
        foreach (var (id, q) in searches)
            csv.AppendLine(string.Join(";", "SEARCH", id, F(q.ExitMin), F(q.ExitMax), F(q.EntryMin), F(q.EntryMax), F(q.DepthMin), F(q.DepthMax), q.Grid, q.Slices, q.Refinements));
        var none = new Dictionary<string, double>();
        SlopeFactors Static(string name, IReadOnlyDictionary<string, double> loads, bool undrained = false) => new(name, 1, 1, 1.25, 1.25, 1.4, 1.1, 0, 0, undrained, loads);
        SlopeFactors Quake(string name, double kh, double kv, IReadOnlyDictionary<string, double> loads, bool undrained = false) => new(name, 1, 1, 1, 1, 1, 1.2, kh, kv, undrained, loads);
        var runs = new List<(string Section, string Search, SlopeFactors[] Cases)>
        {
            ("S1", "Q1", new[] { Static("A2+M2+R2", none), Quake("SISMA kv+", .12, .06, none), Quake("SISMA kv-", .12, -.06, none), new SlopeFactors("Pesi 1.3", 1.3, 1, 1, 1, 1, 1, 0, 0, false, none) }),
            ("S1", "Q1F", new[] { Static("A2+M2+R2 senza raffinamento", none) }),
            ("S2", "Q2", new[] { Static("A2+M2+R2", new Dictionary<string, double> { ["Q1"] = 1.3, ["P1"] = 1, ["M1"] = 1, ["H1"] = 1.3 }),
                Quake("SISMA", .1, .05, new Dictionary<string, double> { ["Q1"] = .3, ["P1"] = 1, ["M1"] = 1, ["H1"] = 0 }), Static("Senza carichi", none),
                new SlopeFactors("Muro 1.3", 1, 1.3, 1.25, 1.25, 1.4, 1.1, 0, 0, false, new Dictionary<string, double> { ["Q1"] = 1.5 }) }),
            ("S2", "Q2N", new[] { Static("Dominio sopra la fondazione", none) }),
            ("S3", "Q3", new[] { Static("Non drenata", new Dictionary<string, double> { ["Q"] = 1.3 }, true), Quake("Non drenata sisma", .08, .04, new Dictionary<string, double> { ["Q"] = .3 }, true),
                Static("Drenata", new Dictionary<string, double> { ["Q"] = 1.3 }) }),
            ("S4", "Q4", new[] { Static("A2+M2+R2", none), Quake("SISMA", .15, -.075, none) })
        };
        int count = 0;
        foreach (var (sectionId, searchId, cases) in runs)
        {
            var result = SlopeStability.Calculate(sections[sectionId], searches[searchId], cases);
            if (count == 0) csv.AppendLine("NOTES;" + result.Notes.Length + ";" + Clean(result.Notes[0]));
            foreach (var c in result.Cases)
            {
                var b = c.Critical;
                csv.AppendLine(string.Join(";", "CASE", sectionId, searchId, Factors(c.Factors), b is null ? "null" : "ok", Circle(b?.Circle), F(b?.Factor), F(b?.Ratio), b?.Iterations.ToString() ?? "",
                    F(b?.Residual), F(b?.Driving), F(b?.Resistance), c.Tried, c.GeometricallyValid, c.Solved, c.NumericalFailures, c.Boundary, Clean(c.Status),
                    b is null ? "" : string.Join("|", b.Slices.Select(Slice))));
                count++;
            }
        }
        // Slices of assigned circles (geometry, weights, water, bodies and loads without the search).
        var fixedCircles = new List<(string Section, SlipCircle? Circle, SlopeFactors Factors, int Count)>
        {
            ("S1", SlopeGeometry.Through(new(-6, 0), new(16, 6), -3), Static("A2+M2+R2", none), 30),
            ("S2", SlopeGeometry.Through(new(-8, 0), new(10, 5), -4), Static("A2+M2+R2", new Dictionary<string, double> { ["Q1"] = 1.3, ["P1"] = 1, ["M1"] = 1, ["H1"] = 1.3 }), 40),
            ("S2", SlopeGeometry.Through(new(-4, 0), new(9, 5), -2), Quake("SISMA", .1, .05, new Dictionary<string, double> { ["Q1"] = .3, ["P1"] = 1 }), 25),
            ("S2", SlopeGeometry.TangentAtEntry(new(-4, 0), new(6, 5)), Static("Tangente", none), 30),
            ("S3", SlopeGeometry.Through(new(-5, 0), new(12, 4), -2.5), Static("Non drenata", new Dictionary<string, double> { ["Q"] = 1.3 }, true), 30),
            ("S4", SlopeGeometry.Through(new(-8, 0), new(18, 5), -6), Static("A2+M2+R2", none), 50)
        };
        if (fixedCircles.Any(c => c.Circle is null)) throw new InvalidOperationException("Assigned circle not constructible.");
        foreach (var (sectionId, circle, factors, n) in fixedCircles)
        {
            var slices = SlopeStability.Slices(sections[sectionId], circle!, factors, n);
            var solved = BishopSolver.Solve(circle!, slices, factors.R);
            csv.AppendLine(string.Join(";", "SLICES", sectionId, Factors(factors), n, Circle(circle), SlopeGeometry.Admissible(sections[sectionId], circle!), F(solved?.Factor),
                string.Join("|", slices.Select(Slice))));
        }
        // Geometry functions.
        foreach (var (a, b, bottom) in new[] { (new SlopePoint(-4, 0), new SlopePoint(12, 3.45), -3.0), (new SlopePoint(-2, 0), new SlopePoint(4, 3.5), -3.0),
            (new SlopePoint(0, 0), new SlopePoint(10, 0), -1.0), (new SlopePoint(0, 0), new SlopePoint(10, 5), 0.0), (new SlopePoint(5, 0), new SlopePoint(1, 2), -2.0),
            (new SlopePoint(0, 0), new SlopePoint(1, 8), -.5), (new SlopePoint(-30, 0), new SlopePoint(40, 6), -20.0), (new SlopePoint(0, 1), new SlopePoint(2, 1), .999) })
            csv.AppendLine(string.Join(";", "GEO", "THROUGH", Points(new[] { a, b }), F(bottom), Circle(SlopeGeometry.Through(a, b, bottom))));
        foreach (var (a, b) in new[] { (new SlopePoint(-2, 0), new SlopePoint(4, 3.5)), (new SlopePoint(0, 0), new SlopePoint(10, 0)), (new SlopePoint(0, 0), new SlopePoint(3, 3)),
            (new SlopePoint(0, 0), new SlopePoint(3, -1)), (new SlopePoint(1, 0), new SlopePoint(0, 1)), (new SlopePoint(-6, 0), new SlopePoint(9, 5)) })
            csv.AppendLine(string.Join(";", "GEO", "TANGENT", Points(new[] { a, b }), "", Circle(SlopeGeometry.TangentAtEntry(a, b))));
        var probe = new SlipCircle(2, 8, 10, -6, 9);
        foreach (double y in new[] { -2.0, -1.9, 0, 5, 8, 8.1, -3 })
            csv.AppendLine(string.Join(";", "GEO", "CROSSINGS", Circle(probe), F(y), string.Join("/", SlopeGeometry.Crossings(probe, y).Select(v => F(v)))));
        var polygons = new[] { wall.Polygon, new SlopePoint[] { new(0, 0), new(3, 0), new(0, 6) }, new SlopePoint[] { new(0, 0), new(4, 0), new(4, 1), new(2, .5), new(0, 1) } };
        foreach (var polygon in polygons)
        {
            var props = SlopeGeometry.Properties(polygon);
            csv.AppendLine(string.Join(";", "GEO", "PROPERTIES", Points(polygon), "", F(props.Area) + ":" + F(props.Centroid.X) + ":" + F(props.Centroid.Y)));
            foreach (double x in new[] { -1, 0, .3, .6, 1, 2, 2.5, 3, 3.5 })
            {
                string value;
                try { var v = SlopeGeometry.VerticalInterval(polygon, x); value = v is null ? "null" : F(v.Value.Bottom) + ":" + F(v.Value.Top); }
                catch (Exception ex) { value = "error:" + ex.GetType().Name; }
                csv.AppendLine(string.Join(";", "GEO", "INTERVAL", Points(polygon), F(x), value));
            }
        }
        foreach (double x in new[] { -30, -30.000000001, -12, 0, 6, 12, 12.5, 40, 40.1 })
        {
            string value; try { value = F(SlopeGeometry.Height(s1.Surface, x)); } catch (Exception ex) { value = "error:" + ex.GetType().Name; }
            csv.AppendLine(string.Join(";", "GEO", "HEIGHT", Points(s1.Surface), F(x), value));
        }
        foreach (double x in new[] { 0, 0.0000001, 2, 25 })
            csv.AppendLine(string.Join(";", "GEO", "HEIGHT", Points(s2.Surface), F(x), F(SlopeGeometry.Height(s2.Surface, x))));
        foreach (var (sectionId, circle) in new[] { ("S2", SlopeGeometry.Through(new(-8, 0), new(10, 5), -4)), ("S2", SlopeGeometry.Through(new(-3, 0), new(6, 5), -.8)),
            ("S1", SlopeGeometry.Through(new(-6, 0), new(16, 6), -3)), ("S1", SlopeGeometry.Through(new(2, 1), new(16, 6), -3)), ("S1", SlopeGeometry.Through(new(-6, 0), new(16, 6), -25)) })
        {
            csv.AppendLine(string.Join(";", "GEO", "ADMISSIBLE", sectionId, Circle(circle), circle is null ? "" : SlopeGeometry.Admissible(sections[sectionId], circle).ToString()));
            if (circle is not null) csv.AppendLine(string.Join(";", "GEO", "DIVISIONS", sectionId, Circle(circle), string.Join("/", SlopeGeometry.Divisions(sections[sectionId], circle, 12).Select(v => F(v)))));
        }
        // Rejected inputs.
        var invalid = new List<(string Name, Action Run)>
        {
            ("Uscite oltre il muro", () => SlopeStability.Calculate(s1, new(-15, .5, 12.5, 30, .5, 12, 5, 30, 2), new[] { Static("x", none) })),
            ("Ingressi prima del muro", () => SlopeStability.Calculate(s1, new(-15, -.5, 11, 30, .5, 12, 5, 30, 2), new[] { Static("x", none) })),
            ("Profondità non coperta", () => SlopeStability.Calculate(s1, new(-15, -.5, 12.5, 30, .5, 21, 5, 30, 2), new[] { Static("x", none) })),
            ("Griglia 2", () => SlopeStability.Calculate(s1, new(-15, -.5, 12.5, 30, .5, 12, 2, 30, 2), new[] { Static("x", none) })),
            ("Conci 19", () => SlopeStability.Calculate(s1, new(-15, -.5, 12.5, 30, .5, 12, 5, 19, 2), new[] { Static("x", none) })),
            ("Raffinamenti 5", () => SlopeStability.Calculate(s1, new(-15, -.5, 12.5, 30, .5, 12, 5, 30, 5), new[] { Static("x", none) })),
            ("Nessuna combinazione", () => SlopeStability.Calculate(s1, searches["Q1"], Array.Empty<SlopeFactors>())),
            ("Gamma 9", () => SlopeStability.Calculate(s1 with { Soils = new[] { new SlopeSoil("L", -20, 9, 20, 30, 5, 0) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Gamma sat 31", () => SlopeStability.Calculate(s1 with { Soils = new[] { new SlopeSoil("L", -20, 19, 31, 30, 5, 0) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Phi 51", () => SlopeStability.Calculate(s1 with { Soils = new[] { new SlopeSoil("L", -20, 19, 20, 51, 5, 0) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Strati non decrescenti", () => SlopeStability.Calculate(s1 with { Soils = new[] { new SlopeSoil("A", -5, 19, 20, 30, 5, 0), new SlopeSoil("B", -5, 19, 20, 30, 5, 0) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Non drenata senza cu", () => SlopeStability.Calculate(s1, searches["Q1"], new[] { Static("x", none, true) })),
            ("Falda sopra il terreno", () => SlopeStability.Calculate(s1 with { Water = new SlopePoint[] { new(-30, 1), new(40, 7) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Falda corta", () => SlopeStability.Calculate(s1 with { Water = new SlopePoint[] { new(-20, -1), new(40, 2) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Profilo decrescente", () => SlopeStability.Calculate(s1 with { Surface = new SlopePoint[] { new(-30, 0), new(0, 0), new(-1, 6), new(40, 6) } }, searches["Q1"], new[] { Static("x", none) })),
            ("kh 0.6", () => SlopeStability.Calculate(s1, searches["Q1"], new[] { Quake("x", .6, 0, none) })),
            ("R 0", () => SlopeStability.Calculate(s1, searches["Q1"], new[] { new SlopeFactors("x", 1, 1, 1, 1, 1, 0, 0, 0, false, none) })),
            ("Profilo con un punto", () => SlopeStability.Calculate(s1 with { Surface = new SlopePoint[] { new(0, 0) } }, searches["Q1"], new[] { Static("x", none) })),
            ("Confine non finito", () => SlopeStability.Calculate(s4 with { SoilSplitX = double.NaN }, searches["Q4"], new[] { Static("x", none) })),
            ("Corpo non semplice", () => SlopeStability.Slices(s2 with { Bodies = new[] { new SlopeBody("Z", new SlopePoint[] { new(0, 0), new(3, 0), new(3, 3), new(0, 3), new(0, 2), new(2, 1.5), new(0, 1) }, 25) } },
                SlopeGeometry.Through(new(-8, 0), new(10, 5), -4)!, Static("x", none), 30))
        };
        foreach (var (name, run) in invalid)
        {
            string outcome; try { run(); outcome = "accepted"; } catch (Exception ex) { outcome = "error:" + ex.GetType().Name + ";" + Clean(ex.Message); }
            csv.AppendLine(string.Join(";", "ERROR", name, outcome));
        }
        File.WriteAllText(Path.Combine(output, "geotechnics-slope.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Pendii: {count} casi di ricerca, {fixedCircles.Count} cerchi assegnati, {invalid.Count} rifiuti");
    }

    static void Settlement(string output, string header)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# FoundationSettlement legacy outputs; " + header);
        csv.AppendLine("# Units m, kPa, mm. STRESS;x;z;left;right;pLeft;pRight;value. CALC;layers name:thickness:modulus|...;x;left;right;pLeft;pRight;removed;width;subdivisions;outcome;settlement;bottomStress;slices soil:top:bottom:stress:modulus:settlement|...");
        foreach (var (left, right, pl, pr) in new[] { (0.0, 2.0, 100.0, 100.0), (-1.0, 3.0, 50.0, 150.0), (0.0, 4.0, 200.0, 0.0), (0.0, 2.0, 0.0, 0.0) })
            foreach (double x in new[] { -2.0, 0, 1, 2.5, 6 })
                foreach (double z in new[] { .2, 1, 3, 10, 0, -1 })
                {
                    string value; try { value = F(FoundationSettlement.Stress(x, z, left, right, pl, pr)); } catch (Exception ex) { value = "error:" + ex.GetType().Name; }
                    csv.AppendLine(string.Join(";", "STRESS", F(x), F(z), F(left), F(right), F(pl), F(pr), value));
                }
        csv.AppendLine(string.Join(";", "STRESS", "1", "1", "2", "2", "100", "100", Try(() => FoundationSettlement.Stress(1, 1, 2, 2, 100, 100))));
        var sets = new[]
        {
            new[] { new FoundationSettlement.Layer("Sabbia", 3, 15000), new FoundationSettlement.Layer("Argilla", 6, 4000), new FoundationSettlement.Layer("Ghiaia", 10, 50000) },
            new[] { new FoundationSettlement.Layer("Limo", 8, 8000) },
            new[] { new FoundationSettlement.Layer("Torba", .5, 500), new FoundationSettlement.Layer("Argilla", 2.5, 3000) }
        };
        int n = 0;
        foreach (var layers in sets)
            foreach (var (x, left, right, pl, pr, removed, width) in new[] { (1.0, 0.0, 2.0, 150.0, 150.0, 0.0, 2.0), (0.0, 0.0, 2.0, 150.0, 150.0, 20.0, 2.0), (2.5, -.5, 3.5, 80.0, 220.0, 30.0, 3.0),
                (1.5, 0.0, 3.0, 250.0, 50.0, 0.0, 3.0), (1.0, 0.0, 2.0, 15.0, 15.0, 20.0, 2.0), (6.0, 0.0, 2.0, 150.0, 150.0, 0.0, 2.0) })
                foreach (int subdivisions in new[] { 10, 40 })
                {
                    string head = string.Join(";", "CALC", string.Join("|", layers.Select(l => l.Name + ":" + F(l.Thickness) + ":" + F(l.Modulus))), F(x), F(left), F(right), F(pl), F(pr), F(removed), F(width), subdivisions);
                    try
                    {
                        var r = FoundationSettlement.Calculate(layers, x, left, right, pl, pr, removed, width, subdivisions);
                        csv.AppendLine(string.Join(";", head, "ok", F(r.SettlementMm), F(r.BottomStress), string.Join("|", r.Slices.Select(s => string.Join(":", s.Soil, F(s.Top), F(s.Bottom), F(s.Stress), F(s.Modulus), F(s.SettlementMm))))));
                    }
                    catch (Exception ex) { csv.AppendLine(head + ";error:" + ex.GetType().Name + ";;;" + Clean(ex.Message)); }
                    n++;
                }
        foreach (var (name, layers, width, subdivisions) in new[] { ("Nessuno strato", Array.Empty<FoundationSettlement.Layer>(), 2.0, 40), ("Modulo nullo", new[] { new FoundationSettlement.Layer("A", 2, 0) }, 2.0, 40),
            ("Spessore nullo", new[] { new FoundationSettlement.Layer("A", 0, 1000) }, 2.0, 40), ("Larghezza nulla", sets[1], 0.0, 40), ("Suddivisioni 9", sets[1], 2.0, 9),
            ("Suddivisioni 1001", sets[1], 2.0, 1001), ("Discretizzazione eccessiva", new[] { new FoundationSettlement.Layer("A", 2000, 1000) }, .1, 1000) })
            csv.AppendLine(string.Join(";", "ERROR", name, Try(() => FoundationSettlement.Calculate(layers, 1, 0, 2, 100, 100, 0, width, subdivisions).SettlementMm)));
        File.WriteAllText(Path.Combine(output, "geotechnics-settlement.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Cedimenti: {n} calcoli");
    }

    static string Try(Func<double> run) { try { return F(run()); } catch (Exception ex) { return "error:" + ex.GetType().Name + ":" + Clean(ex.Message); } }

    static void Newmark(string output, string header)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# NewmarkSliding legacy outputs; " + header);
        csv.AppendLine("# Units s, g, m/s, mm. RECORD;id;t:a|...  RUN;record;yieldG;scale;outcome;displacement;peakVelocity;pga;points t:a:v:d|...");
        var records = new Dictionary<string, List<NewmarkSliding.Sample>>(StringComparer.Ordinal);
        records["sine"] = Enumerable.Range(0, 101).Select(i => new NewmarkSliding.Sample(i * .01, .3 * Math.Sin(2 * Math.PI * i * .01 / .5))).ToList();
        records["triangle"] = new() { new(0, 0), new(.2, .4), new(.4, 0), new(.5, 0) };
        records["plateau"] = new() { new(0, 0), new(.1, .3), new(.6, .3) };
        records["late"] = new() { new(.5, 0), new(.6, -.2), new(.8, .25), new(1.0, -.1), new(1.3, .15) };
        var random = new Random(20261003); double a = 0;
        records["random"] = Enumerable.Range(0, 1500).Select(i => { a = .85 * a + .15 * (random.NextDouble() * 2 - 1) * .9; return new NewmarkSliding.Sample(i * .01, a); }).ToList();
        foreach (var (id, record) in records) csv.AppendLine(string.Join(";", "RECORD", id, string.Join("|", record.Select(s => F(s.Time) + ":" + F(s.AccelerationG)))));
        int n = 0;
        foreach (var (id, record) in records)
            foreach (double yieldG in new[] { .02, .05, .1, .2, .5 })
                foreach (double scale in new[] { 1, 1.5 })
                {
                    string head = string.Join(";", "RUN", id, F(yieldG), F(scale));
                    try
                    {
                        var r = NewmarkSliding.Calculate(record, yieldG, scale);
                        csv.AppendLine(string.Join(";", head, "ok", F(r.DisplacementMm), F(r.PeakVelocity), F(r.PgaG), string.Join("|", r.Points.Select(p => string.Join(":", F(p.Time), F(p.AccelerationG), F(p.Velocity), F(p.DisplacementMm))))));
                    }
                    catch (Exception ex) { csv.AppendLine(head + ";error:" + ex.GetType().Name + ";" + Clean(ex.Message)); }
                    n++;
                }
        foreach (var (name, record, yieldG, scale) in new[] { ("Un campione", new List<NewmarkSliding.Sample> { new(0, .1) }, .1, 1.0), ("Tempi non crescenti", new() { new(0, 0), new(.1, .2), new(.1, .3) }, .1, 1.0),
            ("Tempo negativo", new() { new(-.1, 0), new(.1, .2) }, .1, 1.0), ("Soglia nulla", records["sine"], 0.0, 1.0), ("Scala nulla", records["sine"], .1, 0.0),
            ("Accelerazione non finita", new() { new(0, 0), new(.1, double.NaN) }, .1, 1.0) })
            csv.AppendLine(string.Join(";", "ERROR", name, Try(() => NewmarkSliding.Calculate(record, yieldG, scale).DisplacementMm)));
        File.WriteAllText(Path.Combine(output, "geotechnics-newmark.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Newmark: {n} integrazioni");
    }

    static void Seismic(string output, string header)
    {
        var csv = new StringBuilder();
        csv.AppendLine("# ShallowFoundationSeismic legacy outputs (EN 1998-5 Annex F); " + header);
        csv.AppendLine("# Units per metre: m, kN/m³, degrees, kN/m, kNm/m. width;gamma;phi;N;V;M;kh;kv;modelFactor;resistanceFactor;outcome;capacity;ratio;Nmax;F;Nbar;Vbar;Mbar;limit;interaction;status");
        int n = 0;
        foreach (double width in new[] { 1.5, 3 })
            foreach (double gamma in new[] { 18.0, 20 })
                foreach (double phi in new[] { 28.0, 34, 40 })
                    foreach (double nEd in new[] { 100.0, 400, 1200 })
                        foreach (var (v, m) in new[] { (0.0, 0.0), (50.0, 0.0), (0.0, 40.0), (50.0, 40.0), (150.0, 150.0), (-60.0, -30.0) })
                            foreach (double kh in new[] { 0, .1, .25 })
                                foreach (double kv in new[] { -.05, .05 })
                                    foreach (var (model, resistance) in new[] { (1.0, 1.0), (1.15, 1.0), (1.0, 1.8) })
                                    {
                                        string head = string.Join(";", F(width), F(gamma), F(phi), F(nEd), F(v), F(m), F(kh), F(kv), F(model), F(resistance));
                                        try
                                        {
                                            var r = ShallowFoundationSeismic.Calculate(width, gamma, phi, nEd, v, m, kh, kv, model, resistance);
                                            csv.AppendLine(string.Join(";", head, "ok", F(r.Capacity), F(r.Ratio), F(r.NMax), F(r.SoilInertia), F(r.NBar), F(r.VBar), F(r.MBar), F(r.VerticalLimit), F(r.Interaction), Clean(r.Status)));
                                        }
                                        catch (Exception ex) { csv.AppendLine(head + ";error:" + ex.GetType().Name + ";;;;;;;;;;" + Clean(ex.Message)); }
                                        n++;
                                    }
        foreach (var p in new[] { new[] { 2.0, 19, 46, 300, 20, 10, .1, 0, 1, 1 }, new[] { 2.0, 19, 0, 300, 20, 10, .1, 0, 1, 1 }, new[] { 2.0, 19, 34, 0, 20, 10, .1, 0, 1, 1 },
            new[] { 2.0, 19, 34, 300, 20, 10, -.1, 0, 1, 1 }, new[] { 2.0, 19, 34, 300, 20, 10, .1, 1, 1, 1 }, new[] { 2.0, 19, 34, 300, 20, 10, .1, 0, .9, 1 },
            new[] { 2.0, 19, 34, 300, 20, 10, .1, 0, 1, .9 }, new[] { 0, 19, 34, 300, 20, 10, .1, 0, 1, 1 }, new[] { 2.0, 19, 34, 300, 20, 10, 1.2, 0, 1, 1 },
            new[] { 2.0, 19, 34, 300, 20, 10, .6, 0, 1, 1 }, new[] { 2.0, 19, 34, 5000, 0, 0, .1, 0, 1, 1 }, new[] { 2.0, 19, 45, 300, 20, 10, .1, 0, 1, 1 } })
        {
            string head = string.Join(";", p.Select(x => F(x)));
            try
            {
                var r = ShallowFoundationSeismic.Calculate(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]);
                csv.AppendLine(string.Join(";", head, "ok", F(r.Capacity), F(r.Ratio), F(r.NMax), F(r.SoilInertia), F(r.NBar), F(r.VBar), F(r.MBar), F(r.VerticalLimit), F(r.Interaction), Clean(r.Status)));
            }
            catch (Exception ex) { csv.AppendLine(head + ";error:" + ex.GetType().Name + ";;;;;;;;;;" + Clean(ex.Message)); }
            n++;
        }
        File.WriteAllText(Path.Combine(output, "geotechnics-seismic-bearing.csv"), csv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Portanza sismica: {n} casi");
    }
}
