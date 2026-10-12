using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Anthea.Calculations;

internal static class ModuleServiceChecks
{
    internal static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks++; }
        foreach (var definition in ModuleCatalog.All)
        {
            var source = definition.NewData();
            // Fix the identity of newly generated CA rows before the two independent executions.
            if (definition.Id == "str_palo") _ = SectionWorkspace.Prepare(source);
            string before = source.ToJsonString();
            var service = ModuleServices.Get(definition.Id);
            Check(service.ModuleId == definition.Id, "Identità modulo duplicata o discordante");
            var prepared = service.Prepare(source); prepared["test_solo_copia"] = true;
            Check(source.ToJsonString() == before, "Prepare muta il documento");
            JsonObject? expected = null, actual = null; string? expectedError = null, actualError = null;
            try { expected = CalculationService.Calculate(definition.Id, source); } catch (ArgumentException ex) { expectedError = ex.Message; }
            try { actual = service.Calculate(source).Values; } catch (ArgumentException ex) { actualError = ex.Message; }
            Check(expectedError == actualError && JsonNode.DeepEquals(expected, actual), "Servizio discorda dall’ingresso esistente: " + definition.Id);
            Check(source.ToJsonString() == before, "Calcolo muta il documento: " + definition.Id);
        }
        var fake = new CountingService(); var session = new ModuleCalculationSession(fake, capacity: 2);
        JsonObject Input(int n) => new() { ["n"] = n };
        var original = Input(1); var first = session.Calculate(original);
        first.Values["n"] = 999; first.Data["n"] = 999;
        var second = session.Calculate(Input(1));
        Check(fake.Calls == 1 && second.Values.D("n") == 1 && second.Data.D("n") == 1, "Cache condivide dati mutabili");
        Check(original.D("n") == 1, "Cache ha mutato l’ingresso");
        _ = session.Calculate(Input(2)); _ = session.Calculate(Input(1)); _ = session.Calculate(Input(3)); _ = session.Calculate(Input(2));
        Check(fake.Calls == 4, "Evizione LRU errata");
        session.Clear(); _ = session.Calculate(Input(2)); Check(fake.Calls == 5, "Cache non svuotata");
        var tiny = new ModuleCalculationSession(fake, byteLimit: 1); _ = tiny.Calculate(Input(4)); _ = tiny.Calculate(Input(4));
        Check(fake.Calls == 7, "Limite memoria ignorato");
        fake.Typed = true; _ = session.Calculate(Input(5)); _ = session.Calculate(Input(5)); Check(fake.Calls == 9, "Risultati tipizzati condivisi dalla cache JSON");
        fake.Typed = false; fake.Fail = true; _ = session.Calculate(Input(6)); _ = session.Calculate(Input(6)); Check(fake.Calls == 11, "Errore memorizzato in cache");

        using var scheduler = new RecalculationScheduler();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool newerEntered = false;
        var old = scheduler.ScheduleAsync(async _ => { entered.SetResult(); await release.Task; return 1; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var newer = scheduler.ScheduleAsync(_ => { newerEntered = true; return Task.FromResult(2); });
        Check(!newerEntered, "Calcoli sovrapposti anche senza annullamento cooperativo del motore");
        release.SetResult();
        var oldResult = await old.WaitAsync(TimeSpan.FromSeconds(3)); var newResult = await newer.WaitAsync(TimeSpan.FromSeconds(3));
        int published = 0;
        Check(oldResult.Cancelled && !oldResult.TryPublish(_ => published = 1), "Risultato precedente pubblicato");
        Check(newResult.TryPublish(r => published = r.Value) && published == 2, "Risultato corrente non pubblicato");
        bool delayedRan = false;
        var delayed = scheduler.ScheduleAsync(_ => { delayedRan = true; return Task.FromResult(3); }, TimeSpan.FromMinutes(1));
        var immediate = await scheduler.ScheduleAsync(_ => Task.FromResult(4));
        Check((await delayed.WaitAsync(TimeSpan.FromSeconds(3))).Cancelled && !delayedRan && immediate.Value == 4, "Debounce non annullato");
        var failure = await scheduler.ScheduleAsync<int>(_ => throw new InvalidOperationException("errore osservato"));
        Check(failure.Error?.Message == "errore osservato" && failure.TryPublish(_ => { }), "Errore del calcolo perso");
        scheduler.Invalidate(); Check(!failure.TryPublish(_ => { }), "Invalidazione non impedisce la pubblicazione");
        var cancelled = await scheduler.ScheduleAsync(_ => Task.FromResult(1), cancellation: new CancellationToken(true));
        Check(cancelled.Cancelled && !cancelled.TryPublish(_ => { }), "Annullamento esterno ignorato");
        var late = await scheduler.ScheduleAsync(_ => Task.FromResult(8)); scheduler.Dispose();
        Check(!late.TryPublish(_ => { }), "Risultato pubblicato dopo la chiusura");
        bool disposed = false;
        try { await scheduler.ScheduleAsync(_ => Task.FromResult(9)); } catch (ObjectDisposedException) { disposed = true; }
        Check(disposed, "Scheduler riavviato dopo Dispose");
        return checks;
    }
    private sealed class CountingService : IModuleService
    {
        public int Calls; public bool Typed, Fail;
        public string ModuleId => "fixture";
        public JsonObject Prepare(JsonObject source) => (JsonObject)source.DeepClone();
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Calls++;
            return new(ModuleId, Prepare(source), new JsonObject { ["n"] = source.D("n"), ["errore"] = Fail ? "incompleto" : "" }, Typed ? new object() : null);
        }
    }
}
