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
