namespace Anthea.Presentation.Shell;

/// <summary>
/// The adaptation to small screens of MainWindow (DisplayAdaptation), as data of the shell (F5.13 c, night of 22-23/10; proposal 4.3):
/// <list type="bullet">
/// <item>the pages of the navigation (home, «Moduli singoli», «Modelli», «Esempi») keep the minimum size of the dashboard of MainWindow without its
/// navigation bar (DisplayAdaptation.Viewport(dashboard, 1120, 680), bar of 250) and scroll in a smaller window;</item>
/// <item>the sheet of a module whose view does not follow the size of the window keeps 1120 × 600 and scrolls (MainWindow.LoadSheetEditor); the views
/// that follow it (<see cref="FitsWindow"/>, the list written in MainWindow.LoadSheetEditor) take the whole tab.</item>
/// </list>
/// The window itself stays inside the work area of its monitor (DisplayAdaptation.Attach), in the view, and its content keeps the minimum of the
/// dashboard of MainWindow (<see cref="WindowMinimumWidth"/>). The list is one table here and not a member
/// of each module (question SH39): the descriptors of the modules are not touched while the modules migrate.
/// </summary>
public static class SheetLayout
{
    /// <summary>
    /// Minimum size of the window content of the shell (navigation, tree and centre): the dashboard of MainWindow (DisplayAdaptation.Viewport(dashboard,
    /// 1120, 680)); in a smaller window the shell scrolls (question SH40).
    /// </summary>
    public const double WindowMinimumWidth = 1120;

    /// <inheritdoc cref="WindowMinimumWidth"/>
    public const double WindowMinimumHeight = 680;

    /// <summary>Minimum size of the sheet of a module that does not follow the window (DisplayAdaptation.Viewport(editor.View, 1120, 600)).</summary>
    public const double SheetMinimumWidth = 1120;

    /// <inheritdoc cref="SheetMinimumWidth"/>
    public const double SheetMinimumHeight = 600;

    /// <summary>Minimum size of a page of the navigation: the dashboard of MainWindow (1120 × 680) without its navigation bar of 250.</summary>
    public const double PageMinimumWidth = 870;

    /// <inheritdoc cref="PageMinimumWidth"/>
    public const double PageMinimumHeight = 680;

    /// <summary>
    /// True when the view of <paramref name="module"/> follows the size of the window (MainWindow.LoadSheetEditor puts it in the sheet area as it is):
    /// the vertical pile and micropile, the horizontal pile and micropile, the retaining walls, the concrete and reinforcing steel materials, the
    /// composite bridge section, Bridge Design. The others keep <see cref="SheetMinimumWidth"/> × <see cref="SheetMinimumHeight"/> and scroll.
    /// </summary>
    public static bool FitsWindow(string module) => module is "geo_palo_verticale" or "geo_micropalo_verticale" or "geo_palo_orizzontale"
        or "geo_micropalo_orizzontale" or "geo_muri_sostegno" or "mat_calcestruzzo" or "mat_acciaio_armatura" or "str_mista_ponte" or "str_bridge_design";
}
