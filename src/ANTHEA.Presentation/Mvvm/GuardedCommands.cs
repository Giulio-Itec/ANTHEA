using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Mvvm;

/// <summary>
/// Conventions of the commands of the ViewModels (proposal F5, section 3.1), on CommunityToolkit.Mvvm:
/// <list type="bullet">
/// <item>every command a view binds goes through a <see cref="CommandGuard"/>, built here or written as
/// <c>[RelayCommand] void X() => guard.Run(...)</c>;</item>
/// <item>long operations are asynchronous commands with a <see cref="CancellationToken"/>; the cancel command comes from
/// <see cref="IAsyncRelayCommandExtensions.CreateCancelCommand"/> (or <c>IncludeCancelCommand = true</c>), the token is linked
/// to the lifetime of the ViewModel, and no concurrent execution is allowed (the command is disabled while it runs);</item>
/// <item>no <c>async void</c>: the command awaits the guard, which never throws for an error of the command.</item>
/// </list>
/// </summary>
public static class GuardedCommands
{
    /// <summary>Synchronous command whose errors are shown by <paramref name="guard"/>.</summary>
    public static IRelayCommand Create(CommandGuard guard, Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(execute);
        return canExecute is null ? new RelayCommand(() => guard.Run(execute)) : new RelayCommand(() => guard.Run(execute), canExecute);
    }

    /// <summary>
    /// Asynchronous cancellable command whose errors are shown by <paramref name="guard"/>; the token passed to
    /// <paramref name="execute"/> is cancelled by the cancel command or by <paramref name="lifetime"/> (the ViewModel closed).
    /// </summary>
    public static IAsyncRelayCommand CreateAsync(CommandGuard guard, Func<CancellationToken, Task> execute, CancellationToken lifetime,
        Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(execute);
        async Task RunAsync(CancellationToken command)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(command, lifetime);
            await guard.RunAsync(execute, linked.Token);
        }
        return canExecute is null ? new AsyncRelayCommand(RunAsync) : new AsyncRelayCommand(RunAsync, canExecute);
    }
}
