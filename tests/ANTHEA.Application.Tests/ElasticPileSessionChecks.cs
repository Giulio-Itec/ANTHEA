using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;

internal static class ElasticPileSessionChecks
{
    internal static async Task<int> Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
            "tests/fixtures/f3/inputs/geo_palo_orizzontale--single-soil-elastic.json")))!["data"]!.AsObject();
        string before = input.ToJsonString();
        using var session = new ElasticPileAnalysisSession();
        JsonObject? response = null;
        var first = await session.CalculateAsync(input, value =>
        {
            response = value;
            value["response_callback_only"] = true;
        });
        Check(session.ResponseCalculations == 1, "FEM iniziale duplicato");
        Check(before == input.ToJsonString(), "Sessione ha mutato il documento");
        Check(first["response_callback_only"] is null, "Il callback condivide i dati del motore");
        string forces = first["risposta"]!.ToJsonString();
        input["sezione"]!["longitudinal_bar_count"] = 20;
        Check(ElasticPileAnalysisSession.Matches(response, input), "Armatura invalida impropriamente il FEM");
        var second = await session.CalculateAsync(input, value =>
            Check(value["response_callback_only"] is null, "Callback ha contaminato la cache"));
        Check(session.ResponseCalculations == 1 && second["risposta"]!.ToJsonString() == forces, "Armatura ricalcola il FEM");
        Check(second["armature"]!.Array("tratti")[0]!["sezione"].D("longitudinal_bar_count") == 20, "Armatura obsoleta dopo riuso FEM");
        input["generali"]!["azione_orizzontale"] = 200;
        Check(!ElasticPileAnalysisSession.Matches(response, input), "Carico non invalida il FEM");
        using var cancel = new CancellationTokenSource();
        try { await session.CalculateAsync(input, _ => cancel.Cancel(), cancellation: cancel.Token); throw new Exception("Richiesta cancellata pubblicata"); }
        catch (OperationCanceledException) { checks++; }
        Check(session.ResponseCalculations == 2, "Nuovo carico non risolto");
        var third = await session.CalculateAsync(input, _ => { });
        Check(session.ResponseCalculations == 2 && third["risposta"]!.ToJsonString() != forces, "FEM intermedio non riusato dopo annullamento delle verifiche");
        third["risposta"]!["HeadDisplacement"] = 123;
        var fourth = await session.CalculateAsync(input, _ => { });
        Check(fourth["risposta"].D("HeadDisplacement") != 123, "Esito restituito contamina il FEM conservato");
        session.Dispose();
        try { await session.CalculateAsync(input, _ => { }); throw new Exception("Sessione chiusa riutilizzata"); }
        catch (ObjectDisposedException) { checks++; }
        var incomplete = (JsonObject)input.DeepClone(); incomplete["stratigrafie"]![0]![0]!["spessore"] = "0";
        var capacity = HorizontalPileAnalysis.Calculate(incomplete);
        Check(capacity.Capacity.S("errore") != "" && capacity.Section is not null, "Terreno incompleto nasconde il momento valido della sezione");
        return checks;
    }
}
