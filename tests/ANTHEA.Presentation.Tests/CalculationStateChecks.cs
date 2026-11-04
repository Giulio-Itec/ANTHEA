using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Step 4 of docs/sviluppo/progetto-sistema.md (decisions of the user of 4/11/2026: «Solo in memoria», «Calcolato» for the modules without a
/// uniform outcome): a calculated sheet shows its state in the tree and in the overview; an edit makes it «da ricalcolare»; the calculation
/// changes neither the document nor the undo; the outcome of the summaries gives «verificato» or «non verificato».
/// </summary>
internal static class CalculationStateChecks
{
    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    internal static int Run() => SingleThreadContext.Run(_ => RunAll());

    private static async Task<int> RunAll()
    {
        var expect = new Expect();

        // The outcome of the summaries of a sheet.
        static VerificationSummary Summary(bool? passed, int total = 1) => VerificationSummary.Create("s", total,
            passed is null ? [] : [("v", passed == true ? 0.5 : 1.5, passed)]);
        expect.That(VerificationSummary.Combine([Summary(true), Summary(true)]) == true && VerificationSummary.Combine([Summary(true), Summary(false)]) == false
            && VerificationSummary.Combine([Summary(true), Summary(null)]) is null && VerificationSummary.Combine([Summary(null, 0)]) is null
            && VerificationSummary.Combine([]) is null, "Esito di un foglio dai suoi riepiloghi");

        var bench = new ShellBench();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(),
            new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var first = project.AddSheet("mat_calcestruzzo", section)!;
        var second = project.AddSheet("str_palo", section)!;
        shell.ShowOverview(section);
        expect.That(shell.Tree.Find(first)?.Calculation == SheetCalculationState.ToCalculate && shell.Tree.Find(first)!.CalculationGlyph is null
            && shell.Overview?.HasCalculations == false, "Prima del calcolo: nessun segno, nessuna riga nel riepilogo");

        var tab = workspace.OpenSheet(first);
        bool dirty = project.IsDirty;
        int undo = project.History.UndoCount;
        await tab.ViewModel.CalculateAsync();
        var node = shell.Tree.Find(first)!;
        shell.ShowOverview(section);
        expect.That(tab.ViewModel.HasResults && node.Calculation == SheetCalculationState.Calculated && node.CalculationGlyph == "●" && node.CalculationText == "Calcolato"
            && shell.Overview?.CalculationsText == "1 calcolato · 1 da calcolare", "Calcolato: segno nell'albero e riga del riepilogo: " + shell.Overview?.CalculationsText);
        expect.That(project.IsDirty == dirty && project.History.UndoCount == undo && first["calcolo"] is null, "Il calcolo non cambia il documento né l'annulla");

        // The overview took the focus from the tab: the edit is made in the active tab, as in the window.
        workspace.Activate(tab);
        ShellBench.Probe(tab).Label = "dopo il calcolo";
        var edited = shell.Tree.Find(first)!;
        expect.That(!tab.ViewModel.HasResults && edited.Calculation == SheetCalculationState.ToRecalculate && edited.CalculationGlyph == "↻", "Modifica dopo il calcolo: da ricalcolare");
        await tab.ViewModel.CalculateAsync();
        workspace.CommitActive();
        expect.That(shell.Tree.Find(first)?.Calculation == SheetCalculationState.Calculated && first["dati"]?["etichetta"]?.ToString() == "dopo il calcolo",
            "Calcolato di nuovo e confermato nel documento");
        project.Restore(false);
        var restored = project.Find(first.S("id"))!;
        shell.Tree.Refresh();
        expect.That(shell.Tree.Find(restored)?.Calculation == SheetCalculationState.ToRecalculate, "Dopo un annulla i dati sono quelli di prima: da ricalcolare");

        // The outcome declared by the concrete and composite sections.
        var secondNow = project.Find(second.S("id"))!;
        if (secondNow["dati"] is null) secondNow["dati"] = Anthea.Application.Documents.DocumentService.CreateSheet("str_palo");
        project.Calculations.Record(second.S("id"), (JsonObject)secondNow["dati"]!, true);
        expect.That(project.Calculations.State(secondNow) == SheetCalculationState.Verified && shell.Tree.Find(secondNow)?.CalculationGlyph == "✓", "Verificato");
        project.Calculations.Record(second.S("id"), (JsonObject)secondNow["dati"]!, false);
        expect.That(project.Calculations.State(secondNow) == SheetCalculationState.NotVerified && shell.Tree.Find(secondNow)?.CalculationText == "Non verificato", "Non verificato");
        return expect.Count;
    }
}
