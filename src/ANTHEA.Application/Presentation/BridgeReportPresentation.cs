using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Bridge;

namespace Anthea.Application.Presentation;

/// <summary>
/// A figure of the report of the composite bridge section: caption, category, the situation drawn (null for the geometry and the
/// sketches), the mode of the drawing (2 geometry, 1 contributions), the local sketch (intermediate or support) and the size in pixels.
/// </summary>
public sealed record BridgeFigurePlan(string Caption, string Category, int? Stage, int Mode, bool Sketch, bool AtSupport, int Width, int Height);

/// <summary>The switches of the drawings of the report, read from the view state of the sheet, and the scale of the concrete.</summary>
public sealed record BridgeFigureStyle(bool NeutralAxis, bool ContourSection, bool ContourDiagram, bool StressLimits, double ConcreteAmplification);

/// <summary>
/// The figures of the report of the composite bridge section (F5.11): which figures, in which order, with which captions, categories and
/// sizes, and the switches of the drawings. Moved without changes from ModuleFigureRenderer.BridgeFigures, which now only draws them.
/// </summary>
public static class BridgeReportPresentation
{
    /// <summary>Size of the drawings of the section in the report.</summary>
    public const int SectionWidth = 1200, SectionHeight = 640;

    /// <summary>Size of the local sketches in the report.</summary>
    public const int SketchWidth = 800, SketchHeight = 165;

    /// <summary>The switches of the drawings, from the view state of the sheet (<c>ui_mista</c>).</summary>
    public static BridgeFigureStyle Style(BridgeResultView result, JsonObject settings) => new(settings.B("asse_neutro", true), settings.B("contour_sezione"),
        settings.B("contour_tensioni"), settings.B("limiti_tensioni"), result.StressScale(settings).Factor);

    /// <summary>The figures of the report with the sections <paramref name="options"/>, in their order.</summary>
    public static IReadOnlyList<BridgeFigurePlan> Figures(BridgeResultView result, JsonObject settings, IReadOnlySet<string> options)
    {
        var figures = new List<BridgeFigurePlan>();
        if (!options.Contains("grafici")) return figures;
        var concreteScale = result.StressScale(settings);
        if (options.Contains("taglio"))
            foreach (bool support in new[] { false, true })
                if (result.Input.B(support ? "appoggio" : "irrigidimenti"))
                    figures.Add(new(support ? "Appoggio e montante terminale schema locale" : "Irrigidimento intermedio e pannelli adiacenti schema locale", "dettagli", null, 0, true, support, SketchWidth, SketchHeight));
        if (options.Contains("geometria")) figures.Add(new("Geometria della sezione e disposizione delle armature", "geometria", null, 2, false, false, SectionWidth, SectionHeight));
        if (options.Overlaps(["tensioni", "classe4", "omogeneizzazione", "taglio"]))
            for (int i = 0; i < result.Stages.Count; i++)
                figures.Add(new($"Situazione {i + 1} dopo {result.Stages[i].Name}. {(settings.B("contour_tensioni") ? "Campiture della somma secondo η: verde/ambra entro limite, rosso oltre limite, viola CLS teso." : "Campiture della somma: blu compressione, rosso trazione;")} contributi tratteggiati. CLS amplificato ×{concreteScale.Factor.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"))} ({concreteScale.Source}); valori indicati in MPa reali. Arancio: parte inefficace.",
                    "fase_" + i, i, 1, false, false, SectionWidth, SectionHeight));
        return figures;
    }
}
