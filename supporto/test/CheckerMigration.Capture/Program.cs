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
TorsionCapture.Run(output, commit, sha);

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
