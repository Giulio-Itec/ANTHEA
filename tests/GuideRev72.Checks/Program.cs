using System.Text.Json;
using GPC.Checkers.Geotechnics.Foundations;

// Worked examples of the Rev72 guide. No frozen reference is read or rewritten.
// Independent guide expressions in kN, m, kPa; library input in N, mm, MPa.
var rows = new List<object>();
foreach (var condition in new[] { FoundationSoilCondition.Drained, FoundationSoilCondition.Undrained })
foreach (var h in new[] { 0d, 60000d, 1d })
{
    var input = new ShallowFoundationInput { Method = FoundationBearingMethod.Hansen,
        Profile = FoundationDesignProfile.Characteristic, Condition = condition,
        Width = 2000, Length = 3000, Depth = 1000, Normal = 600000,
        HorizontalX = h, HorizontalY = 0, MomentX = 0, MomentY = 0,
        FrictionAngle = Math.PI / 6, Cohesion = 0, UndrainedStrength = .075,
        UnitWeight = 18e-6, CoverUnitWeight = 18e-6, WaterEnabled = false,
        Surcharge = 0, IncludeDepthFactors = true, HomogeneousHalfSpace = true };
    var result = ShallowFoundationBearing.Calculate(input);
    double q = result.UltimatePressure * 1000;
    double? independent = null;
    if (condition == FoundationSoilCondition.Undrained)
    {
        double i = .5 * (1 - Math.Sqrt(1 - h / 1000 / (2 * 3 * 75)));
        independent = (Math.PI + 2) * 75 * (1 + (h == 0 ? .2 * 2 / 3 : .2 * i * 2 / 3) + .4 / 2 - i) + 18;
    }
    else
    {
        double phi=Math.PI/6, nq=Math.Exp(Math.PI*Math.Tan(phi))*Math.Pow(Math.Tan(Math.PI/4+phi/2),2);
        double ng=1.5*(nq-1)*Math.Tan(phi), x=h/600000;
        double iq=Math.Pow(1-.5*x,5), ig=Math.Pow(1-.7*x,5);
        double sq=1+iq*(2d/3)*Math.Sin(phi), sg=Math.Max(.6,1-.4*(2d/3)*ig);
        double dq=1+2*Math.Tan(phi)*Math.Pow(1-Math.Sin(phi),2)*.5;
        independent=18*nq*sq*dq*iq+.5*18*2*ng*sg*ig;
    }
    if (Math.Abs(q - independent.Value) > 1e-8) throw new Exception("Independent Hansen expression differs.");
    if (h > 0 && !result.Reference.EndsWith("direzione di B′ (HL = 0)"))
        throw new Exception("Unexpected governing direction for HL=0.");
    rows.Add(new { condition = condition.ToString(), horizontalB_N = h, horizontalL_N = 0,
        pressure_kPa = q, independent_kPa = independent, result.Reference, result.Warnings,
        additive = new { result.Cohesion.AdditiveShape, result.Cohesion.AdditiveDepth, result.Cohesion.AdditiveInclination } });
}
Directory.CreateDirectory(args[0]);
File.WriteAllText(Path.Combine(args[0], "hansen-examples.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(args[0], "esito.txt"), "PASS 6 esempi Hansen; 6 confronti indipendenti in kN/m/kPa; 4 controlli direzione HL=0.\n");
Console.WriteLine(File.ReadAllText(Path.Combine(args[0], "esito.txt")));
