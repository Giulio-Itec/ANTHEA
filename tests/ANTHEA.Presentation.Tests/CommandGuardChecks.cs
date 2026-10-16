using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Tests;

/// <summary>Decision Q19: errors of the commands shown in the screen (ReportFailure), cancellation silent, failures of the message service unhandled.</summary>
internal static class CommandGuardChecks
{
    internal static async Task<int> RunAsync()
    {
        var expect = new Expect();
        var messages = new ScriptedMessages();
        var guard = new CommandGuard(messages);
        expect.That(CommandGuard.FailureTitle == "Operazione non completata", "Titolo diverso da MainWindow.Safe");

        bool ran = false;
        expect.That(guard.Run(() => ran = true) && ran && messages.Failures.Count == 0, "Comando riuscito segnalato");
        var error = new InvalidOperationException("Dati non validi");
        expect.That(!guard.Run(() => throw error), "Comando fallito dato per riuscito");
        expect.That(messages.Failures.Single() == (CommandGuard.FailureTitle, error), "Errore non mostrato con il suo testo");
        // A synchronous command has no token: an OperationCanceledException is an error, as in MainWindow.Safe.
        expect.That(!guard.Run(() => throw new OperationCanceledException()) && messages.Failures.Count == 2, "Annullamento senza token taciuto");

        messages.Failures.Clear();
        expect.That(await Expect.Within(guard.RunAsync(async _ => await Task.Yield())) && messages.Failures.Count == 0, "Comando asincrono riuscito segnalato");
        expect.That(!await Expect.Within(guard.RunAsync(async _ => { await Task.Yield(); throw error; })) && messages.Failures.Single().Error == error, "Errore asincrono non mostrato");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            expect.That(!await Expect.Within(guard.RunAsync(token => Task.FromCanceled(token), cancelled.Token)) && messages.Failures.Count == 1, "Annullamento mostrato come errore");
        }
        expect.That(!await Expect.Within(guard.RunAsync(_ => throw new OperationCanceledException(), CancellationToken.None)) && messages.Failures.Count == 2,
            "Annullamento non richiesto taciuto");

        // The message service failing is not swallowed: it becomes an unhandled error and reaches errori.log, as today.
        var broken = new ScriptedMessages { FailWith = new InvalidOperationException("finestra chiusa") };
        var unguarded = new CommandGuard(broken);
        expect.Throws<InvalidOperationException>(() => unguarded.Run(() => throw error), "Errore del servizio dei messaggi assorbito");
        bool escaped = false;
        try { await Expect.Within(unguarded.RunAsync(_ => throw error)); } catch (InvalidOperationException e) when (e == broken.FailWith) { escaped = true; }
        expect.That(escaped, "Errore asincrono del servizio dei messaggi assorbito");
        expect.Throws<ArgumentNullException>(() => _ = new CommandGuard(null!), "Guardia senza servizio dei messaggi");

        // Commands of the views.
        messages.Failures.Clear();
        bool enabled = false;
        var command = GuardedCommands.Create(guard, () => throw error, () => enabled);
        expect.That(!command.CanExecute(null), "Condizione del comando ignorata");
        enabled = true;
        command.Execute(null);
        expect.That(command.CanExecute(null) && messages.Failures.Single().Error == error, "Errore del comando sincrono non mostrato");

        messages.Failures.Clear();
        using var lifetime = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int runs = 0;
        var calculate = GuardedCommands.CreateAsync(guard, async token =>
        {
            runs++;
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, lifetime.Token);
        var cancel = calculate.CreateCancelCommand();
        var execution = calculate.ExecuteAsync(null);
        await Expect.Within(started.Task);
        expect.That(calculate.IsRunning && !calculate.CanExecute(null) && cancel.CanExecute(null), "Comando in corso rieseguibile o non annullabile");
        cancel.Execute(null);
        await Expect.Within(execution);
        expect.That(!calculate.IsRunning && runs == 1 && messages.Failures.Count == 0, "Annullamento del comando non silenzioso");

        started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        execution = calculate.ExecuteAsync(null);
        await Expect.Within(started.Task);
        lifetime.Cancel();
        await Expect.Within(execution);
        expect.That(!calculate.IsRunning && runs == 2 && messages.Failures.Count == 0, "Chiusura del ViewModel non annulla il comando");

        var failing = GuardedCommands.CreateAsync(guard, async _ => { await Task.Yield(); throw error; }, CancellationToken.None);
        await Expect.Within(failing.ExecuteAsync(null));
        expect.That(failing.ExecutionTask is { IsCompletedSuccessfully: true } && messages.Failures.Single().Error == error, "Errore del comando asincrono non mostrato o rilanciato");
        return expect.Count;
    }
}
