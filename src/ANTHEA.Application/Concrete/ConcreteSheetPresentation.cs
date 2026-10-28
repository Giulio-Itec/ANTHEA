using System.Text.Json.Nodes;

namespace Anthea.Application.Concrete;

/// <summary>
/// Derivations of the shell of the RC section sheet (str_palo), moved without changes from the historic view (ConcreteWorkspace.cs and
/// ConcreteLayout.cs, sub-step S1 of F5.10): the main tab saved in the sheet and the ratios of the dividers saved in <c>layout</c>. The
/// historic view calls them; the ViewModel of the sheet calls them too.
/// </summary>
public static class ConcreteSheetPresentation
{
    /// <summary>
    /// The main tab shown when the sheet opens: the saved <c>tab</c>, limited to the nine tabs of the sheet (1.15.0) and read as a stable code
    /// (<see cref="SavedTab"/>), so that a sheet saved with eight tabs opens on the same tab.
    /// </summary>
    public static int InitialTab(JsonObject settings) => ShownTab(Math.Clamp((int)settings.D("tab"), 0, SavedModelColumnTab));

    /// <summary>The code saved for «Colonna modello» (1.15.0): after the codes of the eight tabs of 1.14, so that they keep their meaning.</summary>
    public const int SavedModelColumnTab = 8;

    // The position of «Colonna modello» among the tabs (after «Momento–curvatura») and the position of «Calcola armature», saved as 7 up to 1.14.
    private const int ModelColumnTab = 7, DesignTab = 8;

    /// <summary>
    /// The code saved in <c>tab</c> for a tab shown (1.15.0): the tabs 01–07 keep their position, «Calcola armature» (shown ninth) keeps the code 7
    /// of the sheets with eight tabs and «Colonna modello» (shown eighth) has the new code 8. Documents of 1.14 and of 1.15 open on the same tab
    /// without migration of the data; 1.14 opens the code 8 on «Calcola armature», its last tab.
    /// </summary>
    public static int SavedTab(int shown) => shown == ModelColumnTab ? SavedModelColumnTab : shown == DesignTab ? ModelColumnTab : shown;

    /// <summary>The tab shown for a saved code (the inverse of <see cref="SavedTab"/>).</summary>
    public static int ShownTab(int saved) => saved == SavedModelColumnTab ? ModelColumnTab : saved == ModelColumnTab ? DesignTab : saved;

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
