using Anthea.Application.Bridge;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c, night of 22-23/10 (section 10.1, fourth item): the data of the small screens (SheetLayout: the list of MainWindow.LoadSheetEditor and
/// the minimum sizes of DisplayAdaptation) and the notice of a tab reloaded because another sheet confirmed shared data (SH9): which sheet and which
/// common data, on the reloaded tab until it is confirmed, in the sheet bar while it has the focus; no notice after undo.
/// </summary>
internal static class ShellLayoutChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private static int RunAll()
    {
        var expect = new Expect();

        // The modules whose view follows the window: the list of MainWindow.LoadSheetEditor, with its constants.
        string[] fitting = ["geo_palo_verticale", "geo_micropalo_verticale", HorizontalPileEditor.Module, "geo_micropalo_orizzontale", WallModel.Module,
            "mat_calcestruzzo", RebarEditor.Module, BridgeEditor.Module, BridgeDesignModel.Module];
        expect.That(ModuleDirectory.All.Where(m => m.Id.Length > 0).Select(m => m.Id).Where(SheetLayout.FitsWindow).Order(StringComparer.Ordinal)
            .SequenceEqual(fitting.Order(StringComparer.Ordinal)), "Moduli che seguono la finestra: l'elenco di MainWindow.LoadSheetEditor");
        expect.That(!SheetLayout.FitsWindow("str_palo") && !SheetLayout.FitsWindow("geo_stratigrafia") && !SheetLayout.FitsWindow("geo_efficienza_orizzontale")
            && SheetLayout.SheetMinimumWidth == 1120 && SheetLayout.SheetMinimumHeight == 600 && SheetLayout.WindowMinimumWidth == 1120 && SheetLayout.WindowMinimumHeight == 680
            && SheetLayout.PageMinimumWidth == 1120 - 250 && SheetLayout.PageMinimumHeight == 680, "Dimensioni minime di MainWindow e DisplayAdaptation");

        // The texts of the notice.
        expect.That(ShellTexts.ReloadNotice("Calcestruzzo", []) == "Aggiornato da «Calcestruzzo»." && ShellTexts.ReloadNotice("A", ["x", "y"]) == "Aggiornato da «A»: x, y."
            && ShellTexts.ReloadNotice("A", ["1", "2", "3", "4", "5", "6", "7"]) == "Aggiornato da «A»: 1, 2, 3, 4, 5 e altre 2 proprietà.", "Testi dell'avviso della scheda ricaricata");

        // A project with two linked concrete sheets: «Aggiorna tutti i fogli collegati» reloads the other tab, which says from where and what.
        var bench = new ShellBench(new ScriptedConfirmations().Then(true));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var material = project.AddSheet("mat_calcestruzzo", section)!;
        var second = project.AddSheet("mat_calcestruzzo", section)!;
        project.Rename(material, "Calcestruzzo della pila");
        var materialTab = workspace.OpenSheet(material);
        var secondTab = workspace.OpenSheet(second);
        expect.That(shell.Windows.TabOf(materialTab) is SheetTabViewModel { FitsWindow: true } && materialTab.ReloadNotice is null, "Scheda del calcestruzzo: segue la finestra, nessun avviso");
        workspace.Activate(materialTab);
        materialTab.ViewModel.Data["classe"] = "C40/50";
        workspace.Activate(secondTab);
        expect.That(secondTab.ReloadNotice == "Aggiornato da «Calcestruzzo della pila»: " + SharedDataCoordinator.FieldLabel("CLS · fck [MPa]") + "."
            && shell.SheetBar.ReloadNotice == secondTab.ReloadNotice && materialTab.ReloadNotice is null, "Avviso della scheda ricaricata: " + secondTab.ReloadNotice);

        // The notice stays while the tab has the focus, and goes when the tab is confirmed (it loses the focus).
        workspace.Activate(materialTab);
        expect.That(secondTab.ReloadNotice is null && shell.SheetBar.ReloadNotice is null, "Avviso tolto dalla conferma della scheda");

        // Undo reloads the tabs without a notice: no sheet confirmed the change.
        bench.Reloads.Clear();
        workspace.Activate(null);
        project.Restore(false);
        expect.That(workspace.Sheets.All(s => s.ReloadNotice is null), "Annulla: schede ricaricate senza avviso");
        return expect.Count;
    }
}
