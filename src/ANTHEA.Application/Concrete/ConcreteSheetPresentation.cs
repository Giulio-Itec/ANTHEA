using System.Text.Json.Nodes;

namespace Anthea.Application.Concrete;

/// <summary>
/// Derivations of the shell of the RC section sheet (str_palo), moved without changes from the historic view (ConcreteWorkspace.cs and
/// ConcreteLayout.cs, sub-step S1 of F5.10): the main tab saved in the sheet and the ratios of the dividers saved in <c>layout</c>. The
/// historic view calls them; the ViewModel of the sheet calls them too.
/// </summary>
public static class ConcreteSheetPresentation
{
    /// <summary>The main tab shown when the sheet opens: the saved <c>tab</c>, limited to the eight tabs of the sheet.</summary>
    public static int InitialTab(JsonObject settings) => Math.Clamp((int)settings.D("tab"), 0, 7);

    /// <summary>
    /// The ratio of the first part of a group of dividers (<c>ingressi</c>, <c>risultati</c>, <c>righe</c>) saved in <c>layout</c>; the
    /// initial ratio of the group when it is missing, not finite or outside 0,15–0,85.
    /// </summary>
    public static double SplitRatio(JsonObject settings, string group, double initial)
    {
        double ratio = settings["layout"].D(group, initial);
        if (!double.IsFinite(ratio) || ratio < .15 || ratio > .85) ratio = initial;
        return ratio;
    }

    /// <summary>The ratio saved after a divider is moved: limited to 0,15–0,85.</summary>
    public static double MovedSplitRatio(double ratio) => Math.Clamp(ratio, .15, .85);
}
