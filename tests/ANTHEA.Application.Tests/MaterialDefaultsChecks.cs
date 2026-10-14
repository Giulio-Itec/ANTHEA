using System.Text.Json.Nodes;
using Anthea.Calculations;
using Materiali;

/// <summary>F2.9-U4 (decision of 14/10/2026): explicit initial values of the material sheet, numbers unchanged.</summary>
internal static class MaterialDefaultsChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var created = ModuleCatalog.CreateData("mat_calcestruzzo");
        foreach (string key in new[] { "classe", "esposizione_principale", "numeri", "scelte", "opzioni", "esposizioni" })
            Check(created.ContainsKey(key), "Nuovo foglio Materiali senza " + key);
        Check(JsonNode.DeepEquals(MaterialSheetInput.NormalizeState(created), created), "Nuovo foglio diverso dallo stato canonico salvato dalla scheda");
        var older = new JsonObject { ["versione_materiali"] = 1 };
        Check(JsonNode.DeepEquals(MaterialSheetInput.NormalizeState(older), created), "Foglio precedente letto con valori diversi da quelli iniziali");
        double fck = 30, cover = MaterialCover.Required(created, fck);
        Check(cover == 35 && MaterialCover.Required(older, fck) == cover, "Copriferro nominale del foglio iniziale cambiato: " + cover);
        return checks;
    }
}
