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
