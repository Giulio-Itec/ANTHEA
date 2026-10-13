using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;

internal static class PileSegmentOperationChecks
{
    internal static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
            "tests/fixtures/f3/inputs/geo_palo_orizzontale--single-soil-elastic.json")))!["data"]!.AsObject();
        HorizontalSheetPresentation.PrepareReport(data);
        string original = data.ToJsonString();
        var split = PileReinforcementOperations.Split(data, 0, 4);
        Check(data.ToJsonString() == original && split.Count == 2 && split[0].D("fine") == 4 && split[1]!["fine"] is null, "Divisione non atomica o copertura alterata");
        Check(split[0].S("id") != split[1].S("id") && split.All(s => s.B("collegato")), "Identità o ereditarietà dei tratti alterate");
        bool rejected = false;
        try { PileReinforcementOperations.Split(data, 0, 10); } catch (ArgumentException) { rejected = true; }
        Check(rejected && data.ToJsonString() == original, "Divisione invalida modifica il documento");
        var suggestion = new PileSegmentProposal(original, split);
        data["generali"]!["lunghezza"] = 11;
        string edited = data.ToJsonString(); rejected = false;
        try { PileReinforcementOperations.ApplyProposal(data, suggestion); } catch (ArgumentException) { rejected = true; }
        Check(rejected && data.ToJsonString() == edited, "Proposta suddivisione sovrascrive una revisione successiva");
        data = JsonNode.Parse(original)!.AsObject();
        PileReinforcementOperations.ApplyProposal(data, suggestion);
        Check(JsonNode.DeepEquals(data["elastico"]!["storico_tratti"]![0], JsonNode.Parse(original)!["elastico"]!["tratti"]), "Suddivisione non archivia i tratti precedenti");
        split[0]!["fine"] = 3;
        Check(data["elastico"]!["tratti"]![0].D("fine") == 4, "Proposta condivide i tratti applicati");
        data = JsonNode.Parse(original)!.AsObject(); data["generali"]!["azione_orizzontale"] = 1;
        data["elastico"]!["dettagli"]!["azioni_progetto"] = true; data["elastico"]!["dettagli"]!["taglio_confermato"] = true;
        var response = ElasticHorizontalPile.CalculateResponse(data);
        var expected = ElasticHorizontalPile.DesignSegment(data, response, 0, [16], [24], [10], [150]);
        string before = data.ToJsonString();
        using var session = new PileSegmentDesignSession(data, response, 0);
        using var gate = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int[] counts = [16];
        var running = session.CalculateAsync(counts, [24], [10], [150], new InlineProgress(p =>
        {
            if (started.TrySetResult() && !gate.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Barriera proposta armature");
        }));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
            counts[0] = 500; data["sezione"]!["longitudinal_bar_count"] = 100; response["risposta"]!.Array("Points").Clear();
            gate.Set(); var result = await running.WaitAsync(TimeSpan.FromSeconds(30));
            Check(result.Error is null && JsonNode.DeepEquals(result.Value, expected), "Ricerca non conserva fotografia e catalogo o differisce dal motore originale");
            rejected = false; edited = data.ToJsonString();
            try { session.Apply(data, result.Value!); } catch (ArgumentException) { rejected = true; }
            Check(rejected && data.ToJsonString() == edited, "Proposta armatura applicata su dati cambiati");
            var current = JsonNode.Parse(before)!.AsObject();
            rejected = false;
            try { session.Apply(current, new JsonObject { ["longitudinal_bar_count"] = 16 }); } catch (ArgumentException) { rejected = true; }
            Check(rejected && current.ToJsonString() == before, "Proposta incompleta applicata parzialmente");
            session.Apply(current, result.Value!);
            Check(current["sezione"].D("longitudinal_bar_count") == 16 && current["sezione"].D("transverse_spacing_mm") == 150, "Proposta valida non applicata");
            result.Value!["longitudinal_bar_count"] = 99;
            Check(current["sezione"]!["proposta_armatura"].D("longitudinal_bar_count") == 16, "Armatura applicata condivide la proposta");
            session.Dispose();
            Check(!result.TryPublish(_ => { }) && !session.Matches(JsonNode.Parse(before)!.AsObject()), "Sessione chiusa pubblica risultati");
        }
        finally { gate.Set(); }
        data["generali"]!["azione_orizzontale"] = 2;
        // A mismatching FEM key must be rejected before the native search starts.
        using var wrong = new PileSegmentDesignSession(data, response, 0);
        var refused = await wrong.CalculateAsync([16], [24], [10], [150]);
        Check(refused.Error is ArgumentException && refused.Error.Message.Contains("obsoleta"), "Ricerca usa sollecitazioni FEM obsolete");
        return checks;
    }
    private sealed class InlineProgress(Action<PileCalculationProgress> action) : IProgress<PileCalculationProgress>
    { public void Report(PileCalculationProgress value) => action(value); }
}
