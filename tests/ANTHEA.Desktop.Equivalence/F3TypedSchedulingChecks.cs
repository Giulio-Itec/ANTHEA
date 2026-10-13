namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal static async Task<int> CheckF3Scheduling()
    {
        using var service = new GatedModuleService(BridgeSection.Module);
        using var view = new BridgeWorkspace(BridgeSection.Defaults(), service);
        var form = view.inputForms.Single(f => f.Editors.ContainsKey("h_trave"));
        return await F3SchedulingProbe.Run(service, () => view.CalculateAsync(),
            value => { form.Set("h_trave", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); form.Commit(); },
            () => view.Calculation?.Input.D("h_trave"), () => view.Busy, view.Dispose, 1955, 2055);
    }
}
internal sealed partial class RetainingWallWorkspace
{
    internal static async Task<int> CheckF3Scheduling()
    {
        using var service = new GatedModuleService(RetainingWall.Module);
        using var view = new RetainingWallWorkspace(RetainingWall.Defaults(), service);
        return await F3SchedulingProbe.Run(service, () => view.CalculateAsync(),
            value => { view.Forms["geometry"].Set("height", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); view.Forms["geometry"].Commit(); },
            () => view.Calculation?.Input["geometry"].D("height"), () => view.Busy, view.Dispose, 3.2, 3.4);
    }
}
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
internal static class F3SchedulingProbe
{
    internal static async Task<int> Run(GatedModuleService service, Func<Task> calculate, Action<double> edit,
        Func<double?> result, Func<bool> busy, Action close, double next, double final)
    {
        int checks = 0;
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(service.ModuleId + ": " + text); checks++; }
        var first = calculate(); await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Check(busy(), "Stato Busy assente");
        edit(next); Check(result() is null, "Modifica non invalida il risultato");
        var last = calculate(); Check(service.Calls == 1, "Calcoli tipizzati sovrapposti");
        service.Release(); await Task.WhenAll(first, last).WaitAsync(TimeSpan.FromSeconds(20));
        Check(result() == next, "Pubblicata una geometria precedente");
        Check(!busy() && service.Calls == 2, "Calcolo automatico duplicato o stato occupato residuo");
        service.Arm(); edit(final); var closing = calculate();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(15)); close(); service.Release();
        await closing.WaitAsync(TimeSpan.FromSeconds(20));
        Check(result() is null && !busy() && service.Calls == 3, "Risultato tardivo dopo la chiusura");
        return checks;
    }
}

internal sealed partial class ConcreteWorkspace
{
    internal static async Task<int> CheckF3Scheduling()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        using var view = new ConcreteWorkspace(ModuleCatalog.CreateData("str_palo"));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = view.RunAnalysis(async token => { started.SetResult(); await release.Task; });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Check(view.Busy, "CA non segnala il calcolo in corso");
        view.SetGeometry("1350");
        Check(view.Result is null, "CA conserva risultati dopo modifica");
        view.Dispose();
        string status = view.status.Text;
        release.SetResult(); await work.WaitAsync(TimeSpan.FromSeconds(15));
        Check(!view.Busy && !view.calculationQueued && view.Result is null, "Chiusura CA lascia calcoli o risultati in coda");
        Check(view.status.Text == status, "CA aggiorna lo stato dopo la chiusura");
        return checks;
    }
}
