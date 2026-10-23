using System.Globalization;
using Anthea.Presentation.Modules.BridgeDesign;

namespace X.Desktop;

/// <summary>
/// F5.12: the probe of ui/f3-scheduling on Bridge Design with the MVVM view, as the Release opens it since the switch of 23/10/2026, in place of
/// BridgeDesignWorkspace.CheckF3Scheduling (archived with the historic view): the sheet opened by the factory of the bench in MvvmSheetEditor with the gated engine, «Calcola» of the
/// window (the editor commits, then calculates), the length typed in its parameter (input/length, as the historic probe types it in the editor of
/// the view), the result read from the sheet; then the search that waits for the calculation of the opening (registered, not doubled, cancelled,
/// closed with the sheet). Same barriers and the same expectations as the historic probe.
/// </summary>
internal static class F3BridgeDesignMvvmSchedulingChecks
{
    internal static async Task<int> CheckF3Scheduling()
    {
        int checks;
        using (var service = new GatedModuleService(BridgeConcept.Module))
        {
            var editor = ModuleHostBench.SheetEditors().Open(BridgeConcept.Module, BridgeConcept.Defaults(), service);
            var sheet = (BridgeDesignSheetViewModel)((MvvmSheetEditor)editor).Sheet;
            try
            {
                checks = await F3SchedulingProbe.Run(service, () => editor.CalculateAsync(),
                    value => sheet.Parameters["input/length"].Text = value.ToString(CultureInfo.InvariantCulture),
                    () => sheet.Calculation?.Length, () => editor.Busy, editor.Dispose, 185, 195);
            }
            finally { editor.Dispose(); }
        }
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(BridgeConcept.Module + " (MVVM): " + message); checks++; }
        using var pendingService = new GatedModuleService(BridgeConcept.Module);
        var pendingEditor = ModuleHostBench.SheetEditors().Open(BridgeConcept.Module, BridgeConcept.Defaults(), pendingService);
        var pending = (BridgeDesignSheetViewModel)((MvvmSheetEditor)pendingEditor).Sheet;
        try
        {
            await pendingService.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var optimization = pending.RunOptimizationAsync();
            Check(!optimization.IsCompleted && !pending.SearchButtons.Run && !pending.CanRunOptimization, "Ricerca in attesa del calcolo non registrata");
            var duplicate = pending.RunOptimizationAsync();
            Check(duplicate.IsCompleted && pendingService.Calls == 1, "Doppia ricerca in attesa accettata");
            pending.StopOptimization();
            await optimization.WaitAsync(TimeSpan.FromSeconds(15));
            Check(pending.Optimization is null && pending.SearchButtons.Run && pending.CanRunOptimization, "Annullamento durante l’attesa non ripristina la ricerca");
            var closingSearch = pending.RunOptimizationAsync();
            pendingEditor.Dispose(); pendingService.Release();
            await Task.WhenAll(closingSearch, pending.WaitForCalculationAsync()).WaitAsync(TimeSpan.FromSeconds(20));
            Check(pending.Calculation is null && pending.Optimization is null, "Chiusura durante l’attesa pubblica risultati tardivi");
        }
        finally { pendingEditor.Dispose(); pendingService.Release(); }
        return checks;
    }
}
