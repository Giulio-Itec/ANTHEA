using System.Globalization;
using System.Text.Json.Nodes;

namespace Anthea.Application.Presentation;

/// <summary>
/// Presentation of the pile group sheet (geo_efficienza_orizzontale), shared by the historic view HorizontalPileGroupWorkspace
/// (archived at the F5.4 switch) and by the ViewModel of ANTHEA.Presentation (refactoring F5.4): the readings of the rows of the comparison and the
/// derivations that the view computed itself, moved here unchanged. No calculation of the group: the values come from the
/// result of the pile group adapter; these are the rules and the arithmetic of what the sheet shows.
/// </summary>
public static class PileGroupPresentation
{
    /// <summary>Numbers of the sheet: up to three decimals in the current culture, «—» without a value.</summary>
    public static string Format(double? value) => value?.ToString("0.###", CultureInfo.CurrentCulture) ?? "—";

    /// <summary>Index of the method of a row of the comparison (or of the selected result), in the order of the method names.</summary>
    public static int Method(JsonNode row) => (int)row.D("Method");

    /// <summary>Direction of the load of a row [° counterclockwise from +X].</summary>
    public static double Angle(JsonNode row) => row.D("Angle");

    /// <summary>The method gives values for the group (the mean factor exists): otherwise it is not applicable.</summary>
    public static bool Available(JsonNode row) => row["Factor"] is not null;

    /// <summary>The row of the comparison selected in the sheet: same method and same direction as the data.</summary>
    public static bool IsSelected(JsonNode row, JsonObject data) => Method(row) == data.D("metodo") && row.S("DirectionId") == data.S("direzione_selezionata");

    /// <summary>
    /// Applicability of a row of the comparison: the error of the method when it stops; «Approssimazione esplicita» when a
    /// warning declares an extrapolation or the geometric extension of projected rows; otherwise «Disponibile».
    /// </summary>
    public static string Applicability(JsonNode row) => row.S("Error") != "" ? row.S("Error")
        : row.Array("Warnings").Any(w => w!.ToString().Contains("strapolazione") || w.ToString().Contains("estensione geometrica")) ? "Approssimazione esplicita" : "Disponibile";

    /// <summary>What the factors of an applicable method reduce: kh/nh for Davisson (method 0), the reaction p for the p-multipliers.</summary>
    public static string FactorDescription(int method) => method == 0 ? "coefficiente Rg di kh/nh" : "coefficiente Pm di reazione p";

    /// <summary>β of the piles is shown for the pile-to-pile methods: Reese/Van Impe and Caltrans (methods 4 and 5).</summary>
    public static bool ShowsBeta(int method) => method >= 4;

    /// <summary>α of the rows is shown for Caltrans (method 5).</summary>
    public static bool ShowsAlpha(int method) => method == 5;

    /// <summary>
    /// Distances of the cap shown under the plan [m]: axis of the outer pile to the edge, bordo_d × D, and free margin between
    /// the pile and the edge, (bordo_d − 0,5) × D. Null when bordo_d is not a number.
    /// </summary>
    public static (double? Edge, double? Margin) CapDistances(JsonObject data)
    {
        double? edge = J.Number(data["basamento"]?["bordo_d"]);
        double diameter = data.D("diametro");
        // 0,5 D: the radius of the pile, between its axis and its surface.
        return (edge * diameter, (edge - .5) * diameter);
    }

    /// <summary>Row of a pile in the direction of the load (1 = leading row).</summary>
    public static double Row(JsonNode pile) => pile.D("Row");

    /// <summary>Reduction of a pile [%]: (1 − factor) × 100; null without factor.</summary>
    public static double? Reduction(JsonNode pile) => (1 - J.Number(pile["Factor"])) * 100;
}
