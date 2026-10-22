using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Anthea.Application.Concrete;

/// <summary>
/// The moment–curvature tab of the RC section (ConcreteCurvature.cs of the historic view, moved in F5.10 S7): the messages, the signature of
/// the data of a curve, the series of the chart, the text of the result and its CSV.
/// </summary>
public static class ConcreteCurvaturePresentation
{
    /// <summary>The message while the curve is calculated.</summary>
    public const string Running = "Calcolo del dominio e dei punti M–χ…";
    /// <summary>The message after a change of the data.</summary>
    public const string Stale = "Dati modificati · ricalcolare la curva.";
    /// <summary>The message of a calculation stopped.</summary>
    public const string Interrupted = "Calcolo interrotto; nessuna curva parziale pubblicata.";
    /// <summary>The notice under the form.</summary>
    public const string Notice = "N fisso; (Mx, My) = M · (cos θ, sin θ), negli assi locali. Analisi non lineare con i materiali del pannello di controllo. Campionamento a momento crescente fino al limite plastico; χ è il modulo del gradiente di deformazione. Il primo snervamento può essere raffinato per bisezione tra due campioni successivi.";
    /// <summary>The chart: title, axes, number format of the axes, message without a curve, name of the series.</summary>
    public const string Title = "Momento–curvatura", XLabel = "Curvatura χ [1/m]", YLabel = "Momento M [kNm]", AxisNumberFormat = "G4",
        EmptyMessage = "Impostare N e direzione per calcolare la curva", SeriesName = "M–χ";
    /// <summary>The file proposed by «Esporta CSV…».</summary>
    public const string CsvFileName = "ANTHEA_Momento_Curvatura.csv";

    /// <summary>The message of a curve that could not be calculated.</summary>
    public static string Unavailable(string error) => "Curva non disponibile: " + error;

    /// <summary>The data a curve depends on: the inputs, the tendons, the coefficients and the options of the curve (a curve with the same signature is not calculated again).</summary>
    public static string Signature(JsonObject input, JsonObject workspace)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace);
        return input.ToJsonString() + workspace["trefoli"]?.ToJsonString() + workspace["coefficienti"]?.ToJsonString() + workspace["momento_curvatura"]!.ToJsonString();
    }

    /// <summary>The points of the curve (χ, M) of the series of the chart.</summary>
    public static List<double[]> Points(ConcreteResults.CurvatureResult source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Points.Select(p => new[] { p.Curvature, p.Moment }).ToList();
    }

    /// <summary>The text of the result: status, MRd, χy, χu, ductility and the table of the points (numbers in the current culture).</summary>
    public static string Text(ConcreteResults.CurvatureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder(result.Status); b.AppendLine($"\n\nMRd = {result.LimitMoment:0.###} kNm\nχy = {result.YieldCurvature?.ToString("G6") ?? "non raggiunta"} 1/m\nχu = {result.UltimateCurvature?.ToString("G6") ?? "non raggiunta"} 1/m");
        if (result.YieldCurvature is > 0 && result.UltimateCurvature is double u) b.AppendLine($"μχ = {u / result.YieldCurvature:0.###}");
        b.AppendLine("\nM [kNm]   χ [1/m]   ε0 [‰]   εc,comp [‰]   |εs|max [‰]");
        foreach (var p in result.Points) b.AppendLine($"{p.Moment:G7}   {p.Curvature:G7}   {p.Epsilon0:G6}   {p.ConcreteCompressionStrain:G6}   {p.SteelStrain:G6}{(p.Limit ? " · limite" : "")}");
        return b.ToString();
    }

    /// <summary>The CSV of «Esporta CSV…»: every point with invariant numbers (17 significant digits), separated by semicolons.</summary>
    public static string Csv(ConcreteResults.CurvatureResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        static string F(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
        var b = new StringBuilder("M_kNm;Mx_kNm;My_kNm;chi_1_m;gx_1_m;gy_1_m;epsilon0_permille;epsilon_c_permille;epsilon_s_permille;limite\n");
        foreach (var p in result.Points) b.AppendLine(string.Join(";", new[] { p.Moment, p.Mx, p.My, p.Curvature, p.GradientX, p.GradientY, p.Epsilon0, p.ConcreteCompressionStrain, p.SteelStrain }.Select(F)) + ";" + p.Limit);
        return b.ToString();
    }
}
