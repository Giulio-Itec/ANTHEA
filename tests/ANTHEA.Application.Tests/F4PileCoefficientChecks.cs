using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4PileCoefficientChecks
{
    internal static int Run(string root)
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); count++; }
        JsonObject Input(string module)
        {
            string id = module == "geo_palo_verticale" ? "regressione-palo_storico_0" : "regressione-micropalo_igu_0_nessuna-riduzione";
            return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs", module + "--" + id + ".json")))!["data"]!.AsObject();
        }
        (string Key, double Value)[] factors = [("sicurezza_laterale_compressione", 1.15),
            ("sicurezza_laterale_trazione", 1.25), ("peso_palo_sfavorevole", 1.3), ("peso_palo_favorevole", 1)];
        foreach (string module in new[] { "geo_palo_verticale", "geo_micropalo_verticale" })
        foreach (string technology in new[] { "Trivellato", "Elica continua", "Battuto" })
        foreach (string surveys in new[] { "1", "3", "≥10" })
        {
            var explicitInput = Input(module);
            var general = explicitInput["generali"]!.AsObject();
            general["tipo_palo"] = technology; general["verticali_indagate"] = surveys;
            foreach (var factor in factors) general[factor.Key] = factor.Value;
            var missing = (JsonObject)explicitInput.DeepClone();
            foreach (var factor in factors) missing["generali"]!.AsObject().Remove(factor.Key);
            string before = missing.ToJsonString();
            var expected = Calcolo.Calcola(explicitInput, module == "geo_micropalo_verticale");
            var actual = Calcolo.Calcola(missing, module == "geo_micropalo_verticale");
            Check(expected.S("errore") == "", module + ": fixture dei coefficienti non valida: " + expected.S("errore"));
            Check(JsonNode.DeepEquals(expected, actual), module + ": coefficienti assenti diversi dai valori espliciti");
            Check(missing.ToJsonString() == before, "Lettura dei coefficienti ha modificato il documento");
        }
        var custom = Input("geo_palo_verticale");
        custom["generali"]!["sicurezza_laterale_compressione"] = "1.71";
        string customBefore = custom.ToJsonString();
        var customResult = Calcolo.Calcola(custom);
        var defaultInput = (JsonObject)custom.DeepClone(); defaultInput["generali"]!.AsObject().Remove("sicurezza_laterale_compressione");
        Check(customResult.S("errore") == "" && custom.ToJsonString() == customBefore, "Override non conservato");
        Check(!JsonNode.DeepEquals(customResult, Calcolo.Calcola(defaultInput)), "Override ignorato nel calcolo");
        foreach (var factor in factors)
        foreach (string invalid in new[] { "", "non numerico", "NaN", "-1", "0" })
        {
            var data = Input("geo_palo_verticale"); data["generali"]![factor.Key] = invalid;
            Check(Calcolo.Calcola(data).S("errore") != "", factor.Key + ": valore esplicito invalido sostituito dalla norma");
        }
        return count;
    }
}
