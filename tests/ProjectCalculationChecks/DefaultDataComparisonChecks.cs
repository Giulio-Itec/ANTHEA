using System.Text.Json.Nodes;
using X.Core;

internal static class DefaultDataComparisonChecks
{
    private const string Wall = "geo_muri_sostegno";

    public static void Run(CheckLog log)
    {
        var left = ModuleCatalog.CreateData(Wall);
        var right = ModuleCatalog.CreateData(Wall);
        var beforeLeft = left.ToJsonString();
        var beforeRight = right.ToJsonString();
        log.Check(DefaultDataComparison.Equals(Wall, left, right), "Default muri: identità nuove devono essere equivalenti");
        log.Check(!left["actions"]!.AsArray().Select(a => a!["id"]!.GetValue<string>())
            .Intersect(right["actions"]!.AsArray().Select(a => a!["id"]!.GetValue<string>())).Any(),
            "Default muri: le nuove azioni devono avere identità indipendenti");
        log.Check(!DefaultDataComparison.Equals("altro-modulo", left, right), "Altri moduli: confronto degli ID indebolito");
        log.Check(DefaultDataComparison.Equals("altro-modulo", left, left.DeepClone()), "Confronto esatto di copie identiche");

        void Changed(string description, Action<JsonObject> change)
        {
            var changed = (JsonObject)right.DeepClone();
            change(changed);
            log.Check(!DefaultDataComparison.Equals(Wall, left, changed), "Default muri: differenza nascosta in " + description);
        }
        Changed("altezza", d => d["geometry"]!["height"] = d["geometry"].D("height") + .01);
        Changed("valore carico", d => d["actions"]![0]!["value"] = d["actions"]![0].D("value") + .01);
        Changed("coefficiente psi0", d => d["actions"]![0]!["psi0"] = .69);
        Changed("nome azione", d => d["actions"]![0]!["name"] = "Altro carico");
        Changed("abilitazione", d => d["actions"]![0]!["enabled"] = false);
        Changed("azione mancante", d => d["actions"]!.AsArray().RemoveAt(0));
        Changed("ID mancante", d => d["actions"]![0]!.AsObject().Remove("id"));
        Changed("ID vuoto", d => d["actions"]![0]!["id"] = "");
        Changed("ID non valido", d => d["actions"]![0]!["id"] = "non-un-guid");
        Changed("ID nullo", d => d["actions"]![0]!["id"] = (JsonNode?)null);
        Changed("ID numerico", d => d["actions"]![0]!["id"] = 42);
        Changed("GUID vuoto", d => d["actions"]![0]!["id"] = Guid.Empty.ToString("N"));
        Changed("azioni malformate", d => d["actions"] = "non-un-array");
        Changed("riga nulla", d => d["actions"]![0] = (JsonNode?)null);
        Changed("altro campo ID", d => d["id"] = Guid.NewGuid().ToString("N"));
        Changed("riferimenti combinazione", d => d["combinations"]!.AsArray().Add(
            new JsonObject { ["coefficients"] = new JsonObject { [d["actions"]![0]!["id"]!.GetValue<string>()] = 1.5 } }));

        var pair = (JsonObject)left.DeepClone();
        var second = (JsonObject)pair["actions"]![0]!.DeepClone();
        second["id"] = Guid.NewGuid().ToString("N"); second["value"] = 23;
        pair["actions"]!.AsArray().Add(second);
        var duplicate = (JsonObject)pair.DeepClone();
        duplicate["actions"]![1]!["id"] = duplicate["actions"]![0]!["id"]!.DeepClone();
        log.Check(!DefaultDataComparison.Equals(Wall, duplicate, duplicate.DeepClone()), "Default muri: identità duplicate accettate");
        var reversed = (JsonObject)pair.DeepClone();
        reversed["actions"] = new JsonArray(pair["actions"]![1]!.DeepClone(), pair["actions"]![0]!.DeepClone());
        log.Check(!DefaultDataComparison.Equals(Wall, pair, reversed), "Default muri: ordine delle azioni ignorato");
        var noteA = (JsonObject)left.DeepClone(); var noteB = (JsonObject)left.DeepClone();
        noteA["nota"] = Guid.NewGuid().ToString("N"); noteB["nota"] = Guid.NewGuid().ToString("N");
        log.Check(!DefaultDataComparison.Equals(Wall, noteA, noteB), "Default muri: GUID fuori dall'identità ignorati");
        log.Check(left.ToJsonString() == beforeLeft && right.ToJsonString() == beforeRight,
            "Il confronto dei default modifica i documenti originali");
    }
}
