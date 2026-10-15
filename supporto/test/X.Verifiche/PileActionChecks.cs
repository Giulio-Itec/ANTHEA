using System.Text.Json.Nodes;
using X.Core;

/// <summary>
/// PV-1 (analisi dei bug del 15/10/2026): azioni del palo verticale per condizione e verso (GPCChecker.Geotechnics 0.4, AxialCapacityResult.Actions)
/// e loro uso nel riepilogo, nel report e nelle tabelle. Attesi dalla statica del peso del palo (formule a mano, come
/// scratchpad agent-geo/pv1_buoyancy.py e il verificatore bug/pali-sottospinta/indep.py): A = π D²/4, W = γc A z, W′ = W − γw A max(0, z − zf),
/// γw = 9,81 kN/m³, γG,sfav = 1,3 e γG,fav = 1,0 assegnati nel foglio.
/// </summary>
internal static class PileActionChecks
{
    internal static void Run()
    {
        int passed = 0;
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
        void Near(double actual, double expected, string message) => Assert(Math.Abs(actual - expected) <= 1e-6 * Math.Max(1, Math.Abs(expected)), $"{message}: {actual:R} invece di {expected:R}");
        const double gammaC = 25, gammaW = 9.81, d = 1, l = 20;
        double area = Math.PI * d * d / 4;
        double Weight(double z) => gammaC * area * z;
        double Buoyant(double z, double zf) => Math.Max(0, Weight(z) - gammaW * area * Math.Max(0, z - zf));
        JsonObject Pile(string soil, double zf, bool buoyancy, string compression, string tension)
        {
            var data = Archivio.NuovoFoglio("geo_palo_verticale"); var g = data["generali"]!;
            g["tipo_palo"] = "Trivellato"; g["diametro"] = "1"; g["lunghezza"] = "20"; g["peso_specifico_palo"] = "25";
            g["presenza_falda"] = true; g["profondita_falda"] = zf; g["considera_sottospinta"] = buoyancy;
            g["azione_compressione"] = compression; g["azione_trazione"] = tension; g["peso_palo_sfavorevole"] = "1.3"; g["peso_palo_favorevole"] = "1";
            var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!;
            layer["spessore"] = "25"; layer["tipologia"] = soil; layer["addensamento"] = "Denso"; layer["peso_specifico"] = soil == "Granulare" ? "19" : "20";
            layer["peso_specifico_saturo"] = "20"; layer["angolo_attrito"] = soil == "Granulare" ? "32" : "0"; layer["coesione_efficace"] = "0"; layer["coesione_non_drenata"] = soil == "Granulare" ? "0" : "80";
            return data;
        }
        double Last(JsonObject result, string curve) => Calcolo.AzioniCurva(result, curve)![^1]![1]!.GetValue<double>();

        // (a) Sabbia densa, falda a 2 m, sottospinta non richiesta, T = 1200 kN, N = 900 kN: a trazione, drenata e non drenata, sempre W′;
        // a compressione il peso intero della scelta dell'utente (nessuna base lorda nel granulare).
        var sand = Calcolo.Calcola(Pile("Granulare", 2, false, "900", "1200"));
        Assert(sand.S("errore") == "", "PV-1 (a): " + sand.S("errore"));
        foreach (string curve in new[] { "drenante_trazione", "non_drenante_trazione" })
            Near(Last(sand, curve), 1200 - 1.0 * Buoyant(l, 2), "PV-1 (a) " + curve + ": Ed,t = T − γG,fav W′ (946,0 kN)");
        foreach (string curve in new[] { "drenante_compressione", "non_drenante_compressione" })
            Near(Last(sand, curve), 900 + 1.3 * Weight(l), "PV-1 (a) " + curve + ": Ed,c = N + γG,sfav W");
        foreach (var point in Calcolo.AzioniCurva(sand, "drenante_trazione")!)
            Near(point![1]!.GetValue<double>(), Math.Max(0, 1200 - Buoyant(point[0]!.GetValue<double>(), 2)), "PV-1 (a): azione a trazione a ogni quota");
        Assert(JsonNode.DeepEquals(sand["azioni"]!["trazione"], sand["azioni_condizione"]!["drenante_trazione"]), "PV-1: «azioni» conserva le azioni drenate");

        // (b) Argilla cu 80 kPa, falda al piano campagna, sottospinta richiesta, N = 900 kN: compressione non drenata con la base lorda
        // A (Nc cu + σv,L) e il peso intero W; compressione drenata con W′ (1410,5 e 1210,2 kN).
        var clay = Calcolo.Calcola(Pile("Coesivo", 0, true, "900", ""));
        Assert(clay.S("errore") == "", "PV-1 (b): " + clay.S("errore"));
        Near(Last(clay, "non_drenante_compressione"), 900 + 1.3 * Weight(l), "PV-1 (b): non drenata con la base lorda e il peso intero");
        Near(Last(clay, "drenante_compressione"), 900 + 1.3 * Buoyant(l, 0), "PV-1 (b): drenata con W′ per la scelta dell'utente");
        Assert(Calcolo.AzioniCurva(clay, "drenante_trazione")!.Count == 0, "PV-1 (b): nessuna azione di trazione");

        // Risultati salvati prima del 16/10/2026: solo «azioni» per verso, usate per tutte le condizioni come allora.
        var old = (JsonObject)clay.DeepClone(); old.Remove("azioni_condizione");
        Assert(JsonNode.DeepEquals(Calcolo.AzioniCurva(old, "non_drenante_compressione"), old["azioni"]!["compressione"]), "PV-1: risultato storico con le azioni per verso");

        // Report e tabelle: ogni condizione confrontata con le proprie azioni.
        var table = Tabelle.Crea(clay, false).Single(t => t.Titolo == "non drenante compressione — verifica a ogni quota");
        Near(double.Parse(table.Righe[^1][4], System.Globalization.CultureInfo.GetCultureInfo("it-IT")), Math.Round(900 + 1.3 * Weight(l), 4), "PV-1: tabella con l'azione non drenata");
        Console.WriteLine($"Azioni del palo per condizione (PV-1): {passed} controlli superati.");
    }
}
