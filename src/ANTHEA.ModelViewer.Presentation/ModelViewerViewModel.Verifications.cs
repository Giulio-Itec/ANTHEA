using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>Work reported by the application runner, not the engineering verdict of a check.</summary>
public sealed record VerificationProgress(int Completed, int? Total, string Activity);
public enum VerificationRunOutcome { Completed, Cancelled, Failed }

public sealed partial class ModelViewerViewModel
{
    private CancellationTokenSource? verificationCancellation;
    private long verificationGeneration;
    [ObservableProperty] private bool hasVerificationProgress;
    [ObservableProperty] private bool isVerificationRunning;
    [ObservableProperty] private bool isVerificationCancellationRequested;
    [ObservableProperty] private bool isVerificationIndeterminate;
    [ObservableProperty] private double verificationPercentage;
    [ObservableProperty] private string verificationStatus = "";
    [ObservableProperty] private string verificationActivity = "";
    [ObservableProperty] private string verificationCount = "";

    partial void OnHasVerificationProgressChanged(bool value) => DismissVerificationProgressCommand.NotifyCanExecuteChanged();
    partial void OnIsVerificationRunningChanged(bool value)
    {
        NotifyCombinationCommands();
        InspectMidasCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged();
        CancelVerificationCommand.NotifyCanExecuteChanged(); DismissVerificationProgressCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsVerificationCancellationRequestedChanged(bool value) => CancelVerificationCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Called on the presentation context by the future application runner. Work runs off the UI thread
    /// and must honor cancellation. No engineering work or result persistence is implemented here.
    /// A completed execution does not mean that the engineering checks passed.
    /// </summary>
    public async Task<VerificationRunOutcome> RunVerificationAsync(Func<IProgress<VerificationProgress>, CancellationToken, Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        if (disposed || IsReadOnly || Snapshot == null || IsBusy || IsVerificationRunning)
            throw new InvalidOperationException("La vista non può avviare una verifica in questo stato.");
        long generation = ++verificationGeneration;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        verificationCancellation = cancellation;
        HasVerificationProgress = true; IsVerificationCancellationRequested = false; IsVerificationRunning = true;
        IsVerificationIndeterminate = true; VerificationPercentage = 0;
        VerificationStatus = "Preparazione delle verifiche…"; VerificationActivity = ""; VerificationCount = "Totale da determinare";
        var progress = new VerificationProgressDispatcher(SynchronizationContext.Current, value =>
        {
            if (disposed || generation != verificationGeneration || !IsVerificationRunning || IsVerificationCancellationRequested) return;
            // Invalid counters must not produce an invented percentage.
            if (value.Completed < 0 || value.Total is < 0 || value.Total is int total && value.Completed > total) return;
            IsVerificationIndeterminate = value.Total is null;
            VerificationPercentage = value.Total is > 0 ? 100.0 * value.Completed / value.Total.Value : 0;
            VerificationCount = value.Total is int count ? $"{value.Completed:N0} / {count:N0} attività" : $"{value.Completed:N0} attività completate";
            VerificationActivity = value.Activity ?? "";
            VerificationStatus = "Verifiche in esecuzione";
        });
        try
        {
            await Task.Run(() => execute(progress, cancellation.Token), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!disposed) { VerificationStatus = "Esecuzione completata"; VerificationPercentage = 100; }
            return VerificationRunOutcome.Completed;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!disposed) VerificationStatus = "Esecuzione annullata";
            return VerificationRunOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            if (!disposed) { VerificationStatus = "Esecuzione non completata"; VerificationActivity = ex.Message; }
            return VerificationRunOutcome.Failed;
        }
        finally
        {
            verificationCancellation = null;
            if (!disposed) { IsVerificationRunning = false; IsVerificationIndeterminate = false; }
        }
    }

    private bool CanCancelVerification() => IsVerificationRunning && !IsVerificationCancellationRequested && !disposed;
    [RelayCommand(CanExecute = nameof(CanCancelVerification))]
    private void CancelVerification()
    {
        IsVerificationCancellationRequested = true; VerificationStatus = "Annullamento richiesto…";
        verificationCancellation?.Cancel();
    }
    private bool CanDismissVerificationProgress() => HasVerificationProgress && !IsVerificationRunning && !disposed;
    [RelayCommand(CanExecute = nameof(CanDismissVerificationProgress))]
    private void DismissVerificationProgress() => HasVerificationProgress = false;

    private sealed class VerificationProgressDispatcher(SynchronizationContext? context, Action<VerificationProgress> apply) : IProgress<VerificationProgress>
    {
        public void Report(VerificationProgress value)
        {
            if (context == null) apply(value);
            else context.Post(_ => apply(value), null);
        }
    }
}
