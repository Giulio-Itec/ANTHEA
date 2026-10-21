using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Bridge;

namespace Anthea.Application.Presentation;

/// <summary>A fibre of the curves of the section: its identifier and the label of the choice.</summary>
public sealed record BridgeFiberChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A point of a chart of the curves (x, y in the units of the axes).</summary>
public readonly record struct BridgeChartPoint(double X, double Y);

/// <summary>A chart of the curves: labels of the axes and points.</summary>
public sealed record BridgeResponseChart(string XLabel, string YLabel, IReadOnlyList<BridgeChartPoint> Points);

/// <summary>
/// The page of the curves of the composite bridge section (F5.11): fields shown by the path, the phases of the starting state, the texts,
/// the table of the accepted points, the charts of the section and of a fibre. Moved without changes from the historic view
/// (BridgeWorkspace.BuildResponsePage, CalculateResponseAsync, RefreshResponsePlots), which calls them. Units: ε in µε, κ in 1/m, forces in
/// kN, moments in kNm, stresses in MPa.
/// </summary>
public static class BridgeResponsePresentation
{
    private const double Kilo = 1000, Mega = 1e6;
    private static string F(double x) => BridgeResultPresentation.F(x);
    private static string E(double? x) => BridgeResultPresentation.E(x);

    public const string NoCurve = "Nessuna curva calcolata";
    public const string Stale = "Dati modificati · curva precedente da ricalcolare";
    public const string Running = "Calcolo del percorso e dello storico di partenza…";
    public const string Cancelled = "Calcolo annullato · eventuale curva precedente da ricalcolare";
    public const string NoPoints = "Nessuno stato accettato per questa curva.";

    /// <summary>The proposed name of the CSV of the points and of the fibres.</summary>
    public const string CsvFileName = "Curve_sezione_ponte.csv";

    /// <summary>The text after a calculation of the curve.</summary>
    public static string Calculated(SectionResponseResult result) => $"{result.Points.Count} stati accettati · " + result.Message;

    /// <summary>The text after a calculation that failed.</summary>
    public static string Failed(string message) => "Curva non calcolata: " + message;

    /// <summary>The path of <paramref name="request"/> controls the moment and the curvature (M–κ), otherwise the force and the strain.</summary>
    public static bool MomentCurvature(JsonObject request) => request.S("tipo") == BridgeEditor.ResponseModes[0];

    /// <summary>The fields of the path shown by its kind.</summary>
    public static bool FieldShown(string key, JsonObject request)
    {
        bool mc = MomentCurvature(request);
        return key switch
        {
            "n_storico" => mc,
            "N" => mc && !request.B("n_storico"),
            "k_storico" => !mc,
            "k" => !mc && !request.B("k_storico"),
            "incremento_k" => mc,
            "incremento_e" => !mc,
            _ => true
        };
    }

    /// <summary>The choice of the phase is shown when the path starts after a phase of the history.</summary>
    public static bool PhaseShown(JsonObject request) => request.S("origine") == BridgeEditor.ResponseOrigins[1];

    /// <summary>The phases of the starting state: the active phases («01 · Dopo G1 …»).</summary>
    public static IReadOnlyList<string> Phases(JsonObject data) =>
        data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).Select((p, i) => $"{i + 1:00} · Dopo {p.S("nome")}").ToArray();

    /// <summary>The fibres that can be followed (the active fibres of the first point) and the one chosen at first (the highest).</summary>
    public static (IReadOnlyList<BridgeFiberChoice> Choices, int Selected) Fibers(SectionResponseResult result)
    {
        var fibers = result.Points.FirstOrDefault()?.State.Fibers.Where(f => f.Active).ToArray() ?? [];
        var choices = fibers.Select(f => new BridgeFiberChoice(f.Fiber.Id, $"{BridgeEditor.ComponentName(f.ComponentId)} · x = {f.Fiber.X.ToString("F1", CultureInfo.CurrentCulture)} · y = {f.Fiber.Y.ToString("F2", CultureInfo.CurrentCulture)} mm")).ToArray();
        return (choices, fibers.Length == 0 ? -1 : Array.FindIndex(fibers, f => f.Fiber.Y == fibers.Max(a => a.Fiber.Y)));
    }

    /// <summary>«Punti accettati del percorso».</summary>
    public static BridgePanel Table(SectionResponseResult result) => BridgePanel.Of(BridgeBlock.Table(["Punto", "ε rif", "κ", "N", "M rif", "εmecc min", "εmecc max", "σ min", "σ max", "Residuo N [N]"],
        result.Points.Select(p => new[] { p.Index.ToString(), F(p.ReferenceStrain * Mega), E(p.State.TotalPlane.Curvature * Kilo), F(p.State.N / Kilo), F(p.MomentAtReference / Mega),
            F(p.State.Fibers.Where(f => f.Active).Min(f => f.MechanicalStrain) * Mega), F(p.State.Fibers.Where(f => f.Active).Max(f => f.MechanicalStrain) * Mega),
            F(p.State.Fibers.Where(f => f.Active).Min(f => f.Stress)), F(p.State.Fibers.Where(f => f.Active).Max(f => f.Stress)), E(p.State.ForceResidual) })));

    /// <summary>The chart of the section: M–κ or N–ε at the reference level.</summary>
    public static BridgeResponseChart Section(SectionResponseResult result)
    {
        bool mc = result.Options.Control == SectionResponseControl.MomentCurvature;
        return new(mc ? "κ [1/m]" : "ε alla quota di riferimento [µε]", mc ? "M rif [kNm]" : "N [kN]",
            result.Points.Select(p => mc ? new BridgeChartPoint(p.State.TotalPlane.Curvature * Kilo, p.MomentAtReference / Mega) : new BridgeChartPoint(p.ReferenceStrain * Mega, p.State.N / Kilo)).ToArray());
    }

    /// <summary>The labels of the chart of a fibre.</summary>
    public const string FiberXLabel = "ε meccanica [µε]", FiberYLabel = "σ [MPa]";

    /// <summary>The chart of the fibre <paramref name="id"/>: mechanical strain and stress at every point; null when the point has no such fibre.</summary>
    public static BridgeResponseChart? Fiber(SectionResponseResult result, int point, string id)
    {
        var p = result.Points[point];
        if (!p.State.Fibers.Any(f => f.Fiber.Id == id)) return null;
        return new(FiberXLabel, FiberYLabel, result.Points.Select(s => s.State.Fibers.Single(f => f.Fiber.Id == id)).Select(f => new BridgeChartPoint(f.MechanicalStrain * Mega, f.Stress)).ToArray());
    }

    /// <summary>The text of the chosen point and, with a fibre of the point, of the fibre.</summary>
    public static string PointInfo(SectionResponseResult result, int point, string? fiber)
    {
        var p = result.Points[point];
        string text = $"Punto {point} · ε rif {F(p.ReferenceStrain * Mega)} µε · κ {E(p.State.TotalPlane.Curvature * Kilo)} 1/m · N {F(p.State.N / Kilo)} kN · M rif {F(p.MomentAtReference / Mega)} kNm";
        if (fiber is not null && p.State.Fibers.Any(f => f.Fiber.Id == fiber))
        {
            var f = p.State.Fibers.Single(f => f.Fiber.Id == fiber);
            text += $"\nFibra: σ {F(f.Stress)} MPa · εtot {F(f.TotalStrain * Mega)} · εgetto {F(f.ActivationStrain * Mega)} · εimposta {F(f.ImposedStrain * Mega)} · εmecc {F(f.MechanicalStrain * Mega)} µε";
            if (f.MaterialState is HistoryPlasticState plastic) text += $" · εpl {F(plastic.PlasticStrain * Mega)} µε";
        }
        return text;
    }
}
