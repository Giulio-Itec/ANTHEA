using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;

namespace Anthea.Presentation.Tests;

/// <summary>
/// SheetCalculation on the existing RecalculationScheduler: states, publication of the current request only, cancellation,
/// closing during the calculation, copies of the data, cache of the module session and limiter. Everything runs on one
/// "UI" thread (SingleThreadContext); the engine is held by barriers, never by timed waits.
/// </summary>
internal static class SheetCalculationChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private static JsonObject Data(double value) => new() { ["valore"] = value };

    private static SheetCalculation<ModuleCalculation> Uncached(BarrierModuleService service, TimeSpan delay = default, CalculationLimiter? limiter = null, Action? release = null)
        => new(service.Calculate, SheetCalculation.Review, delay, limiter, release);

    private static double Echo(SheetCalculation<ModuleCalculation> calculation) => calculation.Result!.Values["eco"]!.GetValue<double>();

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();

        // Immediate calculation: Pending, Running, Completed, all on the UI thread; the engine on another thread.
        var service = new BarrierModuleService();
        var calculation = Uncached(service);
        var states = new List<CalculationState>();
        var threads = new HashSet<int>();
        int published = 0;
        calculation.PropertyChanged += (_, e) => { threads.Add(Environment.CurrentManagedThreadId); if (e.PropertyName == nameof(calculation.State)) states.Add(calculation.State); };
        calculation.Published += (_, _) => { threads.Add(Environment.CurrentManagedThreadId); published++; };
        expect.That(calculation.State == CalculationState.Idle && !calculation.HasResult && !calculation.IsBusy, "Stato iniziale");
        expect.That(await Expect.Within(calculation.CalculateAsync(Data(3))), "Calcolo corrente non pubblicato");
        expect.That(states.SequenceEqual([CalculationState.Pending, CalculationState.Running, CalculationState.Completed]), "Sequenza degli stati: " + string.Join(", ", states));
        expect.That(calculation.HasResult && Echo(calculation) == 3 && calculation.Message == "" && calculation.Warnings.Count == 0 && published == 1, "Risultato pubblicato");
        expect.That(threads.SequenceEqual([ui.ThreadId]) && !service.Threads.Contains(ui.ThreadId), "Pubblicazione fuori dal thread dell'interfaccia o calcolo sul thread dell'interfaccia");

        // The engine works on a copy taken at the request: later edits of the document do not reach it.
        service.Hold = true;
        var data = Data(5);
        var running = calculation.CalculateAsync(data);
        await service.EnteredAsync();
        expect.That(calculation.State == CalculationState.Running && calculation.IsBusy, "Stato durante il calcolo");
        data["valore"] = 99;
        service.Release();
        expect.That(await Expect.Within(running) && Echo(calculation) == 5, "Il calcolo ha letto il documento modificato");
        expect.That(!ReferenceEquals(service.Inputs.Last(), data), "Il motore ha ricevuto il JsonObject del documento");

        // Latest request wins: the running one is cancelled and never published.
        var first = calculation.CalculateAsync(Data(1));
        await service.EnteredAsync();
        var second = calculation.CalculateAsync(Data(2));
        expect.That(calculation.State == CalculationState.Pending, "Richiesta nuova non in attesa");
        await service.EnteredAsync();
        service.Release();
        expect.That(!await Expect.Within(first) && await Expect.Within(second), "Pubblicata una revisione superata");
        expect.That(Echo(calculation) == 2 && calculation.State == CalculationState.Completed && published == 3, "Risultato della revisione superata");

        // Automatic recalculation with the delay of the module: superseded during the delay, never calculated.
        var delayed = Uncached(service, TimeSpan.FromHours(1));
        service.Hold = false;
        int calls = service.Calls;
        var automatic = delayed.ScheduleAsync(Data(7));
        expect.That(delayed.State == CalculationState.Pending && delayed.Delay == TimeSpan.FromHours(1), "Ricalcolo automatico non in attesa");
        expect.That(await Expect.Within(delayed.CalculateAsync(Data(8))) && !await Expect.Within(automatic), "Calcola non sostituisce il ricalcolo in attesa");
        expect.That(service.Calls == calls + 1 && Echo(delayed) == 8, "Ricalcolo superato eseguito");

        // Missing data and warnings, as SheetEditor reads «errore» and «avvisi».
        service.Values = _ => new JsonObject { ["errore"] = "Lunghezza del palo mancante", ["avvisi"] = new JsonArray("Falda assente") };
        expect.That(await Expect.Within(calculation.CalculateAsync(Data(4))), "Dati da completare non pubblicati");
        expect.That(calculation.State == CalculationState.Incomplete && !calculation.HasResult && calculation.Message == "Lunghezza del palo mancante"
            && calculation.Warnings.SequenceEqual(["Falda assente"]), "Dati da completare");
        service.Values = data => new JsonObject { ["eco"] = data["valore"]?.DeepClone(), ["avvisi"] = new JsonArray("Primo", "Secondo") };
        expect.That(await Expect.Within(calculation.CalculateAsync(Data(6))) && calculation.State == CalculationState.Completed
            && calculation.Warnings.SequenceEqual(["Primo", "Secondo"]) && Echo(calculation) == 6, "Avvisi del risultato");
        service.Values = _ => throw new InvalidOperationException("Motore non convergente");
        expect.That(await Expect.Within(calculation.CalculateAsync(Data(9))) && calculation.State == CalculationState.Failed
            && calculation.Message == "Motore non convergente" && !calculation.HasResult && calculation.Warnings.Count == 0, "Errore del motore");
        service.Values = data => new JsonObject { ["eco"] = data["valore"]?.DeepClone() };

        // Data changed during the calculation: no publication, results cleared.
        await Expect.Within(calculation.CalculateAsync(Data(10)));
        service.Hold = true;
        int before = published;
        running = calculation.CalculateAsync(Data(11));
        await service.EnteredAsync();
        calculation.Invalidate();
        expect.That(calculation.State == CalculationState.Idle && !calculation.HasResult, "Modifica dei dati senza azzerare i risultati");
        expect.That(!await Expect.Within(running) && published == before && calculation.State == CalculationState.Idle, "Pubblicato un calcolo su dati superati");

        // Cancelled by the caller.
        using (var cancel = new CancellationTokenSource())
        {
            running = calculation.CalculateAsync(Data(12), cancel.Token);
            await service.EnteredAsync();
            cancel.Cancel();
            expect.That(!await Expect.Within(running) && calculation.State == CalculationState.Cancelled && published == before, "Annullamento");
        }

        // Closed during the calculation: no publication, no error, resources released once.
        int released = 0;
        var closing = Uncached(service, release: () => released++);
        running = closing.CalculateAsync(Data(13));
        await service.EnteredAsync();
        closing.Dispose();
        closing.Dispose();
        expect.That(!await Expect.Within(running) && !closing.HasResult && closing.State == CalculationState.Running && released == 1, "Chiusura durante il calcolo");
        expect.Throws<ObjectDisposedException>(() => _ = closing.CalculateAsync(Data(14)), "Calcolo dopo la chiusura");
        calculation.Dispose();

        // The module session caches identical data, as in SheetEditor.
        service.Hold = false;
        using (var cached = SheetCalculation.ForModule(service, TimeSpan.Zero))
        {
            calls = service.Calls;
            await Expect.Within(cached.CalculateAsync(Data(15)));
            await Expect.Within(cached.CalculateAsync(Data(15)));
            expect.That(service.Calls == calls + 1 && Echo(cached) == 15, "Cache della sessione del modulo non usata");
        }

        // Limiter of the application: two sheets, one place.
        service.Hold = true;
        var limiter = new CalculationLimiter(1);
        using var left = Uncached(service, limiter: limiter);
        using var right = Uncached(service, limiter: limiter);
        var leftRun = left.CalculateAsync(Data(16));
        await service.EnteredAsync();
        var rightRun = right.CalculateAsync(Data(17));
        expect.That(left.State == CalculationState.Running && right.State == CalculationState.Pending, "Limitatore non rispettato all'avvio");
        service.Release();
        await service.EnteredAsync();
        expect.That(await Expect.Within(leftRun) && right.State == CalculationState.Running, "Secondo foglio non avviato al posto libero");
        service.Release();
        expect.That(await Expect.Within(rightRun) && Echo(right) == 17 && service.MaxRunning == 1, "Calcoli contemporanei oltre il limite");
        expect.That(CalculationLimiter.DefaultMaximum >= 1 && limiter.Maximum == 1, "Limite predefinito");
        expect.Throws<ArgumentOutOfRangeException>(() => _ = new CalculationLimiter(0), "Limite nullo accettato");
        expect.Throws<ArgumentOutOfRangeException>(() => _ = Uncached(service, TimeSpan.FromMilliseconds(-1)), "Ritardo negativo accettato");
        return expect.Count;
    }
}
