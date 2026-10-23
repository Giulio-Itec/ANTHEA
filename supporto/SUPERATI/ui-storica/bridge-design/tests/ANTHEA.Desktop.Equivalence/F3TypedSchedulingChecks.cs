namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    internal static async Task<int> CheckF3Scheduling()
    {
        using var service=new GatedModuleService(BridgeConcept.Module);
        using var view=new BridgeDesignWorkspace(BridgeConcept.Defaults(),service);
        int checks = await F3SchedulingProbe.Run(service,()=>view.CalculateAsync(),
            value=>view.Editors["input/length"].Text=value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ()=>view.Calculation?.Length,()=>view.Busy,view.Dispose,185,195);
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        using var pendingService = new GatedModuleService(BridgeConcept.Module);
        using var pendingView = new BridgeDesignWorkspace(BridgeConcept.Defaults(), pendingService);
        await pendingService.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var optimization = pendingView.RunOptimization();
        Check(!optimization.IsCompleted && pendingView.optimizationCancellation is not null,
            "Ricerca in attesa del calcolo non registrata");
        var duplicate = pendingView.RunOptimization();
        Check(duplicate.IsCompleted && pendingService.Calls == 1, "Doppia ricerca in attesa accettata");
        pendingView.optimizationCancellation!.Cancel();
        await optimization.WaitAsync(TimeSpan.FromSeconds(15));
        Check(pendingView.Optimization is null && pendingView.optimizationCancellation is null &&
            pendingView.optimizationRun.IsEnabled, "Annullamento durante l’attesa non ripristina la ricerca");
        var closingSearch = pendingView.RunOptimization();
        pendingView.Dispose(); pendingService.Release();
        await Task.WhenAll(closingSearch, pendingView.WaitForCalculationAsync()).WaitAsync(TimeSpan.FromSeconds(20));
        Check(pendingView.Calculation is null && pendingView.Optimization is null &&
            pendingView.optimizationCancellation is null, "Chiusura durante l’attesa pubblica risultati tardivi");
        return checks;
    }
}
