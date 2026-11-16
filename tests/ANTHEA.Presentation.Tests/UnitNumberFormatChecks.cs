using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Shell.Commands;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The decimals by unit approved by the user on 16/11/2026 (items a, b, c and e of the digits): the shared rule <see cref="UnitNumberFormat"/>, its
/// use in the views (<see cref="NumericDisplay.Unit"/>, culture of the application) and in the report of the embedded walls (it-IT, no «E»).
/// </summary>
internal static class UnitNumberFormatChecks
{
    internal static int Run(string output)
    {
        var e = new Expect();
        var comma = NumberSeparators.Culture(DecimalSeparatorChoice.Comma, CultureInfo.GetCultureInfo("it-IT"));
        var point = NumberSeparators.Culture(DecimalSeparatorChoice.Point, CultureInfo.GetCultureInfo("it-IT"));
        var italian = CultureInfo.GetCultureInfo("it-IT");
        // The approved table, unit by unit (16/11/2026), plus the three units of the module of the embedded walls (kN/m², kNm²/m, %).
        var table = new (string Unit, int Decimals)[] { ("m", 3), ("mm", 2), ("rad", 5), ("kN", 2), ("kN/m", 2), ("kNm", 2), ("kNm/m", 2), ("kPa", 2),
            ("kN/m³", 2), ("°", 2), ("N", 2), ("Nmm", 2), ("kNm²", 2), ("MPa", 3), ("", 3), ("-", 3), ("mm²", 3), ("L/s/m", 4), ("mm²/s", 4),
            ("kN/m²", 2), ("kNm²/m", 2), ("%", 1) };
        foreach (var (unit, decimals) in table) e.That(UnitNumberFormat.Decimals(unit) == decimals, "Decimali di «" + unit + "»: " + UnitNumberFormat.Decimals(unit));
        e.That(UnitNumberFormat.Format(-0.0172, "m", comma) == "-0,017" && UnitNumberFormat.Format(-0.0172, "m", point) == "-0.017", "Metri: tre decimali e separatore");
        e.That(UnitNumberFormat.Format(111.0904, "mm", comma) == "111,09" && UnitNumberFormat.Format(110.9, "mm", comma) == "110,9"
            && UnitNumberFormat.Format(110.9, "mm", comma, table: true) == "110,90", "Tabella con gli zeri finali, testo senza");
        e.That(UnitNumberFormat.Format(4.25419E-10, "kNm", comma) == "0" && UnitNumberFormat.Format(-3.13861E-05, "mm", comma, table: true) == "0,00"
            && UnitNumberFormat.Format(-0.001, "kN", comma) == "0", "Rumore del solutore: 0, mai «-0»");
        e.That(UnitNumberFormat.Format(0.00123456, "rad", comma) == "0,00123" && UnitNumberFormat.Format(0.015312, "L/s/m", comma) == "0,0153"
            && UnitNumberFormat.Format(1.2504, "MPa", comma) == "1,25" && UnitNumberFormat.Format(0.61549, "", comma) == "0,615", "Decimali di rad, L/s/m, MPa e numeri puri");
        e.That(UnitNumberFormat.Format(1234567.891, "kNm", comma) == "1234567,89", "Nessun separatore delle migliaia, nessun esponente nei numeri grandi");
        e.That(UnitNumberFormat.Format(1.5e-7, "m/s", comma) == "1,5·10⁻⁷" && UnitNumberFormat.Format(1.5e-7, "m/s", point) == "1.5·10⁻⁷"
            && UnitNumberFormat.Format(-2.04e-5, "m/s", comma) == "-2,0·10⁻⁵" && UnitNumberFormat.Format(0, "m/s", comma) == "0"
            && UnitNumberFormat.Format(15, "m/s", comma) == "1,5·10¹" && UnitNumberFormat.Format(1.5, "m/s", comma) == "1,5", "Potenza di dieci per m/s");
        e.That(UnitNumberFormat.Format(9.81e-6, "N/mm³", italian) == "9,8·10⁻⁶", "N/mm³ (γw) come potenza di dieci: con tre decimali sarebbe 0");
        e.That(UnitNumberFormat.Format(null, "kN", comma) == "—" && UnitNumberFormat.Format(double.NaN, "kN", comma) == "—"
            && UnitNumberFormat.Format(double.PositiveInfinity, "m/s", comma) == "—", "Valori mancanti");
        e.That(UnitNumberFormat.UnitOf("γw [N/mm³]") == "N/mm³" && UnitNumberFormat.UnitOf("Momento [kNm]") == "kNm" && UnitNumberFormat.UnitOf("γR") == "", "Unità dal titolo");
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = point;
            e.That(NumericDisplay.Unit(-153.584, "kNm") == "-153.58" && NumericDisplay.Unit(null, "kNm") == "—", "Viste: cultura dell'applicazione «Punto»");
            CultureInfo.CurrentCulture = comma;
            e.That(NumericDisplay.Unit(-153.584, "kNm", table: true) == "-153,58" && NumericDisplay.Unit(2, "m", table: true) == "2,000", "Viste: «Virgola» e tabelle");
        }
        finally { CultureInfo.CurrentCulture = saved; }
        Report(e, output);
        return e.Count;
    }

    /// <summary>
    /// The report of the embedded walls with the rule by unit (item e): the analytic cantilever of <see cref="EmbeddedWallChecks.Example"/> with the noise
    /// of a solver on two values (4,25419E-10 kNm and -3,13861E-05 kN), no «E» in any number, fixed decimals in the tables, the fingerprint kept.
    /// Writes the text of the report and the comparison with the former formats of the same numbers (G7 and «0.##E+0», copied here) for the user.
    /// </summary>
    private static void Report(Expect e, string output)
    {
        var result = ModuleServices.Get(EmbeddedWallDocument.Module).Calculate(EmbeddedWallChecks.Example()).NativeAs<EmbeddedWallReportData>();
        var stage = result.Stages[0];
        var points = stage.Points.ToList();
        points[0] = points[0] with { Moment = 4.25419E-10, Shear = -3.13861E-05 };
        var noisy = result with { Stages = [stage with { Points = points.AsReadOnly() }] };
        var xml = EmbeddedWallReport.Create("Paratia — cifre per unità", noisy, allStations: true).GetXml("word/document.xml");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        static string Text(XElement element, XNamespace w) => string.Concat(element.Descendants(w + "t").Select(t => t.Value));
        var paragraphs = xml.Descendants(w + "p").Select(p => Text(p, w)).Where(t => t.Length > 0).ToArray();
        string text = string.Join("\n", paragraphs);
        // Computed numbers: every cell of the tables but those of «Dati assegnati» (the values as the user typed them) and the line of the residuals.
        var cells = xml.Descendants(w + "tbl").Where(t => Text(t.Descendants(w + "tc").First(), w) != "Dato")
            .SelectMany(t => t.Descendants(w + "tc").Select(c => Text(c, w))).ToArray();
        var computed = cells.Concat(paragraphs.Where(p => p.StartsWith("Incrementi", StringComparison.Ordinal))).ToArray();
        var exponent = new Regex(@"\d[eE][+-]?\d");
        e.That(computed.Length > 20 && !computed.Any(exponent.IsMatch), "Esponente «E» nel report: " + string.Join(" | ", computed.Where(c => exponent.IsMatch(c)).Take(3)));
        e.That(text.Contains(result.Fingerprint, StringComparison.Ordinal), "Impronta perduta nel report.");
        // Table of the stage: z [m] with three decimals, N, V [kN] and M [kNm] with two, u [mm] with two, final zeros kept.
        e.That(cells.Contains("3,60") && cells.Contains("0,00") && cells.Contains("100,00") && cells.Any(p => p.StartsWith("0,000 ", StringComparison.Ordinal)),
            "Tabella delle fasi senza i decimali fissi per unità.");
        e.That(!computed.Any(p => Regex.IsMatch(p, @"(^|[\s(=/;])-0([,.]0+)?($|[\s);/])")), "«-0» nel report.");
        string folder = Path.Combine(output, "paratie-cifre"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "report-dopo.txt"), text, new UTF8Encoding(false));
        // The former formats of the report (EmbeddedWallReport.F and Compact before 16/11/2026), only to show the user the difference.
        static string FormerF(double v) => v.ToString("G7", CultureInfo.GetCultureInfo("it-IT"));
        static string FormerCompact(double v) => v != 0 && (Math.Abs(v) < .0001 || Math.Abs(v) >= 100000) ? v.ToString("0.##E+0", CultureInfo.GetCultureInfo("it-IT")) : FormerF(v);
        var rows = new[] { ("M [kNm], rumore", 4.25419E-10, "kNm"), ("V [kN], rumore", -3.13861E-05, "kN"), ("u [mm]", stage.Points[0].Displacement, "mm"),
            ("z [m]", stage.Points[^1].Elevation, "m"), ("residuo forze [N]", stage.ForceResidualN, "N"), ("residuo momenti [Nmm]", stage.MomentResidualNmm, "Nmm") };
        File.WriteAllLines(Path.Combine(folder, "prima-dopo.txt"), rows.Select(r =>
            $"{r.Item1}: prima {FormerCompact(r.Item2)} (tabella) / {FormerF(r.Item2)} (testo); dopo {UnitNumberFormat.Format(r.Item2, r.Item3, CultureInfo.GetCultureInfo("it-IT"), table: true)} (tabella) / {UnitNumberFormat.Format(r.Item2, r.Item3, CultureInfo.GetCultureInfo("it-IT"))} (testo)"),
            new UTF8Encoding(false));
    }
}
