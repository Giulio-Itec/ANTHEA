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
