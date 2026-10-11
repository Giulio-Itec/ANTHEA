using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static async Task VerificationProgressChecks()
    {
        using var vm = new ModelViewerViewModel(Sample.Model(), [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        var ready = new TaskCompletionSource<IProgress<VerificationProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = vm.RunVerificationAsync(async (progress, _) => { ready.SetResult(progress); await finish.Task; });
        var reporter = await ready.Task;
        Check(vm.HasVerificationProgress && vm.IsVerificationRunning && vm.IsVerificationIndeterminate && !vm.ImportCommand.CanExecute(null), "verification starts with unknown work total and prevents replacing its model");
        reporter.Report(new(25, 100, "Ambito A"));
        Check(vm.VerificationPercentage == 25 && !vm.IsVerificationIndeterminate && vm.VerificationActivity == "Ambito A", "progress uses reported work counts rather than elapsed-time estimates");
        reporter.Report(new(101, 100, "Incoerente"));
        Check(vm.VerificationPercentage == 25 && vm.VerificationActivity == "Ambito A", "invalid work counters cannot fabricate a percentage");
        bool rejected = false;
        try { await vm.RunVerificationAsync((_, _) => Task.CompletedTask); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "concurrent verification runs are rejected");
        vm.CancelVerificationCommand.Execute(null);
        Check(vm.IsVerificationRunning && vm.VerificationStatus == "Annullamento richiesto…" && !vm.CancelVerificationCommand.CanExecute(null), "cancellation remains pending until the worker acknowledges termination");
        finish.SetResult();
        Check(await running == VerificationRunOutcome.Cancelled && !vm.IsVerificationRunning && vm.VerificationPercentage == 25 && vm.ImportCommand.CanExecute(null), "cancelled work does not claim 100 percent completion and releases import");
        var secondReady = new TaskCompletionSource<IProgress<VerificationProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = vm.RunVerificationAsync(async (progress, _) => { secondReady.SetResult(progress); await secondFinish.Task; });
        var secondReporter = await secondReady.Task; secondReporter.Report(new(1, 2, "Nuova esecuzione")); reporter.Report(new(90, 100, "Vecchia esecuzione"));
        Check(vm.VerificationPercentage == 50 && vm.VerificationActivity == "Nuova esecuzione", "late notifications from an earlier run cannot overwrite the current one");
        secondFinish.SetResult();
        Check(await second == VerificationRunOutcome.Completed && vm.VerificationPercentage == 100 && vm.VerificationStatus == "Esecuzione completata" && vm.DismissVerificationProgressCommand.CanExecute(null), "successful execution is distinguished from engineering acceptance");
        vm.DismissVerificationProgressCommand.Execute(null);
        Check(!vm.HasVerificationProgress && !vm.DismissVerificationProgressCommand.CanExecute(null), "completed progress can be dismissed");
        var failure = await vm.RunVerificationAsync((_, _) => Task.FromException(new InvalidOperationException("Errore controllato")));
        Check(failure == VerificationRunOutcome.Failed && vm.VerificationPercentage == 0 && vm.VerificationActivity == "Errore controllato", "failed execution retains its error without pretending to complete");
        var disposedReady = new TaskCompletionSource<IProgress<VerificationProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var third = vm.RunVerificationAsync(async (progress, token) => { disposedReady.SetResult(progress); await Task.Delay(Timeout.Infinite, token); });
        var disposedReporter = await disposedReady.Task; vm.Dispose(); string before = vm.VerificationActivity;
        disposedReporter.Report(new(50, 100, "Messaggio tardivo"));
        Check(await third == VerificationRunOutcome.Cancelled && vm.VerificationActivity == before, "closing the view cancels its worker and discards late progress");
    }
}
