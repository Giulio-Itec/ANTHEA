using System.Text.Json.Nodes;
using X.Core;

internal static class SaturatedWeightChecks
{
    internal static void Run()
    {
        int passed = 0;
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
        Assert(Archivio.NuovoStratoPalo().S("peso_specifico_saturo") == "", "Nuovi strati: γsat deve essere automatico");
        foreach (bool water in new[] { false, true })
        foreach (string gamma in new[] { "20", "19,5" })
        {
            var data = Archivio.NuovoFoglio("geo_palo_verticale");
            var g = data["generali"]!;
            g["lunghezza"] = "30"; g["presenza_falda"] = water; g["profondita_falda"] = "10";
            g["azione_compressione"] = "2000"; g["azione_trazione"] = "1000";
            var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!.AsObject();
            layer["spessore"] = "30"; layer["tipologia"] = "Granulare"; layer["addensamento"] = "Sciolto";
            layer["angolo_attrito"] = "30"; layer["peso_specifico"] = gamma; layer["peso_specifico_saturo"] = gamma;
            var expected = Calcolo.Calcola(data);
            Assert(expected.S("errore") == "", "Caso di riferimento non calcolabile");
            foreach (string blank in new[] { "", "  " })
            {
                layer["peso_specifico_saturo"] = blank;
                string before = data.ToJsonString(); var actual = Calcolo.Calcola(data);
                Assert(JsonNode.DeepEquals(expected, actual), "γsat vuoto diverso da γ");
                Assert(data.ToJsonString() == before, "Il calcolo materializza il valore automatico");
                var loaded = JsonNode.Parse(data.ToJsonString())!;
                Assert(JsonNode.DeepEquals(expected, Calcolo.Calcola(loaded)), "γsat automatico perso dopo serializzazione");
            }
            layer.Remove("peso_specifico_saturo");
            Assert(JsonNode.DeepEquals(expected, Calcolo.Calcola(data)), "γsat assente diverso da γ");
            foreach (string explicitValue in new[] { "0", "22" })
            {
                layer["peso_specifico_saturo"] = explicitValue;
                var actual = Calcolo.Calcola(data);
                Assert(actual.S("errore") == "", "Valore esplicito non calcolabile");
                Assert(JsonNode.DeepEquals(expected, actual) == !water, "γsat esplicito non rispettato");
                Assert(layer.S("peso_specifico_saturo") == explicitValue, "Valore esplicito modificato");
            }
        }
        Console.WriteLine($"γsat automatico: {passed} controlli superati.");
    }
}
