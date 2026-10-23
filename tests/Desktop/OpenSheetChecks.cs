using Anthea.Presentation.Modules;

namespace X.Desktop;

/// <summary>
/// f5-suite-ui: the open sheet as the UI suites drive and read it, with either implementation of its module: the historic
/// <see cref="SheetEditor"/> (the Release until the switch of the module) or <see cref="MvvmSheetEditor"/> (the bench
/// <c>--module-impl</c> of the suites <c>ui/&lt;suite&gt;-mvvm</c>, and the Release after the switch). The suites call these members
/// where they called the SheetEditor partials through <c>historicEditor</c>; each member keeps the checks of the historic one on the
/// XAML view (VerticalPileSmoke, …). At the Release switch of a module its historic branch here, and the SheetEditor partial it
/// calls, go away (docs/refactoring/f5-suite-ui.md).
/// </summary>
internal sealed class OpenSheet(ISheetEditor editor, BenchSheetServices services)
{
    /// <summary>The vertical pile or micropile with the XAML view, null otherwise.</summary>
    private VerticalPileSmoke? Pile => VerticalPileSmoke.Handles(editor) ? new((MvvmSheetEditor)editor, services) : null;

    /// <summary>The vertical pile or micropile, with the XAML view since the Release switch of 23/10 (f5-pass-geo).</summary>
    private VerticalPileSmoke DisplayPile => Pile ?? throw new InvalidOperationException("Il foglio aperto non è un palo verticale con la vista MVVM: " + editor.Module);

    /// <summary>
    /// The historic sheet; the message of <c>historicEditor</c> for a module without an MVVM branch here. A historic sheet of a module that the
    /// bench of the command line opens with the XAML view fails too: a suite <c>-mvvm</c> never passes on the historic view by mistake.
    /// </summary>
    private SheetEditor Historic => editor switch
    {
        SheetEditor when ModuleHostBench.Implementations(Environment.GetCommandLineArgs()).For(editor.Module) == SheetImplementation.Mvvm =>
            throw new InvalidOperationException("Il banco apre " + editor.Module + " con la vista MVVM, ma il foglio aperto usa SheetEditor."),
        SheetEditor historic => historic,
        _ => throw new InvalidOperationException("Il foglio aperto non usa SheetEditor: " + editor.Module)
    };

    /// <summary>The automatic recalculation finished (within 10 s, as SheetEditor.WaitForAutomatic).</summary>
    internal Task WaitForAutomatic() => editor is MvvmSheetEditor mvvm ? MvvmSheets.WaitForAutomatic(mvvm) : Historic.WaitForAutomatic();

    /// <summary>SetGeometryForSmoke: the diameter of a vertical pile, the geometry of a c.a. section, as the user types it.</summary>
    internal void SetGeometryForSmoke(string value) { if (Pile is { } pile) pile.SetGeometry(value); else Historic.SetGeometryForSmoke(value); }

    // ---------------------------------------------------------------- ui/smoke (SmokeChecks.cs)

    internal Task VerifyWpf(string directory, string name) => Pile is { } pile ? pile.VerifyWpf(directory, name) : Historic.VerifyWpf(directory, name);

    // ---------------------------------------------------------------- ui/smoke-display (DisplaySmokeChecks.cs)

    internal void VerifyDisplayLayout() => DisplayPile.VerifyDisplayLayout();

    internal Task VerifyDisplayScrolling(string directory, string name) => DisplayPile.VerifyDisplayScrolling(directory, name);

    internal Task VerifyStratigraphyEditing() => DisplayPile.VerifyStratigraphyEditing();

    internal void VerifyPileOutcomes() => DisplayPile.VerifyPileOutcomes();

    internal Task VerifyPresentation(string directory) => DisplayPile.VerifyPresentation(directory);

    internal void VerifyReport(string directory) => DisplayPile.VerifyReport(directory);

    internal Task VerifyNavigationThrough(Action leave, Action resume) => DisplayPile.VerifyNavigationThrough(leave, resume);

    // ---------------------------------------------------------------- f5-suite-passaggi: ui/smoke-horizontal (HorizontalSmokeChecks.cs)

    /// <summary>The horizontal pile or micropile, MVVM in the Release since 23/10/2026 (f5-pass-orizzontale: the historic view is archived).</summary>
    private HorizontalPileSmoke HorizontalPile => HorizontalPileSmoke.Handles(editor) ? new((MvvmSheetEditor)editor, services)
        : throw new InvalidOperationException("Il foglio aperto non è un palo o un micropalo orizzontale con la vista XAML.");

    internal Task WaitForHorizontalAutomatic() => HorizontalPile.WaitForAutomatic();

    internal Task VerifyHorizontal(string directory) => HorizontalPile.Verify(directory);

    internal Task VerifyHorizontalLayout(string directory, string size) => HorizontalPile.VerifyLayout(directory, size);

    internal Task VerifyChs() => HorizontalPile.VerifyChs();

    // ---------------------------------------------------------------- materials (ui/smoke-materials, ui/smoke-hierarchy, ui/smoke-sharing, ui/smoke-steel)

    /// <summary>The concrete with the XAML view (the only one since the Release switch of the materials, f5-pass-geo).</summary>
    private ConcreteMaterialSmoke Concrete => ConcreteMaterialSmoke.Handles(editor) ? new((MvvmSheetEditor)editor) : throw new Exception("Scheda materiali non aperta");

    /// <summary>The state of the concrete set through the controls of the view (ConcreteMaterialSmoke.Apply; RestoreState of the archived historic view).</summary>
    internal void SetConcreteState(System.Text.Json.Nodes.JsonObject state) => Concrete.Apply(state);

    /// <summary>The self-check of the concrete sheet (ui/smoke-materials): MaterialView.Check of the archived historic view, on the XAML view.</summary>
    internal void CheckConcrete() => Concrete.SelfCheck();

    /// <summary>The user types in a field of the concrete (before the commit: the preview of the sheet follows).</summary>
    internal void TypeConcreteNumber(string key, string text) => Concrete.Type(key, text);

    // ---------------------------------------------------------------- retaining walls (ui/smoke-retaining-wall, ui/smoke-global-stability)

    /// <summary>The retaining wall with the XAML view, MVVM in the Release since 23/10/2026 (f5-pass-muri: the historic view is archived).</summary>
    internal WallSmoke Wall => MvvmWallSmoke.Handles(editor) ? new MvvmWallSmoke((MvvmSheetEditor)editor, services)
        : throw new Exception("Muro di sostegno non aperto con la vista XAML");

    /// <summary>F5.11: the composite bridge section, with either view (BridgeSmoke).</summary>
    internal BridgeSmoke Bridge => BridgeSmoke.Of(editor);

    /// <summary>The reinforcing steel with the XAML view (SteelSmoke).</summary>
    internal SteelSmoke Steel => editor.Module != RebarMaterial.Module ? throw new Exception("Modulo acciaio non aperto") : new(editor);
}

/// <summary>f5-suite-ui: what the UI suites need from any sheet with the XAML view.</summary>
internal static class MvvmSheets
{
    /// <summary>
    /// WaitForAutomatic of SheetEditor on a ViewModel: the pending automatic recalculation (scheduled at the opening and after each edit)
    /// and any calculation under way finished, within 10 s; otherwise the message of the historic check.
    /// </summary>
    internal static async Task WaitForAutomatic(HostedSheet editor)
    {
        var sheet = (SheetViewModelBase)editor.Sheet;
        for (int i = 0; i < 200 && (!sheet.PendingCalculation.IsCompleted || editor.Busy); i++) await Task.Delay(50);
        if (!sheet.PendingCalculation.IsCompleted || editor.Busy) throw new Exception("Calcolo automatico non terminato");
    }
}

/// <summary>
/// A sheet with the XAML view as the UI suites drive it: its ViewModel and the element that holds its view. In MainWindow the host is
/// <see cref="MvvmSheetEditor"/> (converted implicitly); in the new shell the tab of the sheet (F5.13 e, the suites ui/shell-smoke-*). Calculation and
/// report as MvvmSheetEditor does them (the drafts committed before «Calcola»; the report through SheetFileExports, the code of MvvmSheetEditor).
/// </summary>
internal sealed class HostedSheet(ISheetViewModel sheet, Func<System.Windows.FrameworkElement> view)
{
    /// <param name="sheet">The ViewModel of the sheet.</param>
    /// <param name="view">The element that holds its view, always the same (MvvmSheetEditor).</param>
    internal HostedSheet(ISheetViewModel sheet, System.Windows.FrameworkElement view) : this(sheet, () => view) { }

    internal ISheetViewModel Sheet { get; } = sheet;

    /// <summary>
    /// The element that holds the view of the sheet now (the view is found among its descendants): in the new shell the view of a tab is built again when
    /// the tab comes back on screen (proposal R6), so it is looked up at every use.
    /// </summary>
    internal System.Windows.FrameworkElement View => view();

    internal string Module => Sheet.ModuleId;
    internal System.Text.Json.Nodes.JsonObject Data => Sheet.Data;
    internal System.Text.Json.Nodes.JsonObject? Result => Sheet.Result;
    internal bool Busy => Sheet.IsBusy;

    /// <summary>MvvmSheetEditor.CalculateAsync(commitEdits: true).</summary>
    internal async Task CalculateAsync()
    {
        Sheet.Commit();
        await Sheet.CalculateAsync();
    }

    /// <summary>MvvmSheetEditor.ExportReport: the code shared with the new shell.</summary>
    internal void ExportReport(string filename, string title, HashSet<string> options) =>
        Anthea.Presentation.Shell.Commands.SheetFileExports.WriteReport(Sheet, filename, title, options, false);

    public static implicit operator HostedSheet(MvvmSheetEditor editor) => new(editor.Sheet, editor.View);
}

public sealed partial class MainWindow
{
    /// <summary>f5-suite-ui: the open sheet for the UI suites, with either implementation of its module (<see cref="OpenSheet"/>).</summary>
    private OpenSheet openSheet => new(editor ?? throw new InvalidOperationException("Nessun foglio aperto."), SheetServices);
}
