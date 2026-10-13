using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;

internal static class ConcreteInspectionChecks
{
    internal static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); checks++; }
        var data = ModuleCatalog.CreateData("str_palo"); var workspace = SectionWorkspace.Prepare(data);
        var input = data["input"]!.AsObject(); var options = (JsonObject)workspace["dominio3d"]!.DeepClone();
        options["modello"] = "Non lineare"; var force = new ActionPoint(-100, 10, 5);
        var expected = new CheckerSection(input, workspace, options, "SLU").Stress(force, "ED");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var gate = new ManualResetEventSlim(); int reads = 0;
        // A synthetic check is sufficient here: this test controls orchestration, not resistance.
        var nativeCheck = new DomainCheck(null, force, "fixture") { LimitState = new(() =>
        {
            Interlocked.Increment(ref reads); started.SetResult();
            if (!gate.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Barriera ispezione");
            return expected;
        }) };
        var check = new ConcreteResults.Check(nativeCheck); var ownedForce = new ConcreteResults.Action(force);
        using var session = new ConcreteInspectionSession();
        var first = session.CalculateAsync(check, true, input, workspace, options, "SLU", ownedForce);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var next = session.CalculateAsync(check, false, input, workspace, options, "SLU", ownedForce);
            Check(!next.IsCompleted, "Ispezioni native sovrapposte");
            gate.Set();
            var previous = await first; var applied = await next;
            Check(previous.Cancelled && !previous.TryPublish(_ => { }), "Ispezione superata pubblicabile");
            Check(applied.Error is null && applied.Value!.tensioni_barre.SequenceEqual(expected.tensioni_barre),
                "Azione completa Ed diversa dal percorso originale");
            var again = await session.CalculateAsync(check, false, input, workspace, options, "SLU", ownedForce);
            Check(ReferenceEquals(again.Value, applied.Value), "Stato Ed non riusato per la stessa verifica");
            var limit = await session.CalculateAsync(check, true, input, workspace, options, "SLU", ownedForce);
            Check(ReferenceEquals(limit.Value!.Source, expected) && reads == 1, "Piano limite ricalcolato");
            session.Invalidate();
            Check(!limit.TryPublish(_ => { }), "Invalidazione della vista ignorata");
            var sourceInput = (JsonObject)input.DeepClone(); string before = workspace.ToJsonString();
            var differentCheck = new ConcreteResults.Check(nativeCheck with { Status = "altra identità" });
            var snapshot = session.CalculateAsync(differentCheck, false, sourceInput, workspace, options, "SLU", ownedForce);
            sourceInput["width_mm"] = "invalid";
            var snapshotResult = await snapshot;
            Check(snapshotResult.Error is null && workspace.ToJsonString() == before, "Richiesta usa dati vivi o modifica workspace");
            session.Dispose();
            Check(!snapshotResult.TryPublish(_ => { }), "Ispezione pubblicata dopo chiusura");
            var text = ConcreteResponsePresentation.InspectionText(new ConcreteResults.StressState(expected), ownedForce, "ED", ConcretePreview.PrepareGeometry(input));
            Check(text.Contains("PIANO DI DEFORMAZIONE") && text.Contains("BARRE") && text.Contains("ESTREMI DELLA SOLUZIONE"), "Report dell’ispezione incompleto");
        }
        finally { gate.Set(); }
        return checks;
    }
}
