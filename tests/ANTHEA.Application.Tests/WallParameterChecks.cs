using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;

internal static class WallParameterChecks
{
    internal static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks++; }
        void Reject(Action action, string message)
        { try { action(); } catch (ArgumentException) { checks++; return; } throw new InvalidOperationException(message); }
        var source = RetainingWall.Example("cantilever");
        WallSheetPresentation.Prepare(source);
        source["combinations"] = RetainingWall.GenerateCombinations(source);
        var table = new WallParameterTable(source); string before = source.ToJsonString();
        JsonObject Row(string path) => table.Rows.Single(r => r.S("path") == path);
        Check(table.Rows.All(r => !r.S("path").EndsWith("/id") && !r.S("path").Contains("/__")), "Campi interni esposti");
        Check(JsonNode.DeepEquals(source, table.CreateSnapshot()), "Proiezione senza modifiche altera il documento");
        Row("geometry/height")["value"] = "3,2";
        Row("valley/linked")["value"] = "true";
        Row("layers/0/phi")["value"] = "28";
        var changed = table.CreateSnapshot();
        Check(changed["geometry"].D("height") == 3.2 && changed["valley"]!.Array("layers")[0].D("phi") == 28, "Numeri italiani o colonne collegate non applicati");
        Check(source.ToJsonString() == before, "Preparazione dei parametri modifica il documento");
        Row("valley/layers/0/phi")["value"] = "25";
        Reject(() => table.CreateSnapshot(), "Conflitto fra colonne collegate non rifiutato");
        Row("valley/layers/0/phi")["value"] = source["valley"]!["layers"]![0]!.S("phi");
        Row("geometry/height")["value"] = "NaN";
        Reject(() => table.CreateSnapshot(), "Valore non finito accettato");
        Row("geometry/height")["value"] = "3,2";
        Row("valley/linked")["value"] = "forse";
        Reject(() => table.CreateSnapshot(), "Flag invalido accettato");
        Row("valley/linked")["value"] = "true";
        var combination = table.Rows.First(r => r.S("path").StartsWith("combinations/") && r.S("path").EndsWith("/name"));
        combination["value"] = "Combinazione modificata";
        changed = await table.ValidateAsync(ModuleServices.Get(RetainingWall.Module));
        Check(changed.S("combination_mode") == "Personalizzate" && changed.Array("combinations")[0].S("name") == "Combinazione modificata",
            "Modifica delle combinazioni non personalizzata");
        Check(changed.S("combination_signature") == RetainingWall.CombinationSignature(changed), "Firma combinazioni incoerente");
        changed["geometry"]!["height"] = 99;
        Check(table.CreateSnapshot()["geometry"].D("height") == 3.2, "Esito restituito condivide dati del tavolo");
        bool cancelled = false;
        try { await table.ValidateAsync(ModuleServices.Get(RetainingWall.Module), new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && source.ToJsonString() == before, "Annullamento modifica il documento");
        return checks;
    }
}
