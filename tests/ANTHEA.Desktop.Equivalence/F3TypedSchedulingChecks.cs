namespace X.Desktop;

// F5.11: the probe of the historic view of the composite bridge section (BridgeWorkspace.CheckF3Scheduling) is archived with the view in
// supporto/SUPERATI/ui-storica/sezione-mista; the probe runs on the MVVM sheet (F3CompositeSectionMvvmSchedulingChecks).
// f5-pass-muri: the probes of the historic view of the retaining walls (RetainingWallWorkspace.CheckF3Scheduling, here until 23/10/2026, and
// CheckParameterRevision, F3WallParameterChecks.cs) are archived with the view in supporto/SUPERATI/ui-storica/muri; the probes run on the MVVM
// sheet (F3WallMvvmSchedulingChecks).
// F5.12: the probes of the historic view of Bridge Design (BridgeDesignWorkspace.CheckF3Scheduling) are archived with the view in
// supporto/SUPERATI/ui-storica/bridge-design; the probes run on the MVVM sheet (F3BridgeDesignMvvmSchedulingChecks).
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
        var curve = view.CalculateCurvature();
        var quick = view.RefreshQuickResistance();
        view.Dispose();
        string status = view.status.Text, curveStatus = view.curvatureText.Text, quickStatus = view.quickResistanceText.Text;
        int quickCards = view.quickResistanceResults.Children.Count;
        release.SetResult(); await Task.WhenAll(work, curve, quick).WaitAsync(TimeSpan.FromSeconds(20));
        Check(!view.Busy && !view.calculationQueued && view.Result is null, "Chiusura CA lascia calcoli o risultati in coda");
        Check(view.status.Text == status, "CA aggiorna lo stato dopo la chiusura");
        Check(view.curvatureResult is null && !view.curvatureRunning && view.curvatureText.Text == curveStatus,
            "Curva M–χ pubblicata nella vista chiusa");
        Check(view.quickResistanceResults.Children.Count == quickCards && view.quickResistanceText.Text == quickStatus,
            "Resistenze rapide aggiornate nella vista chiusa");
        return checks;
    }
}
