using System.Text.Json.Nodes;
using Anthea.Calculations;
internal static class ConcreteBatchChecks
{
    internal static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
        var settings = SectionWorkspace.Prepare(data);
        var input = data["input"]!.AsObject();
        var rows = SectionWorkspace.Sets.ToDictionary(key => key, key => data["combinazioni"]!.Array(key).OfType<JsonObject>()
            .Select(r => J.Obj(("id", r.S("id")), ("N", r["azioni"]![0]), ("Mx", r["azioni"]![1]), ("My", r["azioni"]![2]))).ToArray());
        var session = new ConcreteAnalysisSession();
        HashSet<string> steps = ["2D:SLU", "2D:SLV", "SLE"];
        var first = new Dictionary<string, ConcreteAnalysisStep>();
        string before = data.ToJsonString();
        await session.CalculateBatchAsync(input, settings, rows, steps, x => first.Add(x.Name, x));
        Check(first.Count == 3 && first.Values.All(x => x.Error is null), "Analisi incrementale incompleta");
        Check(before == data.ToJsonString(), "Batch muta il foglio sorgente");
        Check(ReferenceEquals(first["2D:SLU"].Domain!.Two!.Source.Section.Section, first["2D:SLV"].Domain!.Two!.Source.Section.Section),
            "Domini non condividono il modello di sezione");
        var second = new Dictionary<string, ConcreteAnalysisStep>();
        settings["dominio2d"]!["filtro"] = "Fuori dominio";
        await session.CalculateBatchAsync(input, settings, rows, steps, x => second.Add(x.Name, x));
        Check(ReferenceEquals(first["2D:SLU"].Domain!.Two, second["2D:SLU"].Domain!.Two),
            "Filtro grafico invalida la cache del dominio");
        Check(ReferenceEquals(first["SLE"].Stress!.First().Value.State, second["SLE"].Stress!.First().Value.State),
            "Filtro grafico invalida la cache SLE");
        string oldAction = rows["SLE"][0].S("N");
        var snapshotResult = new Dictionary<string, ConcreteAnalysisStep>();
        var pending = session.CalculateBatchAsync(input, settings, rows, new HashSet<string> { "SLE" },
            x => snapshotResult.Add(x.Name, x));
        rows["SLE"][0]["N"] = "input modificato durante il calcolo";
        await pending;
        Check(JsonNode.DeepEquals(ConcreteAnalysisSession.ExportStress(first["SLE"].Stress!),
            ConcreteAnalysisSession.ExportStress(snapshotResult["SLE"].Stress!)), "Il batch legge le righe dopo la richiesta");
        rows["SLE"][0]["N"] = oldAction;
        settings["dominio2d"]!["angoli"] = "non valido";
        var partial = new Dictionary<string, ConcreteAnalysisStep>();
        await session.CalculateBatchAsync(input, settings, rows, new HashSet<string> { "2D:SLU", "SLE" },
            x => partial.Add(x.Name, x));
        Check(partial["2D:SLU"].Error is not null && partial["SLE"].Error is null, "Errore locale blocca tutti gli stati");
        settings["dominio2d"]!["angoli"] = "36";
        using var cancel = new CancellationTokenSource();
        int published = 0;
        try
        {
            await session.CalculateBatchAsync(input, settings, rows, new HashSet<string> { "SLE" }, _ => published++,
                _ => cancel.Cancel(), cancel.Token);
            throw new InvalidOperationException("Batch annullato non interrotto");
        }
        catch (OperationCanceledException) { Check(published == 0, "Risultato pubblicato dopo annullamento"); }
        await session.CalculateBatchAsync(input, settings, rows, new HashSet<string> { "SLE" }, _ => published++);
        Check(published == 1, "Sessione inutilizzabile dopo annullamento");
        return checks;
    }
}
