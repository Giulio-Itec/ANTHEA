using System.Text.Json.Nodes;
using X.Core;

internal static class CohesionChecks
{
    internal static void Run()
    {
        int passed = 0;
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
        void Near(double actual, double expected, string message) => Assert(Math.Abs(actual - expected) < 1e-8, message);
        foreach (string pileType in new[] { "Trivellato", "Battuto", "Elica continua" })
        foreach (double? water in new double?[] { null, 0, 5, 20 })
        foreach (bool active in new[] { false, true })
        foreach (string soil in new[] { "Granulare", "Coesivo" })
        {
            var data = Archivio.NuovoFoglio("geo_palo_verticale"); var g = data["generali"]!;
            g["tipo_palo"] = pileType; g["diametro"] = "1"; g["lunghezza"] = "10";
            g["presenza_falda"] = water.HasValue; g["profondita_falda"] = water ?? 0;
            var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!;
            layer["spessore"] = "10"; layer["tipologia"] = soil; layer["addensamento"] = "Denso";
            layer["peso_specifico"] = "18"; layer["peso_specifico_saturo"] = "20";
            layer["angolo_attrito"] = "30"; layer["coesione_non_drenata"] = "40"; layer["laterale_attiva"] = active;
            layer["coesione_efficace"] = "0"; var zero = Calcolo.Calcola(data);
            layer["coesione_efficace"] = "12"; string original = data.ToJsonString(); var cohesion = Calcolo.Calcola(data);
            Assert(zero.S("errore") == "" && cohesion.S("errore") == "", "Scenario non calcolabile");
            Assert(data.ToJsonString() == original, "Il calcolo altera il valore inserito");
            if (soil == "Granulare") Assert(JsonNode.DeepEquals(zero, cohesion), "c′ modifica una resistenza granulare");
            var a = zero.Array("dettagli")[^1]!.Array("sondaggi")[0]!;
            var b = cohesion.Array("dettagli")[^1]!.Array("sondaggi")[0]!;
            double delta = active && soil == "Coesivo" ? 12 : 0;
            Near(b["drenante"].D("laterale") - a["drenante"].D("laterale"), Math.PI * 10 * delta, "Contributo laterale drenato errato");
            Near(b["non_drenante"].D("laterale") - a["non_drenante"].D("laterale"), Math.PI * Math.Min(water ?? 10, 10) * delta, "Contributo sopra falda errato");
            foreach (string condition in new[] { "drenante", "non_drenante" })
                Near(a[condition].D("base"), b[condition].D("base"), "c′ modifica la punta");
        }
        Console.WriteLine($"Coesione efficace: {passed} controlli superati (granulari, coesivi, falda, laterale disattivata e tipi di palo).");
    }
}
