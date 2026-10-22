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

    internal void VerifyDisplayLayout() { if (Pile is { } pile) pile.VerifyDisplayLayout(); else Historic.VerifyDisplayLayout(); }

    internal Task VerifyDisplayScrolling(string directory, string name) => Pile is { } pile ? pile.VerifyDisplayScrolling(directory, name) : Historic.VerifyDisplayScrolling(directory, name);

    internal Task VerifyStratigraphyEditing() => Pile is { } pile ? pile.VerifyStratigraphyEditing() : Historic.VerifyStratigraphyEditing();

    internal void VerifyPileOutcomes() { if (Pile is { } pile) pile.VerifyPileOutcomes(); else Historic.VerifyPileOutcomes(); }

    internal Task VerifyPresentation(string directory) => Pile is { } pile ? pile.VerifyPresentation(directory) : Historic.VerifyPresentation(directory);

    internal void VerifyReport(string directory) { if (Pile is { } pile) pile.VerifyReport(directory); else Historic.VerifyReport(directory); }

    internal Task VerifyNavigationThrough(Action leave, Action resume) => Pile is { } pile ? pile.VerifyNavigationThrough(leave, resume) : Historic.VerifyNavigationThrough(leave, resume);

    // ---------------------------------------------------------------- f5-suite-passaggi: ui/smoke-horizontal (HorizontalSmokeChecks.cs)

    /// <summary>The horizontal pile or micropile with the XAML view, null otherwise.</summary>
    private HorizontalPileSmoke? HorizontalPile => HorizontalPileSmoke.Handles(editor) ? new((MvvmSheetEditor)editor, services) : null;

    internal Task WaitForHorizontalAutomatic() => HorizontalPile is { } pile ? pile.WaitForAutomatic() : Historic.WaitForHorizontalAutomatic();

    internal Task VerifyHorizontal(string directory) => HorizontalPile is { } pile ? pile.Verify(directory) : Historic.VerifyHorizontal(directory);

    internal Task VerifyHorizontalLayout(string directory, string size) => HorizontalPile is { } pile ? pile.VerifyLayout(directory, size) : Historic.VerifyHorizontalLayout(directory, size);

    internal Task VerifyChs() => HorizontalPile is { } pile ? pile.VerifyChs() : Historic.VerifyChs();

    // ---------------------------------------------------------------- materials (ui/smoke-materials, ui/smoke-hierarchy, ui/smoke-sharing, ui/smoke-steel)

    /// <summary>The concrete with the XAML view, null otherwise.</summary>
    private ConcreteMaterialSmoke? Concrete => ConcreteMaterialSmoke.Handles(editor) ? new((MvvmSheetEditor)editor) : null;

    private Materiali.MaterialView HistoricConcrete => Historic.materials ?? throw new Exception("Scheda materiali non aperta");

    /// <summary>RestoreState of the historic concrete view; on the XAML view the same state set through its controls (ConcreteMaterialSmoke.Apply).</summary>
    internal void SetConcreteState(System.Text.Json.Nodes.JsonObject state) { if (Concrete is { } concrete) concrete.Apply(state); else HistoricConcrete.RestoreState(state); }

    /// <summary>MaterialView.Check, the self-check of the concrete sheet (ui/smoke-materials).</summary>
    internal void CheckConcrete() { if (Concrete is { } concrete) concrete.SelfCheck(); else HistoricConcrete.Check(); }

    /// <summary>The user types in a field of the concrete (before the commit: the preview of the sheet follows).</summary>
    internal void TypeConcreteNumber(string key, string text) { if (Concrete is { } concrete) concrete.Type(key, text); else HistoricConcrete.NumberBoxForChecks(key).Text = text; }

    // ---------------------------------------------------------------- retaining walls (ui/smoke-retaining-wall, ui/smoke-global-stability)

    /// <summary>The retaining wall, with either view (WallSmoke).</summary>
    internal WallSmoke Wall => MvvmWallSmoke.Handles(editor) ? new MvvmWallSmoke((MvvmSheetEditor)editor, services)
        : new HistoricWallSmoke(Historic.retainingWall ?? throw new Exception("Muro di sostegno non aperto"));

    /// <summary>F5.11: the composite bridge section, with either view (BridgeSmoke).</summary>
    internal BridgeSmoke Bridge => BridgeSmoke.Of(editor);

    /// <summary>The reinforcing steel, with either view (SteelSmoke).</summary>
    internal SteelSmoke Steel => editor.Module != RebarMaterial.Module ? throw new Exception("Modulo acciaio non aperto")
        : new(editor is MvvmSheetEditor ? editor : Historic);
}

/// <summary>f5-suite-ui: what the UI suites need from any sheet with the XAML view.</summary>
internal static class MvvmSheets
{
    /// <summary>
    /// WaitForAutomatic of SheetEditor on a ViewModel: the pending automatic recalculation (scheduled at the opening and after each edit)
    /// and any calculation under way finished, within 10 s; otherwise the message of the historic check.
    /// </summary>
    internal static async Task WaitForAutomatic(MvvmSheetEditor editor)
    {
        var sheet = (SheetViewModelBase)editor.Sheet;
        for (int i = 0; i < 200 && (!sheet.PendingCalculation.IsCompleted || editor.Busy); i++) await Task.Delay(50);
        if (!sheet.PendingCalculation.IsCompleted || editor.Busy) throw new Exception("Calcolo automatico non terminato");
    }
}

public sealed partial class MainWindow
{
    /// <summary>f5-suite-ui: the open sheet for the UI suites, with either implementation of its module (<see cref="OpenSheet"/>).</summary>
    private OpenSheet openSheet => new(editor ?? throw new InvalidOperationException("Nessun foglio aperto."), SheetServices);
}
